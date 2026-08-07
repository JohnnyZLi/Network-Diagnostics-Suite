using System.Reflection;
using System.Text.Json;
using NetworkDeepProbe.Diagnostics;
using NetworkDeepProbe.Models;
using NetworkDeepProbe.Planning;
using NetworkDiagnostics.Desktop.Presentation;
using NetworkDiagnostics.Desktop.Services;
using Photino.NET;

namespace NetworkDiagnostics.Desktop;

public sealed class PhotinoDesktopBridge : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string? ApplicationVersion =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3);

    private readonly object runGate = new();
    private readonly PhotinoSettingsStore settingsStore;
    private readonly ReportStore reportStore;
    private PhotinoWindow? window;
    private CancellationTokenSource? activeRun;
    private Guid activeBridgeRunId;
    private bool disposed;

    public PhotinoDesktopBridge(
        PhotinoSettingsStore? settingsStore = null,
        ReportStore? reportStore = null)
    {
        this.settingsStore = settingsStore ?? new PhotinoSettingsStore();
        this.reportStore = reportStore ?? new ReportStore(this.settingsStore.RootDirectory);
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
        lock (runGate)
        {
            activeRun?.Cancel();
            activeRun?.Dispose();
            activeRun = null;
        }
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
                    SendResponse(sender, request.Id, true, new
                    {
                        product = "Network Diagnostics",
                        host = "photino",
                        version = ApplicationVersion,
                        platform = Environment.OSVersion.Platform.ToString(),
                        architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
                        appearance = BridgeProtocol.AppearanceId(settings.Appearance),
                        capabilities = new[]
                        {
                            "diagnostic.run",
                            "diagnostic.cancel",
                            "diagnostic.describePlan",
                            "reports.list",
                            "reports.get",
                            "reports.compare",
                            "settings.get",
                            "settings.setAppearance"
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

                case "reports.list":
                    await SendReportListAsync(sender, request.Id);
                    break;

                case "reports.get":
                    await SendReportDetailAsync(sender, request);
                    break;

                case "reports.compare":
                    await SendReportComparisonAsync(sender, request);
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

    private static void SendSettings(PhotinoWindow sender, string? requestId, PhotinoAppSettings settings)
    {
        SendResponse(sender, requestId, true, new
        {
            appearance = BridgeProtocol.AppearanceId(settings.Appearance)
        });
    }

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
        var presentation = DiagnosticReportPresenter.FromReport(stored.Report);

        SendResponse(sender, request.Id, true, new
        {
            report = ReportSummary(stored),
            context = ReportComparisonService.ContextLabel(stored.Report),
            method = BridgeProtocol.MethodId(stored.Report.Run.TransferMethod),
            presentation = new
            {
                outcome = presentation.Outcome.ToString().ToLowerInvariant(),
                presentation.Label,
                presentation.Verdict,
                presentation.Summary,
                presentation.NextAction,
                presentation.Metrics,
                presentation.Findings,
                presentation.TechnicalEvidence
            }
        });
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

    private static StoredReport FindReport(IReadOnlyList<StoredReport> reports, Guid reportId)
    {
        var stored = reports.FirstOrDefault(item => item.Report.Run.Id == reportId);
        return stored ?? throw new KeyNotFoundException($"Saved report '{reportId}' was not found.");
    }

    private static object ReportSummary(StoredReport stored)
    {
        var internet = stored.Report.InternetTransfer;
        return new
        {
            id = stored.Report.Run.Id,
            generatedAt = stored.Report.GeneratedAt,
            storedAt = stored.StoredAt,
            profile = BridgeProtocol.ProfileId(stored.Report.Run.Profile),
            profileName = stored.ProfileName,
            label = stored.Label,
            tags = stored.Tags,
            latencyMs = internet?.IdleLatency.MedianMs,
            requestLossPercent = internet?.IdleLatency.LossPercent,
            downloadMbps = internet?.Download.SteadyMbps,
            uploadMbps = internet?.Upload.SteadyMbps,
            dataUsedBytes = internet?.DataUsedBytes
        };
    }

    private static void DescribePlan(PhotinoWindow sender, BridgeRequest request)
    {
        var profile = BridgeProtocol.ParseProfile(request.Payload);
        var method = BridgeProtocol.ParseTransferMethod(request.Payload);
        var plan = NetworkDiagnosticsRunner.DescribePlan(profile, method);

        SendResponse(sender, request.Id, true, new
        {
            profile = BridgeProtocol.ProfileId(profile),
            profileName = plan.ProfileName,
            method = BridgeProtocol.MethodId(method),
            transferCapBytes = plan.TransferCapBytes,
            downloadStages = plan.DownloadStages.Count,
            uploadStages = plan.UploadStages.Count
        });
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
        var plan = NetworkDiagnosticsRunner.DescribePlan(profile, method);

        SendResponse(sender, request.Id, true, new
        {
            runId = bridgeRunId,
            profile = BridgeProtocol.ProfileId(profile),
            method = BridgeProtocol.MethodId(method),
            transferCapBytes = plan.TransferCapBytes
        });

        var progress = new Progress<NativeRunProgress>(item =>
        {
            SendEvent(sender, "diagnostic.progress", new
            {
                runId = bridgeRunId,
                phase = item.Phase,
                message = item.Message,
                fraction = item.Fraction,
                liveMbps = item.LiveMbps,
                liveLatencyMs = item.LiveLatencyMs,
                bytesTransferred = item.BytesTransferred
            });
        });

        try
        {
            var report = await NetworkDiagnosticsRunner.RunAsync(
                new NativeDiagnosticRunOptions(
                    Profile: profile,
                    TransferMethod: method,
                    IncludeAddresses: false,
                    TestOrigins: new[] { new Uri("https://network.johnnyli.dev/") },
                    InterfaceId: null,
                    LanTarget: null,
                    LanPort: 8765,
                    LanDurationSeconds: 8,
                    LanConnections: 4,
                    ProducerApplication: "desktop-photino",
                    ProducerVersion: ApplicationVersion),
                progress,
                cancellation.Token);

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

            var internet = report.InternetTransfer;
            SendEvent(sender, "diagnostic.completed", new
            {
                runId = bridgeRunId,
                reportId = report.Run.Id,
                generatedAt = report.GeneratedAt,
                profile = BridgeProtocol.ProfileId(report.Run.Profile),
                method = BridgeProtocol.MethodId(method),
                latencyMs = internet?.IdleLatency.MedianMs,
                requestLossPercent = internet?.IdleLatency.LossPercent,
                downloadMbps = internet?.Download.SteadyMbps,
                uploadMbps = internet?.Upload.SteadyMbps,
                dataUsedBytes = internet?.DataUsedBytes,
                savedLocally = stored is not null,
                storageError,
                storedReport = stored is null ? null : ReportSummary(stored),
                report
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
