using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using NetworkDeepProbe.Models;

namespace NetworkDeepProbe.Diagnostics;

internal sealed record InterfaceProbeResult(
    IReadOnlyList<NetworkInterfaceReport> Reports,
    IPAddress? PrimaryGateway);

internal static class InterfaceProbe
{
    public static InterfaceProbeResult Collect(bool includeAddresses)
    {
        var candidates = NetworkInterface.GetAllNetworkInterfaces()
            .Where(network => network.OperationalStatus == OperationalStatus.Up)
            .Where(network => network.NetworkInterfaceType is not NetworkInterfaceType.Loopback and not NetworkInterfaceType.Tunnel)
            .Select(network => new { Network = network, Properties = TryGetProperties(network) })
            .Where(item => item.Properties is not null)
            .ToArray();

        var reports = candidates.Select(item =>
        {
            var properties = item.Properties!;
            var ipv4 = TryGetIpv4Properties(properties);
            return new NetworkInterfaceReport(
                item.Network.Name,
                item.Network.Description,
                item.Network.NetworkInterfaceType.ToString(),
                item.Network.Speed > 0 ? item.Network.Speed / 1_000_000 : null,
                ipv4?.Mtu,
                item.Network.Supports(NetworkInterfaceComponent.IPv4),
                item.Network.Supports(NetworkInterfaceComponent.IPv6),
                includeAddresses
                    ? properties.UnicastAddresses.Select(address => address.Address.ToString()).ToArray()
                    : null,
                includeAddresses
                    ? properties.GatewayAddresses.Select(gateway => gateway.Address.ToString()).ToArray()
                    : null,
                includeAddresses
                    ? properties.DnsAddresses.Select(address => address.ToString()).ToArray()
                    : null);
        }).ToArray();

        var primaryGateway = candidates
            .SelectMany(item => item.Properties!.GatewayAddresses)
            .Select(gateway => gateway.Address)
            .FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.Any.Equals(address));

        return new InterfaceProbeResult(reports, primaryGateway);
    }

    private static IPInterfaceProperties? TryGetProperties(NetworkInterface network)
    {
        try { return network.GetIPProperties(); }
        catch (NetworkInformationException) { return null; }
    }

    private static IPv4InterfaceProperties? TryGetIpv4Properties(IPInterfaceProperties properties)
    {
        try { return properties.GetIPv4Properties(); }
        catch (NetworkInformationException) { return null; }
    }
}
