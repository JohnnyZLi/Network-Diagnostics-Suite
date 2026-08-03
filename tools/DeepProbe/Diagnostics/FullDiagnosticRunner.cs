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
            options.TestEndpoints ?? [MeasurementEndpointCatalog.FromOrigin(options.TestOrigin)],
            cancellationToken);
        progress?.Report($"Using {endpointSelection.Selected.Name} for Internet transfer measurements");
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
        var deepDiagnostics = await ProbeRunner.RunAsync(options, progress, cancellationToken);
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
            deepDiagnostics.LocalLink)
        {
            Measurement = EndpointSelector.CreateContext(
                endpointSelection,
                options.EngineName,
                NativeCapabilities(options))
        };
        return report with { Findings = DiagnosticClassifier.Classify(report) };
    }

    private static IEnumerable<string> NativeCapabilities(ProbeOptions options)
    {
        yield return "application-latency";
        yield return "content-throughput";
        yield return "loaded-latency";
        yield return "single-flow";
        yield return "aggregate-flow";
        yield return "icmp";
        yield return "gateway-latency";
        yield return "traceroute";
        yield return "dns-resolvers";
        yield return "path-mtu-ipv4";
        yield return "tls-phases";
        yield return "interfaces";
        yield return "wifi";
        yield return "routing";
        if (options.LanTarget is not null) yield return "lan-throughput";
    }
}
