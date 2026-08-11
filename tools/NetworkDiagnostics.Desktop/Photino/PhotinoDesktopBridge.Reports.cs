using NetworkDeepProbe.Diagnostics;
using NetworkDeepProbe.Models;
using Photino.NET;

namespace NetworkDiagnostics.Desktop;

public sealed partial class PhotinoDesktopBridge
{
    private void CacheCompletedReport(NetworkDiagnosticsReportV2 report)
    {
        lock (completedReportGate)
        {
            completedReports[report.Run.Id] = report;
            foreach (var staleId in completedReports.Values
                         .OrderByDescending(item => item.GeneratedAt)
                         .Skip(4)
                         .Select(item => item.Run.Id)
                         .ToArray())
            {
                completedReports.Remove(staleId);
            }
        }
    }

    private bool TryGetCompletedReport(Guid reportId, out NetworkDiagnosticsReportV2? report)
    {
        lock (completedReportGate)
        {
            return completedReports.TryGetValue(reportId, out report);
        }
    }

    private async Task SaveCurrentReportAsync(PhotinoWindow sender, BridgeRequest request)
    {
        var reportId = BridgeProtocol.ParseRequiredGuid(request.Payload, "id");
        var reports = await reportStore.ListAsync();
        var existing = reports.FirstOrDefault(item => item.Report.Run.Id == reportId);
        if (existing is not null)
        {
            SendResponse(sender, request.Id, true, new
            {
                saved = true,
                alreadySaved = true,
                detail = ReportDetailPayload(existing)
            });
            return;
        }

        if (!TryGetCompletedReport(reportId, out var report) || report is null)
        {
            throw new KeyNotFoundException("The completed report is no longer available in memory. Export it before closing the application or run the diagnostic again.");
        }

        var stored = await reportStore.SaveAsync(report, CancellationToken.None);
        SendResponse(sender, request.Id, true, new
        {
            saved = true,
            alreadySaved = false,
            detail = ReportDetailPayload(stored)
        });
    }

    private async Task ExportCurrentReportAsync(PhotinoWindow sender, BridgeRequest request)
    {
        var reportId = BridgeProtocol.ParseRequiredGuid(request.Payload, "id");
        NetworkDiagnosticsReportV2? report = null;
        if (TryGetCompletedReport(reportId, out var current)) report = current;
        if (report is null)
        {
            var stored = (await reportStore.ListAsync()).FirstOrDefault(item => item.Report.Run.Id == reportId);
            report = stored?.Report;
        }
        if (report is null) throw new KeyNotFoundException($"Report '{reportId}' was not found.");

        var destinationPath = sender.ShowSaveFile(
            title: "Export Network Diagnostics report",
            defaultPath: Path.Combine(reportStore.ReportsDirectory, SuggestedExportName(report)),
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
        await reportStore.ExportAsync(report, destinationPath, CancellationToken.None);
        SendResponse(sender, request.Id, true, new
        {
            cancelled = false,
            fileName = Path.GetFileName(destinationPath)
        });
    }

    private async Task DeleteReportAsync(PhotinoWindow sender, BridgeRequest request)
    {
        var reportId = BridgeProtocol.ParseRequiredGuid(request.Payload, "id");
        var reports = await reportStore.ListAsync();
        var stored = FindReport(reports, reportId);
        var deleted = reportStore.Delete(stored);
        SendResponse(sender, request.Id, true, new
        {
            deleted,
            id = reportId,
            recoverableFromCurrentSession = TryGetCompletedReport(reportId, out _)
        });
    }
}
