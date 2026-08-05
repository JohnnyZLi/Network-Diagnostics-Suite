using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using NetworkDeepProbe.Models;
using NetworkDiagnostics.Desktop.Presentation;

namespace NetworkDiagnostics.Desktop.Monitoring;

public sealed class ContinuousMonitorService : IAsyncDisposable
{
    private readonly HttpClient httpClient;
    private readonly MonitorHistoryStore store;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly List<MonitorSample> samples = [];
    private readonly List<MonitorAlert> alerts = [];
    private CancellationTokenSource? cancellation;
    private Task? loopTask;
    private MonitorOptions? options;
    private DateTimeOffset? startedAt;
    private DateTimeOffset? lastContentSpeedDueRaised;
    private bool loaded;

    public ContinuousMonitorService(string rootDirectory, HttpMessageHandler? handler = null)
    {
        store = new MonitorHistoryStore(rootDirectory);
        httpClient = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: true);
        httpClient.Timeout = TimeSpan.FromSeconds(4);
        httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("NetworkDiagnosticsDesktop", "1.0"));
    }

    public event EventHandler<MonitorSnapshotChangedEventArgs>? SnapshotChanged;

    public event EventHandler<MonitorContentSpeedDueEventArgs>? ContentSpeedDue;

    public MonitorSnapshot Snapshot { get; private set; } = MonitorSnapshot.Stopped;

    public async Task StartAsync(MonitorOptions nextOptions, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(nextOptions);
        await gate.WaitAsync(cancellationToken);
        try
        {
            options = nextOptions;
            if (!loaded)
            {
                samples.AddRange(await store.LoadSamplesAsync(cancellationToken));
                alerts.AddRange(await store.LoadAlertsAsync(cancellationToken));
                loaded = true;
            }

            if (!nextOptions.Enabled)
            {
                Publish(false, "Monitoring is off");
                return;
            }

            if (loopTask is { IsCompleted: false })
            {
                Publish(true, "Monitoring is active");
                return;
            }

            cancellation?.Dispose();
            cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            startedAt = DateTimeOffset.UtcNow;
            loopTask = RunLoopAsync(cancellation.Token);
            Publish(true, "Monitoring is active");
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task UpdateOptionsAsync(MonitorOptions nextOptions, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(nextOptions);
        options = nextOptions;
        if (nextOptions.Enabled)
        {
            await StartAsync(nextOptions, cancellationToken);
        }
        else
        {
            await StopAsync(cancellationToken);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task? activeLoop;
        await gate.WaitAsync(cancellationToken);
        try
        {
            cancellation?.Cancel();
            activeLoop = loopTask;
            loopTask = null;
            startedAt = null;
        }
        finally
        {
            gate.Release();
        }

        if (activeLoop is not null)
        {
            try
            {
                await activeLoop.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
            }
            catch (Exception error) when (error is OperationCanceledException or TimeoutException)
            {
                // Shutdown should not block the application closing.
            }
        }

        Publish(false, "Monitoring is off");
    }

    public async Task RecordDiagnosticAsync(
        NetworkDiagnosticsReportV2 report,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (report.InternetTransfer is not { } transfer) return;

        var sample = new MonitorSample(
            DateTimeOffset.UtcNow,
            MonitorSampleState.Responsive,
            transfer.IdleLatency.MedianMs,
            null,
            null,
            null,
            transfer.IdleLatency.LossPercent ?? 0,
            ActiveNetworkIdentity().InterfaceName,
            ActiveNetworkIdentity().Signature,
            transfer.Download.SteadyMbps,
            transfer.Upload.SteadyMbps,
            true);

        var priorSpeed = samples
            .Where(item => item.IsSpeedMeasurement)
            .OrderByDescending(item => item.Timestamp)
            .FirstOrDefault();

        await AddSampleAsync(sample, cancellationToken);
        await DetectBandwidthAlertAsync(priorSpeed, sample, cancellationToken);
        Publish(IsRunning, "Monitoring is active");
    }

    public async Task MarkAllAlertsReadAsync(CancellationToken cancellationToken = default)
    {
        for (var index = 0; index < alerts.Count; index++)
        {
            alerts[index] = alerts[index] with { IsRead = true };
        }
        await store.SaveAlertsAsync(alerts, cancellationToken);
        Publish(IsRunning, Snapshot.StatusMessage);
    }

    public async Task ClearAlertsAsync(CancellationToken cancellationToken = default)
    {
        alerts.Clear();
        await store.SaveAlertsAsync(alerts, cancellationToken);
        Publish(IsRunning, Snapshot.StatusMessage);
    }

    private bool IsRunning => loopTask is { IsCompleted: false } && cancellation?.IsCancellationRequested == false;

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var activeOptions = options;
            if (activeOptions is null || !activeOptions.Enabled) break;

            var sample = await ProbeAsync(activeOptions, cancellationToken);
            var previous = samples
                .Where(item => !item.IsSpeedMeasurement)
                .OrderByDescending(item => item.Timestamp)
                .FirstOrDefault();
            var previousScore = CurrentFiveMinuteScore(activeOptions);

            await AddSampleAsync(sample, cancellationToken);
            await DetectTransitionAlertsAsync(previous, sample, previousScore, activeOptions, cancellationToken);
            Publish(true, sample.State == MonitorSampleState.Unresponsive
                ? "Endpoint is currently unreachable"
                : "Monitoring is active");
            RaiseContentSpeedDueIfNeeded(activeOptions);

            try
            {
                await Task.Delay(activeOptions.Interval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task<MonitorSample> ProbeAsync(MonitorOptions activeOptions, CancellationToken cancellationToken)
    {
        var identity = ActiveNetworkIdentity();
        double? dnsMs = null;
        double? ttfbMs = null;
        double? latencyMs = null;
        var state = MonitorSampleState.Unresponsive;
        var loss = 100d;

        try
        {
            var dnsWatch = Stopwatch.StartNew();
            await Dns.GetHostAddressesAsync(activeOptions.Endpoint.Host, cancellationToken);
            dnsWatch.Stop();
            dnsMs = dnsWatch.Elapsed.TotalMilliseconds;

            using var request = new HttpRequestMessage(HttpMethod.Get, activeOptions.Endpoint);
            request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
            var requestWatch = Stopwatch.StartNew();
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            requestWatch.Stop();
            ttfbMs = requestWatch.Elapsed.TotalMilliseconds;
            latencyMs = ttfbMs;
            var reachable = response.StatusCode < HttpStatusCode.InternalServerError;
            loss = reachable ? 0 : 100;
            state = !reachable
                ? MonitorSampleState.Unresponsive
                : latencyMs <= 120
                    ? MonitorSampleState.Responsive
                    : MonitorSampleState.Laggy;
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or SocketException)
        {
            state = MonitorSampleState.Unresponsive;
        }

        var recentLatency = samples
            .Where(item => !item.IsSpeedMeasurement && item.LatencyMs is not null)
            .OrderByDescending(item => item.Timestamp)
            .Take(5)
            .Select(item => item.LatencyMs!.Value)
            .Reverse()
            .ToList();
        if (latencyMs is not null) recentLatency.Add(latencyMs.Value);
        var jitter = recentLatency.Count < 2
            ? 0
            : recentLatency.Zip(recentLatency.Skip(1), (left, right) => Math.Abs(right - left)).Average();

        return new MonitorSample(
            DateTimeOffset.UtcNow,
            state,
            latencyMs,
            jitter,
            dnsMs,
            ttfbMs,
            loss,
            identity.InterfaceName,
            identity.Signature);
    }

    private async Task AddSampleAsync(MonitorSample sample, CancellationToken cancellationToken)
    {
        samples.Add(sample);
        await store.AppendSampleAsync(sample, cancellationToken);

        var cutoff = DateTimeOffset.UtcNow - TimeSpan.FromDays(7);
        samples.RemoveAll(item => item.Timestamp < cutoff);
        if (samples.Count % 720 == 0)
        {
            await store.PruneAsync(samples, cancellationToken);
        }
    }

    private async Task DetectTransitionAlertsAsync(
        MonitorSample? previous,
        MonitorSample current,
        int? previousScore,
        MonitorOptions activeOptions,
        CancellationToken cancellationToken)
    {
        if (previous is not null)
        {
            if (previous.State != MonitorSampleState.Unresponsive
                && current.State == MonitorSampleState.Unresponsive)
            {
                await AddAlertAsync(new MonitorAlert(
                    Guid.NewGuid(),
                    current.Timestamp,
                    MonitorAlertKind.Outage,
                    MonitorAlertSeverity.Critical,
                    "Connection became unreachable",
                    $"The monitor could not reach {activeOptions.Endpoint.Host}.") , cancellationToken);
            }
            else if (previous.State == MonitorSampleState.Unresponsive
                     && current.State != MonitorSampleState.Unresponsive)
            {
                await AddAlertAsync(new MonitorAlert(
                    Guid.NewGuid(),
                    current.Timestamp,
                    MonitorAlertKind.Recovery,
                    MonitorAlertSeverity.Information,
                    "Connection recovered",
                    $"The monitor can reach {activeOptions.Endpoint.Host} again."), cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(previous.NetworkSignature)
                && !string.Equals(previous.NetworkSignature, current.NetworkSignature, StringComparison.Ordinal))
            {
                await AddAlertAsync(new MonitorAlert(
                    Guid.NewGuid(),
                    current.Timestamp,
                    MonitorAlertKind.NetworkChange,
                    MonitorAlertSeverity.Warning,
                    "Network path changed",
                    $"The active interface or local network identity changed to {current.InterfaceName}."), cancellationToken);
            }
        }

        var currentScore = CurrentFiveMinuteScore(activeOptions);
        if (currentScore is not null
            && currentScore < activeOptions.AlertScoreThreshold
            && (previousScore is null || previousScore >= activeOptions.AlertScoreThreshold))
        {
            await AddAlertAsync(new MonitorAlert(
                Guid.NewGuid(),
                current.Timestamp,
                MonitorAlertKind.Degradation,
                MonitorAlertSeverity.Warning,
                "Network score dropped",
                $"The five-minute network score fell to {currentScore}."), cancellationToken);
        }
    }

    private async Task DetectBandwidthAlertAsync(
        MonitorSample? previous,
        MonitorSample current,
        CancellationToken cancellationToken)
    {
        if (previous?.DownloadMbps is not > 0 || current.DownloadMbps is not > 0) return;
        var ratio = current.DownloadMbps.Value / previous.DownloadMbps.Value;
        if (ratio >= 0.65) return;

        await AddAlertAsync(new MonitorAlert(
            Guid.NewGuid(),
            current.Timestamp,
            MonitorAlertKind.BandwidthChange,
            MonitorAlertSeverity.Warning,
            "Download speed changed significantly",
            $"Content download fell from {previous.DownloadMbps:0.#} Mbps to {current.DownloadMbps:0.#} Mbps."), cancellationToken);
    }

    private async Task AddAlertAsync(MonitorAlert alert, CancellationToken cancellationToken)
    {
        var duplicate = alerts.Any(existing =>
            existing.Kind == alert.Kind
            && alert.Timestamp - existing.Timestamp < TimeSpan.FromMinutes(5));
        if (duplicate) return;

        alerts.Insert(0, alert);
        if (alerts.Count > 200) alerts.RemoveRange(200, alerts.Count - 200);
        await store.SaveAlertsAsync(alerts, cancellationToken);
    }

    private int? CurrentFiveMinuteScore(MonitorOptions activeOptions)
    {
        var snapshot = new MonitorSnapshot(
            IsRunning,
            startedAt,
            samples.LastOrDefault()?.Timestamp,
            samples.ToArray(),
            alerts.ToArray(),
            Snapshot.StatusMessage);
        return NetworkExperiencePresenter.Build(snapshot, activeOptions, MonitorWindow.FiveMinutes).Score;
    }

    private void RaiseContentSpeedDueIfNeeded(MonitorOptions activeOptions)
    {
        if (activeOptions.ContentSpeedCadenceHours <= 0) return;
        var mostRecent = samples
            .Where(sample => sample.IsSpeedMeasurement)
            .OrderByDescending(sample => sample.Timestamp)
            .FirstOrDefault()?.Timestamp;
        var dueAt = (mostRecent ?? startedAt ?? DateTimeOffset.UtcNow)
            + TimeSpan.FromHours(activeOptions.ContentSpeedCadenceHours);
        if (DateTimeOffset.UtcNow < dueAt) return;
        if (lastContentSpeedDueRaised is not null
            && DateTimeOffset.UtcNow - lastContentSpeedDueRaised < TimeSpan.FromMinutes(10)) return;
        lastContentSpeedDueRaised = DateTimeOffset.UtcNow;
        ContentSpeedDue?.Invoke(this, new MonitorContentSpeedDueEventArgs(dueAt));
    }

    private void Publish(bool running, string status)
    {
        Snapshot = new MonitorSnapshot(
            running,
            running ? startedAt : null,
            samples.LastOrDefault()?.Timestamp,
            samples.ToArray(),
            alerts.ToArray(),
            status);
        SnapshotChanged?.Invoke(this, new MonitorSnapshotChangedEventArgs(Snapshot));
    }

    private static (string InterfaceName, string Signature) ActiveNetworkIdentity()
    {
        try
        {
            var candidates = NetworkInterface.GetAllNetworkInterfaces()
                .Where(item => item.OperationalStatus == OperationalStatus.Up)
                .Where(item => item.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
                .Select(item => new
                {
                    Interface = item,
                    Properties = item.GetIPProperties()
                })
                .OrderByDescending(item => item.Properties.GatewayAddresses.Count)
                .ToArray();
            var selected = candidates.FirstOrDefault();
            if (selected is null) return ("No active interface", "offline");

            var address = selected.Properties.UnicastAddresses
                .FirstOrDefault(item => item.Address.AddressFamily == AddressFamily.InterNetwork)?.Address.ToString()
                ?? selected.Properties.UnicastAddresses.FirstOrDefault()?.Address.ToString()
                ?? "unknown";
            var gateway = selected.Properties.GatewayAddresses.FirstOrDefault()?.Address.ToString() ?? "none";
            return (
                selected.Interface.Name,
                $"{selected.Interface.Id}|{address}|{gateway}");
        }
        catch (NetworkInformationException)
        {
            return ("Automatic routing", "unknown");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        cancellation?.Dispose();
        gate.Dispose();
        httpClient.Dispose();
    }
}
