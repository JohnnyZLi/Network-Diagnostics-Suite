using System.Diagnostics;
using NetworkDeepProbe.Diagnostics;
using NetworkDeepProbe.Models;
using NetworkDeepProbe.Planning;

namespace NetworkDiagnostics.Desktop.Services;

public sealed record StoredReport(
    string Path,
    NetworkDiagnosticsReportV2 Report,
    DateTimeOffset StoredAt)
{
    public string ProfileName => Report.Run.Profile switch
    {
        TestProfileId.ConnectionCheck => "Connection Check",
        TestProfileId.Quick => "Quick",
        TestProfileId.Standard => "Full",
        TestProfileId.Extended => "Stress",
        _ => "Diagnostic"
    };

    public string DisplayDate => Report.GeneratedAt.ToLocalTime().ToString("MMM d, yyyy · h:mm tt");
}

public sealed class ReportStore
{
    private readonly string defaultDirectory;

    public ReportStore(string settingsRootDirectory, string? configuredDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsRootDirectory);
        defaultDirectory = Path.Combine(settingsRootDirectory, "reports");
        Configure(configuredDirectory);
    }

    public string ReportsDirectory { get; private set; } = string.Empty;

    public void Configure(string? configuredDirectory)
    {
        ReportsDirectory = string.IsNullOrWhiteSpace(configuredDirectory)
            ? defaultDirectory
            : Path.GetFullPath(configuredDirectory);
        Directory.CreateDirectory(ReportsDirectory);
    }

    public async Task<StoredReport> SaveAsync(
        NetworkDiagnosticsReportV2 report,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        Directory.CreateDirectory(ReportsDirectory);
        var profile = ProfileFileName(report.Run.Profile);
        var fileName = $"{report.GeneratedAt:yyyyMMdd-HHmmss}-{profile}-{report.Run.Id:N}.json";
        var path = Path.Combine(ReportsDirectory, fileName);
        await NetworkDiagnosticsJson.WriteAsync(path, report, cancellationToken);
        return new StoredReport(path, report, DateTimeOffset.UtcNow);
    }

    public async Task<IReadOnlyList<StoredReport>> ListAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(ReportsDirectory);
        var reports = new List<StoredReport>();
        foreach (var path in Directory.EnumerateFiles(ReportsDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var report = await NetworkDiagnosticsJson.ReadAsync(path, cancellationToken);
                reports.Add(new StoredReport(
                    path,
                    report,
                    File.GetLastWriteTimeUtc(path)));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
            {
                // Leave unreadable files untouched so the user can inspect or recover them manually.
            }
        }
        return reports
            .OrderByDescending(item => item.Report.GeneratedAt)
            .ToArray();
    }

    public async Task<StoredReport> ImportAsync(
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        var report = await NetworkDiagnosticsJson.ReadAsync(sourcePath, cancellationToken);
        return await SaveAsync(report, cancellationToken);
    }

    public Task ExportAsync(
        NetworkDiagnosticsReportV2 report,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        return NetworkDiagnosticsJson.WriteAsync(destinationPath, report, cancellationToken);
    }

    public void OpenReportsFolder()
    {
        Directory.CreateDirectory(ReportsDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = ReportsDirectory,
            UseShellExecute = true
        });
    }

    private static string ProfileFileName(TestProfileId profile) => profile switch
    {
        TestProfileId.ConnectionCheck => "connection-check",
        TestProfileId.Quick => "quick",
        TestProfileId.Standard => "full",
        TestProfileId.Extended => "stress",
        _ => "diagnostic"
    };
}
