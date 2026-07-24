using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using NetworkDeepProbe.Models;

namespace NetworkDeepProbe.Diagnostics;

internal static class LanThroughputServer
{
    private const int MaximumCommandBytes = 128;
    private static readonly byte[] Payload = CreatePayload(1024 * 1024);

    public static async Task RunAsync(int port, CancellationToken cancellationToken)
    {
        var listener = new TcpListener(IPAddress.Any, port);
        listener.Start(128);

        Console.WriteLine("LAN throughput server");
        Console.WriteLine($"Listening on TCP port {port}. Leave this window open during the client test.");
        foreach (var address in GetLocalAddresses())
        {
            Console.WriteLine($"  Client target: {address}");
        }
        Console.WriteLine("Press Ctrl+C to stop.");
        Console.WriteLine();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(cancellationToken);
                _ = HandleClientSafelyAsync(client, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task HandleClientSafelyAsync(TcpClient client, CancellationToken serverCancellation)
    {
        try
        {
            using (client)
            {
                ConfigureClient(client);
                var stream = client.GetStream();
                var command = await ReadCommandAsync(stream, serverCancellation);
                var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length == 0 || !string.Equals(parts[0], "NDS/1", StringComparison.Ordinal)) return;

                if (parts.Length == 2 && string.Equals(parts[1], "PING", StringComparison.Ordinal))
                {
                    await stream.WriteAsync("PONG\n"u8.ToArray(), serverCancellation);
                    return;
                }

                if (parts.Length != 3 || !int.TryParse(parts[2], out var durationMs) || durationMs is < 1000 or > 60000)
                {
                    return;
                }

                if (string.Equals(parts[1], "DOWNLOAD", StringComparison.Ordinal))
                {
                    await ServeDownloadAsync(stream, durationMs, serverCancellation);
                }
                else if (string.Equals(parts[1], "UPLOAD", StringComparison.Ordinal))
                {
                    await ReceiveUploadAsync(stream, durationMs, serverCancellation);
                }
            }
        }
        catch (Exception error) when (error is IOException or SocketException or OperationCanceledException)
        {
            // A throughput client closing a saturated stream is expected.
        }
    }

    private static async Task ServeDownloadAsync(NetworkStream stream, int durationMs, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(started).TotalMilliseconds < durationMs)
        {
            await stream.WriteAsync(Payload, cancellationToken);
        }
    }

    private static async Task ReceiveUploadAsync(NetworkStream stream, int durationMs, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(durationMs + 5000);
        var buffer = ArrayPool<byte>.Shared.Rent(1024 * 1024);
        long bytes = 0;
        try
        {
            while (true)
            {
                var read = await stream.ReadAsync(buffer, timeout.Token);
                if (read == 0) break;
                bytes += read;
            }
            var receipt = Encoding.ASCII.GetBytes($"OK {bytes}\n");
            await stream.WriteAsync(receipt, cancellationToken);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async Task<string> ReadCommandAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[MaximumCommandBytes];
        var length = 0;
        while (length < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(length, 1), cancellationToken);
            if (read == 0) break;
            if (buffer[length] == (byte)'\n') break;
            length += read;
        }
        return Encoding.ASCII.GetString(buffer, 0, length).Trim();
    }

    private static IReadOnlyList<string> GetLocalAddresses()
    {
        var addresses = NetworkInterface.GetAllNetworkInterfaces()
            .Where(network => network.OperationalStatus == OperationalStatus.Up && network.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(network => network.GetIPProperties().UnicastAddresses)
            .Select(address => address.Address)
            .Where(address => address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
            .Distinct()
            .Select(address => address.ToString())
            .OrderBy(address => address, StringComparer.Ordinal)
            .ToArray();
        return addresses.Length == 0 ? ["<this machine's LAN address>"] : addresses;
    }

    private static byte[] CreatePayload(int size)
    {
        var bytes = new byte[size];
        uint state = 0x6d2b79f5;
        for (var index = 0; index < bytes.Length; index++)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            bytes[index] = (byte)(state & 0xff);
        }
        return bytes;
    }

    private static void ConfigureClient(TcpClient client)
    {
        client.NoDelay = true;
        client.ReceiveBufferSize = 1024 * 1024;
        client.SendBufferSize = 1024 * 1024;
    }
}

internal static class LanThroughputClient
{
    private static readonly byte[] UploadPayload = CreatePayload(256 * 1024);

    public static async Task<LanThroughputReport> RunAsync(
        string target,
        int port,
        int durationSeconds,
        int concurrency,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var address = await ResolveAsync(target, cancellationToken);
        progress?.Report($"Checking the local throughput server at {target}:{port}");
        var latencySamples = new List<double?>();
        for (var attempt = 0; attempt < 8; attempt++)
        {
            latencySamples.Add(await MeasureLatencyAsync(address, port, cancellationToken));
            if (attempt < 7) await Task.Delay(100, cancellationToken);
        }

        var durationMs = durationSeconds * 1000;
        progress?.Report($"Measuring local download with {concurrency} parallel streams");
        var download = await RunDownloadAsync(address, port, durationMs, concurrency, cancellationToken);

        progress?.Report($"Measuring local upload with {concurrency} parallel streams");
        var upload = await RunUploadAsync(address, port, durationMs, concurrency, cancellationToken);

        return new LanThroughputReport(
            target,
            address.ToString(),
            port,
            durationMs,
            concurrency,
            Statistics.Summarize(latencySamples),
            download.Mbps,
            download.Bytes,
            upload.Mbps,
            upload.Bytes);
    }

    private static async Task<double?> MeasureLatencyAsync(IPAddress address, int port, CancellationToken cancellationToken)
    {
        try
        {
            using var client = await ConnectAsync(address, port, cancellationToken);
            var stream = client.GetStream();
            var started = Stopwatch.GetTimestamp();
            await stream.WriteAsync("NDS/1 PING\n"u8.ToArray(), cancellationToken);
            var response = await ReadLineAsync(stream, cancellationToken);
            return string.Equals(response, "PONG", StringComparison.Ordinal)
                ? Stopwatch.GetElapsedTime(started).TotalMilliseconds
                : null;
        }
        catch (Exception error) when (error is IOException or SocketException or OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested) throw;
            return null;
        }
    }

    private static async Task<TransferResult> RunDownloadAsync(
        IPAddress address,
        int port,
        int durationMs,
        int concurrency,
        CancellationToken cancellationToken)
    {
        var clients = await ConnectManyAsync(address, port, concurrency, cancellationToken);
        var started = Stopwatch.GetTimestamp();
        try
        {
            var tasks = clients.Select(async client =>
            {
                var stream = client.GetStream();
                await stream.WriteAsync(Encoding.ASCII.GetBytes($"NDS/1 DOWNLOAD {durationMs}\n"), cancellationToken);
                var buffer = ArrayPool<byte>.Shared.Rent(1024 * 1024);
                long bytes = 0;
                try
                {
                    while (true)
                    {
                        var read = await stream.ReadAsync(buffer, cancellationToken);
                        if (read == 0) break;
                        bytes += read;
                    }
                    return bytes;
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }).ToArray();
            var totals = await Task.WhenAll(tasks);
            var elapsed = Stopwatch.GetElapsedTime(started);
            return TransferResult.Create(totals.Sum(), elapsed);
        }
        finally
        {
            foreach (var client in clients) client.Dispose();
        }
    }

    private static async Task<TransferResult> RunUploadAsync(
        IPAddress address,
        int port,
        int durationMs,
        int concurrency,
        CancellationToken cancellationToken)
    {
        var clients = await ConnectManyAsync(address, port, concurrency, cancellationToken);
        var started = Stopwatch.GetTimestamp();
        try
        {
            var tasks = clients.Select(async client =>
            {
                var stream = client.GetStream();
                await stream.WriteAsync(Encoding.ASCII.GetBytes($"NDS/1 UPLOAD {durationMs}\n"), cancellationToken);
                long bytes = 0;
                while (Stopwatch.GetElapsedTime(started).TotalMilliseconds < durationMs)
                {
                    await stream.WriteAsync(UploadPayload, cancellationToken);
                    bytes += UploadPayload.Length;
                }
                client.Client.Shutdown(SocketShutdown.Send);
                return bytes;
            }).ToArray();
            var totals = await Task.WhenAll(tasks);
            var elapsed = Stopwatch.GetElapsedTime(started);
            return TransferResult.Create(totals.Sum(), elapsed);
        }
        finally
        {
            foreach (var client in clients) client.Dispose();
        }
    }

    private static async Task<TcpClient[]> ConnectManyAsync(
        IPAddress address,
        int port,
        int concurrency,
        CancellationToken cancellationToken)
    {
        var tasks = Enumerable.Range(0, concurrency)
            .Select(_ => ConnectAsync(address, port, cancellationToken))
            .ToArray();
        try
        {
            return await Task.WhenAll(tasks);
        }
        catch
        {
            foreach (var task in tasks)
            {
                if (task.IsCompletedSuccessfully) task.Result.Dispose();
            }
            throw;
        }
    }

    private static async Task<TcpClient> ConnectAsync(IPAddress address, int port, CancellationToken cancellationToken)
    {
        var client = new TcpClient(address.AddressFamily)
        {
            NoDelay = true,
            ReceiveBufferSize = 1024 * 1024,
            SendBufferSize = 1024 * 1024
        };
        try
        {
            await client.ConnectAsync(address, port, cancellationToken);
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private static async Task<IPAddress> ResolveAsync(string target, CancellationToken cancellationToken)
    {
        if (IPAddress.TryParse(target, out var direct)) return direct;
        var addresses = await Dns.GetHostAddressesAsync(target, cancellationToken);
        return addresses.FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork)
            ?? addresses.FirstOrDefault()
            ?? throw new InvalidOperationException($"Could not resolve LAN target {target}.");
    }

    private static async Task<string> ReadLineAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[128];
        var length = 0;
        while (length < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(length, 1), cancellationToken);
            if (read == 0) break;
            if (buffer[length] == (byte)'\n') break;
            length += read;
        }
        return Encoding.ASCII.GetString(buffer, 0, length).Trim();
    }

    private static byte[] CreatePayload(int size)
    {
        var bytes = new byte[size];
        uint state = 0x9e3779b9;
        for (var index = 0; index < bytes.Length; index++)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            bytes[index] = (byte)(state & 0xff);
        }
        return bytes;
    }

    private sealed record TransferResult(long Bytes, double Mbps)
    {
        public static TransferResult Create(long bytes, TimeSpan elapsed)
        {
            var seconds = Math.Max(elapsed.TotalSeconds, 0.001);
            return new TransferResult(bytes, bytes * 8d / seconds / 1_000_000d);
        }
    }
}
