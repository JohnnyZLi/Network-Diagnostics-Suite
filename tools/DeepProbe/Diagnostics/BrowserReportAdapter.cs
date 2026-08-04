using System.Text.Json;
using NetworkDeepProbe.Models;
using NetworkDeepProbe.Planning;

namespace NetworkDeepProbe.Diagnostics;

internal static class BrowserReportAdapter
{
    public static bool Matches(JsonElement root)
    {
        return root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("id", out _)
            && root.TryGetProperty("startedAt", out _)
            && root.TryGetProperty("completedAt", out _)
            && root.TryGetProperty("mode", out _)
            && root.TryGetProperty("idleLatency", out _)
            && root.TryGetProperty("download", out _)
            && root.TryGetProperty("upload", out _);
    }

    public static NetworkDiagnosticsReportV2 Deserialize(string json, JsonSerializerOptions options)
    {
        var browser = JsonSerializer.Deserialize<BrowserDiagnosticResultDto>(json, options)
            ?? throw new InvalidDataException("The browser report could not be parsed.");
        browser.Validate();

        var profile = NativeTransferPlanBuilder.ParseProfile(browser.Mode!);
        var method = NativeTransferPlanBuilder.ParseMethod(browser.TransferMode ?? "compare");
        var plan = NativeTransferPlanBuilder.Build(profile, method);
        var idle = ToLatency(browser.IdleLatency!);
        var download = ToThroughput(browser.Download!);
        var upload = ToThroughput(browser.Upload!);
        var downloadLatency = ToLoadedLatency(browser.DownloadLatency!);
        var uploadLatency = ToLoadedLatency(browser.UploadLatency!);
        var flows = browser.FlowMeasurements is { Count: > 0 }
            ? browser.FlowMeasurements.Select(ToFlowMeasurement).ToArray()
            :
            [
                new NativeFlowMeasurement(
                    method == TransferMethod.Single ? TransferStrategy.Single : TransferStrategy.Aggregate,
                    PrimaryConnections(plan, method),
                    download,
                    upload,
                    downloadLatency,
                    uploadLatency)
            ];
        var scaling = browser.DownloadScaling?
            .Select(point => new NativeFlowScalingPoint(
                Math.Max(1, point.Concurrency),
                ToThroughput(point.Download ?? throw new InvalidDataException("A browser scaling point is missing its download result.")),
                ToLoadedLatency(point.DownloadLatency ?? throw new InvalidDataException("A browser scaling point is missing loaded latency."))))
            .ToArray() ?? [];
        var internet = new NativeInternetTransferReport(
            InternetTransferProbe.DefaultOrigin.ToString(),
            idle,
            download,
            upload,
            downloadLatency,
            uploadLatency,
            flows,
            scaling,
            Math.Max(0, browser.DataUsedBytes));
        var evidence = new BrowserReportEvidence(
            browser.Edge is null
                ? null
                : new BrowserEdgeEvidence(
                    browser.Edge.Edge,
                    browser.Edge.Network,
                    browser.Edge.Asn,
                    browser.Edge.Protocol,
                    browser.Edge.TlsVersion,
                    browser.Edge.IpVersion),
            browser.Services?
                .Select(service => new BrowserServiceCheck(
                    service.Id ?? "unknown-service",
                    service.Name ?? service.Id ?? "Unknown service",
                    service.Reachable,
                    service.DurationMs,
                    service.Note))
                .ToArray() ?? []);
        var capabilities = new List<string>
        {
            "browser-http-latency",
            "download-throughput",
            "upload-throughput",
            "loaded-latency"
        };
        if (flows.Length > 1) capabilities.Add("flow-comparison");
        if (scaling.Length > 0) capabilities.Add("connection-scaling");
        if (evidence.ServiceChecks.Count > 0) capabilities.Add("service-reachability");
        var edgeName = browser.Edge?.Edge ?? "Website edge";
        var measurement = new MeasurementContextReport(
            "1.0",
            "network-diagnostics-web",
            "browser-export",
            capabilities,
            new MeasurementEndpointReport(
                "website-edge",
                edgeName,
                "Cloudflare",
                InternetTransferProbe.DefaultOrigin.ToString(),
                "selected-by-browser",
                null),
            []);
        var report = new NetworkDiagnosticsReportV2(
            "2.0",
            browser.CompletedAt,
            new DiagnosticRunMetadata(
                ParseId(browser.Id),
                "web",
                "browser",
                profile,
                method,
                browser.StartedAt,
                browser.CompletedAt,
                false),
            NativeTransferPlanReport.FromPlan(plan),
            internet,
            null,
            null,
            new ReportProducer("web", null, "network-diagnostics-web"),
            measurement,
            null,
            evidence);

        return report with { Findings = DiagnosticClassifier.Classify(report) };
    }

    private static NativeFlowMeasurement ToFlowMeasurement(BrowserFlowMeasurementDto flow)
    {
        var strategy = flow.Strategy?.Trim().ToLowerInvariant() switch
        {
            "single" => TransferStrategy.Single,
            "aggregate" => TransferStrategy.Aggregate,
            _ => throw new InvalidDataException($"Unsupported browser flow strategy '{flow.Strategy}'.")
        };
        return new NativeFlowMeasurement(
            strategy,
            Math.Max(1, flow.Concurrency),
            flow.Download is null ? null : ToThroughput(flow.Download),
            flow.Upload is null ? null : ToThroughput(flow.Upload),
            flow.DownloadLatency is null ? null : ToLoadedLatency(flow.DownloadLatency),
            flow.UploadLatency is null ? null : ToLoadedLatency(flow.UploadLatency));
    }

    private static NativeThroughputSummary ToThroughput(BrowserThroughputDto throughput)
    {
        throughput.Validate();
        return new NativeThroughputSummary(
            throughput.Mbps,
            throughput.SteadyMbps,
            Math.Max(0, throughput.Bytes),
            Math.Max(0, throughput.DurationMs),
            Math.Max(0, throughput.PeakMbps),
            Math.Clamp(throughput.StabilityPercent, 0, 100),
            throughput.RampRatio,
            throughput.CapReached,
            string.IsNullOrWhiteSpace(throughput.Qualification) ? "unknown" : throughput.Qualification,
            throughput.Timeline?
                .Select(point => new NativeTimelinePoint(Math.Max(0, point.ElapsedMs), Math.Max(0, point.Value)))
                .ToArray() ?? [],
            string.IsNullOrWhiteSpace(throughput.Aggregation) ? "single" : throughput.Aggregation,
            throughput.Samples?
                .Select(sample => new NativeThroughputSampleSummary(
                    sample.Sample,
                    sample.Mbps,
                    sample.SteadyMbps,
                    Math.Max(0, sample.Bytes),
                    Math.Max(0, sample.DurationMs),
                    Math.Max(0, sample.PeakMbps),
                    Math.Clamp(sample.StabilityPercent, 0, 100),
                    sample.RampRatio,
                    sample.CapReached,
                    string.IsNullOrWhiteSpace(sample.Qualification) ? "unknown" : sample.Qualification))
                .ToArray() ?? []);
    }

    private static NativeLoadedLatencyReport ToLoadedLatency(BrowserLoadedLatencyDto latency)
    {
        return new NativeLoadedLatencyReport(
            ToLatency(latency),
            latency.IncreaseMs,
            string.IsNullOrWhiteSpace(latency.Grade) ? "—" : latency.Grade);
    }

    private static LatencyStatistics ToLatency(BrowserLatencyDto latency)
    {
        latency.Validate();
        return new LatencyStatistics(
            Math.Max(0, latency.Sent),
            Math.Max(0, latency.Received),
            Math.Max(0, latency.Lost),
            Math.Clamp(latency.LossPercent, 0, 100),
            latency.MinMs,
            latency.MaxMs,
            latency.MeanMs,
            latency.MedianMs,
            latency.P95Ms,
            latency.JitterMs,
            latency.Samples?.ToArray() ?? []);
    }

    private static Guid ParseId(string? id) => Guid.TryParse(id, out var parsed) ? parsed : Guid.NewGuid();

    private static int PrimaryConnections(NativeTransferPlan plan, TransferMethod method)
    {
        if (method == TransferMethod.Single) return 1;
        return Math.Max(1, plan.DownloadStages.Select(stage => stage.Connections).DefaultIfEmpty(1).Max());
    }

    private sealed class BrowserDiagnosticResultDto
    {
        public string? Id { get; init; }
        public DateTimeOffset StartedAt { get; init; }
        public DateTimeOffset CompletedAt { get; init; }
        public string? Mode { get; init; }
        public string? TransferMode { get; init; }
        public BrowserEdgeDto? Edge { get; init; }
        public BrowserLatencyDto? IdleLatency { get; init; }
        public BrowserThroughputDto? Download { get; init; }
        public BrowserThroughputDto? Upload { get; init; }
        public BrowserLoadedLatencyDto? DownloadLatency { get; init; }
        public BrowserLoadedLatencyDto? UploadLatency { get; init; }
        public List<BrowserFlowMeasurementDto>? FlowMeasurements { get; init; }
        public List<BrowserScalingPointDto>? DownloadScaling { get; init; }
        public List<BrowserServiceDto>? Services { get; init; }
        public long DataUsedBytes { get; init; }

        public void Validate()
        {
            if (StartedAt == default || CompletedAt == default || CompletedAt < StartedAt)
            {
                throw new InvalidDataException("The browser report has an invalid measurement time range.");
            }
            if (string.IsNullOrWhiteSpace(Mode)) throw new InvalidDataException("The browser report is missing its mode.");
            if (IdleLatency is null || Download is null || Upload is null || DownloadLatency is null || UploadLatency is null)
            {
                throw new InvalidDataException("The browser report is missing one or more required measurement sections.");
            }
        }
    }

    private class BrowserLatencyDto
    {
        public int Sent { get; init; }
        public int Received { get; init; }
        public int Lost { get; init; }
        public double LossPercent { get; init; }
        public double? MinMs { get; init; }
        public double? MaxMs { get; init; }
        public double? MeanMs { get; init; }
        public double? MedianMs { get; init; }
        public double? P95Ms { get; init; }
        public double? JitterMs { get; init; }
        public List<double?>? Samples { get; init; }

        public void Validate()
        {
            if (Sent < 0 || Received < 0 || Lost < 0 || Received + Lost > Sent)
            {
                throw new InvalidDataException("The browser report contains invalid latency sample counts.");
            }
        }
    }

    private sealed class BrowserLoadedLatencyDto : BrowserLatencyDto
    {
        public double? IncreaseMs { get; init; }
        public string? Grade { get; init; }
    }

    private sealed class BrowserThroughputDto
    {
        public double Mbps { get; init; }
        public double SteadyMbps { get; init; }
        public long Bytes { get; init; }
        public double DurationMs { get; init; }
        public double PeakMbps { get; init; }
        public double StabilityPercent { get; init; }
        public double? RampRatio { get; init; }
        public bool CapReached { get; init; }
        public string? Qualification { get; init; }
        public string? Aggregation { get; init; }
        public List<BrowserTimelineDto>? Timeline { get; init; }
        public List<BrowserThroughputSampleDto>? Samples { get; init; }

        public void Validate()
        {
            if (!double.IsFinite(Mbps) || !double.IsFinite(SteadyMbps) || Mbps < 0 || SteadyMbps < 0 || Bytes < 0 || DurationMs < 0)
            {
                throw new InvalidDataException("The browser report contains an invalid throughput measurement.");
            }
        }
    }

    private sealed class BrowserTimelineDto
    {
        public double ElapsedMs { get; init; }
        public double Value { get; init; }
    }

    private sealed class BrowserThroughputSampleDto
    {
        public int Sample { get; init; }
        public double Mbps { get; init; }
        public double SteadyMbps { get; init; }
        public long Bytes { get; init; }
        public double DurationMs { get; init; }
        public double PeakMbps { get; init; }
        public double StabilityPercent { get; init; }
        public double? RampRatio { get; init; }
        public bool CapReached { get; init; }
        public string? Qualification { get; init; }
    }

    private sealed class BrowserFlowMeasurementDto
    {
        public string? Strategy { get; init; }
        public int Concurrency { get; init; }
        public BrowserThroughputDto? Download { get; init; }
        public BrowserThroughputDto? Upload { get; init; }
        public BrowserLoadedLatencyDto? DownloadLatency { get; init; }
        public BrowserLoadedLatencyDto? UploadLatency { get; init; }
    }

    private sealed class BrowserScalingPointDto
    {
        public int Concurrency { get; init; }
        public BrowserThroughputDto? Download { get; init; }
        public BrowserLoadedLatencyDto? DownloadLatency { get; init; }
    }

    private sealed class BrowserServiceDto
    {
        public string? Id { get; init; }
        public string? Name { get; init; }
        public bool Reachable { get; init; }
        public double? DurationMs { get; init; }
        public string? Note { get; init; }
    }

    private sealed class BrowserEdgeDto
    {
        public string? Edge { get; init; }
        public string? Network { get; init; }
        public int? Asn { get; init; }
        public string? Protocol { get; init; }
        public string? TlsVersion { get; init; }
        public string? IpVersion { get; init; }
    }
}
