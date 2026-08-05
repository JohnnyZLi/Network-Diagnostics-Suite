namespace NetworkDiagnostics.Desktop.Presentation;

public enum HealthGroupKind
{
    Responsiveness,
    Reliability,
    Throughput
}

public enum HealthGroupTone
{
    Positive,
    Attention,
    Neutral
}

public sealed record HealthGroupPresentation(
    HealthGroupKind Kind,
    string Title,
    string State,
    string Summary,
    string Detail,
    IReadOnlyList<MetricPresentation> Metrics,
    HealthGroupTone Tone);

public static class HealthGroupPresenter
{
    public static IReadOnlyList<HealthGroupPresentation> Build(ConnectionCheckPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);

        return
        [
            BuildGroup(presentation, HealthGroupKind.Responsiveness, "Responsiveness"),
            BuildGroup(presentation, HealthGroupKind.Reliability, "Reliability"),
            BuildGroup(presentation, HealthGroupKind.Throughput, "Throughput")
        ];
    }

    private static HealthGroupPresentation BuildGroup(
        ConnectionCheckPresentation presentation,
        HealthGroupKind kind,
        string title)
    {
        var metrics = presentation.Metrics
            .Where(metric => MetricGroup(metric) == kind)
            .ToArray();
        var finding = presentation.Findings.FirstOrDefault(item => FindingMatches(item, kind));
        var hasMeasuredMetric = metrics.Any(metric => metric.WasMeasured);
        var (state, tone) = State(presentation.Outcome, finding is not null, hasMeasuredMetric);

        var summary = finding?.Title
            ?? MetricSummary(metrics)
            ?? DefaultSummary(kind);
        var detail = finding?.Summary
            ?? MetricDetail(metrics)
            ?? DefaultDetail(kind);

        return new HealthGroupPresentation(
            kind,
            title,
            state,
            summary,
            detail,
            metrics,
            tone);
    }

    private static HealthGroupKind MetricGroup(MetricPresentation metric)
    {
        var text = $"{metric.Label} {metric.Detail}".ToLowerInvariant();
        if (ContainsAny(text, "download", "upload", "throughput", "bandwidth", "capacity", "speed", "transfer rate"))
        {
            return HealthGroupKind.Throughput;
        }
        if (ContainsAny(text, "loss", "reliab", "reach", "status", "wi-fi", "wifi", "signal", "availability", "stable", "error", "discard", "retrans"))
        {
            return HealthGroupKind.Reliability;
        }
        if (ContainsAny(text, "latency", "jitter", "delay", "dns", "gateway", "internet", "response"))
        {
            return HealthGroupKind.Responsiveness;
        }
        return HealthGroupKind.Reliability;
    }

    private static bool FindingMatches(FindingPresentation finding, HealthGroupKind kind)
    {
        var text = $"{finding.Label} {finding.Title} {finding.Summary}".ToLowerInvariant();
        return kind switch
        {
            HealthGroupKind.Responsiveness => ContainsAny(text, "respons", "latency", "jitter", "delay", "dns"),
            HealthGroupKind.Reliability => ContainsAny(text, "reliab", "loss", "packet", "reach", "stability", "disconnect", "availability", "failure", "gateway", "wi-fi", "wifi", "signal"),
            HealthGroupKind.Throughput => ContainsAny(text, "throughput", "download", "upload", "speed", "bandwidth", "capacity", "scaling"),
            _ => false
        };
    }

    private static (string State, HealthGroupTone Tone) State(
        ConnectionCheckOutcome outcome,
        bool hasFinding,
        bool hasMeasuredMetric)
    {
        if (hasFinding)
        {
            return ("Needs attention", HealthGroupTone.Attention);
        }
        if (!hasMeasuredMetric)
        {
            return ("Not measured", HealthGroupTone.Neutral);
        }

        return outcome switch
        {
            ConnectionCheckOutcome.Healthy => ("Looks normal", HealthGroupTone.Positive),
            ConnectionCheckOutcome.Inconclusive => ("Mixed evidence", HealthGroupTone.Neutral),
            ConnectionCheckOutcome.Unavailable or ConnectionCheckOutcome.Failed => ("Partial evidence", HealthGroupTone.Neutral),
            _ => ("Measured", HealthGroupTone.Neutral)
        };
    }

    private static string? MetricSummary(IReadOnlyList<MetricPresentation> metrics)
    {
        var measured = metrics.Where(metric => metric.WasMeasured).Take(2).ToArray();
        if (measured.Length == 0) return null;
        return string.Join(" · ", measured.Select(metric => $"{metric.Label} {metric.Value}"));
    }

    private static string? MetricDetail(IReadOnlyList<MetricPresentation> metrics)
    {
        var detail = metrics
            .Select(metric => metric.Detail.Trim())
            .FirstOrDefault(value => value.Length > 0);
        return string.IsNullOrWhiteSpace(detail) ? null : detail;
    }

    private static string DefaultSummary(HealthGroupKind kind) => kind switch
    {
        HealthGroupKind.Responsiveness => "No supported response-time measurement",
        HealthGroupKind.Reliability => "No supported reliability measurement",
        _ => "No supported transfer measurement"
    };

    private static string DefaultDetail(HealthGroupKind kind) => kind switch
    {
        HealthGroupKind.Responsiveness => "Latency, jitter, and response-time evidence appear here when the selected profile collects them.",
        HealthGroupKind.Reliability => "Loss, reachability, stability, and interface evidence appear here when available.",
        _ => "Download, upload, capacity, and scaling evidence appear here when the selected profile includes transfers."
    };

    private static bool ContainsAny(string text, params string[] values) =>
        values.Any(value => text.Contains(value, StringComparison.Ordinal));
}
