using ShutItDown.Core.Security;

namespace ShutItDown.Tests;

public class PinHasherTests
{
    [Theory]
    [InlineData("1234", true)]
    [InlineData("12345678", true)]
    [InlineData("123", false)]
    [InlineData("123456789", false)]
    [InlineData("12a4", false)]
    [InlineData("١٢٣٤", false)] // non-ASCII digits
    [InlineData(null, false)]
    public void IsValidFormat(string? pin, bool expected) =>
        Assert.Equal(expected, PinHasher.IsValidFormat(pin));

    [Fact]
    public void Verify_accepts_the_right_pin_and_rejects_others()
    {
        var hash = PinHasher.Hash("4821");

        Assert.True(PinHasher.Verify("4821", hash));
        Assert.False(PinHasher.Verify("4822", hash));
        Assert.False(PinHasher.Verify("", hash));
        Assert.False(PinHasher.Verify(null, hash));
    }

    [Fact]
    public void Hash_does_not_contain_the_pin_and_is_salted()
    {
        var first = PinHasher.Hash("4821");
        var second = PinHasher.Hash("4821");

        Assert.DoesNotContain("4821", first);
        Assert.NotEqual(first, second);
    }

    [Theory]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("v1.notanumber.AAAA.AAAA")]
    [InlineData("v1.0.AAAA.AAAA")]
    [InlineData("v1.1000.!!!.AAAA")]
    public void Verify_returns_false_for_a_broken_hash(string storedHash) =>
        Assert.False(PinHasher.Verify("1234", storedHash));

    [Fact]
    public void Hash_rejects_an_invalid_pin() =>
        Assert.Throws<ArgumentException>(() => PinHasher.Hash("12"));
}
