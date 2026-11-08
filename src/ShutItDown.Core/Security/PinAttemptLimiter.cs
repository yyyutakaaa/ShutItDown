namespace ShutItDown.Core.Security;

/// <summary>Locks a client out for a while after too many wrong PINs, so a short PIN cannot be brute-forced.</summary>
public sealed class PinAttemptLimiter
{
    private int _maxFailures;
    private readonly TimeSpan _lockout;
    private readonly TimeProvider _clock;
    private readonly Dictionary<string, Entry> _entries = new();
    private readonly object _lock = new();

    public PinAttemptLimiter(int maxFailures = 5, TimeSpan? lockout = null, TimeProvider? clock = null)
    {
        _maxFailures = maxFailures;
        _lockout = lockout ?? TimeSpan.FromMinutes(1);
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>Wrong PINs in a row before the lockout starts. Can be changed while the server runs.</summary>
    public int MaxFailures
    {
        get { lock (_lock) return _maxFailures; }
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            lock (_lock) _maxFailures = value;
        }
    }

    /// <summary>How long <paramref name="client"/> still has to wait, or <see cref="TimeSpan.Zero"/> when it may try.</summary>
    public TimeSpan GetLockoutRemaining(string client)
    {
        lock (_lock)
        {
            if (!_entries.TryGetValue(client, out var entry) || entry.LockedUntil is not { } until)
                return TimeSpan.Zero;

            var remaining = until - _clock.GetUtcNow();
            if (remaining > TimeSpan.Zero)
                return remaining;

            _entries.Remove(client);
            return TimeSpan.Zero;
        }
    }

    /// <summary>Records a wrong PIN and returns how many attempts are left before the lockout starts.</summary>
    public int RegisterFailure(string client)
    {
        lock (_lock)
        {
            _entries.TryGetValue(client, out var entry);
            var failures = entry.Failures + 1;
            if (failures >= _maxFailures)
            {
                _entries[client] = new Entry(0, _clock.GetUtcNow() + _lockout);
                return 0;
            }

            _entries[client] = new Entry(failures, null);
            return _maxFailures - failures;
        }
    }

    public void Reset(string client)
    {
        lock (_lock)
        {
            _entries.Remove(client);
        }
    }

    private readonly record struct Entry(int Failures, DateTimeOffset? LockedUntil);
}
