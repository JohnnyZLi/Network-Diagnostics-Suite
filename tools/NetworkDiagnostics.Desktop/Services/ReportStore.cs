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
    public string? Label => Report.Annotations?.Label;
    public IReadOnlyList<string> Tags => Report.Annotations?.Tags ?? [];
}

public sealed class ReportStore
{
    private const int MaximumLabelLength = 80;
    private const int MaximumTagLength = 32;
    private const int MaximumTags = 10;
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
        RemoveAbandonedTemporaryFiles();
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
        await WriteAtomicAsync(path, report, cancellationToken);
        return new StoredReport(path, report, DateTimeOffset.UtcNow);
    }

    public async Task<IReadOnlyList<StoredReport>> ListAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(ReportsDirectory);
        RemoveAbandonedTemporaryFiles();
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

    public async Task<StoredReport> UpdateAnnotationsAsync(
        StoredReport stored,
        string? label,
        IEnumerable<string> tags,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stored);
        ArgumentNullException.ThrowIfNull(tags);
        var normalizedLabel = NormalizeLabel(label);
        var normalizedTags = NormalizeTags(tags);
        var updatedReport = stored.Report with
        {
            Annotations = normalizedLabel is null && normalizedTags.Count == 0
                ? null
                : new ReportAnnotations(normalizedLabel, normalizedTags)
        };
        await WriteAtomicAsync(stored.Path, updatedReport, cancellationToken);
        return new StoredReport(stored.Path, updatedReport, DateTimeOffset.UtcNow);
    }

    public Task ExportAsync(
        NetworkDiagnosticsReportV2 report,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        return WriteAtomicAsync(destinationPath, report, cancellationToken);
    }

    public bool Delete(StoredReport stored)
    {
        ArgumentNullException.ThrowIfNull(stored);
        var reportPath = Path.GetFullPath(stored.Path);
        var reportDirectory = Path.GetFullPath(ReportsDirectory);
        if (!string.Equals(Path.GetDirectoryName(reportPath), reportDirectory, PathComparison))
        {
            throw new InvalidOperationException("The report is outside the configured reports directory.");
        }
        if (!File.Exists(reportPath)) return false;
        File.Delete(reportPath);
        return true;
    }

    public int Prune(int retentionDays)
    {
        if (retentionDays is < 0 or > 3650) throw new ArgumentOutOfRangeException(nameof(retentionDays));
        if (retentionDays == 0 || !Directory.Exists(ReportsDirectory)) return 0;

        var cutoff = DateTimeOffset.UtcNow.AddDays(-retentionDays);
        var removed = 0;
        foreach (var path in Directory.EnumerateFiles(ReportsDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(path) >= cutoff.UtcDateTime) continue;
                File.Delete(path);
                removed++;
            }
            catch (IOException)
            {
                // Keep reports that are in use and try again during the next maintenance pass.
            }
            catch (UnauthorizedAccessException)
            {
                // Keep reports that cannot be removed with the current permissions.
            }
        }
        return removed;
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

    private static async Task WriteAtomicAsync(
        string destinationPath,
        NetworkDiagnosticsReportV2 report,
        CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(destinationPath);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("The report destination does not have a parent directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await NetworkDiagnosticsJson.WriteAsync(temporaryPath, report, cancellationToken);
            File.Move(temporaryPath, fullPath, true);
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
            catch (IOException)
            {
                // A failed cleanup must not hide the original save result.
            }
            catch (UnauthorizedAccessException)
            {
                // A failed cleanup must not hide the original save result.
            }
        }
    }

    private void RemoveAbandonedTemporaryFiles()
    {
        if (!Directory.Exists(ReportsDirectory)) return;
        foreach (var path in Directory.EnumerateFiles(ReportsDirectory, ".*.tmp", SearchOption.TopDirectoryOnly))
        {
            try
            {
                if (DateTime.UtcNow - File.GetLastWriteTimeUtc(path) > TimeSpan.FromHours(1)) File.Delete(path);
            }
            catch (IOException)
            {
                // Another process may still own the file.
            }
            catch (UnauthorizedAccessException)
            {
                // Keep the file for manual inspection when deletion is not permitted.
            }
        }
    }

    private static string? NormalizeLabel(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized is null) return null;
        return normalized.Length <= MaximumLabelLength ? normalized : normalized[..MaximumLabelLength];
    }

    private static IReadOnlyList<string> NormalizeTags(IEnumerable<string> values) => values
        .Select(value => value.Trim())
        .Where(value => value.Length > 0)
        .Select(value => value.Length <= MaximumTagLength ? value : value[..MaximumTagLength])
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Take(MaximumTags)
        .ToArray();

    private static string ProfileFileName(TestProfileId profile) => profile switch
    {
        TestProfileId.ConnectionCheck => "connection-check",
        TestProfileId.Quick => "quick",
        TestProfileId.Standard => "full",
        TestProfileId.Extended => "stress",
        _ => "diagnostic"
    };

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
