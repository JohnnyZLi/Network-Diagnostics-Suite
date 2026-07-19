using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using NetworkDeepProbe.Models;

namespace NetworkDeepProbe.Diagnostics;

internal static class TraceRouteProbe
{
    private static readonly byte[] Payload = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();

    public static async Task<TraceRouteReport> RunAsync(
        string target,
        IPAddress destination,
        int maximumHops,
        bool includeLocalAddresses,
        CancellationToken cancellationToken)
    {
        var hops = new List<TraceHop>();
        var reachedDestination = false;

        for (var ttl = 1; ttl <= maximumHops && !reachedDestination; ttl++)
        {
            IPAddress? hopAddress = null;
            var roundTrips = new List<double?>(3);
            using var ping = new Ping();
            for (var probe = 0; probe < 3; probe++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var reply = await ping.SendPingAsync(destination, 1_200, Payload, new PingOptions(ttl, false))
                        .WaitAsync(cancellationToken);
                    if (reply.Address is not null) hopAddress ??= reply.Address;
                    if (reply.Status is IPStatus.Success or IPStatus.TtlExpired)
                    {
                        roundTrips.Add(reply.RoundtripTime);
                    }
                    else
                    {
                        roundTrips.Add(null);
                    }
                    if (reply.Status == IPStatus.Success) reachedDestination = true;
                }
                catch (PingException)
                {
                    roundTrips.Add(null);
                }
            }

            var addressRedacted = hopAddress is not null
                && !includeLocalAddresses
                && IsLocalOrPrivate(hopAddress);
            var hostname = hopAddress is null || addressRedacted
                ? null
                : await TryReverseLookupAsync(hopAddress, cancellationToken);
            hops.Add(new TraceHop(
                ttl,
                addressRedacted ? null : hopAddress?.ToString(),
                hostname,
                roundTrips,
                reachedDestination,
                addressRedacted));
        }

        var destinationRedacted = !includeLocalAddresses && IsLocalOrPrivate(destination);
        return new TraceRouteReport(
            target,
            destinationRedacted ? null : destination.ToString(),
            maximumHops,
            reachedDestination,
            hops);
    }

    internal static bool IsLocalOrPrivate(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) || IPAddress.Any.Equals(address) || IPAddress.IPv6Any.Equals(address))
        {
            return true;
        }

        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return bytes[0] == 10
                || bytes[0] == 127
                || (bytes[0] == 169 && bytes[1] == 254)
                || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
                || (bytes[0] == 192 && bytes[1] == 168)
                || (bytes[0] == 100 && bytes[1] is >= 64 and <= 127);
        }

        return address.IsIPv6LinkLocal
            || address.IsIPv6SiteLocal
            || address.IsIPv6Multicast
            || (bytes[0] & 0xfe) == 0xfc;
    }

    private static async Task<string?> TryReverseLookupAsync(IPAddress address, CancellationToken cancellationToken)
    {
        try
        {
            var lookup = Dns.GetHostEntryAsync(address);
            var result = await lookup.WaitAsync(TimeSpan.FromMilliseconds(600), cancellationToken);
            return string.Equals(result.HostName, address.ToString(), StringComparison.Ordinal) ? null : result.HostName;
        }
        catch (Exception error) when (error is SocketException or TimeoutException)
        {
            return null;
        }
    }
}
