using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using NetworkDeepProbe.Diagnostics;

namespace NetworkDeepProbe.Tests;

public sealed class LanThroughputTests
{
    [Fact]
    public async Task ServerAcceptsPingAndStopsCleanly()
    {
        var port = ReserveTcpPort();
        var messages = new ConcurrentQueue<string>();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var server = LanThroughputServer.RunAsync(
            port,
            new InlineProgress<string>(messages.Enqueue),
            cancellation.Token);

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port, cancellation.Token);
            var stream = client.GetStream();
            await stream.WriteAsync("NDS/1 PING\n"u8.ToArray(), cancellation.Token);

            var response = new byte[5];
            await stream.ReadExactlyAsync(response, cancellation.Token);

            Assert.Equal("PONG\n", Encoding.ASCII.GetString(response));
            Assert.Contains(messages, message => message.Contains($"TCP port {port}", StringComparison.Ordinal));
        }
        finally
        {
            await cancellation.CancelAsync();
            await server.WaitAsync(TimeSpan.FromSeconds(2));
        }
    }

    private static int ReserveTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private sealed class InlineProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value) => callback(value);
    }
}
