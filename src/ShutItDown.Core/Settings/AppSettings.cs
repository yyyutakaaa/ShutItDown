using ShutItDown.Core.Pairing;

namespace ShutItDown.Core.Settings;

public sealed class AppSettings
{
    public const int DefaultPort = 5050;
    public const int DefaultMaxPinAttempts = 5;
    public const int MaxGraceSeconds = 300;

    public int Port { get; set; } = DefaultPort;

    /// <summary>
    /// Hash made by <see cref="Security.PinHasher"/>. Lets phones that are not paired shut down the PC
    /// with the PIN; null means only paired phones can.
    /// </summary>
    public string? PinHash { get; set; }

    public List<PairedDevice> PairedDevices { get; set; } = new();

    /// <summary>Put the Tailscale address in the QR code when Tailscale is running, so the phone works from anywhere.</summary>
    public bool PreferTailscale { get; set; } = true;

    /// <summary>Extra time before a "now" shutdown, so it can still be cancelled on the PC.</summary>
    public int GraceSeconds { get; set; }

    /// <summary>Show a notification on the PC when a phone schedules a shutdown.</summary>
    public bool NotifyOnRequest { get; set; } = true;

    /// <summary>Wrong PINs in a row before a phone is locked out for a minute.</summary>
    public int MaxPinAttempts { get; set; } = DefaultMaxPinAttempts;

    /// <summary>Paired phones may see a screenshot of this PC.</summary>
    public bool AllowScreenPreview { get; set; } = true;

    /// <summary>The user closed the "turn on the server here" hint.</summary>
    public bool ServerHintDismissed { get; set; }

    public static bool IsValidPort(int port) => port is >= 1024 and <= 65535;

    /// <summary>A copy to save, with a stable copy of the device list (phones can pair while saving).</summary>
    public AppSettings Snapshot(IEnumerable<PairedDevice> devices)
    {
        var copy = (AppSettings)MemberwiseClone();
        copy.PairedDevices = devices.ToList();
        return copy;
    }

    /// <summary>Replaces out-of-range values (for example from a hand-edited file) with defaults.</summary>
    public void Normalize()
    {
        if (!IsValidPort(Port)) Port = DefaultPort;
        if (GraceSeconds is < 0 or > MaxGraceSeconds) GraceSeconds = 0;
        if (MaxPinAttempts is < 1 or > 20) MaxPinAttempts = DefaultMaxPinAttempts;
        PairedDevices ??= new();
        PairedDevices.RemoveAll(d => d is null || string.IsNullOrEmpty(d.TokenHash));
    }
}
