using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using NetworkDeepProbe.Diagnostics;
using NetworkDeepProbe.Models;
using NetworkDeepProbe.Planning;

namespace NetworkDiagnostics.Desktop;

public sealed partial class MainWindow : Window
{
    private enum AppPage
    {
        Setup,
        Results,
        History
    }

    private enum ThemePreference
    {
        System,
        Light,
        Dark
    }

    private CancellationTokenSource? runCancellation;
    private string? latestReportPath;
    private bool initialized;
    private bool? compactLayout;
    private AppPage currentPage;
    private ThemePreference themePreference;
    private NetworkDiagnosticsReportV2? displayedReport;
    private readonly DispatcherTimer progressAnimationTimer;
    private NativeTransferPlan? runningPlan;
    private double displayedProgress;
    private double targetProgress;
    private double progressAnimationStart;
    private double progressAnimationDurationMs;
    private long progressAnimationStartedAt;

    public MainWindow()
    {
        InitializeComponent();
        progressAnimationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        progressAnimationTimer.Tick += ProgressAnimationTick;
        themePreference = ThemeStore.Load();
        ApplyThemePreference();
        if (Application.Current is { } application)
        {
            application.ActualThemeVariantChanged += (_, _) => HandleActualThemeChanged();
        }
        ProfileSelector.SelectedIndex = 0;
        MethodSelector.SelectedIndex = 0;
        initialized = true;
        RefreshPlan();
        LoadHistory();
        ShowPage(AppPage.Setup);
        ApplyResponsiveLayout(ClientSize.Width > 0 ? ClientSize.Width : Width);
    }

    private void MainWindowSizeChanged(object? sender, SizeChangedEventArgs eventArgs) =>
        ApplyResponsiveLayout(eventArgs.NewSize.Width);

    private void ApplyResponsiveLayout(double width)
    {
        var compact = width < 1020;
        if (compactLayout == compact) return;
        compactLayout = compact;

        if (compact)
        {
            ShellGrid.ColumnDefinitions = new ColumnDefinitions("76,*");
            BrandCopy.IsVisible = false;
            NavWorkspaceLabel.IsVisible = false;
            NewTestNavLabel.IsVisible = false;
            ResultsNavLabel.IsVisible = false;
            HistoryNavLabel.IsVisible = false;
            ThemeModeText.IsVisible = false;
            PrivacyCopy.IsVisible = false;

            SetupContentGrid.ColumnDefinitions = new ColumnDefinitions("*");
            SetupContentGrid.RowDefinitions = new RowDefinitions("Auto,Auto");
            Grid.SetRow(SetupConfigPane, 0);
            Grid.SetColumn(SetupConfigPane, 0);
            Grid.SetRow(PlanPane, 1);
            Grid.SetColumn(PlanPane, 0);
            PlanPane.Margin = new Thickness(0, 20, 0, 0);

            ResultsHeaderGrid.ColumnDefinitions = new ColumnDefinitions("*");
            ResultsHeaderGrid.RowDefinitions = new RowDefinitions("Auto,Auto");
            Grid.SetRow(ReportPathText, 1);
            Grid.SetColumn(ReportPathText, 0);
            ReportPathText.HorizontalAlignment = HorizontalAlignment.Left;
            ReportPathText.TextAlignment = TextAlignment.Left;
            ReportPathText.Margin = new Thickness(0, 8, 0, 0);

            AnalysisTabs.TabStripPlacement = Dock.Top;
            foreach (var tab in AnalysisTabs.Items.OfType<TabItem>())
            {
                tab.Width = 160;
                tab.Margin = new Thickness(0, 0, 5, 8);
            }
        }
        else
        {
            ShellGrid.ColumnDefinitions = new ColumnDefinitions("216,*");
            BrandCopy.IsVisible = true;
            NavWorkspaceLabel.IsVisible = true;
            NewTestNavLabel.IsVisible = true;
            ResultsNavLabel.IsVisible = true;
            HistoryNavLabel.IsVisible = true;
            ThemeModeText.IsVisible = true;
            PrivacyCopy.IsVisible = true;

            SetupContentGrid.ColumnDefinitions = new ColumnDefinitions("*,310");
            SetupContentGrid.RowDefinitions = new RowDefinitions("Auto");
            Grid.SetRow(SetupConfigPane, 0);
            Grid.SetColumn(SetupConfigPane, 0);
            Grid.SetRow(PlanPane, 0);
            Grid.SetColumn(PlanPane, 1);
            PlanPane.Margin = new Thickness(0);

            ResultsHeaderGrid.ColumnDefinitions = new ColumnDefinitions("*,Auto");
            ResultsHeaderGrid.RowDefinitions = new RowDefinitions("Auto");
            Grid.SetRow(ReportPathText, 0);
            Grid.SetColumn(ReportPathText, 1);
            ReportPathText.HorizontalAlignment = HorizontalAlignment.Right;
            ReportPathText.TextAlignment = TextAlignment.Right;
            ReportPathText.Margin = new Thickness(0);

            AnalysisTabs.TabStripPlacement = Dock.Left;
            foreach (var tab in AnalysisTabs.Items.OfType<TabItem>())
            {
                tab.Width = 176;
                tab.Margin = new Thickness(0, 0, 0, 4);
            }
        }
    }

    private void ThemeClicked(object? sender, RoutedEventArgs eventArgs)
    {
        themePreference = themePreference switch
        {
            ThemePreference.System => ThemePreference.Light,
            ThemePreference.Light => ThemePreference.Dark,
            _ => ThemePreference.System
        };
        ThemeStore.Save(themePreference);
        ApplyThemePreference();
    }

    private void ApplyThemePreference()
    {
        if (Application.Current is not { } application) return;
        application.RequestedThemeVariant = themePreference switch
        {
            ThemePreference.Light => ThemeVariant.Light,
            ThemePreference.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
        UpdateThemeControl();
    }

    private void HandleActualThemeChanged()
    {
        UpdateThemeControl();
        if (displayedReport is not null) RenderReport(displayedReport);
    }

    private void UpdateThemeControl()
    {
        if (ThemeModeText is null || ThemeGlyphText is null) return;
        var dark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
        ThemeGlyphText.Text = dark ? "☾" : "☀";
        ThemeModeText.Text = themePreference switch
        {
            ThemePreference.Light => "Appearance · Light",
            ThemePreference.Dark => "Appearance · Dark",
            _ => "Appearance · System"
        };
    }

    private void NewTestNavClicked(object? sender, RoutedEventArgs eventArgs) => ShowPage(AppPage.Setup);

    private void ResultsNavClicked(object? sender, RoutedEventArgs eventArgs) => ShowPage(AppPage.Results);

    private void HistoryNavClicked(object? sender, RoutedEventArgs eventArgs)
    {
        LoadHistory();
        ShowPage(AppPage.History);
    }

    private void ShowPage(AppPage page)
    {
        currentPage = page;
        SetupPage.IsVisible = page == AppPage.Setup;
        ResultsPage.IsVisible = page == AppPage.Results;
        HistoryPage.IsVisible = page == AppPage.History;
        SetActive(NewTestNavButton, page == AppPage.Setup);
        SetActive(ResultsNavButton, page == AppPage.Results);
        SetActive(HistoryNavButton, page == AppPage.History);
        HeaderContextText.Text = page switch
        {
            AppPage.Setup => "New test",
            AppPage.Results => runCancellation is null ? "Results" : "Test running",
            AppPage.History => "Report history",
            _ => "Network diagnostics"
        };
    }

    private static void SetActive(Button button, bool active)
    {
        if (active && !button.Classes.Contains("active")) button.Classes.Add("active");
        if (!active) button.Classes.Remove("active");
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
        RunButton.Content = RunLabel(plan.Profile);
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
        runningPlan = plan;
        ShowPage(AppPage.Results);
        SetRunning(true);
        ResetResults();
        var profile = SelectedProfile();
        var options = new NativeDiagnosticRunOptions(
            Profile: profile,
            TransferMethod: SelectedMethod(),
            Target: string.IsNullOrWhiteSpace(TargetInput.Text) ? "1.1.1.1" : TargetInput.Text.Trim(),
            PingCount: profile switch
            {
                TestProfileId.Quick => 8,
                TestProfileId.Standard => 20,
                TestProfileId.Extended => 30,
                _ => 20
            },
            IncludeAddresses: IncludeAddressesCheck.IsChecked == true,
            LanTarget: string.IsNullOrWhiteSpace(LanTargetInput.Text) ? null : LanTargetInput.Text.Trim());

        var completed = false;
        try
        {
            var report = await NetworkDiagnosticsRunner.RunAsync(
                options,
                new Progress<NativeRunProgress>(UpdateProgress),
                runCancellation.Token);
            latestReportPath = await SaveReportAsync(report, runCancellation.Token);
            RenderReport(report);
            LoadHistory();
            StatusText.Text = "Diagnostic complete";
            ReportPathText.Text = $"Saved report: {latestReportPath}";
            SetProgressTarget(100);
            LiveText.Text = $"{FormatBytes(report.InternetTransfer?.DataUsedBytes ?? 0)} transferred during this test.";
            completed = true;
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Diagnostic stopped";
            LiveText.Text = "Interrupted tests are not saved.";
        }
        catch (Exception error)
        {
            StatusText.Text = "Diagnostic failed";
            LiveText.Text = error.Message;
        }
        finally
        {
            if (!completed) FreezeProgressAnimation();
            runningPlan = null;
            runCancellation.Dispose();
            runCancellation = null;
            SetRunning(false);
        }
    }

    private void StopClicked(object? sender, RoutedEventArgs eventArgs) => runCancellation?.Cancel();

    private void UpdateProgress(NativeRunProgress progress)
    {
        StatusText.Text = progress.Message;
        SetProgressTarget(OverallProgress(progress));
        var live = new List<string>();
        if (progress.LiveMbps is not null) live.Add($"{progress.LiveMbps:0.#} Mbps");
        if (progress.LiveLatencyMs is not null) live.Add($"{progress.LiveLatencyMs:0.#} ms");
        if (progress.BytesTransferred > 0) live.Add(FormatBytes(progress.BytesTransferred));
        LiveText.Text = live.Count == 0 ? progress.Stage : string.Join(" · ", live);
    }

    private void SetRunning(bool running)
    {
        RunButton.IsEnabled = !running;
        RunButton.IsVisible = !running;
        StopButton.IsVisible = running;
        ProfileSelector.IsEnabled = !running;
        MethodSelector.IsEnabled = !running;
        TargetInput.IsEnabled = !running;
        LanTargetInput.IsEnabled = !running;
        IncludeAddressesCheck.IsEnabled = !running;
        NewTestNavButton.IsEnabled = !running;
        HistoryNavButton.IsEnabled = !running;
        ResultsNavButton.IsEnabled = true;
        if (running)
        {
            StatusText.Text = "Starting native diagnostic…";
            RunProgress.IsIndeterminate = false;
            SetProgressTarget(2, immediate: true);
        }
        if (currentPage == AppPage.Results)
        {
            HeaderContextText.Text = running ? "Test running" : "Results";
        }
    }

    private double OverallProgress(NativeRunProgress progress)
    {
        if (progress.Phase == "diagnostics") return DiagnosticCheckpoint(progress.Message);
        if (progress.Phase == "idle") return 2 + Math.Clamp(progress.Fraction, 0, 1) * 8;
        if (progress.Phase == "complete") return 82;
        if (runningPlan is null) return targetProgress;

        var direction = progress.Phase switch
        {
            "download" => TransferDirection.Download,
            "upload" => TransferDirection.Upload,
            _ => (TransferDirection?)null
        };
        if (direction is null) return targetProgress;

        var stages = runningPlan.DownloadStages.Concat(runningPlan.UploadStages).ToArray();
        var stage = stages.FirstOrDefault(item =>
            item.Direction == direction.Value && item.Id == progress.Stage);
        if (stage is null) return targetProgress;

        var totalDuration = stages.Sum(item => Math.Max(item.DurationMs, 1));
        var elapsedBeforeStage = stages
            .TakeWhile(item => !ReferenceEquals(item, stage))
            .Sum(item => Math.Max(item.DurationMs, 1));
        var stageFraction = Math.Clamp(progress.Fraction, 0, 1);
        var transferFraction = (elapsedBeforeStage + Math.Max(stage.DurationMs, 1) * stageFraction)
            / (double)Math.Max(totalDuration, 1);
        return 10 + transferFraction * 72;
    }

    private double DiagnosticCheckpoint(string message)
    {
        if (message.StartsWith("Selecting the measurement endpoint", StringComparison.Ordinal)) return 3;
        if (message.StartsWith("Using ", StringComparison.Ordinal)) return 4;
        if (message.StartsWith("Inspecting active network interfaces", StringComparison.Ordinal)) return 83;
        if (message.StartsWith("Inspecting Wi-Fi and routing details", StringComparison.Ordinal)) return 85;
        if (message.StartsWith("Resolving ", StringComparison.Ordinal)) return 87;
        if (message.StartsWith("Measuring the default gateway", StringComparison.Ordinal)) return 89;
        if (message.StartsWith("Sending ", StringComparison.Ordinal)) return 91;
        if (message.StartsWith("Tracing the route", StringComparison.Ordinal)) return 93;
        if (message.StartsWith("Testing Domain Name System resolvers", StringComparison.Ordinal)) return 95;
        if (message.StartsWith("Estimating the IPv4 path", StringComparison.Ordinal)) return 97;
        if (message.StartsWith("Timing common Transport Layer Security endpoints", StringComparison.Ordinal)) return 98;
        if (message.StartsWith("Checking the local throughput server", StringComparison.Ordinal)) return 98.3;
        if (message.StartsWith("Measuring local download", StringComparison.Ordinal)) return 98.6;
        if (message.StartsWith("Measuring local upload", StringComparison.Ordinal)) return 99;
        return targetProgress;
    }

    private void SetProgressTarget(double value, bool immediate = false)
    {
        value = Math.Clamp(value, 0, 100);
        if (immediate)
        {
            progressAnimationTimer.Stop();
            displayedProgress = value;
            targetProgress = value;
            RunProgress.Value = value;
            return;
        }

        // Progress callbacks from different probes can arrive slightly out of order.
        // Ignoring regressions prevents the indicator from flashing back to an earlier phase.
        if (value <= targetProgress) return;
        progressAnimationStart = displayedProgress;
        targetProgress = value;
        progressAnimationStartedAt = System.Diagnostics.Stopwatch.GetTimestamp();
        progressAnimationDurationMs = Math.Clamp((targetProgress - displayedProgress) * 18, 160, 560);
        if (!progressAnimationTimer.IsEnabled) progressAnimationTimer.Start();
    }

    private void ProgressAnimationTick(object? sender, EventArgs eventArgs)
    {
        var elapsedMs = System.Diagnostics.Stopwatch.GetElapsedTime(progressAnimationStartedAt).TotalMilliseconds;
        var position = Math.Clamp(elapsedMs / progressAnimationDurationMs, 0, 1);
        var eased = 1 - Math.Pow(1 - position, 3);
        displayedProgress = progressAnimationStart + (targetProgress - progressAnimationStart) * eased;
        RunProgress.Value = displayedProgress;
        if (position < 1) return;

        displayedProgress = targetProgress;
        RunProgress.Value = targetProgress;
        progressAnimationTimer.Stop();
    }

    private void FreezeProgressAnimation()
    {
        progressAnimationTimer.Stop();
        targetProgress = displayedProgress;
        RunProgress.Value = displayedProgress;
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
        FindingEndpointText.Text = "Selecting measurement endpoint…";
        FindingsPanel.Children.Clear();
        FindingsPanel.Children.Add(Muted("Findings will appear after the diagnostic completes."));
    }

    private void RenderReport(NetworkDiagnosticsReportV2 report)
    {
        displayedReport = report;
        var transfer = report.InternetTransfer;
        var deep = report.DeepDiagnostics;
        if (transfer is not null)
        {
            DownloadMetric.Text = $"{transfer.Download.SteadyMbps:0.#}";
            DownloadDetail.Text = $"Mbps · {QualificationLabel(transfer.Download.Qualification)}";
            UploadMetric.Text = $"{transfer.Upload.SteadyMbps:0.#}";
            UploadDetail.Text = $"Mbps · {QualificationLabel(transfer.Upload.Qualification)}";
        }
        if (deep is not null)
        {
            LossMetric.Text = $"{deep.InternetPing.Statistics.LossPercent:0.#}%";
            LossDetail.Text = $"{deep.InternetPing.Statistics.Received} of {deep.InternetPing.Statistics.Sent} replies";
            LatencyMetric.Text = deep.InternetPing.Statistics.MedianMs is null ? "—" : $"{deep.InternetPing.Statistics.MedianMs:0.#}";
            LatencyDetail.Text = $"ms median · jitter {Ms(deep.InternetPing.Statistics.JitterMs)}";
        }
        RenderFlows(transfer);
        RenderDeep(deep);
        RenderFindings(report);
    }

    private void RenderFindings(NetworkDiagnosticsReportV2 report)
    {
        FindingsPanel.Children.Clear();
        var findings = report.Findings.Count > 0
            ? report.Findings
            : DiagnosticClassifier.Classify(report);
        var endpoint = report.Measurement?.SelectedEndpoint;
        FindingEndpointText.Text = endpoint is null
            ? "Legacy report · endpoint context unavailable"
            : $"{report.Measurement!.Engine} engine · {endpoint.Name}\n{endpoint.Provider} · {endpoint.SelectionReason}";

        if (findings.Count == 0)
        {
            FindingsPanel.Children.Add(Muted("No interpretation is available for this report."));
            return;
        }

        foreach (var finding in findings)
        {
            FindingsPanel.Children.Add(FindingRow(finding));
        }
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
                item.DownloadLatency?.IncreaseMs is null ? "Loaded delay unavailable" : $"Loaded download delay: +{item.DownloadLatency.IncreaseMs:0.#} ms"));
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
                    $"{point.Download.SteadyMbps:0.#} Mbps steady rate",
                    $"{point.Download.Mbps:0.#} Mbps whole phase",
                    $"Loaded delay: +{point.DownloadLatency.IncreaseMs:0.#} ms · {QualificationLabel(point.Download.Qualification)}"));
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
            DnsPanel.Children.Add(Line(resolver.Name, $"{resolver.Successful} of {resolver.Attempts} replies · Median {Ms(resolver.MedianMs)} · P95 {Ms(resolver.P95Ms)}"));
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
            WifiDetailText.Text = "Wi-Fi details could not be read on this Mac.";
            return;
        }
        if (wifi.Status == "not-connected")
        {
            WifiTitleText.Text = "Not connected";
            WifiDetailText.Text = "A Wi-Fi interface was found, but it is not connected.";
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
            RoutingDetailText.Text = "Routing details could not be read on this Mac.";
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

    private static string QualificationLabel(string qualification) => qualification switch
    {
        "cap-limited" => "Profile cap reached early",
        "still-ramping" => "Still ramping at finish",
        "declining" => "Declined during sample",
        "unstable" => "Variable sample",
        _ => "Qualified duration sample"
    };

    private static string RunLabel(TestProfileId profile) => profile switch
    {
        TestProfileId.Quick => "Run connection check",
        TestProfileId.Standard => "Run full diagnostic",
        TestProfileId.Extended => "Run stress test",
        _ => "Run diagnostic"
    };

    private static TextBlock Muted(string text) => new()
    {
        Text = text,
        FontSize = 12,
        LineHeight = 17,
        TextWrapping = TextWrapping.Wrap,
        Foreground = ThemeBrush("TextSecondaryBrush")
    };

    private static TextBlock Label(string text) => new()
    {
        Text = text,
        FontFamily = new FontFamily("Menlo"),
        FontSize = 10,
        FontWeight = FontWeight.Bold,
        LetterSpacing = 1.1,
        Foreground = ThemeBrush("AccentBrush"),
        Margin = new Thickness(0, 10, 0, 3)
    };

    private static Border Card(string title, params string[] lines)
    {
        var titleText = new TextBlock
        {
            Text = title,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 18, 0)
        };
        var details = new StackPanel { Spacing = 3 };
        foreach (var line in lines.Where(line => !string.IsNullOrWhiteSpace(line))) details.Children.Add(Muted(line));
        Grid.SetColumn(details, 1);
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("170,*") };
        row.Children.Add(titleText);
        row.Children.Add(details);
        return new Border
        {
            BorderBrush = ThemeBrush("BorderBrush"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(0, 11),
            Child = row
        };
    }

    private static Border Line(string title, string detail)
    {
        var titleText = new TextBlock
        {
            Text = title,
            FontFamily = new FontFamily("Menlo"),
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 18, 0)
        };
        var detailText = new TextBlock
        {
            Text = detail,
            FontFamily = new FontFamily("Menlo"),
            FontSize = 10,
            LineHeight = 15,
            Foreground = ThemeBrush("TextSecondaryBrush"),
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetColumn(detailText, 1);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("170,*") };
        grid.Children.Add(titleText);
        grid.Children.Add(detailText);
        return new Border
        {
            BorderBrush = ThemeBrush("BorderBrush"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(0, 8),
            Child = grid
        };
    }

    private static Border FindingRow(DiagnosticFinding finding)
    {
        var tone = finding.Severity switch
        {
            "critical" => ThemeBrush("DangerTextBrush"),
            "warning" => ThemeBrush("AccentBrush"),
            _ => ThemeBrush("SuccessTextBrush")
        };
        var status = finding.Severity switch
        {
            "critical" => "ACTION RECOMMENDED",
            "warning" => "WORTH INVESTIGATING",
            _ => "CONTEXT"
        };
        var content = new StackPanel { Spacing = 7 };
        content.Children.Add(new TextBlock
        {
            Text = $"{status} · {finding.Confidence.ToUpperInvariant()} CONFIDENCE",
            FontFamily = new FontFamily("Menlo"),
            FontSize = 9,
            FontWeight = FontWeight.Bold,
            LetterSpacing = 1,
            Foreground = tone,
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(new TextBlock
        {
            Text = finding.Title,
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = -0.2,
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(new TextBlock
        {
            Text = finding.Summary,
            FontSize = 11,
            LineHeight = 17,
            Foreground = ThemeBrush("TextSecondaryBrush"),
            TextWrapping = TextWrapping.Wrap
        });

        if (finding.Evidence.Count > 0)
        {
            var evidence = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                ItemWidth = 210,
                ItemHeight = double.NaN
            };
            foreach (var item in finding.Evidence)
            {
                evidence.Children.Add(new Border
                {
                    BorderBrush = ThemeBrush("BorderBrush"),
                    BorderThickness = new Thickness(0, 1, 0, 0),
                    Margin = new Thickness(0, 5, 16, 0),
                    Padding = new Thickness(0, 7, 0, 0),
                    Child = new StackPanel
                    {
                        Spacing = 2,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = item.Label.ToUpperInvariant(),
                                FontFamily = new FontFamily("Menlo"),
                                FontSize = 8,
                                LetterSpacing = 0.8,
                                Foreground = ThemeBrush("TextTertiaryBrush"),
                                TextWrapping = TextWrapping.Wrap
                            },
                            new TextBlock
                            {
                                Text = item.Value,
                                FontFamily = new FontFamily("Menlo"),
                                FontSize = 11,
                                FontWeight = FontWeight.SemiBold,
                                TextWrapping = TextWrapping.Wrap
                            }
                        }
                    }
                });
            }
            content.Children.Add(evidence);
        }

        if (finding.Recommendations.Count > 0)
        {
            content.Children.Add(Label("WHAT TO TRY"));
            foreach (var recommendation in finding.Recommendations)
            {
                content.Children.Add(Muted($"→  {recommendation}"));
            }
        }
        if (!string.IsNullOrWhiteSpace(finding.NextTest))
        {
            content.Children.Add(new TextBlock
            {
                Text = $"NEXT TEST  {finding.NextTest}",
                FontFamily = new FontFamily("Menlo"),
                FontSize = 9,
                LineHeight = 14,
                Foreground = tone,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 3, 0, 0)
            });
        }

        var marker = new Border
        {
            Width = 3,
            MinHeight = 28,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = tone
        };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("3,*"), ColumnSpacing = 14 };
        Grid.SetColumn(content, 1);
        grid.Children.Add(marker);
        grid.Children.Add(content);
        return new Border
        {
            BorderBrush = ThemeBrush("BorderBrush"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(0, 16, 0, 18),
            Child = grid
        };
    }

    private sealed record ReportItem(string Path, DateTime Modified)
    {
        public string FileName => System.IO.Path.GetFileName(Path);

        public string DisplayDate => Modified.ToString("MMM d, yyyy · h:mm tt");

        public string DisplayTitle
        {
            get
            {
                var parts = System.IO.Path.GetFileNameWithoutExtension(Path).Split('-');
                if (parts.Length < 2) return "Completed test";
                var profile = parts[^2] switch
                {
                    "quick" => "Connection Check",
                    "standard" => "Full",
                    "extended" => "Stress",
                    _ => "Completed"
                };
                var method = parts[^1] switch
                {
                    "compare" => "Compare",
                    "single" => "Single",
                    "aggregate" => "Aggregate",
                    _ => "Test"
                };
                return $"{profile} · {method}";
            }
        }
    }

    private static IBrush ThemeBrush(string key)
    {
        if (Application.Current is { } application
            && application.TryFindResource(key, application.ActualThemeVariant, out var value)
            && value is IBrush brush)
        {
            return brush;
        }
        return Brushes.Gray;
    }

    private static class ThemeStore
    {
        public static ThemePreference Load()
        {
            try
            {
                if (!File.Exists(SettingsPath())) return ThemePreference.System;
                return Enum.TryParse<ThemePreference>(File.ReadAllText(SettingsPath()).Trim(), true, out var value)
                    ? value
                    : ThemePreference.System;
            }
            catch
            {
                return ThemePreference.System;
            }
        }

        public static void Save(ThemePreference preference)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath())!);
                File.WriteAllText(SettingsPath(), preference.ToString().ToLowerInvariant());
            }
            catch
            {
                // Appearance persistence is optional; the current session still uses the selection.
            }
        }

        private static string SettingsPath() => Path.Combine(ReportDirectory(), "..", "appearance.txt");
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
