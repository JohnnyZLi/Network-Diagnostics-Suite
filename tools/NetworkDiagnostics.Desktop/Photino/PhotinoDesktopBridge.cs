using System.Reflection;
using System.Text;
using System.Text.Json;
using NetworkDeepProbe.Diagnostics;
using NetworkDeepProbe.Models;
using NetworkDeepProbe.Planning;
using NetworkDiagnostics.Desktop.Monitoring;
using NetworkDiagnostics.Desktop.Presentation;
using NetworkDiagnostics.Desktop.Services;
using Photino.NET;

namespace NetworkDiagnostics.Desktop;

public sealed partial class PhotinoDesktopBridge : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string? ApplicationVersion =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3);
    private static readonly (string Name, string[] Extensions)[] ReportFileFilters =
    [
        ("Network Diagnostics reports", [".json"])
    ];
    private static readonly (string Name, string[] Extensions)[] MonitorSnapshotFileFilters =
    [
        ("Network health snapshot", [".html"])
    ];
    private static readonly (string Name, string[] Extensions)[] MonitorHistoryFileFilters =
    [
        ("Network monitoring history", [".csv"])
    ];

    private readonly object runGate = new();
    private readonly object lanServerGate = new();
    private readonly object completedReportGate = new();
    private readonly PhotinoSettingsStore settingsStore;
    private readonly ReportStore reportStore;
    private readonly ContinuousMonitorService monitorService;
    private PhotinoWindow? window;
    private CancellationTokenSource? activeRun;
    private Guid activeBridgeRunId;
    private CancellationTokenSource? lanServerCancellation;
    private Task? lanServerTask;
    private int? lanServerPort;
    private CancellationTokenSource? lanClientCancellation;
    private Task? lanClientTask;
    private readonly Dictionary<Guid, NetworkDiagnosticsReportV2> completedReports = [];
    private bool disposed;

    public PhotinoDesktopBridge(
        PhotinoSettingsStore? settingsStore = null,
        ReportStore? reportStore = null,
        ContinuousMonitorService? monitorService = null)
    {
        this.settingsStore = settingsStore ?? new PhotinoSettingsStore();
        var initialSettings = this.settingsStore.Load();
        this.reportStore = reportStore ?? new ReportStore(this.settingsStore.RootDirectory, initialSettings.ReportsDirectory);
        this.reportStore.Prune(initialSettings.ReportRetentionDays);
        this.monitorService = monitorService
            ?? new ContinuousMonitorService(this.settingsStore.RootDirectory);
        this.monitorService.SnapshotChanged += MonitorSnapshotChanged;
    }

    public void Attach(PhotinoWindow targetWindow)
    {
        ArgumentNullException.ThrowIfNull(targetWindow);
        ObjectDisposedException.ThrowIf(disposed, this);
        if (window is not null) throw new InvalidOperationException("The desktop bridge is already attached.");

        window = targetWindow;
        targetWindow.RegisterWebMessageReceivedHandler((_, message) =>
        {
            _ = HandleMessageAsync(targetWindow, message);
        });
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        monitorService.SnapshotChanged -= MonitorSnapshotChanged;
        monitorService.SetDiagnosticActivity(false);
        lock (runGate)
        {
            activeRun?.Cancel();
            activeRun?.Dispose();
            activeRun = null;
        }
        lock (lanServerGate)
        {
            lanServerCancellation?.Cancel();
            lanClientCancellation?.Cancel();
        }
        try
        {
            lanServerTask?.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            // Expected when the application exits with a LAN server active.
        }
        lanServerCancellation?.Dispose();
        lanServerCancellation = null;
        lanServerTask = null;
        try
        {
            lanClientTask?.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            // Expected when the application exits with a LAN test active.
        }
        lanClientCancellation?.Dispose();
        lanClientCancellation = null;
        lanClientTask = null;
        monitorService.DisposeAsync().AsTask().GetAwaiter().GetResult();
        window = null;
    }

    private async Task HandleMessageAsync(PhotinoWindow sender, string message)
    {
        BridgeRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<BridgeRequest>(message, JsonOptions);
        }
        catch (JsonException error)
        {
            SendResponse(sender, null, false, null, $"Invalid bridge message: {error.Message}");
            return;
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Method))
        {
            SendResponse(sender, request?.Id, false, null, "Bridge method is required.");
            return;
        }

        try
        {
            switch (request.Method)
            {
                case "app.ready":
                    var settings = settingsStore.Load();
                    await monitorService.StartAsync(settings.ToMonitorOptions());
                    SendResponse(sender, request.Id, true, new
                    {
                        product = "Network Diagnostics",
                        host = "photino",
                        version = ApplicationVersion,
                        platform = OperatingSystem.IsMacOS()
                            ? "macOS"
                            : OperatingSystem.IsWindows()
                                ? "Windows"
                                : OperatingSystem.IsLinux()
                                    ? "Linux"
                                    : Environment.OSVersion.Platform.ToString(),
                        architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
                        appearance = BridgeProtocol.AppearanceId(settings.Appearance),
                        monitor = MonitorPayload(settings),
                        advanced = AdvancedSettingsPayload(settings),
                        capabilities = new[]
                        {
                            "diagnostic.run",
                            "diagnostic.cancel",
                            "diagnostic.describePlan",
                            "diagnostic.preflight",
                            "diagnostic.interfaces",
                            "diagnostic.download-path",
                            "reports.list",
                            "reports.get",
                            "reports.compare",
                            "reports.import",
                            "reports.export",
                            "reports.saveCurrent",
                            "reports.exportCurrent",
                            "reports.delete",
                            "reports.openFolder",
                            "reports.updateAnnotations",
                            "monitor.get",
                            "monitor.setEnabled",
                            "monitor.setWindow",
                            "monitor.markAlertsRead",
                            "monitor.clearAlerts",
                            "monitor.exportSnapshot",
                            "monitor.exportHistory",
                            "settings.get",
                            "settings.setAppearance",
                            "settings.setExpectedCapacity",
                            "settings.setPreferences",
                            "settings.chooseReportsDirectory",
                            "settings.getAdvanced",
                            "settings.setAdvanced",
                            "lan.server.start",
                            "lan.server.stop",
                            "lan.server.status",
                            "lan.client.run",
                            "lan.client.cancel"
                        }
                    });
                    break;

                case "settings.get":
                    SendSettings(sender, request.Id, settingsStore.Load());
                    break;

                case "settings.setAppearance":
                    SendSettings(
                        sender,
                        request.Id,
                        settingsStore.SaveAppearance(BridgeProtocol.ParseAppearance(request.Payload)));
                    break;

                case "settings.setExpectedCapacity":
                    await SetExpectedCapacityAsync(sender, request);
                    break;

                case "settings.setPreferences":
                    await SetApplicationPreferencesAsync(sender, request);
                    break;

                case "settings.chooseReportsDirectory":
                    ChooseReportsDirectory(sender, request.Id);
                    break;

                case "settings.getAdvanced":
                    SendResponse(sender, request.Id, true, AdvancedSettingsPayload(settingsStore.Load()));
                    break;

                case "settings.setAdvanced":
                    SetAdvancedSettings(sender, request);
                    break;

                case "diagnostic.interfaces":
                    SendResponse(sender, request.Id, true, NetworkDiagnosticsRunner.ListInterfaces());
                    break;

                case "diagnostic.preflight":
                    await SendPreflightAsync(sender, request);
                    break;

                case "lan.server.start":
                    StartLanServer(sender, request);
                    break;

                case "lan.server.stop":
                    StopLanServer(sender, request.Id);
                    break;

                case "lan.server.status":
                    SendLanServerStatus(sender, request.Id);
                    break;

                case "lan.client.run":
                    StartLanClient(sender, request);
                    break;

                case "lan.client.cancel":
                    CancelLanClient(sender, request.Id);
                    break;

                case "monitor.get":
                    SendResponse(sender, request.Id, true, MonitorPayload(settingsStore.Load()));
                    break;

                case "monitor.setEnabled":
                    await SetMonitoringEnabledAsync(sender, request);
                    break;

                case "monitor.setWindow":
                    SetMonitoringWindow(sender, request);
                    break;

                case "monitor.markAlertsRead":
                    await monitorService.MarkAllAlertsReadAsync();
                    SendResponse(sender, request.Id, true, MonitorPayload(settingsStore.Load()));
                    break;

                case "monitor.clearAlerts":
                    await monitorService.ClearAlertsAsync();
                    SendResponse(sender, request.Id, true, MonitorPayload(settingsStore.Load()));
                    break;

                case "monitor.exportSnapshot":
                    await ExportMonitoringSnapshotAsync(sender, request);
                    break;

                case "monitor.exportHistory":
                    await ExportMonitoringHistoryAsync(sender, request);
                    break;

                case "reports.list":
                    await SendReportListAsync(sender, request.Id);
                    break;

                case "reports.get":
                    await SendReportDetailAsync(sender, request);
                    break;

                case "reports.compare":
                    await SendReportComparisonAsync(sender, request);
                    break;

                case "reports.import":
                    await ImportReportAsync(sender, request);
                    break;

                case "reports.export":
                    await ExportReportAsync(sender, request);
                    break;

                case "reports.saveCurrent":
                    await SaveCurrentReportAsync(sender, request);
                    break;

                case "reports.exportCurrent":
                    await ExportCurrentReportAsync(sender, request);
                    break;

                case "reports.delete":
                    await DeleteReportAsync(sender, request);
                    break;

                case "reports.openFolder":
                    reportStore.OpenReportsFolder();
                    SendResponse(sender, request.Id, true, new { path = reportStore.ReportsDirectory });
                    break;

                case "reports.updateAnnotations":
                    await UpdateReportAnnotationsAsync(sender, request);
                    break;

                case "diagnostic.describePlan":
                    DescribePlan(sender, request);
                    break;

                case "diagnostic.run":
                    await StartDiagnosticAsync(sender, request);
                    break;

                case "diagnostic.cancel":
                    CancelDiagnostic(sender, request.Id);
                    break;

                default:
                    SendResponse(sender, request.Id, false, null, $"Unknown bridge method '{request.Method}'.");
                    break;
            }
        }
        catch (Exception error)
        {
            SendResponse(sender, request.Id, false, null, SafeMessage(error));
        }
    }

    private void SendSettings(PhotinoWindow sender, string? requestId, PhotinoAppSettings settings)
    {
        SendResponse(sender, requestId, true, new
        {
            appearance = BridgeProtocol.AppearanceId(settings.Appearance),
            monitoringEnabled = settings.MonitoringEnabled,
            monitoringWindow = settings.SelectedMonitoringWindow.ContractId(),
            monitoringIntervalSeconds = settings.MonitoringIntervalSeconds,
            monitoringAlertScoreThreshold = settings.MonitoringAlertScoreThreshold,
            expectedDownloadMbps = settings.ExpectedDownloadMbps,
            expectedUploadMbps = settings.ExpectedUploadMbps,
            reportsDirectory = settings.ReportsDirectory,
            effectiveReportsDirectory = reportStore.ReportsDirectory,
            reportRetentionDays = settings.ReportRetentionDays
        });
    }

    private static object AdvancedSettingsPayload(PhotinoAppSettings settings) => new
    {
        endpointCandidates = settings.TestOrigins,
        interfaceId = settings.InterfaceId,
        includeLocalIdentifiers = settings.IncludeLocalIdentifiers,
        lanTarget = settings.LanTarget,
        lanPort = settings.LanPort,
        lanDurationSeconds = settings.LanDurationSeconds,
        lanConnections = settings.LanConnections
    };

    private void SetAdvancedSettings(PhotinoWindow sender, BridgeRequest request)
    {
        var endpointCandidates = BridgeProtocol.ParseStringArray(request.Payload, "endpointCandidates");
        var interfaceId = BridgeProtocol.ParseOptionalString(request.Payload, "interfaceId");
        var includeLocalIdentifiers = BridgeProtocol.ParseRequiredBool(request.Payload, "includeLocalIdentifiers");
        var lanTarget = BridgeProtocol.ParseOptionalString(request.Payload, "lanTarget");
        var lanPort = BridgeProtocol.ParseRequiredInt(request.Payload, "lanPort", 1024, 65535);
        var lanDurationSeconds = BridgeProtocol.ParseRequiredInt(request.Payload, "lanDurationSeconds", 3, 30);
        var lanConnections = BridgeProtocol.ParseRequiredInt(request.Payload, "lanConnections", 1, 16);

        var settings = settingsStore.SaveAdvanced(
            endpointCandidates,
            interfaceId,
            includeLocalIdentifiers,
            lanTarget,
            lanPort,
            lanDurationSeconds,
            lanConnections);
        SendResponse(sender, request.Id, true, AdvancedSettingsPayload(settings));
    }

    private async Task SendPreflightAsync(PhotinoWindow sender, BridgeRequest request)
    {
        var profile = BridgeProtocol.ParseProfile(request.Payload);
        var method = BridgeProtocol.ParseTransferMethod(request.Payload);
        var downloadPath = BridgeProtocol.ParseDownloadPath(request.Payload);
        var settings = settingsStore.Load();
        var origins = settings.TestOrigins.Count == 0
            ? new[] { new Uri("https://network.johnnyli.dev/") }
            : settings.TestOrigins.Select(value => new Uri(value)).ToArray();
        var result = await NetworkDiagnosticsRunner.PreflightAsync(new NativePreflightOptions(
            Profile: profile,
            TransferMethod: method,
            TestOrigins: origins,
            InterfaceId: string.IsNullOrWhiteSpace(settings.InterfaceId) ? null : settings.InterfaceId,
            IncludeAddresses: settings.IncludeLocalIdentifiers,
            DownloadPath: downloadPath));
        SendResponse(sender, request.Id, true, result);
    }

    private void StartLanServer(PhotinoWindow sender, BridgeRequest request)
    {
        var port = BridgeProtocol.ParseRequiredInt(request.Payload, "port", 1024, 65535);
        lock (lanServerGate)
        {
            if (lanServerTask is { IsCompleted: false })
            {
                throw new InvalidOperationException("The LAN throughput server is already running.");
            }

            lanServerCancellation?.Dispose();
            lanServerCancellation = new CancellationTokenSource();
            lanServerPort = port;
            var cancellation = lanServerCancellation;
            var progress = new Progress<string>(message =>
            {
                var target = window;
                if (target is not null && !disposed)
                {
                    SendEvent(target, "lan.server.progress", new
                    {
                        port,
                        addresses = NetworkDiagnosticsRunner.ListLanServerAddresses(),
                        message
                    });
                }
            });
            lanServerTask = Task.Run(async () =>
            {
                try
                {
                    await NetworkDiagnosticsRunner.RunLanServerAsync(port, progress, cancellation.Token);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                    // Normal stop path.
                }
                catch (Exception error)
                {
                    var target = window;
                    if (target is not null && !disposed)
                    {
                        SendEvent(target, "lan.server.failed", new { message = SafeMessage(error) });
                    }
                }
                finally
                {
                    lock (lanServerGate)
                    {
                        if (lanServerPort == port) lanServerPort = null;
                    }
                    var target = window;
                    if (target is not null && !disposed)
                    {
                        SendEvent(target, "lan.server.stopped", new { port });
                    }
                }
            });
        }
        var payload = new
        {
            running = true,
            port,
            addresses = NetworkDiagnosticsRunner.ListLanServerAddresses()
        };
        SendResponse(sender, request.Id, true, payload);
        SendEvent(sender, "lan.server.started", payload);
    }

    private void StopLanServer(PhotinoWindow sender, string? requestId)
    {
        bool wasRunning;
        lock (lanServerGate)
        {
            wasRunning = lanServerTask is { IsCompleted: false };
            lanServerCancellation?.Cancel();
        }
        SendResponse(sender, requestId, true, new { stopped = wasRunning, port = lanServerPort });
    }

    private void SendLanServerStatus(PhotinoWindow sender, string? requestId)
    {
        bool running;
        lock (lanServerGate)
        {
            running = lanServerTask is { IsCompleted: false };
        }
        SendResponse(sender, requestId, true, new
        {
            running,
            port = lanServerPort,
            addresses = NetworkDiagnosticsRunner.ListLanServerAddresses(),
            clientRunning = lanClientTask is { IsCompleted: false }
        });
    }

    private async Task SetMonitoringEnabledAsync(PhotinoWindow sender, BridgeRequest request)
    {
        var enabled = BridgeProtocol.ParseRequiredBool(request.Payload, "enabled");
        var settings = settingsStore.SaveMonitoringEnabled(enabled);
        await monitorService.UpdateOptionsAsync(settings.ToMonitorOptions());
        SendResponse(sender, request.Id, true, MonitorPayload(settings));
    }

    private async Task SetExpectedCapacityAsync(PhotinoWindow sender, BridgeRequest request)
    {
        var downloadMbps = BridgeProtocol.ParseRequiredDouble(request.Payload, "downloadMbps", 1, 100_000);
        var uploadMbps = BridgeProtocol.ParseRequiredDouble(request.Payload, "uploadMbps", 1, 100_000);
        var settings = settingsStore.SaveExpectedCapacity(downloadMbps, uploadMbps);
        await monitorService.UpdateOptionsAsync(settings.ToMonitorOptions());
        SendSettings(sender, request.Id, settings);
    }

    private async Task SetApplicationPreferencesAsync(PhotinoWindow sender, BridgeRequest request)
    {
        var interval = BridgeProtocol.ParseRequiredInt(request.Payload, "monitoringIntervalSeconds", 2, 60);
        var threshold = BridgeProtocol.ParseRequiredInt(request.Payload, "monitoringAlertScoreThreshold", 1, 100);
        var reportsDirectory = BridgeProtocol.ParseOptionalString(request.Payload, "reportsDirectory");
        var retentionDays = BridgeProtocol.ParseRequiredInt(request.Payload, "reportRetentionDays", 0, 3650);
        var settings = settingsStore.SaveApplicationPreferences(interval, threshold, reportsDirectory, retentionDays);
        reportStore.Configure(settings.ReportsDirectory);
        var pruned = reportStore.Prune(settings.ReportRetentionDays);
        await monitorService.UpdateOptionsAsync(settings.ToMonitorOptions());
        SendResponse(sender, request.Id, true, new
        {
            settings = SettingsPayload(settings),
            prunedReports = pruned,
            effectiveReportsDirectory = reportStore.ReportsDirectory
        });
    }

    private void ChooseReportsDirectory(PhotinoWindow sender, string? requestId)
    {
        var paths = sender.ShowOpenFolder(
            title: "Choose reports folder",
            defaultPath: reportStore.ReportsDirectory,
            multiSelect: false);
        var selected = paths.FirstOrDefault();
        SendResponse(sender, requestId, true, new
        {
            cancelled = string.IsNullOrWhiteSpace(selected),
            path = string.IsNullOrWhiteSpace(selected) ? null : Path.GetFullPath(selected)
        });
    }

    private static object SettingsPayload(PhotinoAppSettings settings) => new
    {
        appearance = BridgeProtocol.AppearanceId(settings.Appearance),
        monitoringEnabled = settings.MonitoringEnabled,
        monitoringWindow = settings.SelectedMonitoringWindow.ContractId(),
        monitoringIntervalSeconds = settings.MonitoringIntervalSeconds,
        monitoringAlertScoreThreshold = settings.MonitoringAlertScoreThreshold,
        expectedDownloadMbps = settings.ExpectedDownloadMbps,
        expectedUploadMbps = settings.ExpectedUploadMbps,
        reportsDirectory = settings.ReportsDirectory,
        reportRetentionDays = settings.ReportRetentionDays
    };

    private void SetMonitoringWindow(PhotinoWindow sender, BridgeRequest request)
    {
        var contractId = BridgeProtocol.ParseOptionalString(request.Payload, "window")?.Trim().ToLowerInvariant();
        if (contractId is not ("1m" or "5m" or "1h" or "24h" or "7d"))
        {
            throw new ArgumentException("Monitoring window must be 1m, 5m, 1h, 24h, or 7d.");
        }
        var settings = settingsStore.SaveMonitoringWindow(MonitorWindowExtensions.Parse(contractId));
        SendResponse(sender, request.Id, true, MonitorPayload(settings));
    }

    private async Task ExportMonitoringSnapshotAsync(PhotinoWindow sender, BridgeRequest request)
    {
        var settings = settingsStore.Load();
        var presentation = NetworkExperiencePresenter.Build(
            monitorService.Snapshot,
            settings.ToMonitorOptions(),
            settings.SelectedMonitoringWindow);
        var fileName = $"network-health-{DateTime.Now:yyyyMMdd-HHmm}.html";
        var destinationPath = sender.ShowSaveFile(
            title: "Share network health snapshot",
            defaultPath: SuggestedUserPath(fileName),
            filters: MonitorSnapshotFileFilters);
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            SendResponse(sender, request.Id, true, new { cancelled = true });
            return;
        }
        if (!string.Equals(Path.GetExtension(destinationPath), ".html", StringComparison.OrdinalIgnoreCase))
        {
            destinationPath = Path.ChangeExtension(destinationPath, ".html");
        }

        await File.WriteAllTextAsync(
            destinationPath,
            MonitoringExportService.BuildShareHtml(presentation),
            new UTF8Encoding(false),
            CancellationToken.None);
        SendResponse(sender, request.Id, true, new
        {
            cancelled = false,
            fileName = Path.GetFileName(destinationPath)
        });
    }

    private async Task ExportMonitoringHistoryAsync(PhotinoWindow sender, BridgeRequest request)
    {
        var settings = settingsStore.Load();
        var windowId = settings.SelectedMonitoringWindow.ContractId();
        var fileName = $"network-history-{windowId}-{DateTime.Now:yyyyMMdd-HHmm}.csv";
        var destinationPath = sender.ShowSaveFile(
            title: "Export monitoring history",
            defaultPath: SuggestedUserPath(fileName),
            filters: MonitorHistoryFileFilters);
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            SendResponse(sender, request.Id, true, new { cancelled = true });
            return;
        }
        if (!string.Equals(Path.GetExtension(destinationPath), ".csv", StringComparison.OrdinalIgnoreCase))
        {
            destinationPath = Path.ChangeExtension(destinationPath, ".csv");
        }

        var csv = MonitoringExportService.BuildHistoryCsv(
            monitorService.Snapshot,
            settings.SelectedMonitoringWindow,
            settings.IncludeLocalIdentifiers);
        await File.WriteAllTextAsync(
            destinationPath,
            csv,
            new UTF8Encoding(false),
            CancellationToken.None);
        SendResponse(sender, request.Id, true, new
        {
            cancelled = false,
            fileName = Path.GetFileName(destinationPath),
            includesLocalIdentifiers = settings.IncludeLocalIdentifiers,
            window = windowId
        });
    }

    private void MonitorSnapshotChanged(object? sender, MonitorSnapshotChangedEventArgs eventArgs)
    {
        var target = window;
        if (target is null || disposed) return;
        try
        {
            SendEvent(target, "monitor.snapshot", MonitorPayload(settingsStore.Load()));
        }
        catch (Exception error) when (error is ObjectDisposedException or InvalidOperationException)
        {
            // The WebView may already be closing while a final monitor sample is published.
        }
    }

    private object MonitorPayload(PhotinoAppSettings settings)
    {
        var presentation = NetworkExperiencePresenter.Build(
            monitorService.Snapshot,
            settings.ToMonitorOptions(),
            settings.SelectedMonitoringWindow);
        return new
        {
            enabled = settings.MonitoringEnabled,
            running = monitorService.Snapshot.IsRunning,
            window = settings.SelectedMonitoringWindow.ContractId(),
            presentation.Score,
            band = presentation.Band.ToString().ToLowerInvariant(),
            presentation.Status,
            presentation.Summary,
            presentation.DeviceName,
            presentation.InterfaceName,
            presentation.LastUpdated,
            presentation.UnreadAlertCount,
            responsiveness = MonitorComponentPayload(presentation.Responsiveness),
            reliability = MonitorComponentPayload(presentation.Reliability),
            speed = MonitorComponentPayload(presentation.Speed),
            timeline = presentation.Timeline.Select(sample => new
            {
                sample.Timestamp,
                state = sample.State.ToString().ToLowerInvariant(),
                sample.LatencyMs,
                sample.JitterMs,
                sample.PacketLossPercent,
                diagnosticLoad = sample.IsDiagnosticLoad
            }).ToArray(),
            alerts = presentation.Alerts.Select(alert => new
            {
                alert.Id,
                alert.Timestamp,
                kind = MonitorAlertKindId(alert.Kind),
                severity = alert.Severity.ToString().ToLowerInvariant(),
                alert.Title,
                alert.Detail,
                alert.IsRead
            }).ToArray()
        };
    }

    private static string MonitorAlertKindId(MonitorAlertKind kind) => kind switch
    {
        MonitorAlertKind.Outage => "outage",
        MonitorAlertKind.Recovery => "recovery",
        MonitorAlertKind.NetworkChange => "network-change",
        MonitorAlertKind.Degradation => "degradation",
        MonitorAlertKind.BandwidthChange => "bandwidth-change",
        _ => "monitor"
    };

    private static object MonitorComponentPayload(ExperienceComponentPresentation component) => new
    {
        component.Title,
        component.Score,
        band = component.Band.ToString().ToLowerInvariant(),
        component.Status,
        component.Summary,
        metrics = component.Metrics.Select(metric => new
        {
            label = metric.Label,
            value = metric.Value
        }).ToArray()
    };

    private async Task SendReportListAsync(PhotinoWindow sender, string? requestId)
    {
        var reports = await reportStore.ListAsync();
        SendResponse(sender, requestId, true, reports.Select(ReportSummary).ToArray());
    }

    private async Task SendReportDetailAsync(PhotinoWindow sender, BridgeRequest request)
    {
        var reportId = BridgeProtocol.ParseRequiredGuid(request.Payload, "id");
        var reports = await reportStore.ListAsync();
        var stored = FindReport(reports, reportId);
        SendResponse(sender, request.Id, true, ReportDetailPayload(stored));
    }

    private async Task SendReportComparisonAsync(PhotinoWindow sender, BridgeRequest request)
    {
        var baselineId = BridgeProtocol.ParseRequiredGuid(request.Payload, "baselineId");
        var candidateId = BridgeProtocol.ParseRequiredGuid(request.Payload, "candidateId");
        if (baselineId == candidateId)
        {
            throw new ArgumentException("Choose two different saved reports to compare.");
        }

        var reports = await reportStore.ListAsync();
        var baseline = FindReport(reports, baselineId);
        var candidate = FindReport(reports, candidateId);
        var comparison = ReportComparisonService.Compare(baseline.Report, candidate.Report);

        SendResponse(sender, request.Id, true, new
        {
            baseline = ReportSummary(baseline),
            candidate = ReportSummary(candidate),
            baselineContext = ReportComparisonService.ContextLabel(baseline.Report),
            candidateContext = ReportComparisonService.ContextLabel(candidate.Report),
            comparison.Comparable,
            comparison.Warnings,
            comparison.Summary,
            comparison.Metrics
        });
    }

    private async Task ImportReportAsync(PhotinoWindow sender, BridgeRequest request)
    {
        var paths = sender.ShowOpenFile(filters: ReportFileFilters);
        var sourcePath = paths.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            SendResponse(sender, request.Id, true, new { cancelled = true });
            return;
        }
        if (!string.Equals(Path.GetExtension(sourcePath), ".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Choose a Network Diagnostics JSON report.");
        }

        var stored = await reportStore.ImportAsync(sourcePath);
        SendResponse(sender, request.Id, true, new
        {
            cancelled = false,
            detail = ReportDetailPayload(stored)
        });
    }

    private async Task ExportReportAsync(PhotinoWindow sender, BridgeRequest request)
    {
        var reportId = BridgeProtocol.ParseRequiredGuid(request.Payload, "id");
        var reports = await reportStore.ListAsync();
        var stored = FindReport(reports, reportId);
        var suggestedPath = Path.Combine(reportStore.ReportsDirectory, SuggestedExportName(stored.Report));
        var destinationPath = sender.ShowSaveFile(
            title: "Export Network Diagnostics report",
            defaultPath: suggestedPath,
            filters: ReportFileFilters);

        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            SendResponse(sender, request.Id, true, new { cancelled = true });
            return;
        }
        if (!string.Equals(Path.GetExtension(destinationPath), ".json", StringComparison.OrdinalIgnoreCase))
        {
            destinationPath = Path.ChangeExtension(destinationPath, ".json");
        }

        await reportStore.ExportAsync(stored.Report, destinationPath);
        SendResponse(sender, request.Id, true, new
        {
            cancelled = false,
            fileName = Path.GetFileName(destinationPath)
        });
    }

    private async Task UpdateReportAnnotationsAsync(PhotinoWindow sender, BridgeRequest request)
    {
        var reportId = BridgeProtocol.ParseRequiredGuid(request.Payload, "id");
        var label = BridgeProtocol.ParseOptionalString(request.Payload, "label");
        var tags = BridgeProtocol.ParseStringArray(request.Payload, "tags");
        var reports = await reportStore.ListAsync();
        var stored = FindReport(reports, reportId);
        var updated = await reportStore.UpdateAnnotationsAsync(stored, label, tags);
        SendResponse(sender, request.Id, true, ReportDetailPayload(updated));
    }

    private static StoredReport FindReport(IReadOnlyList<StoredReport> reports, Guid reportId)
    {
        var stored = reports.FirstOrDefault(item => item.Report.Run.Id == reportId);
        return stored ?? throw new KeyNotFoundException($"Saved report '{reportId}' was not found.");
    }

    private static object ReportDetailPayload(StoredReport stored) =>
        ReportDetailPayload(stored.Report, stored);

    private static object ReportDetailPayload(NetworkDiagnosticsReportV2 report, StoredReport? stored)
    {
        var presentation = DiagnosticReportPresenter.FromReport(report);
        return new
        {
            report = stored is null
                ? ReportSummary(report, report.GeneratedAt, savedLocally: false)
                : ReportSummary(stored),
            context = ReportComparisonService.ContextLabel(report),
            method = BridgeProtocol.MethodId(report.Run.TransferMethod),
            downloadDelivery = report.InternetTransfer?.DownloadDelivery,
            measurement = report.Measurement,
            localLink = report.LocalLink,
            technicalReport = report,
            presentation = new
            {
                outcome = presentation.Outcome.ToString().ToLowerInvariant(),
                presentation.Label,
                presentation.Verdict,
                presentation.Summary,
                presentation.NextAction,
                presentation.Metrics,
                presentation.Findings,
                technicalData = presentation.TechnicalEvidence,
                presentation.TechnicalEvidence
            }
        };
    }

    private static object ReportSummary(StoredReport stored) =>
        ReportSummary(stored.Report, stored.StoredAt, savedLocally: true);

    private static object ReportSummary(NetworkDiagnosticsReportV2 report, DateTimeOffset storedAt, bool savedLocally)
    {
        var internet = report.InternetTransfer;
        var presentation = DiagnosticReportPresenter.FromReport(report);
        return new
        {
            id = report.Run.Id,
            generatedAt = report.GeneratedAt,
            storedAt,
            profile = BridgeProtocol.ProfileId(report.Run.Profile),
            profileName = ProfileName(report.Run.Profile),
            label = report.Annotations?.Label,
            tags = report.Annotations?.Tags ?? [],
            savedLocally,
            outcome = presentation.Outcome.ToString().ToLowerInvariant(),
            outcomeLabel = presentation.Label,
            latencyMs = internet?.IdleLatency.MedianMs,
            requestLossPercent = internet?.IdleLatency.LossPercent,
            downloadMbps = internet?.Download.SteadyMbps,
            uploadMbps = internet?.Upload.SteadyMbps,
            dataUsedBytes = internet?.DataUsedBytes,
            requestedDownloadPath = internet?.DownloadDelivery?.RequestedPath,
            selectedDownloadPath = internet?.DownloadDelivery?.SelectedPath
        };
    }

    private static string ProfileName(TestProfileId profile) => profile switch
    {
        TestProfileId.ConnectionCheck => "Connection Check",
        TestProfileId.Quick => "Quick",
        TestProfileId.Standard => "Full",
        TestProfileId.Extended => "Stress",
        _ => "Diagnostic"
    };

    private static string SuggestedExportName(NetworkDiagnosticsReportV2 report)
    {
        var profile = BridgeProtocol.ProfileId(report.Run.Profile);
        return $"network-diagnostics-{report.GeneratedAt:yyyyMMdd-HHmmss}-{profile}.json";
    }

    private static string SuggestedUserPath(string fileName)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home)) return fileName;
        var downloads = Path.Combine(home, "Downloads");
        return Path.Combine(Directory.Exists(downloads) ? downloads : home, fileName);
    }

    private void DescribePlan(PhotinoWindow sender, BridgeRequest request)
    {
        var profile = BridgeProtocol.ParseProfile(request.Payload);
        var method = BridgeProtocol.ParseTransferMethod(request.Payload);
        var downloadPath = BridgeProtocol.ParseDownloadPath(request.Payload);
        var plan = DiagnosticRunPlanPresenter.Build(settingsStore.Load(), profile, method, downloadPath);
        SendResponse(sender, request.Id, true, plan);
    }

    private static NativeDiagnosticRunOptions BuildRunOptions(
        PhotinoAppSettings settings,
        TestProfileId profile,
        TransferMethod method,
        DownloadPathPreference downloadPath)
    {
        var origins = settings.TestOrigins.Count == 0
            ? new[] { new Uri("https://network.johnnyli.dev/") }
            : settings.TestOrigins.Select(value => new Uri(value)).ToArray();
        return new NativeDiagnosticRunOptions(
            Profile: profile,
            TransferMethod: method,
            IncludeAddresses: settings.IncludeLocalIdentifiers,
            TestOrigins: origins,
            InterfaceId: string.IsNullOrWhiteSpace(settings.InterfaceId) ? null : settings.InterfaceId,
            LanTarget: string.IsNullOrWhiteSpace(settings.LanTarget) ? null : settings.LanTarget,
            LanPort: settings.LanPort,
            LanDurationSeconds: settings.LanDurationSeconds,
            LanConnections: settings.LanConnections,
            ProducerApplication: "desktop-photino",
            ProducerVersion: ApplicationVersion,
            DownloadPath: downloadPath);
    }

    private async Task StartDiagnosticAsync(PhotinoWindow sender, BridgeRequest request)
    {
        CancellationTokenSource cancellation;
        Guid bridgeRunId;
        lock (runGate)
        {
            if (activeRun is not null)
            {
                SendResponse(sender, request.Id, false, null, "A diagnostic is already running.");
                return;
            }

            cancellation = new CancellationTokenSource();
            activeRun = cancellation;
            bridgeRunId = Guid.NewGuid();
            activeBridgeRunId = bridgeRunId;
        }

        var profile = BridgeProtocol.ParseProfile(request.Payload);
        var method = BridgeProtocol.ParseTransferMethod(request.Payload);
        var downloadPath = BridgeProtocol.ParseDownloadPath(request.Payload);
        var settings = settingsStore.Load();
        var plan = NetworkDiagnosticsRunner.DescribePlan(profile, method);
        var presentedPlan = DiagnosticRunPlanPresenter.Build(settings, profile, method, downloadPath);
        var runOptions = BuildRunOptions(settings, profile, method, downloadPath);
        var progressProjector = new NativeRunProgressProjector(
            plan,
            presentedPlan.DeepDiagnostics,
            presentedPlan.LanEnabled,
            presentedPlan.EstimatedSeconds);
        monitorService.SetDiagnosticActivity(true);

        SendResponse(sender, request.Id, true, new
        {
            runId = bridgeRunId,
            profile = BridgeProtocol.ProfileId(profile),
            method = BridgeProtocol.MethodId(method),
            downloadPath = BridgeProtocol.DownloadPathId(downloadPath),
            transferCapBytes = plan.TransferCapBytes,
            estimatedSeconds = presentedPlan.EstimatedSeconds,
            totalStages = presentedPlan.TotalTransferStages + (presentedPlan.DeepDiagnostics ? 4 : 3)
        });

        var progress = new Progress<NativeRunProgress>(item =>
        {
            var presented = progressProjector.Project(item);
            SendEvent(sender, "diagnostic.progress", new
            {
                runId = bridgeRunId,
                presented.Phase,
                presented.Stage,
                presented.StageLabel,
                presented.Message,
                fraction = presented.StageFraction,
                presented.OverallFraction,
                presented.StageIndex,
                presented.TotalStages,
                presented.ElapsedSeconds,
                presented.EstimatedSecondsRemaining,
                presented.LiveMbps,
                presented.LiveLatencyMs,
                bytesTransferred = presented.StageBytesTransferred,
                presented.TotalBytesTransferred
            });
        });

        try
        {
            var report = await NetworkDiagnosticsRunner.RunAsync(
                runOptions,
                progress,
                cancellation.Token);
            CacheCompletedReport(report);

            StoredReport? stored = null;
            string? storageError = null;
            try
            {
                stored = await reportStore.SaveAsync(report, CancellationToken.None);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                storageError = SafeMessage(error);
            }

            try
            {
                await monitorService.RecordDiagnosticAsync(report, CancellationToken.None);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
            {
                SendEvent(sender, "monitor.error", new { message = SafeMessage(error) });
            }

            var internet = report.InternetTransfer;
            var completedProgress = progressProjector.Complete();
            SendEvent(sender, "diagnostic.progress", new
            {
                runId = bridgeRunId,
                completedProgress.Phase,
                completedProgress.Stage,
                completedProgress.StageLabel,
                completedProgress.Message,
                fraction = completedProgress.StageFraction,
                completedProgress.OverallFraction,
                completedProgress.StageIndex,
                completedProgress.TotalStages,
                completedProgress.ElapsedSeconds,
                completedProgress.EstimatedSecondsRemaining,
                completedProgress.LiveMbps,
                completedProgress.LiveLatencyMs,
                bytesTransferred = completedProgress.StageBytesTransferred,
                completedProgress.TotalBytesTransferred
            });
            SendEvent(sender, "diagnostic.completed", new
            {
                runId = bridgeRunId,
                reportId = report.Run.Id,
                generatedAt = report.GeneratedAt,
                profile = BridgeProtocol.ProfileId(report.Run.Profile),
                method = BridgeProtocol.MethodId(method),
                downloadPath = BridgeProtocol.DownloadPathId(downloadPath),
                downloadDelivery = internet?.DownloadDelivery,
                latencyMs = internet?.IdleLatency.MedianMs,
                requestLossPercent = internet?.IdleLatency.LossPercent,
                downloadMbps = internet?.Download.SteadyMbps,
                uploadMbps = internet?.Upload.SteadyMbps,
                dataUsedBytes = internet?.DataUsedBytes,
                savedLocally = stored is not null,
                storageError,
                storedReport = stored is null ? null : ReportSummary(stored),
                detail = ReportDetailPayload(report, stored)
            });
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            SendEvent(sender, "diagnostic.cancelled", new { runId = bridgeRunId });
        }
        catch (Exception error)
        {
            SendEvent(sender, "diagnostic.failed", new
            {
                runId = bridgeRunId,
                message = SafeMessage(error),
                errorType = error.GetType().Name
            });
        }
        finally
        {
            monitorService.SetDiagnosticActivity(false);
            lock (runGate)
            {
                if (activeBridgeRunId == bridgeRunId)
                {
                    activeRun?.Dispose();
                    activeRun = null;
                    activeBridgeRunId = Guid.Empty;
                }
            }
        }
    }

    private void CancelDiagnostic(PhotinoWindow sender, string? requestId)
    {
        var cancelled = false;
        lock (runGate)
        {
            if (activeRun is not null)
            {
                activeRun.Cancel();
                cancelled = true;
            }
        }

        SendResponse(sender, requestId, true, new { cancelled });
    }

    private static void SendResponse(
        PhotinoWindow sender,
        string? id,
        bool ok,
        object? payload,
        string? error = null)
    {
        sender.SendWebMessage(JsonSerializer.Serialize(new
        {
            type = "response",
            id,
            ok,
            payload,
            error
        }, JsonOptions));
    }

    private static void SendEvent(PhotinoWindow sender, string eventName, object payload)
    {
        sender.SendWebMessage(JsonSerializer.Serialize(new
        {
            type = "event",
            @event = eventName,
            payload
        }, JsonOptions));
    }

    private static string SafeMessage(Exception error)
    {
        var message = error.Message.Trim();
        return string.IsNullOrWhiteSpace(message) ? "The desktop host returned an error." : message;
    }

    public sealed record BridgeRequest(string? Id, string Method, JsonElement Payload);
}
