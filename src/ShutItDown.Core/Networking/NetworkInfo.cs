using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace ShutItDown.Core.Networking;

/// <summary>The address a phone uses to reach this PC.</summary>
/// <param name="ViaTailscale">True when it is a Tailscale address, which works from anywhere and never changes.</param>
public sealed record PhoneAddress(string Host, bool ViaTailscale);

public static class NetworkInfo
{
    /// <summary>Picks the Tailscale address when wanted and available, otherwise the Wi-Fi/LAN address.</summary>
    public static PhoneAddress GetPhoneAddress(bool preferTailscale)
    {
        if (preferTailscale && GetTailscaleAddress() is { } tailscale)
            return new PhoneAddress(tailscale, ViaTailscale: true);
        return new PhoneAddress(GetLanAddress(), ViaTailscale: false);
    }

    public static string? GetTailscaleAddress() =>
        GetIPv4Addresses().FirstOrDefault(a => a.IsTailscale)?.Address.ToString();

    /// <summary>
    /// Returns the IPv4 address a phone on the same Wi-Fi can reach. Adapters with a default gateway
    /// come first, so virtual adapters (WSL, Hyper-V, VPN) are not picked over the real network.
    /// </summary>
    public static string GetLanAddress() =>
        GetIPv4Addresses()
            .Where(a => !a.IsTailscale)
            .OrderByDescending(a => a.HasGateway)
            .Select(a => a.Address.ToString())
            .FirstOrDefault() ?? "127.0.0.1";

    /// <summary>Tailscale hands out addresses in 100.64.0.0/10.</summary>
    public static bool IsTailscaleRange(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes.Length == 4 && bytes[0] == 100 && (bytes[1] & 0xC0) == 64;
    }

    private sealed record Candidate(IPAddress Address, bool HasGateway, bool IsTailscale);

    private static IEnumerable<Candidate> GetIPv4Addresses()
    {
        NetworkInterface[] nics;
        try
        {
            nics = NetworkInterface.GetAllNetworkInterfaces();
        }
        catch (NetworkInformationException)
        {
            yield break;
        }

        foreach (var nic in nics)
        {
            if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;

            var props = nic.GetIPProperties();
            var hasGateway = props.GatewayAddresses.Any(g =>
                g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any));
            var namedTailscale = nic.Name.Contains("Tailscale", StringComparison.OrdinalIgnoreCase)
                || nic.Description.Contains("Tailscale", StringComparison.OrdinalIgnoreCase);

            foreach (var unicast in props.UnicastAddresses)
            {
                var address = unicast.Address;
                if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address))
                    continue;

                var isTailscale = namedTailscale && IsTailscaleRange(address);
                // Other tunnels (VPNs) are never useful for a phone on the LAN.
                if (!isTailscale && nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                    continue;

                yield return new Candidate(address, hasGateway, isTailscale);
            }
        }
    }
}
