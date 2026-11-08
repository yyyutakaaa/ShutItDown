namespace ShutItDown.Core.Pairing;

/// <summary>A phone that scanned the pairing QR code. Only a hash of its secret token is stored.</summary>
public sealed class PairedDevice
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Phone";
    public string TokenHash { get; set; } = "";
    public DateTimeOffset PairedAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
}
