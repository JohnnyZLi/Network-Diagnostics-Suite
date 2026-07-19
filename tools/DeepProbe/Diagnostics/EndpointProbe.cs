using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using NetworkDeepProbe.Models;

namespace NetworkDeepProbe.Diagnostics;

internal static class EndpointProbe
{
    private static readonly (string Name, string Host)[] Targets =
    [
        ("Cloudflare", "www.cloudflare.com"),
        ("Google", "www.google.com"),
        ("Microsoft", "www.microsoft.com"),
        ("GitHub", "github.com"),
        ("Apple", "www.apple.com"),
        ("Amazon", "www.amazon.com")
    ];

    public static async Task<IReadOnlyList<TlsEndpointReport>> RunAsync(CancellationToken cancellationToken)
    {
        var results = new List<TlsEndpointReport>(Targets.Length);
        foreach (var target in Targets)
        {
            results.Add(await TestEndpointAsync(target.Name, target.Host, cancellationToken));
        }
        return results;
    }

    private static async Task<TlsEndpointReport> TestEndpointAsync(
        string name,
        string host,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));

            var stopwatch = Stopwatch.StartNew();
            var addresses = await Dns.GetHostAddressesAsync(host, timeout.Token);
            var dnsMs = stopwatch.Elapsed.TotalMilliseconds;
            var address = addresses.FirstOrDefault(candidate => candidate.AddressFamily == AddressFamily.InterNetwork)
                ?? addresses.FirstOrDefault()
                ?? throw new SocketException((int)SocketError.HostNotFound);

            using var client = new TcpClient(address.AddressFamily);
            stopwatch.Restart();
            await client.ConnectAsync(address, 443, timeout.Token);
            var tcpMs = stopwatch.Elapsed.TotalMilliseconds;

            await using var ssl = new SslStream(client.GetStream(), false);
            stopwatch.Restart();
            await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = host,
                EnabledSslProtocols = SslProtocols.None,
                ApplicationProtocols = [SslApplicationProtocol.Http2, SslApplicationProtocol.Http11]
            }, timeout.Token);
            var tlsMs = stopwatch.Elapsed.TotalMilliseconds;

            return new TlsEndpointReport(
                name,
                host,
                true,
                dnsMs,
                tcpMs,
                tlsMs,
                ssl.SslProtocol.ToString(),
                ssl.NegotiatedApplicationProtocol.ToString(),
                null);
        }
        catch (Exception error) when (error is SocketException or AuthenticationException or IOException or OperationCanceledException)
        {
            if (error is OperationCanceledException && cancellationToken.IsCancellationRequested) throw;
            return new TlsEndpointReport(name, host, false, null, null, null, null, null, FriendlyError(error));
        }
    }

    private static string FriendlyError(Exception error) => error switch
    {
        OperationCanceledException => "Timed out",
        SocketException socket => socket.SocketErrorCode.ToString(),
        AuthenticationException => "TLS negotiation failed",
        IOException => "The encrypted connection closed unexpectedly",
        _ => "Connection failed"
    };
}
