using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using NetworkDeepProbe.Models;

namespace NetworkDeepProbe.Diagnostics;

internal static class DnsProbe
{
    private const string QueryName = "example.com";
    private const int Attempts = 5;

    public static async Task<IReadOnlyList<DnsResolverReport>> RunAsync(
        bool includeAddresses,
        CancellationToken cancellationToken)
    {
        var systemResolvers = DiscoverSystemResolvers()
            .Select((address, index) => new ResolverTarget($"System resolver {index + 1}", address, includeAddresses ? address.ToString() : "Local resolver"));

        var targets = systemResolvers.Concat([
            new ResolverTarget("Cloudflare", IPAddress.Parse("1.1.1.1"), "1.1.1.1"),
            new ResolverTarget("Google", IPAddress.Parse("8.8.8.8"), "8.8.8.8"),
            new ResolverTarget("Quad9", IPAddress.Parse("9.9.9.9"), "9.9.9.9")
        ]).ToArray();

        var results = new List<DnsResolverReport>(targets.Length);
        foreach (var target in targets)
        {
            results.Add(await TestResolverAsync(target, cancellationToken));
        }
        return results;
    }

    internal static IReadOnlyList<IPAddress> DiscoverSystemResolvers()
    {
        var addresses = NetworkInterface.GetAllNetworkInterfaces()
            .Where(network => network.OperationalStatus == OperationalStatus.Up)
            .SelectMany(TryGetDnsAddresses)
            .Where(IsUsableResolver)
            .Distinct()
            .ToList();

        if (addresses.Count < 2 && (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()))
        {
            addresses.AddRange(ReadResolverConfiguration().Where(IsUsableResolver));
        }

        return addresses.Distinct().Take(2).ToArray();
    }

    internal static IReadOnlyList<IPAddress> ParseResolverConfiguration(IEnumerable<string> lines)
    {
        var addresses = new List<IPAddress>();
        foreach (var rawLine in lines)
        {
            var line = rawLine.Split('#', 2)[0].Trim();
            var fields = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length >= 2
                && string.Equals(fields[0], "nameserver", StringComparison.OrdinalIgnoreCase)
                && IPAddress.TryParse(fields[1], out var address))
            {
                addresses.Add(address);
            }
        }
        return addresses.Distinct().ToArray();
    }

    internal static byte[] BuildQuery(ushort transactionId, string name)
    {
        using var stream = new MemoryStream();
        Span<byte> header = stackalloc byte[12];
        BinaryPrimitives.WriteUInt16BigEndian(header, transactionId);
        BinaryPrimitives.WriteUInt16BigEndian(header[2..], 0x0100);
        BinaryPrimitives.WriteUInt16BigEndian(header[4..], 1);
        stream.Write(header);

        foreach (var label in name.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var bytes = Encoding.ASCII.GetBytes(label);
            if (bytes.Length is 0 or > 63) throw new ArgumentException("DNS labels must contain 1 through 63 bytes.", nameof(name));
            stream.WriteByte((byte)bytes.Length);
            stream.Write(bytes);
        }
        stream.WriteByte(0);
        Span<byte> question = stackalloc byte[4];
        BinaryPrimitives.WriteUInt16BigEndian(question, 1);
        BinaryPrimitives.WriteUInt16BigEndian(question[2..], 1);
        stream.Write(question);
        return stream.ToArray();
    }

    internal static bool IsSuccessfulResponse(ReadOnlySpan<byte> response, ushort expectedTransactionId)
    {
        if (response.Length < 12) return false;
        if (BinaryPrimitives.ReadUInt16BigEndian(response) != expectedTransactionId) return false;
        var responseCode = response[3] & 0x0f;
        var answerCount = BinaryPrimitives.ReadUInt16BigEndian(response[6..]);
        return responseCode == 0 && answerCount > 0;
    }

    private static async Task<DnsResolverReport> TestResolverAsync(
        ResolverTarget target,
        CancellationToken cancellationToken)
    {
        var successful = new List<double>();
        string? lastError = null;
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            var transactionId = (ushort)RandomNumberGenerator.GetInt32(0, ushort.MaxValue + 1);
            var query = BuildQuery(transactionId, QueryName);
            using var client = new UdpClient(target.Address.AddressFamily);
            client.Connect(target.Address, 53);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            var stopwatch = Stopwatch.StartNew();
            try
            {
                await client.SendAsync(query, timeout.Token);
                var response = await client.ReceiveAsync(timeout.Token);
                stopwatch.Stop();
                if (IsSuccessfulResponse(response.Buffer, transactionId))
                {
                    successful.Add(stopwatch.Elapsed.TotalMilliseconds);
                    lastError = null;
                }
                else
                {
                    lastError = "The resolver returned an invalid or empty answer.";
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                lastError = "Timed out";
            }
            catch (SocketException error)
            {
                lastError = error.SocketErrorCode.ToString();
            }
        }

        return new DnsResolverReport(
            target.Name,
            target.DisplayAddress,
            Attempts,
            successful.Count,
            successful.Count == 0 ? null : successful.Min(),
            Statistics.Percentile(successful, 50),
            Statistics.Percentile(successful, 95),
            successful.Count == 0 ? null : successful.Max(),
            successful.Count == Attempts ? null : lastError);
    }

    private static IEnumerable<IPAddress> TryGetDnsAddresses(NetworkInterface network)
    {
        try { return network.GetIPProperties().DnsAddresses; }
        catch (Exception error) when (error is NetworkInformationException or PlatformNotSupportedException) { return []; }
    }

    private static IReadOnlyList<IPAddress> ReadResolverConfiguration()
    {
        try
        {
            return File.Exists("/etc/resolv.conf")
                ? ParseResolverConfiguration(File.ReadLines("/etc/resolv.conf"))
                : [];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static bool IsUsableResolver(IPAddress address) =>
        !IPAddress.Any.Equals(address) && !IPAddress.IPv6Any.Equals(address);

    private sealed record ResolverTarget(string Name, IPAddress Address, string DisplayAddress);
}
