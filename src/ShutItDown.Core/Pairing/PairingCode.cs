using System.Security.Cryptography;
using System.Text;

namespace ShutItDown.Core.Pairing;

/// <summary>
/// The one-time secret inside the QR code. It works once and only for a few minutes,
/// so a photo of an old QR code is useless.
/// </summary>
public sealed class PairingCode
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private readonly TimeProvider _clock;
    private readonly object _lock = new();
    private string _code = "";
    private DateTimeOffset _expiresAt;

    public PairingCode(TimeProvider? clock = null)
    {
        _clock = clock ?? TimeProvider.System;
        Renew();
    }

    /// <summary>Raised when a new code replaces the old one (after use or expiry). May be raised on any thread.</summary>
    public event EventHandler? Changed;

    /// <summary>The code to put in the QR code. A fresh one is made when the old one expired.</summary>
    public string Current
    {
        get
        {
            bool renewed;
            string code;
            lock (_lock)
            {
                renewed = _clock.GetUtcNow() >= _expiresAt;
                if (renewed) RenewLocked();
                code = _code;
            }
            if (renewed) Changed?.Invoke(this, EventArgs.Empty);
            return code;
        }
    }

    /// <summary>Checks <paramref name="code"/> and, if it is right and not expired, uses it up.</summary>
    public bool TryConsume(string? code)
    {
        if (string.IsNullOrEmpty(code)) return false;

        lock (_lock)
        {
            var valid = _clock.GetUtcNow() < _expiresAt
                && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(code), Encoding.ASCII.GetBytes(_code));
            if (!valid) return false;
            RenewLocked();
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void Renew()
    {
        lock (_lock) RenewLocked();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void RenewLocked()
    {
        // 96 random bits, URL-safe: short enough for an easy-to-scan QR code, far too many to guess.
        _code = Convert.ToBase64String(RandomNumberGenerator.GetBytes(12)).Replace('+', '-').Replace('/', '_');
        _expiresAt = _clock.GetUtcNow() + Lifetime;
    }
}
