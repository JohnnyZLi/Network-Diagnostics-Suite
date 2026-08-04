using System.Globalization;
using NetworkDeepProbe.Models;

namespace NetworkDiagnostics.Desktop.Services;

public sealed record ReportMetricDelta(
    string Id,
    string Label,
    string Baseline,
    string Candidate,
    string Change,
    double? NumericChange);

public sealed record ReportComparisonResult(
    bool Comparable,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<ReportMetricDelta> Metrics,
    string Summary);

public sealed record ReportTrendResult(
    int CompatibleRuns,
    string Summary,
    IReadOnlyList<string> Details);

public static class ReportComparisonService
{
    public static ReportComparisonResult Compare(
        NetworkDiagnosticsReportV2 baseline,
        NetworkDiagnosticsReportV2 candidate)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(candidate);
        var warnings = CompatibilityWarnings(baseline, candidate);
        var metrics = new List<ReportMetricDelta>();
        AddMetric(metrics, "download", "Download", baseline.InternetTransfer?.Download.SteadyMbps, candidate.InternetTransfer?.Download.SteadyMbps, "Mbps");
        AddMetric(metrics, "upload", "Upload", baseline.InternetTransfer?.Upload.SteadyMbps, candidate.InternetTransfer?.Upload.SteadyMbps, "Mbps");
        AddMetric(metrics, "idle-latency", "Idle latency", baseline.InternetTransfer?.IdleLatency.MedianMs, candidate.InternetTransfer?.IdleLatency.MedianMs, "ms", lowerIsBetter: true);
        AddMetric(metrics, "request-loss", "Request loss", baseline.InternetTransfer?.IdleLatency.LossPercent, candidate.InternetTransfer?.IdleLatency.LossPercent, "%", lowerIsBetter: true);
        AddMetric(metrics, "download-loaded", "Download added latency", baseline.InternetTransfer?.DownloadLatency.IncreaseMs, candidate.InternetTransfer?.DownloadLatency.IncreaseMs, "ms", lowerIsBetter: true);
        AddMetric(metrics, "upload-loaded", "Upload added latency", baseline.InternetTransfer?.UploadLatency.IncreaseMs, candidate.InternetTransfer?.UploadLatency.IncreaseMs, "ms", lowerIsBetter: true);
        AddMetric(metrics, "gateway", "Gateway latency", baseline.DeepDiagnostics?.GatewayPing?.Statistics.MedianMs, candidate.DeepDiagnostics?.GatewayPing?.Statistics.MedianMs, "ms", lowerIsBetter: true);
        AddMetric(metrics, "dns", "Fastest DNS", FastestDns(baseline), FastestDns(candidate), "ms", lowerIsBetter: true);
        AddMetric(metrics, "wifi", "Wi-Fi signal", baseline.DeepDiagnostics?.Wifi?.SignalPercent, candidate.DeepDiagnostics?.Wifi?.SignalPercent, "%");
        AddMetric(metrics, "process-cpu", "Diagnostic process CPU", baseline.HostResources?.ProcessCpuPercent, candidate.HostResources?.ProcessCpuPercent, "%", lowerIsBetter: true);

        var significant = metrics
            .Where(item => item.NumericChange is { } change && Math.Abs(change) >= SignificantThreshold(item.Id))
            .ToArray();
        var summary = significant.Length == 0
            ? "The compatible headline measurements stayed within the project comparison thresholds."
            : string.Join(" ", significant.Take(3).Select(item => $"{item.Label}: {item.Change}."));
        return new ReportComparisonResult(warnings.Count == 0, warnings, metrics, summary);
    }

    public static ReportTrendResult AnalyzeTrend(IReadOnlyList<StoredReport> reports)
    {
        ArgumentNullException.ThrowIfNull(reports);
        var latest = reports.FirstOrDefault();
        if (latest is null)
        {
            return new ReportTrendResult(0, "No reports are available for trend analysis.", []);
        }
        var compatible = reports
            .Where(item => IsCompatible(latest.Report, item.Report))
            .Take(10)
            .OrderBy(item => item.Report.GeneratedAt)
            .ToArray();
        if (compatible.Length < 2)
        {
            return new ReportTrendResult(
                compatible.Length,
                "Save another report with the same profile, method, endpoint, and interface to establish a trend.",
                []);
        }

        var first = compatible[0].Report;
        var last = compatible[^1].Report;
        var comparison = Compare(first, last);
        var details = comparison.Metrics
            .Where(item => item.NumericChange is not null)
            .Select(item => $"{item.Label}: {item.Baseline} → {item.Candidate} ({item.Change})")
            .ToArray();
        return new ReportTrendResult(
            compatible.Length,
            $"{compatible.Length} compatible runs from {first.GeneratedAt.ToLocalTime():MMM d} to {last.GeneratedAt.ToLocalTime():MMM d}. {comparison.Summary}",
            details);
    }

    public static bool IsCompatible(NetworkDiagnosticsReportV2 left, NetworkDiagnosticsReportV2 right) =>
        left.Run.Profile == right.Run.Profile
        && left.Run.TransferMethod == right.Run.TransferMethod
        && string.Equals(Endpoint(left), Endpoint(right), StringComparison.OrdinalIgnoreCase)
        && string.Equals(Interface(left), Interface(right), StringComparison.Ordinal);

    public static string ContextLabel(NetworkDiagnosticsReportV2 report)
    {
        var profile = report.TransferPlan.ProfileName;
        var method = report.Run.TransferMethod.ToString();
        var network = report.Measurement?.Network?.Network;
        var networkInterface = report.Measurement?.SelectedInterface?.Name;
        var parts = new[] { profile, method, networkInterface, network }
            .Where(item => !string.IsNullOrWhiteSpace(item));
        return string.Join(" · ", parts);
    }

    private static List<string> CompatibilityWarnings(NetworkDiagnosticsReportV2 baseline, NetworkDiagnosticsReportV2 candidate)
    {
        var warnings = new List<string>();
        if (baseline.Run.Profile != candidate.Run.Profile) warnings.Add("Profiles differ.");
        if (baseline.Run.TransferMethod != candidate.Run.TransferMethod) warnings.Add("Transfer methods differ.");
        if (!string.Equals(Endpoint(baseline), Endpoint(candidate), StringComparison.OrdinalIgnoreCase)) warnings.Add("Selected endpoints differ.");
        if (!string.Equals(Interface(baseline), Interface(candidate), StringComparison.Ordinal)) warnings.Add("Selected interfaces differ.");
        if (baseline.TransferPlan.TransferCapBytes != candidate.TransferPlan.TransferCapBytes) warnings.Add("Transfer ceilings differ.");
        return warnings;
    }

    private static void AddMetric(
        ICollection<ReportMetricDelta> destination,
        string id,
        string label,
        double? baseline,
        double? candidate,
        string unit,
        bool lowerIsBetter = false)
    {
        if (baseline is null && candidate is null) return;
        var baselineText = Format(baseline, unit);
        var candidateText = Format(candidate, unit);
        if (baseline is null || candidate is null)
        {
            destination.Add(new ReportMetricDelta(id, label, baselineText, candidateText, "not comparable", null));
            return;
        }
        var change = candidate.Value - baseline.Value;
        var direction = change == 0
            ? "unchanged"
            : lowerIsBetter
                ? change < 0 ? "improved" : "worsened"
                : change > 0 ? "increased" : "decreased";
        destination.Add(new ReportMetricDelta(
            id,
            label,
            baselineText,
            candidateText,
            $"{direction} by {Math.Abs(change).ToString("0.0", CultureInfo.InvariantCulture)} {unit}",
            change));
    }

    private static double? FastestDns(NetworkDiagnosticsReportV2 report) => report.DeepDiagnostics?.DnsResolvers
        .Where(item => item.MedianMs is not null)
        .Select(item => item.MedianMs)
        .Min();

    private static string Endpoint(NetworkDiagnosticsReportV2 report) =>
        report.Measurement?.SelectedEndpoint.Origin ?? report.InternetTransfer?.Origin ?? string.Empty;

    private static string Interface(NetworkDiagnosticsReportV2 report) =>
        report.Measurement?.SelectedInterface?.Id ?? "automatic";

    private static double SignificantThreshold(string id) => id switch
    {
        "download" or "upload" => 10,
        "idle-latency" or "gateway" or "dns" => 5,
        "download-loaded" or "upload-loaded" => 15,
        "request-loss" => 1,
        "wifi" => 10,
        "process-cpu" => 20,
        _ => double.MaxValue
    };

    private static string Format(double? value, string unit) => value is null
        ? "Not measured"
        : $"{value.Value.ToString(value.Value >= 100 ? "0" : "0.0", CultureInfo.InvariantCulture)} {unit}";
}
