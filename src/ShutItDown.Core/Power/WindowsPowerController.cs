using System.Diagnostics;

namespace ShutItDown.Core.Power;

/// <summary>Uses the built-in shutdown.exe, which needs no admin rights for the signed-in user.</summary>
public sealed class WindowsPowerController : IPowerController
{
    public static readonly TimeSpan MaxDelay = TimeSpan.FromDays(1);

    private readonly TimeProvider _clock;
    private readonly object _lock = new();
    private DateTimeOffset? _scheduledAt;

    public WindowsPowerController(TimeProvider? clock = null)
    {
        _clock = clock ?? TimeProvider.System;
    }

    public DateTimeOffset? ScheduledShutdown
    {
        get
        {
            lock (_lock)
            {
                return _scheduledAt > _clock.GetUtcNow() ? _scheduledAt : null;
            }
        }
    }

    public void ScheduleShutdown(TimeSpan delay)
    {
        if (delay < TimeSpan.Zero || delay > MaxDelay)
            throw new ArgumentOutOfRangeException(nameof(delay));

        lock (_lock)
        {
            // shutdown.exe refuses a new timer while one is pending, so clear it first.
            RunShutdownExe("/a", throwOnError: false);
            var seconds = (int)Math.Ceiling(delay.TotalSeconds);
            RunShutdownExe($"/s /t {seconds}", throwOnError: true);
            _scheduledAt = _clock.GetUtcNow().AddSeconds(seconds);
        }
    }

    public void CancelShutdown()
    {
        lock (_lock)
        {
            RunShutdownExe("/a", throwOnError: false);
            _scheduledAt = null;
        }
    }

    private static void RunShutdownExe(string arguments, bool throwOnError)
    {
        using var process = Process.Start(new ProcessStartInfo("shutdown", arguments)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
        }) ?? throw new InvalidOperationException("Could not start shutdown.exe.");

        if (!process.WaitForExit(10_000))
            throw new TimeoutException("shutdown.exe did not respond.");

        if (throwOnError && process.ExitCode != 0)
            throw new InvalidOperationException($"shutdown.exe failed with exit code {process.ExitCode}.");
    }
}
