using System.Net;
using ShutItDown.Core.Networking;
using ShutItDown.Core.Pairing;

namespace ShutItDown.Tests;

public class PairingTests
{
    private readonly FakeClock _clock = new();

    [Fact]
    public void Pairing_code_expires_and_is_replaced()
    {
        var pairing = new PairingCode(_clock);
        var first = pairing.Current;

        _clock.Advance(PairingCode.Lifetime);

        Assert.False(pairing.TryConsume(first));
        Assert.NotEqual(first, pairing.Current);
    }

    [Fact]
    public void Using_a_pairing_code_replaces_it_and_raises_changed()
    {
        var pairing = new PairingCode(_clock);
        var changes = 0;
        pairing.Changed += (_, _) => changes++;
        var code = pairing.Current;

        Assert.True(pairing.TryConsume(code));
        Assert.NotEqual(code, pairing.Current);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void Registry_finds_a_phone_by_token_and_stores_only_a_hash()
    {
        var saves = 0;
        var list = new List<PairedDevice>();
        var registry = new DeviceRegistry(list, () => saves++, _clock);

        var token = registry.Pair("Pixel");

        Assert.Equal("Pixel", registry.Find(token)?.Name);
        Assert.Null(registry.Find(token + "x"));
        Assert.Null(registry.Find(null));
        Assert.DoesNotContain(token, list.Single().TokenHash);
        Assert.Equal(1, saves);
    }

    [Fact]
    public void Each_phone_gets_its_own_token()
    {
        var registry = new DeviceRegistry(new List<PairedDevice>(), () => { });

        var a = registry.Pair("a");
        var b = registry.Pair("b");

        Assert.NotEqual(a, b);
        Assert.Equal("a", registry.Find(a)?.Name);
        Assert.Equal("b", registry.Find(b)?.Name);
    }

    [Theory]
    [InlineData(null, "Phone")]
    [InlineData("   ", "Phone")]
    [InlineData("Pixel\n8", "Pixel8")]
    [InlineData("A very long phone name that keeps going and going", "A very long phone name that keeps going ")]
    public void Phone_names_are_cleaned(string? name, string expected)
    {
        var registry = new DeviceRegistry(new List<PairedDevice>(), () => { });

        registry.Pair(name);

        Assert.Equal(expected, registry.Devices.Single().Name);
    }

    [Theory]
    [InlineData("100.64.0.1", true)]
    [InlineData("100.117.164.52", true)]
    [InlineData("100.127.255.255", true)]
    [InlineData("100.128.0.1", false)]
    [InlineData("100.63.255.255", false)]
    [InlineData("192.168.0.201", false)]
    public void Tailscale_range_is_100_64_slash_10(string address, bool expected) =>
        Assert.Equal(expected, NetworkInfo.IsTailscaleRange(IPAddress.Parse(address)));
}
