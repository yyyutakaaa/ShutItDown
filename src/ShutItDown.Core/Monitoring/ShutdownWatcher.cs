using ShutItDown.Core.Power;

namespace ShutItDown.Core.Monitoring;

public enum WatchKind
{
    /// <summary>Nobody uses the PC and the processor is quiet (renders, updates, exports are done).</summary>
    Idle,

    /// <summary>Nobody uses the PC and the network is quiet (downloads are done).</summary>
    Network,

    /// <summary>A chosen program has closed (for example a game or an installer).</summary>
    AppExit,
}

public sealed record WatchRequest(WatchKind Kind, int Minutes, string? ProcessName = null);

/// <param name="QuietSince">Since when the condition has held; null while the PC is still busy.</param>
public sealed record WatchState(WatchKind Kind, int Minutes, string? ProcessName, DateTimeOffset StartedAt, DateTimeOffset? QuietSince)
{
    public string Describe() => Kind switch
    {
        WatchKind.Idle => $"when the PC has been idle for {Minutes} min",
        WatchKind.Network => $"when downloads have stopped for {Minutes} min",
        _ => $"when {ProcessName} closes",
    };
}

/// <summary>Shuts the PC down once it is done with its work. Call <see cref="Tick"/> every few seconds.</summary>
public sealed class ShutdownWatcher
{
    public const double IdleCpuPercent = 15;
    public const double QuietBytesPerSecond = 150 * 1024;
    public const int MaxMinutes = 240;

    /// <summary>Someone at the PC counts as "using it" until this long after their last input.</summary>
    public static readonly TimeSpan UserAwayAfter = TimeSpan.FromMinutes(1);

    /// <summary>Always at least this much warning, so the shutdown can still be cancelled.</summary>
    public static readonly TimeSpan MinimumWarning = TimeSpan.FromSeconds(60);

    private readonly IPcMonitor _monitor;
    private readonly IPowerController _power;
    private readonly Func<TimeSpan> _gracePeriod;
    private readonly TimeProvider _clock;
    private readonly object _lock = new();
    private WatchState? _current;

    public ShutdownWatcher(IPcMonitor monitor, IPowerController power, Func<TimeSpan>? gracePeriod = null, TimeProvider? clock = null)
    {
        _monitor = monitor;
        _power = power;
        _gracePeriod = gracePeriod ?? (() => TimeSpan.Zero);
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>Raised after starting, cancelling or triggering. May be raised on any thread.</summary>
    public event EventHandler? Changed;

    /// <summary>Raised when the condition was met and the shutdown was scheduled.</summary>
    public event EventHandler<WatchState>? Triggered;

    public WatchState? Current
    {
        get { lock (_lock) return _current; }
    }

    public void Start(WatchRequest request)
    {
        if (request.Kind != WatchKind.AppExit && request.Minutes is < 1 or > MaxMinutes)
            throw new ArgumentOutOfRangeException(nameof(request), $"Pick between 1 and {MaxMinutes} minutes.");

        string? process = null;
        if (request.Kind == WatchKind.AppExit)
        {
            process = request.ProcessName?.Trim();
            if (string.IsNullOrEmpty(process))
                throw new ArgumentException("Pick the app to wait for.", nameof(request));
            if (!_monitor.IsProcessRunning(process))
                throw new ArgumentException($"{process} isn't running.", nameof(request));
        }

        lock (_lock)
            _current = new WatchState(request.Kind, request.Minutes, process, _clock.GetUtcNow(), null);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Cancel()
    {
        lock (_lock)
        {
            if (_current is null) return;
            _current = null;
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Tick()
    {
        WatchState? triggered = null;
        lock (_lock)
        {
            if (_current is not { } state) return;

            var now = _clock.GetUtcNow();
            bool done;
            if (state.Kind == WatchKind.AppExit)
            {
                done = !_monitor.IsProcessRunning(state.ProcessName!);
            }
            else
            {
                var snapshot = _monitor.GetSnapshot();
                var userAway = snapshot.UserIdle >= UserAwayAfter;
                var quiet = userAway && (state.Kind == WatchKind.Idle
                    ? snapshot.CpuPercent < IdleCpuPercent
                    : snapshot.DownloadBytesPerSecond < QuietBytesPerSecond && snapshot.UploadBytesPerSecond < QuietBytesPerSecond);

                var quietSince = quiet ? state.QuietSince ?? now : (DateTimeOffset?)null;
                _current = state with { QuietSince = quietSince };
                done = quietSince is { } since && now - since >= TimeSpan.FromMinutes(state.Minutes);
            }

            if (done)
            {
                var grace = _gracePeriod();
                _power.ScheduleShutdown(grace > MinimumWarning ? grace : MinimumWarning);
                triggered = state;
                _current = null;
            }
        }

        if (triggered is not null)
        {
            Triggered?.Invoke(this, triggered);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
