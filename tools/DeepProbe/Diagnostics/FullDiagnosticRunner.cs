using System.Runtime.InteropServices;
using NetworkDeepProbe.Models;
using NetworkDeepProbe.Planning;

namespace NetworkDeepProbe.Diagnostics;

internal static class FullDiagnosticRunner
{
    public static async Task<NetworkDiagnosticsReportV2> RunAsync(
        ProbeOptions options,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        return await RunAsync(options, progress, null, cancellationToken);
    }

    public static async Task<NetworkDiagnosticsReportV2> RunAsync(
        ProbeOptions options,
        IProgress<string>? progress,
        IProgress<NativeTransferProgress>? detailedTransferProgress,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var plan = NativeTransferPlanBuilder.Build(options.Profile, options.TransferMethod);

        progress?.Report("Selecting the measurement endpoint");
        var endpointSelection = await EndpointSelector.SelectAsync(
            [MeasurementEndpointCatalog.FromOrigin(options.TestOrigin)],
            cancellationToken);
        var measurement = EndpointSelector.CreateContext(
            endpointSelection,
            "network-diagnostics-native",
            Capabilities(options));

        var lastStage = string.Empty;
        var transferProgress = new Progress<NativeTransferProgress>(current =>
        {
            detailedTransferProgress?.Report(current);
            if (current.Stage == lastStage) return;
            lastStage = current.Stage;
            progress?.Report(current.Phase switch
            {
                "idle" => "Measuring first-party HTTP latency",
                "download" => $"Measuring {current.Stage} download",
                "upload" => $"Measuring {current.Stage} upload",
                "complete" => "Internet transfer measurements complete",
                _ => $"Running {current.Stage}"
            });
        });

        var internetTransfer = await InternetTransferProbe.RunAsync(
            plan,
            endpointSelection.Selected.Origin,
            transferProgress,
            cancellationToken);

        DeepProbeReport? deepDiagnostics = null;
        if (options.Profile is TestProfileId.Standard or TestProfileId.Extended)
        {
            deepDiagnostics = await ProbeRunner.RunAsync(options, progress, cancellationToken);
        }

        var completedAt = DateTimeOffset.UtcNow;
        var report = new NetworkDiagnosticsReportV2(
            "2.0",
            completedAt,
            new DiagnosticRunMetadata(
                Guid.NewGuid(),
                RuntimeInformation.OSDescription,
                RuntimeInformation.OSArchitecture.ToString(),
                plan.Profile,
                plan.Method,
                startedAt,
                completedAt,
                options.IncludeAddresses),
            NativeTransferPlanReport.FromPlan(plan),
            internetTransfer,
            deepDiagnostics,
            deepDiagnostics?.LocalLink,
            null,
            measurement,
            null);

        return report with { Findings = DiagnosticClassifier.Classify(report) };
    }

    private static IEnumerable<string> Capabilities(ProbeOptions options)
    {
        yield return "http-latency";
        yield return "download-throughput";
        yield return "upload-throughput";
        yield return "loaded-latency";

        if (options.Profile is not (TestProfileId.Standard or TestProfileId.Extended)) yield break;

        yield return "network-interfaces";
        yield return "gateway-latency";
        yield return "icmp-latency";
        yield return "traceroute";
        yield return "dns-resolvers";
        yield return "path-mtu";
        yield return "service-reachability";
        yield return "wifi-details";
        yield return "routing-details";
        if (options.LanTarget is not null) yield return "local-link-throughput";
    }
}
