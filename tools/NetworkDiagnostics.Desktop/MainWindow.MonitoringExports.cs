using System.Text;
using Avalonia.Platform.Storage;
using NetworkDiagnostics.Desktop.Monitoring;

namespace NetworkDiagnostics.Desktop;

public sealed partial class MainWindow
{
    private static readonly FilePickerFileType HtmlSnapshotFileType = new("Network health snapshot")
    {
        Patterns = ["*.html"],
        MimeTypes = ["text/html"]
    };

    private static readonly FilePickerFileType CsvHistoryFileType = new("Network monitoring history")
    {
        Patterns = ["*.csv"],
        MimeTypes = ["text/csv"]
    };

    private async Task CopyMonitoringSummaryAsync()
    {
        var presentation = CurrentNetworkExperience();
        var summary = new StringBuilder()
            .Append("Network score: ").Append(presentation.Score?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "Not enough data")
            .Append(" · ").AppendLine(presentation.Status)
            .AppendLine(presentation.Summary)
            .Append("Responsiveness: ").Append(presentation.Responsiveness.Score?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "—")
            .Append(" · Reliability: ").Append(presentation.Reliability.Score?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "—")
            .Append(" · Speed: ").AppendLine(presentation.Speed.Score?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "—")
            .Append("Window: ").Append(presentation.Window.ContractId())
            .Append(" · Updated: ").Append(presentation.LastUpdated)
            .ToString();

        if (Clipboard is not null)
        {
            await Clipboard.SetTextAsync(summary);
            settingsStatus = "Network summary copied to the clipboard.";
        }
    }

    private async Task ExportMonitoringSnapshotAsync()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Share network health snapshot",
            SuggestedFileName = $"network-health-{DateTime.Now:yyyyMMdd-HHmm}.html",
            FileTypeChoices = [HtmlSnapshotFileType],
            DefaultExtension = "html",
            ShowOverwritePrompt = true
        });
        if (file is null) return;

        var html = MonitoringExportService.BuildShareHtml(CurrentNetworkExperience());
        await using var stream = await file.OpenWriteAsync();
        stream.SetLength(0);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        await writer.WriteAsync(html);
        settingsStatus = "Shareable network snapshot exported.";
    }

    private async Task ExportMonitoringHistoryAsync()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export monitoring history",
            SuggestedFileName = $"network-history-{monitorWindow.ContractId()}-{DateTime.Now:yyyyMMdd-HHmm}.csv",
            FileTypeChoices = [CsvHistoryFileType],
            DefaultExtension = "csv",
            ShowOverwritePrompt = true
        });
        if (file is null) return;

        var csv = MonitoringExportService.BuildHistoryCsv(
            monitoringService.Snapshot,
            monitorWindow,
            settings.IncludeLocalIdentifiers);
        await using var stream = await file.OpenWriteAsync();
        stream.SetLength(0);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        await writer.WriteAsync(csv);
        settingsStatus = settings.IncludeLocalIdentifiers
            ? "Monitoring history exported with enabled local identifiers."
            : "Monitoring history exported with local identifiers redacted.";
    }
}
