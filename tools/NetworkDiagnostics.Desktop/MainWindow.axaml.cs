using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using NetworkDeepProbe.Diagnostics;
using NetworkDeepProbe.Models;
using NetworkDeepProbe.Planning;

namespace NetworkDiagnostics.Desktop;

public sealed partial class MainWindow : Window
{
    private CancellationTokenSource? runCancellation;
    private NetworkDiagnosticsReportV2? latestReport;
    private string? latestReportPath;
    private bool initialized;

    public MainWindow()
    {
        InitializeComponent();
        ProfileSelector.SelectedIndex = 0;
        MethodSelector.SelectedIndex = 0;
        initialized = true;
        RefreshPlan();
        LoadHistory();
    }

    private ComboBox ProfileSelector => Required<ComboBox>("ProfileSelector");
    private ComboBox MethodSelector => Required<ComboBox>("MethodSelector");
    private TextBox TargetInput => Required<TextBox>("TargetInput");
    private TextBox LanTargetInput => Required<TextBox>("LanTargetInput");
    private CheckBox IncludeAddressesCheck => Required<CheckBox>("IncludeAddressesCheck");
    private TextBlock EstimatedTimeText => Required<TextBlock>("EstimatedTimeText");
    private TextBlock TransferCapText => Required<TextBlock>("TransferCapText");
    private TextBlock DownloadConnectionsText => Required<TextBlock>("DownloadConnectionsText");
    private TextBlock UploadConnectionsText => Required<TextBlock>("UploadConnectionsText");
    private TextBlock DownloadRunsText => Required<TextBlock>("DownloadRunsText");
    private TextBlock DataUseText => Required<TextBlock>("DataUseText");
    private Button RunButton => Required<Button>("RunButton");
    private Button StopButton => Required<Button>("StopButton");
    private TextBlock StatusText => Required<TextBlock>("StatusText");
    private ProgressBar RunProgress => Required<ProgressBar>("RunProgress");
    private TextBlock LiveText => Required<TextBlock>("LiveText");
    private ListBox HistoryList => Required<ListBox>("HistoryList");
    private TextBlock ReportPathText => Required<TextBlock>("ReportPathText");
    private TextBlock DownloadMetric => Required<TextBlock>("DownloadMetric");
    private TextBlock DownloadDetail => Required<TextBlock>("DownloadDetail");
    private TextBlock UploadMetric => Required<TextBlock>("UploadMetric");
    private TextBlock UploadDetail => Required<TextBlock>("UploadDetail");
    private TextBlock LossMetric => Required<TextBlock>("LossMetric");
    private TextBlock LossDetail => Required<TextBlock>("LossDetail");
    private TextBlock LatencyMetric => Required<TextBlock>("LatencyMetric");
    private TextBlock LatencyDetail => Required<TextBlock>("LatencyDetail");
    private StackPanel FlowResultsPanel => Required<StackPanel>("FlowResultsPanel");
    private StackPanel ScalingResultsPanel => Required<StackPanel>("ScalingResultsPanel");
    private TextBlock WifiTitleText => Required<TextBlock>("WifiTitleText");
    private TextBlock WifiDetailText => Required<TextBlock>("WifiDetailText");
    private TextBlock RoutingTitleText => Required<TextBlock>("RoutingTitleText");
    private TextBlock RoutingDetailText => Required<TextBlock>("RoutingDetailText");
    private StackPanel InterfacesPanel => Required<StackPanel>("InterfacesPanel");
    private TextBlock RouteSummaryText => Required<TextBlock>("RouteSummaryText");
    private StackPanel DnsPanel => Required<StackPanel>("DnsPanel");
    private TextBlock TraceSummaryText => Required<TextBlock>("TraceSummaryText");
    private StackPanel TracePanel => Required<StackPanel>("TracePanel");
    private StackPanel ServicesPanel => Required<StackPanel>("ServicesPanel");

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private T Required<T>(string name) where T : Control
    {
        return this.FindControl<T>(name)
            ?? throw new InvalidOperationException($"Desktop control {name} was not found.");
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
        DownloadConnectionsText.Text = ConnectionSequence(plan.DownloadStages);
        UploadConnectionsText.Text = ConnectionSequence(plan.UploadStages);
        DownloadRunsText.Text = DownloadRunLabel(plan);
        DataUseText.Text = $"Transfers up to {FormatBytes(plan.TransferCapBytes)} against the first-party test origin. Avoid metered or cellular connections.";
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
        ResetResultPanels();
        StatusText.Text = "Starting native diagnostic…";
        var profile = SelectedProfile();
        var pingCount = profile switch
        {
            TestProfileId.Quick => 12,
            TestProfileId.Standard => 20,
            TestProfileId.Extended => 30,
            _ => 20
        };
        var target = string.IsNullOrWhiteSpace(TargetInput.Text) ? "1.1.1.1" : TargetInput.Text.Trim();
        var lanTarget = string.IsNullOrWhiteSpace(LanTargetInput.Text) ? null : LanTargetInput.Text.Trim();
        var options = new NativeDiagnosticRunOptions(
            Profile: profile,
            TransferMethod: SelectedMethod(),
            Target: target,
            PingCount: pingCount,
            IncludeAddresses: IncludeAddressesCheck.IsChecked == true,
            LanTarget: lanTarget);
        var progress = new Progress<NativeRunProgress>(UpdateProgress);

        try
        {
            var report = await NetworkDiagnosticsRunner.RunAsync(options, progress, runCancellation.Token);
            latestReport = report;
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

    private void StopClicked(object? sender, RoutedEventArgs eventArgs)
    {
        runCancellation?.Cancel();
    }

    private void UpdateProgress(NativeRunProgress progress)
    {
        StatusText.Text = progress.Message;
        if (progress.Phase == "diagnostics")
        {
            RunProgress.IsIndeterminate = true;
        }
        else
        {
            RunProgress.IsIndeterminate = false;
            RunProgress.Value = Math.Clamp(progress.Fraction * 100, 0, 100);
        }

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
            RunProgress.Value = 0;
            RunProgress.IsIndeterminate = false;
        }
    }

    private void ResetResultPanels()
    {
        DownloadMetric.Text = "—";
        UploadMetric.Text = "—";
        LossMetric.Text = "—";
        LatencyMetric.Text = "—";
        FlowResultsPanel.Children.Clear();
        FlowResultsPanel.Children.Add(MutedText("Measurement in progress."));
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
        RenderDeepDiagnostics(deep);
    }

    private void RenderFlows(NativeInternetTransferReport? transfer)
    {
        FlowResultsPanel.Children.Clear();
        ScalingResultsPanel.Children.Clear();
        if (transfer is null)
        {
            FlowResultsPanel.Children.Add(MutedText("Internet transfer measurements were not available."));
            return;
        }

        foreach (var measurement in transfer.FlowMeasurements)
        {
            var download = measurement.Download is null ? "Not sampled" : $"{measurement.Download.SteadyMbps:0.#} Mbps download";
            var upload = measurement.Upload is null ? "Not sampled" : $"{measurement.Upload.SteadyMbps:0.#} Mbps upload";
            var delay = measurement.DownloadLatency?.IncreaseMs is null
                ? "Loaded delay unavailable"
                : $"+{measurement.DownloadLatency.IncreaseMs:0.#} ms loaded download delay";
            FlowResultsPanel.Children.Add(InformationCard(
                measurement.Strategy == TransferStrategy.Single ? "Single connection" : "Aggregate capacity",
                $"{measurement.Connections} connection{(measurement.Connections == 1 ? string.Empty : "s")}",
                download,
                upload,
                delay));
        }

        var single = transfer.FlowMeasurements.FirstOrDefault(item => item.Strategy == TransferStrategy.Single)?.Download;
        var aggregate = transfer.FlowMeasurements.FirstOrDefault(item => item.Strategy == TransferStrategy.Aggregate)?.Download;
        if (single is not null && aggregate is not null && aggregate.SteadyMbps > 0)
        {
            var share = single.SteadyMbps / aggregate.SteadyMbps * 100;
            var gain = single.SteadyMbps > 0 ? (aggregate.SteadyMbps / single.SteadyMbps - 1) * 100 : 0;
            FlowResultsPanel.Children.Add(InformationCard(
                "Difference",
                $"One connection reached {share:0}% of aggregate capacity",
                $"Parallel gain: {(gain >= 0 ? "+" : string.Empty)}{gain:0}%",
                InterpretFlowShare(share)));
        }

        if (transfer.DownloadScaling.Count > 2)
        {
            ScalingResultsPanel.Children.Add(SectionLabel("STRESS DOWNLOAD SCALING"));
            foreach (var point in transfer.DownloadScaling)
            {
                ScalingResultsPanel.Children.Add(InformationCard(
                    $"{point.Connections} connection{(point.Connections == 1 ? string.Empty : "s")}",
                    $"{point.Download.SteadyMbps:0.#} Mbps steady",
                    $"{point.Download.Mbps:0.#} Mbps whole phase",
                    $"+{point.DownloadLatency.IncreaseMs:0.#} ms loaded delay · {point.Download.Qualification}"));
            }
        }
    }

    private void RenderDeepDiagnostics(DeepProbeReport? deep)
    {
        InterfacesPanel.Children.Clear();
        DnsPanel.Children.Clear();
        TracePanel.Children.Clear();
        ServicesPanel.Children.Clear();
        if (deep is null)
        {
            InterfacesPanel.Children.Add(MutedText("Deep diagnostics were not available."));
            return;
        }

        RenderWifi(deep.Wifi);
        RenderRouting(deep.Routing);

        foreach (var network in deep.Interfaces)
        {
            InterfacesPanel.Children.Add(InformationCard(
                network.Name,
                network.Description,
                $"{network.Type} · {(network.LinkSpeedMbps is null ? "link rate unavailable" : $"{network.LinkSpeedMbps} Mbps link")}",
                $"MTU {network.Ipv4Mtu?.ToString() ?? "—"} · {[network.SupportsIpv4 ? "IPv4" : null, network.SupportsIpv6 ? "IPv6" : null].Where(value => value is not null).Aggregate(string.Empty, (current, value) => current.Length == 0 ? value! : $"{current} + {value}")}"));
        }

        var defaultRoute = deep.Routing?.Entries.FirstOrDefault(entry => entry.IsDefault);
        RouteSummaryText.Text = defaultRoute is null
            ? $"Traceroute recorded {deep.TraceRoute.Hops.Count} hops. Path MTU: {deep.PathMtu.EstimatedIpv4Mtu?.ToString() ?? "unavailable"}."
            : $"Default route uses {defaultRoute.InterfaceName ?? "an unavailable interface"}{(defaultRoute.Gateway is null ? string.Empty : $" via {defaultRoute.Gateway}")}. Path MTU: {deep.PathMtu.EstimatedIpv4Mtu?.ToString() ?? "unavailable"}.";

        foreach (var resolver in deep.DnsResolvers.OrderBy(item => item.MedianMs ?? double.MaxValue))
        {
            DnsPanel.Children.Add(CompactLine(
                resolver.Name,
                $"{resolver.Successful}/{resolver.Attempts} · median {FormatMilliseconds(resolver.MedianMs)} · p95 {FormatMilliseconds(resolver.P95Ms)}"));
        }

        TraceSummaryText.Text = $"{deep.TraceRoute.Hops.Count} hops to {deep.TraceRoute.Target} · {(deep.TraceRoute.ReachedDestination ? "destination reached" : "partial path")}.";
        foreach (var hop in deep.TraceRoute.Hops.Take(40))
        {
            var address = hop.AddressRedacted ? "Private hop" : hop.Address ?? "No reply";
            var samples = string.Join(" / ", hop.RoundTripsMs.Select(FormatMilliseconds));
            TracePanel.Children.Add(CompactLine($"{hop.Hop:00}  {address}", $"{hop.Hostname ?? "—"} · {samples}"));
        }

        foreach (var endpoint in deep.ServiceEndpoints)
        {
            ServicesPanel.Children.Add(CompactLine(
                endpoint.Name,
                endpoint.Reachable
                    ? $"DNS {FormatMilliseconds(endpoint.DnsMs)} · TCP {FormatMilliseconds(endpoint.TcpMs)} · TLS {FormatMilliseconds(endpoint.TlsMs)} · {endpoint.ApplicationProtocol ?? endpoint.TlsProtocol ?? "connected"}"
                    : endpoint.Error ?? "Connection failed"));
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
        var details = new List<string>();
        if (wifi.SignalPercent is not null) details.Add($"{wifi.SignalPercent}% signal");
        if (wifi.RssiDbm is not null) details.Add($"{wifi.RssiDbm} dBm");
        if (wifi.Band is not null) details.Add(wifi.Band);
        if (wifi.Channel is not null) details.Add($"channel {wifi.Channel}");
        if (wifi.Protocol is not null) details.Add(wifi.Protocol);
        if (wifi.ReceiveRateMbps is not null) details.Add($"{wifi.ReceiveRateMbps} Mbps receive link");
        if (wifi.TransmitRateMbps is not null) details.Add($"{wifi.TransmitRateMbps} Mbps transmit link");
        WifiDetailText.Text = details.Count == 0 ? "Connected; detailed radio fields were unavailable." : string.Join(" · ", details);
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

    private NativeTransferPlan CurrentPlan() =>
        NetworkDiagnosticsRunner.DescribePlan(SelectedProfile(), SelectedMethod());

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
        var path = Path.Combine(
            directory,
            $"network-report-{report.Run.StartedAt:yyyyMMdd-HHmmss}-{report.Run.Profile.ToString().ToLowerInvariant()}-{report.Run.TransferMethod.ToString().ToLowerInvariant()}.json");
        await NetworkDiagnosticsJson.WriteAsync(path, report, cancellationToken);
        return path;
    }

    private void LoadHistory()
    {
        try
        {
            var directory = ReportDirectory();
            Directory.CreateDirectory(directory);
            var items = Directory.EnumerateFiles(directory, "network-report-*.json")
                .Select(path => new ReportHistoryItem(path, File.GetLastWriteTime(path)))
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
            var source = latestReportPath;
            if (source is null || !File.Exists(source))
            {
                ReportPathText.Text = "No completed report is available to export.";
                return;
            }
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrWhiteSpace(documents)) documents = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var directory = Path.Combine(documents, "Network Diagnostics Exports");
            Directory.CreateDirectory(directory);
            var destination = Path.Combine(directory, Path.GetFileName(source));
            await using var input = File.OpenRead(source);
            await using var output = File.Create(destination);
            await input.CopyToAsync(output);
            ReportPathText.Text = $"Exported copy: {destination}";
        }
        catch (Exception error)
        {
            ReportPathText.Text = $"Export failed: {error.Message}";
        }
    }

    private void OpenReportsFolderClicked(object? sender, RoutedEventArgs eventArgs)
    {
        try
        {
            var directory = ReportDirectory();
            Directory.CreateDirectory(directory);
            Process.Start(new ProcessStartInfo { FileName = directory, UseShellExecute = true });
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

    private static string ConnectionSequence(IReadOnlyList<TransferStagePlan> stages) =>
        string.Join(" + ", stages.Select(stage => stage.Connections));

    private static string DownloadRunLabel(NativeTransferPlan plan)
    {
        if (plan.Profile == TestProfileId.Extended && plan.Method == TransferMethod.Compare) return "5 scaling stages";
        var single = plan.DownloadStages.Where(stage => stage.Strategy == TransferStrategy.Single).Sum(stage => stage.Samples);
        var aggregate = plan.DownloadStages.Where(stage => stage.Strategy == TransferStrategy.Aggregate).Sum(stage => stage.Samples);
        var parts = new List<string>();
        if (single > 0) parts.Add($"{single} single");
        if (aggregate > 0) parts.Add($"{aggregate} parallel");
        return string.Join(" + ", parts);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1_000_000_000) return $"{bytes / 1_000_000_000d:0.###} GB";
        if (bytes >= 1_000_000) return $"{bytes / 1_000_000d:0.#} MB";
        if (bytes >= 1_000) return $"{bytes / 1_000d:0.#} kB";
        return $"{bytes} B";
    }

    private static string FormatMilliseconds(double? value) => value is null ? "—" : $"{value:0.#} ms";

    private static string InterpretFlowShare(double share) => share switch
    {
        < 70 => "Parallel transfers used materially more of the measured path.",
        < 90 => "Parallel transfers improved throughput, but one connection still captured most capacity.",
        _ => "One connection captured nearly all measured aggregate capacity."
    };

    private static TextBlock MutedText(string text) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        Foreground = Brush.Parse("#68645E")
    };

    private static TextBlock SectionLabel(string text) => new()
    {
        Text = text,
        FontSize = 11,
        FontWeight = FontWeight.Bold,
        Foreground = Brush.Parse("#A4553B")
    };

    private static Border InformationCard(string title, params string[] lines)
    {
        var content = new StackPanel { Spacing = 5 };
        content.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeight.SemiBold });
        foreach (var line in lines.Where(line => !string.IsNullOrWhiteSpace(line)))
        {
            content.Children.Add(MutedText(line));
        }
        return new Border
        {
            Background = Brush.Parse("#FCFBF8"),
            BorderBrush = Brush.Parse("#DED8CE"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14),
            Child = content
        };
    }

    private static Border CompactLine(string title, string detail)
    {
        return new Border
        {
            BorderBrush = Brush.Parse("#E2DCD2"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(0, 6, 0, 8),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("180,*"),
                Children =
                {
                    new TextBlock { Text = title, FontWeight = FontWeight.SemiBold },
                    new TextBlock { Text = detail, Grid.ColumnProperty = 1, Foreground = Brush.Parse("#68645E"), TextWrapping = TextWrapping.Wrap }
                }
            }
        };
    }

    private sealed record ReportHistoryItem(string Path, DateTime Modified)
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
                var path = SettingsPath();
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, JsonSerializer.Serialize(values));
            }
            catch
            {
                // Confirmation persistence is optional; the dialog will appear again.
            }
        }

        private static Dictionary<string, long> Load()
        {
            try
            {
                var path = SettingsPath();
                if (!File.Exists(path)) return new Dictionary<string, long>(StringComparer.Ordinal);
                return JsonSerializer.Deserialize<Dictionary<string, long>>(File.ReadAllText(path))
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
