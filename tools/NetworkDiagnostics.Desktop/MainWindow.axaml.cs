using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using NetworkDeepProbe.Diagnostics;
using NetworkDeepProbe.Models;
using NetworkDeepProbe.Planning;

namespace NetworkDiagnostics.Desktop;

public sealed partial class MainWindow : Window
{
    private CancellationTokenSource? runCancellation;
    private string? latestReportPath;
    private bool initialized;

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        ProfileSelector.SelectedIndex = 0;
        MethodSelector.SelectedIndex = 0;
        initialized = true;
        RefreshPlan();
        LoadHistory();
    }

    private void PlanSelectionChanged(object? sender, SelectionChangedEventArgs eventArgs)
    {
        if (initialized) RefreshPlan();
    }

    private void RefreshPlan()
    {
        var plan = CurrentPlan();
        EstimatedTimeText.Text = $"{plan.EstimatedSeconds} sec";
        TransferCapText.Text = FormatBytes(plan.TransferCapBytes);
        DownloadConnectionsText.Text = JoinConnections(plan.DownloadStages);
        UploadConnectionsText.Text = JoinConnections(plan.UploadStages);
        DownloadRunsText.Text = DescribeDownloadRuns(plan);
        DataUseText.Text = $"Transfers up to {FormatBytes(plan.TransferCapBytes)}. Avoid metered or cellular connections.";
        RunButton.Content = $"Run {plan.ProfileName.ToLowerInvariant()} diagnostic";
    }

    private async void RunClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (runCancellation is not null) return;
        var plan = CurrentPlan();
        if (plan.Profile != TestProfileId.Quick && !ConfirmationStore.IsApproved(plan.Profile, plan.TransferCapBytes))
        {
            var confirmation = await new DataUseDialog(plan).ShowDialog<DataUseConfirmation?>(this);
            if (confirmation is null || !confirmation.Confirmed) return;
            if (confirmation.Remember) ConfirmationStore.Approve(plan.Profile, plan.TransferCapBytes);
        }

        runCancellation = new CancellationTokenSource();
        SetRunning(true);
        ResetResults();
        var profile = SelectedProfile();
        var options = new NativeDiagnosticRunOptions(
            Profile: profile,
            TransferMethod: SelectedMethod(),
            Target: string.IsNullOrWhiteSpace(TargetInput.Text) ? "1.1.1.1" : TargetInput.Text.Trim(),
            PingCount: profile switch
            {
                TestProfileId.Quick => 12,
                TestProfileId.Standard => 20,
                TestProfileId.Extended => 30,
                _ => 20
            },
            IncludeAddresses: IncludeAddressesCheck.IsChecked == true,
            LanTarget: string.IsNullOrWhiteSpace(LanTargetInput.Text) ? null : LanTargetInput.Text.Trim());

        try
        {
            var report = await NetworkDiagnosticsRunner.RunAsync(
                options,
                new Progress<NativeRunProgress>(UpdateProgress),
                runCancellation.Token);
            latestReportPath = await SaveReportAsync(report, runCancellation.Token);
            RenderReport(report);
            LoadHistory();
            StatusText.Text = "Diagnostic complete.";
            ReportPathText.Text = $"Saved locally: {latestReportPath}";
            RunProgress.IsIndeterminate = false;
            RunProgress.Value = 100;
            LiveText.Text = $"Measured {FormatBytes(report.InternetTransfer?.DataUsedBytes ?? 0)} of transfer payload.";
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Diagnostic stopped.";
            LiveText.Text = "No report was saved for the interrupted run.";
        }
        catch (Exception error)
        {
            StatusText.Text = "Diagnostic failed.";
            LiveText.Text = error.Message;
        }
        finally
        {
            runCancellation.Dispose();
            runCancellation = null;
            SetRunning(false);
        }
    }

    private void StopClicked(object? sender, RoutedEventArgs eventArgs) => runCancellation?.Cancel();

    private void UpdateProgress(NativeRunProgress progress)
    {
        StatusText.Text = progress.Message;
        RunProgress.IsIndeterminate = progress.Phase == "diagnostics";
        if (!RunProgress.IsIndeterminate) RunProgress.Value = Math.Clamp(progress.Fraction * 100, 0, 100);
        var live = new List<string>();
        if (progress.LiveMbps is not null) live.Add($"{progress.LiveMbps:0.#} Mbps");
        if (progress.LiveLatencyMs is not null) live.Add($"{progress.LiveLatencyMs:0.#} ms");
        if (progress.BytesTransferred > 0) live.Add(FormatBytes(progress.BytesTransferred));
        LiveText.Text = live.Count == 0 ? progress.Stage : string.Join(" · ", live);
    }

    private void SetRunning(bool running)
    {
        RunButton.IsEnabled = !running;
        StopButton.IsVisible = running;
        ProfileSelector.IsEnabled = !running;
        MethodSelector.IsEnabled = !running;
        TargetInput.IsEnabled = !running;
        LanTargetInput.IsEnabled = !running;
        IncludeAddressesCheck.IsEnabled = !running;
        if (running)
        {
            StatusText.Text = "Starting native diagnostic…";
            RunProgress.IsIndeterminate = false;
            RunProgress.Value = 0;
        }
    }

    private void ResetResults()
    {
        DownloadMetric.Text = "—";
        UploadMetric.Text = "—";
        LossMetric.Text = "—";
        LatencyMetric.Text = "—";
        FlowResultsPanel.Children.Clear();
        FlowResultsPanel.Children.Add(Muted("Measurement in progress."));
        ScalingResultsPanel.Children.Clear();
    }

    private void RenderReport(NetworkDiagnosticsReportV2 report)
    {
        var transfer = report.InternetTransfer;
        var deep = report.DeepDiagnostics;
        if (transfer is not null)
        {
            DownloadMetric.Text = $"{transfer.Download.SteadyMbps:0.#}";
            DownloadDetail.Text = $"Mbps steady · {transfer.Download.Qualification}";
            UploadMetric.Text = $"{transfer.Upload.SteadyMbps:0.#}";
            UploadDetail.Text = $"Mbps steady · {transfer.Upload.Qualification}";
        }
        if (deep is not null)
        {
            LossMetric.Text = $"{deep.InternetPing.Statistics.LossPercent:0.#}%";
            LossDetail.Text = $"{deep.InternetPing.Statistics.Received}/{deep.InternetPing.Statistics.Sent} replies";
            LatencyMetric.Text = deep.InternetPing.Statistics.MedianMs is null ? "—" : $"{deep.InternetPing.Statistics.MedianMs:0.#}";
            LatencyDetail.Text = $"ms median · {deep.InternetPing.Statistics.JitterMs:0.#} ms jitter";
        }
        RenderFlows(transfer);
        RenderDeep(deep);
    }

    private void RenderFlows(NativeInternetTransferReport? transfer)
    {
        FlowResultsPanel.Children.Clear();
        ScalingResultsPanel.Children.Clear();
        if (transfer is null)
        {
            FlowResultsPanel.Children.Add(Muted("Internet transfer measurements were unavailable."));
            return;
        }

        foreach (var item in transfer.FlowMeasurements)
        {
            FlowResultsPanel.Children.Add(Card(
                item.Strategy == TransferStrategy.Single ? "Single connection" : "Aggregate capacity",
                $"{item.Connections} connection{(item.Connections == 1 ? string.Empty : "s")}",
                item.Download is null ? "Download not sampled" : $"{item.Download.SteadyMbps:0.#} Mbps download",
                item.Upload is null ? "Upload not sampled" : $"{item.Upload.SteadyMbps:0.#} Mbps upload",
                item.DownloadLatency?.IncreaseMs is null ? "Loaded delay unavailable" : $"+{item.DownloadLatency.IncreaseMs:0.#} ms loaded download delay"));
        }

        var single = transfer.FlowMeasurements.FirstOrDefault(item => item.Strategy == TransferStrategy.Single)?.Download;
        var aggregate = transfer.FlowMeasurements.FirstOrDefault(item => item.Strategy == TransferStrategy.Aggregate)?.Download;
        if (single is not null && aggregate is not null && aggregate.SteadyMbps > 0)
        {
            var share = single.SteadyMbps / aggregate.SteadyMbps * 100;
            var gain = single.SteadyMbps <= 0 ? 0 : (aggregate.SteadyMbps / single.SteadyMbps - 1) * 100;
            FlowResultsPanel.Children.Add(Card(
                "Difference",
                $"One connection reached {share:0}% of aggregate capacity",
                $"Parallel gain: {(gain >= 0 ? "+" : string.Empty)}{gain:0}%",
                share < 70 ? "Parallel transfers used materially more of the path." : share < 90 ? "Parallel transfers improved throughput, but one connection still captured most capacity." : "One connection captured nearly all aggregate capacity."));
        }

        if (transfer.DownloadScaling.Count > 2)
        {
            ScalingResultsPanel.Children.Add(Label("STRESS DOWNLOAD SCALING"));
            foreach (var point in transfer.DownloadScaling)
            {
                ScalingResultsPanel.Children.Add(Card(
                    $"{point.Connections} connection{(point.Connections == 1 ? string.Empty : "s")}",
                    $"{point.Download.SteadyMbps:0.#} Mbps steady",
                    $"{point.Download.Mbps:0.#} Mbps whole phase",
                    $"+{point.DownloadLatency.IncreaseMs:0.#} ms loaded delay · {point.Download.Qualification}"));
            }
        }
    }

    private void RenderDeep(DeepProbeReport? deep)
    {
        InterfacesPanel.Children.Clear();
        DnsPanel.Children.Clear();
        TracePanel.Children.Clear();
        ServicesPanel.Children.Clear();
        if (deep is null)
        {
            InterfacesPanel.Children.Add(Muted("Deep diagnostics were unavailable."));
            return;
        }

        RenderWifi(deep.Wifi);
        RenderRouting(deep.Routing);
        foreach (var network in deep.Interfaces)
        {
            InterfacesPanel.Children.Add(Card(
                network.Name,
                network.Description,
                $"{network.Type} · {(network.LinkSpeedMbps is null ? "link rate unavailable" : $"{network.LinkSpeedMbps} Mbps link")}",
                $"MTU {network.Ipv4Mtu?.ToString() ?? "—"} · {IpSupport(network)}"));
        }

        var defaultRoute = deep.Routing?.Entries.FirstOrDefault(entry => entry.IsDefault);
        RouteSummaryText.Text = defaultRoute is null
            ? $"Traceroute recorded {deep.TraceRoute.Hops.Count} hops. Path MTU: {deep.PathMtu.EstimatedIpv4Mtu?.ToString() ?? "unavailable"}."
            : $"Default route uses {defaultRoute.InterfaceName ?? "an unavailable interface"}{(defaultRoute.Gateway is null ? string.Empty : $" via {defaultRoute.Gateway}")}. Path MTU: {deep.PathMtu.EstimatedIpv4Mtu?.ToString() ?? "unavailable"}.";

        foreach (var resolver in deep.DnsResolvers.OrderBy(item => item.MedianMs ?? double.MaxValue))
        {
            DnsPanel.Children.Add(Line(resolver.Name, $"{resolver.Successful}/{resolver.Attempts} · median {Ms(resolver.MedianMs)} · p95 {Ms(resolver.P95Ms)}"));
        }

        TraceSummaryText.Text = $"{deep.TraceRoute.Hops.Count} hops to {deep.TraceRoute.Target} · {(deep.TraceRoute.ReachedDestination ? "destination reached" : "partial path")}.";
        foreach (var hop in deep.TraceRoute.Hops.Take(40))
        {
            var address = hop.AddressRedacted ? "Private hop" : hop.Address ?? "No reply";
            TracePanel.Children.Add(Line($"{hop.Hop:00}  {address}", $"{hop.Hostname ?? "—"} · {string.Join(" / ", hop.RoundTripsMs.Select(Ms))}"));
        }

        foreach (var endpoint in deep.ServiceEndpoints)
        {
            var detail = endpoint.Reachable
                ? $"DNS {Ms(endpoint.DnsMs)} · TCP {Ms(endpoint.TcpMs)} · TLS {Ms(endpoint.TlsMs)} · {endpoint.ApplicationProtocol ?? endpoint.TlsProtocol ?? "connected"}"
                : endpoint.Error ?? "Connection failed";
            ServicesPanel.Children.Add(Line(endpoint.Name, detail));
        }
    }

    private void RenderWifi(WifiDetailsReport? wifi)
    {
        if (wifi is null || wifi.Status == "unavailable")
        {
            WifiTitleText.Text = "Unavailable";
            WifiDetailText.Text = wifi?.Error ?? "This report does not contain Wi-Fi details.";
            return;
        }
        if (wifi.Status == "not-connected")
        {
            WifiTitleText.Text = "Not connected";
            WifiDetailText.Text = wifi.Error ?? "A wireless interface was found without an active association.";
            return;
        }
        WifiTitleText.Text = wifi.Ssid ?? wifi.InterfaceName ?? "Connected Wi-Fi";
        var parts = new List<string>();
        if (wifi.SignalPercent is not null) parts.Add($"{wifi.SignalPercent}% signal");
        if (wifi.RssiDbm is not null) parts.Add($"{wifi.RssiDbm} dBm");
        if (wifi.Band is not null) parts.Add(wifi.Band);
        if (wifi.Channel is not null) parts.Add($"channel {wifi.Channel}");
        if (wifi.Protocol is not null) parts.Add(wifi.Protocol);
        if (wifi.ReceiveRateMbps is not null) parts.Add($"{wifi.ReceiveRateMbps} Mbps receive link");
        if (wifi.TransmitRateMbps is not null) parts.Add($"{wifi.TransmitRateMbps} Mbps transmit link");
        WifiDetailText.Text = parts.Count == 0 ? "Connected; detailed radio fields were unavailable." : string.Join(" · ", parts);
    }

    private void RenderRouting(RoutingDetailsReport? routing)
    {
        if (routing is null || routing.Status != "available")
        {
            RoutingTitleText.Text = "Unavailable";
            RoutingDetailText.Text = routing?.Error ?? "This report does not contain route-table details.";
            return;
        }
        var defaultRoute = routing.Entries.FirstOrDefault(entry => entry.IsDefault);
        RoutingTitleText.Text = $"{routing.Entries.Count} routes";
        RoutingDetailText.Text = defaultRoute is null
            ? "No default route was identified."
            : $"Default via {defaultRoute.InterfaceName ?? "unknown interface"}{(defaultRoute.Gateway is null ? string.Empty : $" · gateway {defaultRoute.Gateway}")}.";
    }

    private NativeTransferPlan CurrentPlan() => NetworkDiagnosticsRunner.DescribePlan(SelectedProfile(), SelectedMethod());

    private TestProfileId SelectedProfile() => ProfileSelector.SelectedIndex switch
    {
        1 => TestProfileId.Standard,
        2 => TestProfileId.Extended,
        _ => TestProfileId.Quick
    };

    private TransferMethod SelectedMethod() => MethodSelector.SelectedIndex switch
    {
        1 => TransferMethod.Single,
        2 => TransferMethod.Aggregate,
        _ => TransferMethod.Compare
    };

    private async Task<string> SaveReportAsync(NetworkDiagnosticsReportV2 report, CancellationToken cancellationToken)
    {
        var directory = ReportDirectory();
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"network-report-{report.Run.StartedAt:yyyyMMdd-HHmmss}-{report.Run.Profile.ToString().ToLowerInvariant()}-{report.Run.TransferMethod.ToString().ToLowerInvariant()}.json");
        await NetworkDiagnosticsJson.WriteAsync(path, report, cancellationToken);
        return path;
    }

    private void LoadHistory()
    {
        try
        {
            Directory.CreateDirectory(ReportDirectory());
            var items = Directory.EnumerateFiles(ReportDirectory(), "network-report-*.json")
                .Select(path => new ReportItem(path, File.GetLastWriteTime(path)))
                .OrderByDescending(item => item.Modified)
                .Take(12)
                .ToArray();
            HistoryList.ItemsSource = items;
            if (latestReportPath is null && items.Length > 0) latestReportPath = items[0].Path;
        }
        catch (Exception error)
        {
            HistoryList.ItemsSource = new[] { $"History unavailable: {error.Message}" };
        }
    }

    private async void ExportLatestClicked(object? sender, RoutedEventArgs eventArgs)
    {
        try
        {
            if (latestReportPath is null || !File.Exists(latestReportPath))
            {
                ReportPathText.Text = "No completed report is available to export.";
                return;
            }
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrWhiteSpace(documents)) documents = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var directory = Path.Combine(documents, "Network Diagnostics Exports");
            Directory.CreateDirectory(directory);
            var destination = Path.Combine(directory, Path.GetFileName(latestReportPath));
            await using var input = File.OpenRead(latestReportPath);
            await using var output = File.Create(destination);
            await input.CopyToAsync(output);
            ReportPathText.Text = $"Exported copy: {destination}";
        }
        catch (Exception error)
        {
            ReportPathText.Text = $"Export failed: {error.Message}";
        }
    }

    private async void OpenReportsFolderClicked(object? sender, RoutedEventArgs eventArgs)
    {
        try
        {
            var directory = new DirectoryInfo(ReportDirectory());
            directory.Create();
            var launcher = TopLevel.GetTopLevel(this)?.Launcher;
            if (launcher is null || !await launcher.LaunchDirectoryInfoAsync(directory))
            {
                ReportPathText.Text = "Could not open reports folder: the operating system did not accept the request.";
            }
        }
        catch (Exception error)
        {
            ReportPathText.Text = $"Could not open reports folder: {error.Message}";
        }
    }

    private static string ReportDirectory()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root)) root = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(root, "Network Diagnostics Suite", "Reports");
    }

    private static string JoinConnections(IEnumerable<TransferStagePlan> stages) => string.Join(" + ", stages.Select(stage => stage.Connections));

    private static string DescribeDownloadRuns(NativeTransferPlan plan)
    {
        if (plan.Profile == TestProfileId.Extended && plan.Method == TransferMethod.Compare) return "5 scaling stages";
        var single = plan.DownloadStages.Where(stage => stage.Strategy == TransferStrategy.Single).Sum(stage => stage.Samples);
        var aggregate = plan.DownloadStages.Where(stage => stage.Strategy == TransferStrategy.Aggregate).Sum(stage => stage.Samples);
        var parts = new List<string>();
        if (single > 0) parts.Add($"{single} single");
        if (aggregate > 0) parts.Add($"{aggregate} parallel");
        return string.Join(" + ", parts);
    }

    private static string IpSupport(NetworkInterfaceReport network)
    {
        var values = new List<string>();
        if (network.SupportsIpv4) values.Add("IPv4");
        if (network.SupportsIpv6) values.Add("IPv6");
        return values.Count == 0 ? "IP support unavailable" : string.Join(" + ", values);
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1_000_000_000 => $"{bytes / 1_000_000_000d:0.###} GB",
        >= 1_000_000 => $"{bytes / 1_000_000d:0.#} MB",
        >= 1_000 => $"{bytes / 1_000d:0.#} kB",
        _ => $"{bytes} B"
    };

    private static string Ms(double? value) => value is null ? "—" : $"{value:0.#} ms";

    private static TextBlock Muted(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse("#68645E") };

    private static TextBlock Label(string text) => new() { Text = text, FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#A4553B") };

    private static Border Card(string title, params string[] lines)
    {
        var panel = new StackPanel { Spacing = 5 };
        panel.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeight.SemiBold });
        foreach (var line in lines.Where(line => !string.IsNullOrWhiteSpace(line))) panel.Children.Add(Muted(line));
        return new Border
        {
            Background = Brush.Parse("#FCFBF8"),
            BorderBrush = Brush.Parse("#DED8CE"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14),
            Child = panel
        };
    }

    private static Border Line(string title, string detail)
    {
        var titleText = new TextBlock { Text = title, FontWeight = FontWeight.SemiBold };
        var detailText = new TextBlock { Text = detail, Foreground = Brush.Parse("#68645E"), TextWrapping = TextWrapping.Wrap };
        Grid.SetColumn(detailText, 1);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("180,*") };
        grid.Children.Add(titleText);
        grid.Children.Add(detailText);
        return new Border
        {
            BorderBrush = Brush.Parse("#E2DCD2"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(0, 6, 0, 8),
            Child = grid
        };
    }

    private sealed record ReportItem(string Path, DateTime Modified)
    {
        public override string ToString() => $"{Modified:g}  ·  {System.IO.Path.GetFileName(Path)}";
    }

    private static class ConfirmationStore
    {
        public static bool IsApproved(TestProfileId profile, long cap)
        {
            var values = Load();
            return values.TryGetValue(profile.ToString(), out var approved) && approved >= cap;
        }

        public static void Approve(TestProfileId profile, long cap)
        {
            var values = Load();
            values[profile.ToString()] = Math.Max(values.GetValueOrDefault(profile.ToString()), cap);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath())!);
                File.WriteAllText(SettingsPath(), JsonSerializer.Serialize(values));
            }
            catch
            {
                // Persistence is optional. The confirmation will appear again if writing fails.
            }
        }

        private static Dictionary<string, long> Load()
        {
            try
            {
                if (!File.Exists(SettingsPath())) return new Dictionary<string, long>(StringComparer.Ordinal);
                return JsonSerializer.Deserialize<Dictionary<string, long>>(File.ReadAllText(SettingsPath()))
                    ?? new Dictionary<string, long>(StringComparer.Ordinal);
            }
            catch
            {
                return new Dictionary<string, long>(StringComparer.Ordinal);
            }
        }

        private static string SettingsPath() => Path.Combine(ReportDirectory(), "..", "confirmations.json");
    }
}
