using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using NetworkDeepProbe.Models;

namespace NetworkDeepProbe.Diagnostics;

internal static class DualStackProbe
{
    public static async Task<DualStackReport> RunAsync(Uri origin, CancellationToken cancellationToken)
    {
        try
        {
            var resolutionStopwatch = Stopwatch.StartNew();
            var addresses = await Dns.GetHostAddressesAsync(origin.Host, cancellationToken);
            resolutionStopwatch.Stop();
            var ipv4Addresses = addresses.Where(item => item.AddressFamily == AddressFamily.InterNetwork).ToArray();
            var ipv6Addresses = addresses.Where(item => item.AddressFamily == AddressFamily.InterNetworkV6).ToArray();
            var ipv4Task = ProbeAsync("IPv4", origin, ipv4Addresses.FirstOrDefault(), cancellationToken);
            var ipv6Task = ProbeAsync("IPv6", origin, ipv6Addresses.FirstOrDefault(), cancellationToken);
            await Task.WhenAll(ipv4Task, ipv6Task);
            var ipv4 = await ipv4Task;
            var ipv6 = await ipv6Task;
            var preferred = Preferred(ipv4, ipv6);
            var nat64 = ipv6Addresses.Any(IsWellKnownNat64) && ipv4Addresses.Length == 0;
            var status = !ipv4.AddressAvailable && !ipv6.AddressAvailable
                ? "unavailable"
                : ipv4.TcpReachable || ipv6.TcpReachable
                    ? "measured"
                    : "unreachable";
            var difference = ipv4.TcpConnectMs is { } ipv4Ms && ipv6.TcpConnectMs is { } ipv6Ms
                ? Math.Abs(ipv4Ms - ipv6Ms)
                : null;
            return new DualStackReport(
                ipv4,
                ipv6,
                preferred,
                nat64,
                status,
                resolutionStopwatch.Elapsed.TotalMilliseconds,
                ipv4Addresses.Length,
                ipv6Addresses.Length,
                preferred == "none" ? null : preferred,
                difference);
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
        Uri origin,
        IPAddress? address,
        CancellationToken cancellationToken)
    {
        if (address is null) return Unavailable(family, "No address was returned by DNS.");
        var port = origin.IsDefaultPort ? (origin.Scheme == Uri.UriSchemeHttps ? 443 : 80) : origin.Port;
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
            // TCP, TLS, and HTTP evidence remain useful when ICMP is filtered.
        }

        var connectStopwatch = Stopwatch.StartNew();
        try
        {
            using (var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
            {
                await socket.ConnectAsync(new IPEndPoint(address, port), cancellationToken).AsTask()
                    .WaitAsync(TimeSpan.FromSeconds(4), cancellationToken);
            }
            connectStopwatch.Stop();
            var application = await ProbeApplicationAsync(origin, address, port, cancellationToken);
            return new AddressFamilyProbeReport(
                family,
                true,
                address.ToString(),
                pingAvailable,
                pingMedian,
                true,
                connectStopwatch.Elapsed.TotalMilliseconds,
                application.Error,
                application.TlsReachable,
                application.TlsHandshakeMs,
                application.TlsProtocol,
                application.ApplicationProtocol,
                application.HttpReachable,
                application.HttpResponseMs,
                application.HttpStatusCode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            connectStopwatch.Stop();
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

    private static async Task<ApplicationProbeResult> ProbeApplicationAsync(
        Uri origin,
        IPAddress address,
        int port,
        CancellationToken cancellationToken)
    {
        using var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        await socket.ConnectAsync(new IPEndPoint(address, port), cancellationToken).AsTask()
            .WaitAsync(TimeSpan.FromSeconds(4), cancellationToken);
        await using var networkStream = new NetworkStream(socket, ownsSocket: false);
        Stream stream = networkStream;
        SslStream? sslStream = null;
        var tlsReachable = false;
        double? tlsHandshakeMs = null;
        string? tlsProtocol = null;
        string? applicationProtocol = null;
        try
        {
            if (origin.Scheme == Uri.UriSchemeHttps)
            {
                sslStream = new SslStream(networkStream, leaveInnerStreamOpen: true);
                var tlsStopwatch = Stopwatch.StartNew();
                await sslStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                {
                    TargetHost = origin.Host,
                    ApplicationProtocols = [SslApplicationProtocol.Http11]
                }, cancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
                tlsStopwatch.Stop();
                tlsReachable = true;
                tlsHandshakeMs = tlsStopwatch.Elapsed.TotalMilliseconds;
                tlsProtocol = sslStream.SslProtocol.ToString();
                applicationProtocol = sslStream.NegotiatedApplicationProtocol.ToString();
                stream = sslStream;
            }

            var path = string.IsNullOrWhiteSpace(origin.AbsolutePath) || origin.AbsolutePath == "/"
                ? "/api/ping"
                : $"{origin.AbsolutePath.TrimEnd('/')}/api/ping";
            var request = Encoding.ASCII.GetBytes(
                $"GET {path}?family={address.AddressFamily}&nonce={Guid.NewGuid():N} HTTP/1.1\r\nHost: {origin.Host}\r\nAccept: */*\r\nConnection: close\r\n\r\n");
            var httpStopwatch = Stopwatch.StartNew();
            await stream.WriteAsync(request, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            var buffer = new byte[512];
            var read = await stream.ReadAsync(buffer, cancellationToken).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            httpStopwatch.Stop();
            var statusCode = read <= 0 ? null : ParseStatusCode(buffer.AsSpan(0, read));
            return new ApplicationProbeResult(
                tlsReachable,
                tlsHandshakeMs,
                tlsProtocol,
                applicationProtocol,
                statusCode is >= 200 and < 500,
                httpStopwatch.Elapsed.TotalMilliseconds,
                statusCode,
                statusCode is null ? "The HTTP probe returned no parseable status line." : null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            return new ApplicationProbeResult(
                tlsReachable,
                tlsHandshakeMs,
                tlsProtocol,
                applicationProtocol,
                false,
                null,
                null,
                SafeError(error));
        }
        finally
        {
            if (sslStream is not null) await sslStream.DisposeAsync();
        }
    }

    private static int? ParseStatusCode(ReadOnlySpan<byte> response)
    {
        var lineEnd = response.IndexOf("\r\n"u8);
        var line = lineEnd >= 0 ? response[..lineEnd] : response;
        var text = Encoding.ASCII.GetString(line);
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && int.TryParse(parts[1], out var statusCode) ? statusCode : null;
    }

    private static AddressFamilyProbeReport Unavailable(string family, string error) =>
        new(family, false, null, false, null, false, null, error);

    private static string Preferred(AddressFamilyProbeReport ipv4, AddressFamilyProbeReport ipv6)
    {
        if (ipv4.HttpReachable && ipv6.HttpReachable)
        {
            return (ipv6.TcpConnectMs ?? double.MaxValue) <= (ipv4.TcpConnectMs ?? double.MaxValue) ? "IPv6" : "IPv4";
        }
        if (ipv6.HttpReachable) return "IPv6";
        if (ipv4.HttpReachable) return "IPv4";
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

    private sealed record ApplicationProbeResult(
        bool TlsReachable,
        double? TlsHandshakeMs,
        string? TlsProtocol,
        string? ApplicationProtocol,
        bool HttpReachable,
        double? HttpResponseMs,
        int? HttpStatusCode,
        string? Error);
}
