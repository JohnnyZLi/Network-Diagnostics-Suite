using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using NetworkDeepProbe.Models;

namespace NetworkDeepProbe.Diagnostics;

internal sealed class LoadedPathLatencyCollector : IAsyncDisposable
{
    private static readonly byte[] Payload = Enumerable.Range(0, 24).Select(item => (byte)item).ToArray();
    private readonly IReadOnlyList<LatencyTarget> targets;
    private readonly Dictionary<string, Dictionary<string, List<double?>>> samples;
    private readonly CancellationTokenSource cancellation = new();
    private Task? loopTask;
    private string phase = "idle";
    private bool stopped;

    private LoadedPathLatencyCollector(IReadOnlyList<LatencyTarget> targets)
    {
        this.targets = targets;
        samples = targets.ToDictionary(
            item => item.Id,
            _ => new Dictionary<string, List<double?>>(StringComparer.Ordinal)
            {
                ["idle"] = [],
                ["download"] = [],
                ["upload"] = []
            },
            StringComparer.Ordinal);
    }

    public static async Task<LoadedPathLatencyCollector> CreateAsync(
        Uri origin,
        string? gatewayAddress,
        CancellationToken cancellationToken)
    {
        var endpointAddresses = await Dns.GetHostAddressesAsync(origin.Host, cancellationToken);
        var endpoint = endpointAddresses.FirstOrDefault(item => item.AddressFamily == AddressFamily.InterNetwork)
            ?? endpointAddresses.FirstOrDefault();
        var targets = new List<LatencyTarget>();
        if (IPAddress.TryParse(gatewayAddress, out var gateway))
        {
            targets.Add(new LatencyTarget("gateway", "Default gateway", gateway));
        }
        if (endpoint is not null)
        {
            var firstPublicHop = await DiscoverFirstPublicHopAsync(endpoint, cancellationToken);
            if (firstPublicHop is not null && !firstPublicHop.Equals(endpoint))
            {
                targets.Add(new LatencyTarget("first-public-hop", "First responsive public hop", firstPublicHop));
            }
            targets.Add(new LatencyTarget("endpoint", "Measurement endpoint", endpoint));
        }
        return new LoadedPathLatencyCollector(targets);
    }

    public void Start()
    {
        if (loopTask is not null) throw new InvalidOperationException("Latency collection has already started.");
        loopTask = Task.Run(CollectLoopAsync);
    }

    public void SetPhase(string value)
    {
        var normalized = value is "download" or "upload" ? value : "idle";
        Volatile.Write(ref phase, normalized);
    }

    public async Task<LoadedPathLocalizationReport> StopAsync(CancellationToken cancellationToken)
    {
        if (stopped) throw new InvalidOperationException("Latency collection has already stopped.");
        stopped = true;
        await cancellation.CancelAsync();
        if (loopTask is not null)
        {
            try
            {
                await loopTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                // Expected collector shutdown.
            }
        }

        var reports = targets.Select(target => new LoadedPathTargetReport(
            target.Id,
            target.Label,
            target.Address.ToString(),
            Statistics.Summarize(samples[target.Id]["idle"]),
            Statistics.Summarize(samples[target.Id]["download"]),
            Statistics.Summarize(samples[target.Id]["upload"]))).ToArray();
        var (boundary, summary) = Interpret(reports);
        return new LoadedPathLocalizationReport(
            reports.Length == 0 ? "unavailable" : "measured",
            reports,
            boundary,
            summary);
    }

    public async ValueTask DisposeAsync()
    {
        if (!stopped)
        {
            stopped = true;
            await cancellation.CancelAsync();
            if (loopTask is not null)
            {
                try
                {
                    await loopTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected collector shutdown.
                }
            }
        }
        cancellation.Dispose();
    }

    private async Task CollectLoopAsync()
    {
        while (!cancellation.IsCancellationRequested)
        {
            var activePhase = Volatile.Read(ref phase);
            var results = await Task.WhenAll(targets.Select(target => ProbeAsync(target, cancellation.Token)));
            for (var index = 0; index < targets.Count; index++)
            {
                samples[targets[index].Id][activePhase].Add(results[index]);
            }
            await Task.Delay(120, cancellation.Token);
        }
    }

    private static async Task<double?> ProbeAsync(LatencyTarget target, CancellationToken cancellationToken)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(target.Address, 1_000, Payload, new PingOptions(64, false))
                .WaitAsync(cancellationToken);
            return reply.Status == IPStatus.Success ? reply.RoundtripTime : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (PingException)
        {
            return null;
        }
    }

    private static async Task<IPAddress?> DiscoverFirstPublicHopAsync(
        IPAddress destination,
        CancellationToken cancellationToken)
    {
        if (destination.AddressFamily != AddressFamily.InterNetwork) return null;
        using var ping = new Ping();
        for (var ttl = 2; ttl <= 8; ttl++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var reply = await ping.SendPingAsync(destination, 1_000, Payload, new PingOptions(ttl, false))
                    .WaitAsync(cancellationToken);
                if (reply.Address is null) continue;
                if (reply.Status == IPStatus.Success) return destination;
                if (reply.Status == IPStatus.TtlExpired && !IsPrivate(reply.Address)) return reply.Address;
            }
            catch (PingException)
            {
                // Continue to the next time-to-live value.
            }
        }
        return null;
    }

    private static bool IsPrivate(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal) return true;
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return (address.GetAddressBytes()[0] & 0xfe) == 0xfc;
        }
        var bytes = address.GetAddressBytes();
        return bytes[0] == 10
            || bytes[0] == 127
            || (bytes[0] == 169 && bytes[1] == 254)
            || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
            || (bytes[0] == 192 && bytes[1] == 168);
    }

    private static (string? Boundary, string Summary) Interpret(IReadOnlyList<LoadedPathTargetReport> reports)
    {
        static double Increase(LoadedPathTargetReport report)
        {
            var idle = report.Idle.MedianMs;
            var loaded = new[] { report.Download.MedianMs, report.Upload.MedianMs }
                .Where(value => value is not null)
                .Select(value => value!.Value)
                .DefaultIfEmpty(double.NaN)
                .Max();
            return idle is null || double.IsNaN(loaded) ? 0 : Math.Max(0, loaded - idle.Value);
        }

        var gateway = reports.FirstOrDefault(item => item.Id == "gateway");
        var publicHop = reports.FirstOrDefault(item => item.Id == "first-public-hop");
        var endpoint = reports.FirstOrDefault(item => item.Id == "endpoint");
        if (gateway is not null && Increase(gateway) > 15)
        {
            return ("local-network", "Latency rose at the default gateway under load, pointing to local-link or router queueing.");
        }
        if (publicHop is not null && Increase(publicHop) > 15)
        {
            return ("access-link", "The gateway stayed comparatively stable while the first public hop slowed, pointing to the modem or ISP access link.");
        }
        if (endpoint is not null && Increase(endpoint) > 15)
        {
            return ("upstream-path", "Early path evidence stayed comparatively stable while the endpoint slowed, pointing farther upstream on the route.");
        }
        return (null, "No measured path boundary showed a clear loaded-latency increase.");
    }

    private sealed record LatencyTarget(string Id, string Label, IPAddress Address);
}
