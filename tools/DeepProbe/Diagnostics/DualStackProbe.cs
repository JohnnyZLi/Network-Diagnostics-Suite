using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using NetworkDeepProbe.Models;

namespace NetworkDeepProbe.Diagnostics;

internal static class DualStackProbe
{
    public static async Task<DualStackReport> RunAsync(Uri origin, CancellationToken cancellationToken)
    {
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(origin.Host, cancellationToken);
            var ipv4Address = addresses.FirstOrDefault(item => item.AddressFamily == AddressFamily.InterNetwork);
            var ipv6Address = addresses.FirstOrDefault(item => item.AddressFamily == AddressFamily.InterNetworkV6);
            var port = origin.IsDefaultPort ? (origin.Scheme == Uri.UriSchemeHttps ? 443 : 80) : origin.Port;
            var ipv4Task = ProbeAsync("IPv4", ipv4Address, port, cancellationToken);
            var ipv6Task = ProbeAsync("IPv6", ipv6Address, port, cancellationToken);
            await Task.WhenAll(ipv4Task, ipv6Task);
            var ipv4 = await ipv4Task;
            var ipv6 = await ipv6Task;
            var preferred = Preferred(ipv4, ipv6);
            var nat64 = ipv6Address is not null && IsWellKnownNat64(ipv6Address) && ipv4Address is null;
            var status = !ipv4.AddressAvailable && !ipv6.AddressAvailable
                ? "unavailable"
                : ipv4.TcpReachable || ipv6.TcpReachable
                    ? "measured"
                    : "unreachable";
            return new DualStackReport(ipv4, ipv6, preferred, nat64, status);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            var unavailable4 = Unavailable("IPv4", SafeError(error));
            var unavailable6 = Unavailable("IPv6", SafeError(error));
            return new DualStackReport(unavailable4, unavailable6, "none", false, "unavailable");
        }
    }

    private static async Task<AddressFamilyProbeReport> ProbeAsync(
        string family,
        IPAddress? address,
        int port,
        CancellationToken cancellationToken)
    {
        if (address is null) return Unavailable(family, "No address was returned by DNS.");
        double? pingMedian = null;
        var pingAvailable = false;
        try
        {
            var ping = await PingProbe.RunAsync(family, address, 4, false, cancellationToken);
            pingMedian = ping.Statistics.MedianMs;
            pingAvailable = ping.Statistics.Received > 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // TCP reachability remains useful when ICMP is filtered.
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            await socket.ConnectAsync(new IPEndPoint(address, port), cancellationToken).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(4), cancellationToken);
            stopwatch.Stop();
            return new AddressFamilyProbeReport(
                family,
                true,
                address.ToString(),
                pingAvailable,
                pingMedian,
                true,
                stopwatch.Elapsed.TotalMilliseconds,
                null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            stopwatch.Stop();
            return new AddressFamilyProbeReport(
                family,
                true,
                address.ToString(),
                pingAvailable,
                pingMedian,
                false,
                null,
                SafeError(error));
        }
    }

    private static AddressFamilyProbeReport Unavailable(string family, string error) =>
        new(family, false, null, false, null, false, null, error);

    private static string Preferred(AddressFamilyProbeReport ipv4, AddressFamilyProbeReport ipv6)
    {
        if (ipv4.TcpReachable && ipv6.TcpReachable)
        {
            return (ipv6.TcpConnectMs ?? double.MaxValue) <= (ipv4.TcpConnectMs ?? double.MaxValue) ? "IPv6" : "IPv4";
        }
        if (ipv6.TcpReachable) return "IPv6";
        if (ipv4.TcpReachable) return "IPv4";
        return "none";
    }

    private static bool IsWellKnownNat64(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes.Length == 16
            && bytes[0] == 0x00
            && bytes[1] == 0x64
            && bytes[2] == 0xff
            && bytes[3] == 0x9b;
    }

    private static string SafeError(Exception error)
    {
        var value = string.IsNullOrWhiteSpace(error.Message) ? error.GetType().Name : error.Message;
        return value.Length <= 240 ? value : $"{value[..237]}...";
    }
}
