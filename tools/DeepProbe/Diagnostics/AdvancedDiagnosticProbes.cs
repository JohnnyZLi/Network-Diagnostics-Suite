using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using NetworkDeepProbe.Models;

namespace NetworkDeepProbe.Diagnostics;

internal sealed record AdvancedEvidenceResult(
    LoadedPathLocalizationReport? LoadLocalization,
    DualStackReport DualStack,
    NetworkChangeReport NetworkChange,
    HostResourceReport HostResources);

internal sealed class AdvancedEvidenceSession : IAsyncDisposable
{
    private readonly Uri origin;
    private readonly string? interfaceId;
    private readonly bool includeLocalIdentifiers;
    private readonly CapturedNetworkState before;
    private readonly LoadedPathLatencyCollector? localization;
    private readonly HostResourceMonitor hostMonitor;
    private readonly Task<DualStackReport> dualStackTask;
    private readonly Task<bool> captivePortalTask;
    private bool completed;

    private AdvancedEvidenceSession(
        Uri origin,
        string? interfaceId,
        bool includeLocalIdentifiers,
        CapturedNetworkState before,
        LoadedPathLatencyCollector? localization,
        HostResourceMonitor hostMonitor,
        Task<DualStackReport> dualStackTask,
        Task<bool> captivePortalTask)
    {
        this.origin = origin;
        this.interfaceId = interfaceId;
        this.includeLocalIdentifiers = includeLocalIdentifiers;
        this.before = before;
        this.localization = localization;
        this.hostMonitor = hostMonitor;
        this.dualStackTask = dualStackTask;
        this.captivePortalTask = captivePortalTask;
    }

    public static async Task<AdvancedEvidenceSession> StartAsync(
        Uri origin,
        string? interfaceId,
        bool includeLocalIdentifiers,
        bool enableLoadLocalization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(origin);
        var before = NetworkStateProbe.Capture(origin, interfaceId, includeLocalIdentifiers);
        var hostMonitor = HostResourceMonitor.Start();
        var dualStackTask = DualStackProbe.RunAsync(origin, cancellationToken);
        var captivePortalTask = NetworkStateProbe.CheckCaptivePortalAsync(origin, cancellationToken);
        LoadedPathLatencyCollector? localization = null;
        if (enableLoadLocalization)
        {
            localization = await LoadedPathLatencyCollector.CreateAsync(
                origin,
                before.GatewayAddress,
                cancellationToken);
            localization.Start();
        }

        return new AdvancedEvidenceSession(
            origin,
            interfaceId,
            includeLocalIdentifiers,
            before,
            localization,
            hostMonitor,
            dualStackTask,
            captivePortalTask);
    }

    public void SetPhase(string phase) => localization?.SetPhase(phase);

    public async Task<AdvancedEvidenceResult> CompleteAsync(CancellationToken cancellationToken)
    {
        if (completed) throw new InvalidOperationException("Advanced evidence has already been completed.");
        completed = true;
        var loadLocalization = localization is null
            ? null
            : await localization.StopAsync(cancellationToken);
        var hostResources = await hostMonitor.StopAsync(cancellationToken);
        var dualStack = await dualStackTask.WaitAsync(cancellationToken);
        var captivePortal = await captivePortalTask.WaitAsync(cancellationToken);
        var after = NetworkStateProbe.Capture(origin, interfaceId, includeLocalIdentifiers);
        var networkChange = NetworkStateProbe.Compare(before, after, captivePortal);
        return new AdvancedEvidenceResult(loadLocalization, dualStack, networkChange, hostResources);
    }

    public async ValueTask DisposeAsync()
    {
        if (completed) return;
        await hostMonitor.DisposeAsync();
        if (localization is not null) await localization.DisposeAsync();
    }
}

internal sealed record CapturedNetworkState(
    NetworkStateSnapshot Report,
    string? GatewayAddress,
    string Fingerprint);

internal static class NetworkStateProbe
{
    public static CapturedNetworkState Capture(Uri origin, string? selectedInterfaceId, bool includeLocalIdentifiers)
    {
        var active = NetworkInterface.GetAllNetworkInterfaces()
            .Where(item => item.OperationalStatus == OperationalStatus.Up && item.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .ToArray();
        var selected = active.FirstOrDefault(item => string.Equals(item.Id, selectedInterfaceId, StringComparison.Ordinal))
            ?? active.FirstOrDefault(HasGateway)
            ?? active.FirstOrDefault();
        string? gateway = null;
        var families = new HashSet<string>(StringComparer.Ordinal);
        if (selected is not null)
        {
            try
            {
                var properties = selected.GetIPProperties();
                gateway = properties.GatewayAddresses
                    .Select(item => item.Address)
                    .FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork && !address.Equals(IPAddress.Any))
                    ?.ToString();
                foreach (var address in properties.UnicastAddresses.Select(item => item.Address))
                {
                    families.Add(address.AddressFamily == AddressFamily.InterNetwork ? "IPv4" : "IPv6");
                }
            }
            catch (NetworkInformationException)
            {
                // A disappearing interface is represented by the after-snapshot comparison.
            }
        }

        var tunnels = active
            .Where(IsTunnel)
            .Select((item, index) => includeLocalIdentifiers ? item.Name : $"Tunnel interface {index + 1}")
            .ToArray();
        var proxy = ProxyFor(origin);
        var report = new NetworkStateSnapshot(
            selected?.Id,
            selected?.Name,
            includeLocalIdentifiers ? gateway : null,
            families.OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            proxy,
            tunnels);
        var fingerprint = string.Join('|',
            selected?.Id ?? string.Empty,
            gateway ?? string.Empty,
            string.Join(',', families.OrderBy(item => item, StringComparer.Ordinal)),
            proxy ?? string.Empty,
            string.Join(',', active.Select(item => item.Id).OrderBy(item => item, StringComparer.Ordinal)));
        return new CapturedNetworkState(report, gateway, fingerprint);
    }

    public static NetworkChangeReport Compare(CapturedNetworkState before, CapturedNetworkState after, bool captivePortal)
    {
        var changes = new List<string>();
        if (!string.Equals(before.Report.InterfaceId, after.Report.InterfaceId, StringComparison.Ordinal))
        {
            changes.Add("The active interface changed during the run.");
        }
        if (!string.Equals(before.GatewayAddress, after.GatewayAddress, StringComparison.Ordinal))
        {
            changes.Add("The default gateway changed during the run.");
        }
        if (!before.Report.AddressFamilies.SequenceEqual(after.Report.AddressFamilies, StringComparer.Ordinal))
        {
            changes.Add("The active IP address families changed during the run.");
        }
        if (!string.Equals(before.Report.Proxy, after.Report.Proxy, StringComparison.Ordinal))
        {
            changes.Add("The effective system proxy changed during the run.");
        }
        if (!string.Equals(before.Fingerprint, after.Fingerprint, StringComparison.Ordinal) && changes.Count == 0)
        {
            changes.Add("The active network configuration changed during the run.");
        }
        return new NetworkChangeReport(before.Report, after.Report, changes.Count > 0, changes, captivePortal);
    }

    public static async Task<bool> CheckCaptivePortalAsync(Uri origin, CancellationToken cancellationToken)
    {
        try
        {
            using var handler = new HttpClientHandler { AllowAutoRedirect = false };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(origin, $"/api/ping?portal={Guid.NewGuid():N}"));
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var code = (int)response.StatusCode;
            if (code is >= 300 and < 400) return true;
            var mediaType = response.Content.Headers.ContentType?.MediaType;
            return mediaType is not null && mediaType.Contains("html", StringComparison.OrdinalIgnoreCase);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    private static bool HasGateway(NetworkInterface networkInterface)
    {
        try
        {
            return networkInterface.GetIPProperties().GatewayAddresses.Any(item => !item.Address.Equals(IPAddress.Any));
        }
        catch (NetworkInformationException)
        {
            return false;
        }
    }

    private static bool IsTunnel(NetworkInterface networkInterface)
    {
        if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Tunnel) return true;
        var value = $"{networkInterface.Name} {networkInterface.Description}";
        return value.Contains("vpn", StringComparison.OrdinalIgnoreCase)
            || value.Contains("wireguard", StringComparison.OrdinalIgnoreCase)
            || value.Contains("utun", StringComparison.OrdinalIgnoreCase)
            || value.Contains("tunnel", StringComparison.OrdinalIgnoreCase)
            || value.Contains("tap", StringComparison.OrdinalIgnoreCase)
            || value.Contains("tun", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ProxyFor(Uri origin)
    {
        try
        {
            var proxy = HttpClient.DefaultProxy.GetProxy(origin);
            if (proxy is null || SameAuthority(proxy, origin)) return null;
            return proxy.IsDefaultPort ? $"{proxy.Scheme}://{proxy.Host}" : $"{proxy.Scheme}://{proxy.Host}:{proxy.Port}";
        }
        catch
        {
            return null;
        }
    }

    private static bool SameAuthority(Uri left, Uri right) =>
        string.Equals(left.Scheme, right.Scheme, StringComparison.OrdinalIgnoreCase)
        && string.Equals(left.Host, right.Host, StringComparison.OrdinalIgnoreCase)
        && left.Port == right.Port;
}

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
            var unavailable4 = Unavailable("IPv4", error.Message);
            var unavailable6 = Unavailable("IPv6", error.Message);
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
        if (stopped) return;
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

internal sealed class HostResourceMonitor : IAsyncDisposable
{
    private readonly Process process;
    private readonly DateTimeOffset startedAt;
    private readonly TimeSpan processorTimeAtStart;
    private readonly long managedMemoryBefore;
    private readonly IReadOnlyDictionary<string, InterfaceCounterSnapshot> interfaceBefore;
    private readonly CancellationTokenSource cancellation = new();
    private readonly Task sampleTask;
    private long peakWorkingSet;
    private bool stopped;

    private HostResourceMonitor()
    {
        process = Process.GetCurrentProcess();
        process.Refresh();
        startedAt = DateTimeOffset.UtcNow;
        processorTimeAtStart = process.TotalProcessorTime;
        managedMemoryBefore = GC.GetTotalMemory(false);
        interfaceBefore = CaptureInterfaceCounters();
        peakWorkingSet = process.WorkingSet64;
        sampleTask = Task.Run(SampleLoopAsync);
    }

    public static HostResourceMonitor Start() => new();

    public async Task<HostResourceReport> StopAsync(CancellationToken cancellationToken)
    {
        if (stopped) throw new InvalidOperationException("Host monitoring has already stopped.");
        stopped = true;
        await cancellation.CancelAsync();
        try
        {
            await sampleTask.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Expected monitor shutdown.
        }
        process.Refresh();
        var elapsed = Math.Max(0.001, (DateTimeOffset.UtcNow - startedAt).TotalSeconds);
        var processorSeconds = Math.Max(0, (process.TotalProcessorTime - processorTimeAtStart).TotalSeconds);
        var cpuPercent = Math.Clamp(processorSeconds / elapsed / Math.Max(1, Environment.ProcessorCount) * 100, 0, 100);
        var after = CaptureInterfaceCounters();
        var deltas = new List<InterfaceCounterDelta>();
        foreach (var pair in after)
        {
            if (!interfaceBefore.TryGetValue(pair.Key, out var before)) continue;
            var current = pair.Value;
            deltas.Add(new InterfaceCounterDelta(
                current.Id,
                current.Name,
                NonNegative(current.BytesReceived - before.BytesReceived),
                NonNegative(current.BytesSent - before.BytesSent),
                NonNegative(current.IncomingErrors - before.IncomingErrors),
                NonNegative(current.OutgoingErrors - before.OutgoingErrors),
                NonNegative(current.IncomingDiscards - before.IncomingDiscards),
                NonNegative(current.OutgoingDiscards - before.OutgoingDiscards)));
        }
        var bottleneck = cpuPercent >= 85 || deltas.Any(item =>
            item.IncomingErrors > 0
            || item.OutgoingErrors > 0
            || item.IncomingDiscards > 0
            || item.OutgoingDiscards > 0);
        return new HostResourceReport(
            cpuPercent,
            peakWorkingSet,
            managedMemoryBefore,
            GC.GetTotalMemory(false),
            deltas,
            bottleneck);
    }

    public async ValueTask DisposeAsync()
    {
        if (!stopped)
        {
            stopped = true;
            await cancellation.CancelAsync();
            try
            {
                await sampleTask;
            }
            catch (OperationCanceledException)
            {
                // Expected monitor shutdown.
            }
        }
        cancellation.Dispose();
        process.Dispose();
    }

    private async Task SampleLoopAsync()
    {
        while (!cancellation.IsCancellationRequested)
        {
            process.Refresh();
            var workingSet = process.WorkingSet64;
            if (workingSet > peakWorkingSet) Interlocked.Exchange(ref peakWorkingSet, workingSet);
            await Task.Delay(250, cancellation.Token);
        }
    }

    private static IReadOnlyDictionary<string, InterfaceCounterSnapshot> CaptureInterfaceCounters()
    {
        var result = new Dictionary<string, InterfaceCounterSnapshot>(StringComparer.Ordinal);
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            try
            {
                var statistics = networkInterface.GetIPStatistics();
                result[networkInterface.Id] = new InterfaceCounterSnapshot(
                    networkInterface.Id,
                    networkInterface.Name,
                    statistics.BytesReceived,
                    statistics.BytesSent,
                    statistics.IncomingPacketsWithErrors,
                    statistics.OutgoingPacketsWithErrors,
                    statistics.IncomingPacketsDiscarded,
                    statistics.OutgoingPacketsDiscarded);
            }
            catch (NetworkInformationException)
            {
                // Unsupported or disappearing interfaces remain absent from the delta.
            }
        }
        return result;
    }

    private static long NonNegative(long value) => Math.Max(0, value);

    private sealed record InterfaceCounterSnapshot(
        string Id,
        string Name,
        long BytesReceived,
        long BytesSent,
        long IncomingErrors,
        long OutgoingErrors,
        long IncomingDiscards,
        long OutgoingDiscards);
}
