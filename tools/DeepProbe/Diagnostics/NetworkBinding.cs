using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using NetworkDeepProbe.Models;

namespace NetworkDeepProbe.Diagnostics;

internal sealed record ResolvedNetworkBinding(
    NetworkInterfaceChoice Choice,
    IPAddress SourceAddress,
    IPAddress? Gateway);

public sealed record NetworkInterfaceChoice(
    string Id,
    string Name,
    string Description,
    string Type,
    long? LinkSpeedMbps,
    bool SupportsIpv4,
    bool SupportsIpv6);

internal static class NetworkBindingResolver
{
    public static IReadOnlyList<NetworkInterfaceChoice> ListChoices()
    {
        return GetCandidates()
            .Select(item => new NetworkInterfaceChoice(
                item.Network.Id,
                item.Network.Name,
                item.Network.Description,
                item.Network.NetworkInterfaceType.ToString(),
                TryGetLinkSpeedMbps(item.Network),
                item.Network.Supports(NetworkInterfaceComponent.IPv4),
                item.Network.Supports(NetworkInterfaceComponent.IPv6)))
            .OrderByDescending(item => item.Type is "Ethernet" or "Wireless80211")
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static ResolvedNetworkBinding? Resolve(string? interfaceId)
    {
        if (string.IsNullOrWhiteSpace(interfaceId)) return null;

        var candidate = GetCandidates().FirstOrDefault(item =>
            string.Equals(item.Network.Id, interfaceId, StringComparison.Ordinal)
            || string.Equals(item.Network.Name, interfaceId, StringComparison.OrdinalIgnoreCase));
        if (candidate.Network is null || candidate.Properties is null)
        {
            throw new ArgumentException($"Network interface '{interfaceId}' is not active or no longer exists.", nameof(interfaceId));
        }

        var source = candidate.Properties.UnicastAddresses
            .Select(item => item.Address)
            .FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
            ?? candidate.Properties.UnicastAddresses
                .Select(item => item.Address)
                .FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetworkV6 && !address.IsIPv6LinkLocal && !IPAddress.IsLoopback(address))
            ?? throw new InvalidOperationException($"Network interface '{candidate.Network.Name}' has no usable unicast address.");
        var gateway = candidate.Properties.GatewayAddresses
            .Select(item => item.Address)
            .FirstOrDefault(address => address.AddressFamily == source.AddressFamily && !IPAddress.Any.Equals(address));

        return new ResolvedNetworkBinding(
            new NetworkInterfaceChoice(
                candidate.Network.Id,
                candidate.Network.Name,
                candidate.Network.Description,
                candidate.Network.NetworkInterfaceType.ToString(),
                TryGetLinkSpeedMbps(candidate.Network),
                candidate.Network.Supports(NetworkInterfaceComponent.IPv4),
                candidate.Network.Supports(NetworkInterfaceComponent.IPv6)),
            source,
            gateway);
    }

    public static SelectedInterfaceReport? CreateReport(
        ResolvedNetworkBinding? binding,
        bool includeAddresses)
    {
        if (binding is null) return null;
        return new SelectedInterfaceReport(
            binding.Choice.Id,
            binding.Choice.Name,
            binding.Choice.Description,
            binding.Choice.Type,
            binding.Choice.LinkSpeedMbps,
            "HTTP/1.1 and HTTP/2 transfers plus LAN sockets are source-bound. ICMP, traceroute, DNS, and HTTP/3 remain routed by the operating system.",
            includeAddresses ? binding.SourceAddress.ToString() : null);
    }

    private static IReadOnlyList<(NetworkInterface Network, IPInterfaceProperties Properties)> GetCandidates()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(network => network.OperationalStatus == OperationalStatus.Up)
            .Where(network => network.NetworkInterfaceType is not NetworkInterfaceType.Loopback and not NetworkInterfaceType.Tunnel)
            .Select(network => (Network: network, Properties: TryGetProperties(network)))
            .Where(item => item.Properties is not null)
            .Select(item => (item.Network, item.Properties!))
            .Where(item => item.Item2.UnicastAddresses.Any(address =>
                address.Address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6))
            .ToArray();
    }

    private static IPInterfaceProperties? TryGetProperties(NetworkInterface network)
    {
        try { return network.GetIPProperties(); }
        catch (Exception error) when (error is NetworkInformationException or PlatformNotSupportedException) { return null; }
    }

    private static long? TryGetLinkSpeedMbps(NetworkInterface network)
    {
        try { return network.Speed > 0 ? network.Speed / 1_000_000 : null; }
        catch (PlatformNotSupportedException) { return null; }
    }
}

internal static class BoundHttpClientFactory
{
    public static HttpClient Create(int maximumConnections, IPAddress? sourceAddress = null)
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.None,
            ConnectTimeout = TimeSpan.FromSeconds(5),
            MaxConnectionsPerServer = maximumConnections,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        };
        if (sourceAddress is not null)
        {
            handler.ConnectCallback = async (context, cancellationToken) =>
            {
                var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
                var compatible = addresses
                    .Where(address => address.AddressFamily == sourceAddress.AddressFamily)
                    .ToArray();
                if (compatible.Length == 0)
                {
                    throw new SocketException((int)SocketError.AddressFamilyNotSupported);
                }

                Exception? lastError = null;
                foreach (var address in compatible)
                {
                    var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
                    {
                        NoDelay = true
                    };
                    try
                    {
                        socket.Bind(new IPEndPoint(sourceAddress, 0));
                        await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch (Exception error) when (error is SocketException or OperationCanceledException)
                    {
                        socket.Dispose();
                        if (error is OperationCanceledException) throw;
                        lastError = error;
                    }
                }
                throw lastError ?? new SocketException((int)SocketError.HostUnreachable);
            };
        }

        var client = new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("NetworkDiagnosticsSuite/2.0");
        return client;
    }
}
