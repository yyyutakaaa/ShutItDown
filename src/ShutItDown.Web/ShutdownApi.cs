using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ShutItDown.Core.Monitoring;
using ShutItDown.Core.Pairing;
using ShutItDown.Core.Power;
using ShutItDown.Core.Security;

namespace ShutItDown.Web;

/// <summary>What the API needs from the rest of the app.</summary>
/// <param name="GetPinHash">Read on every request, so a PIN change in the tray app applies right away.</param>
public sealed record ShutdownApiDependencies(
    IPowerController Power,
    Func<string?> GetPinHash,
    PinAttemptLimiter Limiter,
    DeviceRegistry Devices,
    PairingCode Pairing)
{
    /// <summary>Added to a "now" shutdown so it can still be cancelled on the PC.</summary>
    public Func<TimeSpan> GetGracePeriod { get; init; } = () => TimeSpan.Zero;

    /// <summary>Called (on a server thread) after a phone has scheduled a shutdown.</summary>
    public Action<ShutdownScheduledInfo>? OnShutdownScheduled { get; init; }

    /// <summary>Called (on a server thread) after a phone has paired.</summary>
    public Action<string>? OnPaired { get; init; }

    /// <summary>Live PC information. Without it, the live and "when done" endpoints answer 503.</summary>
    public IPcMonitor? Monitor { get; init; }

    public ShutdownWatcher? Watcher { get; init; }

    public Func<bool> AllowScreenPreview { get; init; } = () => true;
}

public sealed record ShutdownScheduledInfo(string Client, DateTimeOffset At);

/// <param name="OnlyIfIdleMinutes">Skip the shutdown when someone used the PC more recently than this (for automations).</param>
public sealed record ShutdownRequest(string? Pin, int? DelayMinutes, int? OnlyIfIdleMinutes = null);

public sealed record WatchRequestBody(string? Kind, int? Minutes, string? Process);

public sealed record PairRequest(string? Code, string? Name);

public static class ShutdownApi
{
    public const int MaxDelayMinutes = 24 * 60;

    /// <summary>Paired phones send their token in this header.</summary>
    public const string TokenHeader = "X-Device-Token";

    public static void MapShutdownApi(this IEndpointRouteBuilder routes, ShutdownApiDependencies deps)
    {
        var api = routes.MapGroup("/api");

        api.MapGet("/status", (HttpContext context) =>
        {
            var device = deps.Devices.Find(TokenOf(context));
            return Results.Ok(new
            {
                machineName = Environment.MachineName,
                paired = device is not null,
                deviceName = device?.Name,
                pinAvailable = deps.GetPinHash() is not null,
                scheduledShutdownAt = deps.Power.ScheduledShutdown,
            });
        });

        // The QR code holds a one-time code; trading it in gives the phone its own token.
        api.MapPost("/pair", (PairRequest request) =>
        {
            if (!deps.Pairing.TryConsume(request.Code))
                return Results.BadRequest(new { error = "This QR code has expired or was already used. Open Shutdown Server on your PC and scan the new code." });

            var token = deps.Devices.Pair(request.Name);
            Try(() => deps.OnPaired?.Invoke(request.Name ?? "Phone"));
            return Results.Ok(new { token });
        });

        // POST with a JSON body: a link preview or a stray GET can never turn the PC off,
        // and other websites cannot send this request from the phone's browser.
        api.MapPost("/shutdown", (ShutdownRequest request, HttpContext context) =>
        {
            var client = ClientOf(context);
            if (Authorize(deps, context, request.Pin, client) is { } refused)
                return refused;

            var minutes = request.DelayMinutes ?? 0;
            if (minutes is < 0 or > MaxDelayMinutes)
                return Results.BadRequest(new { error = $"Delay must be between 0 and {MaxDelayMinutes} minutes." });

            // Automations ("when I leave home") must never turn off a PC someone is sitting at.
            if (request.OnlyIfIdleMinutes is > 0 and var idleMinutes && deps.Monitor is { } monitor
                && monitor.GetSnapshot().UserIdle < TimeSpan.FromMinutes(idleMinutes))
            {
                return Results.Ok(new { skipped = true, reason = "Someone is using the PC, so it stays on." });
            }

            var delay = minutes == 0 ? deps.GetGracePeriod() : TimeSpan.FromMinutes(minutes);
            try
            {
                deps.Power.ScheduleShutdown(delay);
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
            }

            var at = deps.Power.ScheduledShutdown ?? DateTimeOffset.UtcNow + delay;
            Try(() => deps.OnShutdownScheduled?.Invoke(new ShutdownScheduledInfo(client, at)));
            return Results.Ok(new { scheduledShutdownAt = at });
        });

        // Cancelling never does harm, so it needs no pairing or PIN.
        api.MapPost("/cancel", () =>
        {
            deps.Power.CancelShutdown();
            return Results.Ok(new { cancelled = true });
        });

        // --- Paired phones only: what the PC is doing ----------------------

        api.MapGet("/pc", (HttpContext context) =>
        {
            if (RequireDevice(deps, context) is { } refused) return refused;
            if (deps.Monitor is not { } monitor) return Unavailable();

            var snapshot = monitor.GetSnapshot();
            var apps = monitor.GetOpenApps();
            return Results.Ok(new
            {
                cpuPercent = Math.Round(snapshot.CpuPercent),
                memoryPercent = Math.Round(snapshot.MemoryPercent),
                downloadBytesPerSecond = Math.Round(snapshot.DownloadBytesPerSecond),
                uploadBytesPerSecond = Math.Round(snapshot.UploadBytesPerSecond),
                idleSeconds = (int)snapshot.UserIdle.TotalSeconds,
                uptimeSeconds = (int)snapshot.Uptime.TotalSeconds,
                apps = apps.Select(a => new { name = a.ProcessName, title = a.Title }),
                unsaved = UnsavedWorkDetector.Find(apps).Select(w => new { name = w.ProcessName, title = w.Title, reason = w.Reason }),
                watch = WatchJson(deps.Watcher?.Current),
                screenPreview = deps.AllowScreenPreview(),
            });
        });

        api.MapGet("/screenshot", (HttpContext context) =>
        {
            if (RequireDevice(deps, context) is { } refused) return refused;
            if (deps.Monitor is not { } monitor) return Unavailable();
            if (!deps.AllowScreenPreview())
                return Results.Json(new { error = "Screen preview is turned off on the PC." }, statusCode: StatusCodes.Status403Forbidden);

            context.Response.Headers.CacheControl = "no-store";
            return monitor.CaptureScreenJpeg(maxWidth: 1280) is { } jpeg
                ? Results.File(jpeg, "image/jpeg")
                : Results.Json(new { error = "Couldn't capture the screen. Is the PC locked?" }, statusCode: StatusCodes.Status503ServiceUnavailable);
        });

        // --- Paired phones only: shut down when the PC is done --------------

        api.MapPost("/watch", (WatchRequestBody body, HttpContext context) =>
        {
            if (RequireDevice(deps, context) is { } refused) return refused;
            if (deps.Watcher is not { } watcher) return Unavailable();

            WatchKind? kind = body.Kind switch
            {
                "idle" => WatchKind.Idle,
                "network" => WatchKind.Network,
                "app" => WatchKind.AppExit,
                _ => null,
            };
            if (kind is null)
                return Results.BadRequest(new { error = "Kind must be idle, network or app." });

            try
            {
                watcher.Start(new WatchRequest(kind.Value, body.Minutes ?? 10, body.Process));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message.Split(" (Parameter")[0] });
            }
            return Results.Ok(new { watch = WatchJson(watcher.Current) });
        });

        api.MapPost("/watch/cancel", (HttpContext context) =>
        {
            if (RequireDevice(deps, context) is { } refused) return refused;
            deps.Watcher?.Cancel();
            return Results.Ok(new { cancelled = true });
        });
    }

    private static object? WatchJson(WatchState? state) => state is null ? null : new
    {
        kind = state.Kind switch { WatchKind.Idle => "idle", WatchKind.Network => "network", _ => "app" },
        minutes = state.Minutes,
        process = state.ProcessName,
        description = state.Describe(),
        quietSince = state.QuietSince,
    };

    private static IResult? RequireDevice(ShutdownApiDependencies deps, HttpContext context) =>
        deps.Devices.Find(TokenOf(context)) is null
            ? Results.Json(new { error = "Only paired phones can see this.", needsPairing = true }, statusCode: StatusCodes.Status401Unauthorized)
            : null;

    private static IResult Unavailable() =>
        Results.Json(new { error = "Not available on this PC." }, statusCode: StatusCodes.Status503ServiceUnavailable);

    /// <summary>Returns null when the request may shut down the PC, otherwise the response to send.</summary>
    private static IResult? Authorize(ShutdownApiDependencies deps, HttpContext context, string? pin, string client)
    {
        if (deps.Devices.Find(TokenOf(context)) is { } device)
        {
            deps.Devices.MarkUsed(device);
            return null;
        }

        if (deps.GetPinHash() is not { } pinHash)
        {
            return Results.Json(
                new { error = "This phone isn't paired. Scan the QR code in Shutdown Server on your PC.", needsPairing = true },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var wait = deps.Limiter.GetLockoutRemaining(client);
        if (wait > TimeSpan.Zero)
        {
            var seconds = (int)Math.Ceiling(wait.TotalSeconds);
            return Results.Json(
                new { error = $"Too many wrong PINs. Try again in {seconds} s.", retryAfterSeconds = seconds },
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        if (!PinHasher.Verify(pin, pinHash))
        {
            var attemptsLeft = deps.Limiter.RegisterFailure(client);
            return Results.Json(new { error = "Wrong PIN.", attemptsLeft }, statusCode: StatusCodes.Status401Unauthorized);
        }

        deps.Limiter.Reset(client);
        return null;
    }

    private static string? TokenOf(HttpContext context) =>
        context.Request.Headers.TryGetValue(TokenHeader, out var value) ? value.ToString() : null;

    private static string ClientOf(HttpContext context)
    {
        var ip = context.Connection.RemoteIpAddress;
        return (ip is { IsIPv4MappedToIPv6: true } ? ip.MapToIPv4() : ip)?.ToString() ?? "unknown";
    }

    // A failing notification must never undo or hide what the request did.
    private static void Try(Action action)
    {
        try { action(); } catch { }
    }
}
