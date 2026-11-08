using System.Security.Cryptography;
using System.Text;

namespace ShutItDown.Core.Pairing;

/// <summary>
/// Keeps track of paired phones. Each phone gets a random 256-bit token; only its SHA-256 hash is kept,
/// so the settings file can't be used to impersonate a phone.
/// </summary>
public sealed class DeviceRegistry
{
    public const int MaxNameLength = 40;

    private readonly List<PairedDevice> _devices;
    private readonly Action _onChanged;
    private readonly TimeProvider _clock;
    private readonly object _lock = new();

    /// <param name="devices">The list stored in the settings; it is changed in place.</param>
    /// <param name="onChanged">Called after every change, for example to save the settings.</param>
    public DeviceRegistry(List<PairedDevice> devices, Action onChanged, TimeProvider? clock = null)
    {
        _devices = devices;
        _onChanged = onChanged;
        _clock = clock ?? TimeProvider.System;
    }

    public IReadOnlyList<PairedDevice> Devices
    {
        get { lock (_lock) return _devices.ToList(); }
    }

    /// <summary>Adds a phone and returns the token it must send from now on.</summary>
    public string Pair(string? name)
    {
        var token = Base64Url(RandomNumberGenerator.GetBytes(32));
        var device = new PairedDevice
        {
            Name = CleanName(name),
            TokenHash = Hash(token),
            PairedAt = _clock.GetUtcNow(),
        };

        lock (_lock) _devices.Add(device);
        _onChanged();
        return token;
    }

    /// <summary>Returns the phone that owns <paramref name="token"/>, or null.</summary>
    public PairedDevice? Find(string? token)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 100)
            return null;

        var hash = Encoding.ASCII.GetBytes(Hash(token));
        lock (_lock)
        {
            return _devices.FirstOrDefault(d =>
                CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(d.TokenHash), hash));
        }
    }

    public void MarkUsed(PairedDevice device)
    {
        lock (_lock) device.LastUsedAt = _clock.GetUtcNow();
        _onChanged();
    }

    public bool Remove(string id)
    {
        bool removed;
        lock (_lock) removed = _devices.RemoveAll(d => d.Id == id) > 0;
        if (removed) _onChanged();
        return removed;
    }

    public void RemoveAll()
    {
        lock (_lock) _devices.Clear();
        _onChanged();
    }

    private static string CleanName(string? name)
    {
        var clean = new string((name ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (clean.Length == 0) return "Phone";
        return clean.Length > MaxNameLength ? clean[..MaxNameLength] : clean;
    }

    private static string Hash(string token) => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
