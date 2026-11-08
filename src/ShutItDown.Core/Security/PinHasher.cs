using System.Security.Cryptography;

namespace ShutItDown.Core.Security;

/// <summary>Stores the PIN as a salted PBKDF2 hash, so the settings file never contains the PIN itself.</summary>
public static class PinHasher
{
    public const int MinLength = 4;
    public const int MaxLength = 8;

    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const string Version = "v1";

    public static bool IsValidFormat(string? pin) =>
        pin is { Length: >= MinLength and <= MaxLength } && pin.All(char.IsAsciiDigit);

    public static string Hash(string pin)
    {
        if (!IsValidFormat(pin))
            throw new ArgumentException($"PIN must be {MinLength}-{MaxLength} digits.", nameof(pin));

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Derive(pin, salt, Iterations);
        return string.Join('.', Version, Iterations, Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    public static bool Verify(string? pin, string storedHash)
    {
        if (pin is null) return false;

        var parts = storedHash.Split('.');
        if (parts.Length != 4 || parts[0] != Version || !int.TryParse(parts[1], out var iterations) || iterations <= 0)
            return false;

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Derive(pin, salt, iterations);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static byte[] Derive(string pin, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(pin, salt, iterations, HashAlgorithmName.SHA256, HashSize);
}
