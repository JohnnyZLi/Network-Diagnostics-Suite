using System.Runtime.InteropServices;
using NetworkDeepProbe.Models;
using NetworkDeepProbe.Planning;

namespace NetworkDeepProbe.Diagnostics;

internal static class FullDiagnosticRunner
{
    public static Task<NetworkDiagnosticsReportV2> RunAsync(
        ProbeOptions options,
        IProgress<string>? progress,
        CancellationToken cancellationToken) =>
        RunAsync(options, progress, null, cancellationToken);

    public static async Task<NetworkDiagnosticsReportV2> RunAsync(
        ProbeOptions options,
        IProgress<string>? progress,
        IProgress<NativeTransferProgress>? detailedTransferProgress,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var plan = NativeTransferPlanBuilder.Build(options.Profile, options.TransferMethod);

        progress?.Report("Selecting the measurement endpoint and reading network metadata");
        var preflight = await MeasurementPreflight.RunAsync(
            options.CandidateOrigins,
            options.InterfaceId,
            options.IncludeAddresses,
            "network-diagnostics-native",
            Capabilities(options),
            cancellationToken);

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
            preflight.EndpointSelection.Selected.Origin,
            transferProgress,
            cancellationToken,
            preflight.Binding?.SourceAddress);

        DeepProbeReport? deepDiagnostics = null;
        string? deepFailure = null;
        if (options.Profile is TestProfileId.Standard or TestProfileId.Extended)
        {
            try
            {
                deepDiagnostics = await ProbeRunner.RunAsync(options, progress, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception error)
            {
                deepFailure = SafeError(error);
                progress?.Report("Local and path diagnostics did not complete; preserving Internet measurements");
            }
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
            preflight.Measurement,
            null);

        var findings = DiagnosticClassifier.Classify(report).ToList();
        if (preflight.Measurement.Http3 is { Attempted: true, Supported: false } http3)
        {
            findings.Add(new DiagnosticFinding(
                "http3-unavailable",
                "protocol",
                "info",
                "high",
                "HTTP/3 was not available on the selected path",
                "The exact-version HTTP/3 request did not complete. The normal transfer phases still used supported HTTP versions and remain valid.",
                [new DiagnosticEvidence("measurement.http3.supported", "HTTP/3", "Unavailable", http3.Error)],
                ["No change is required unless you are diagnosing QUIC or HTTP/3 specifically. Compare another network or endpoint to isolate path filtering."],
                "Repeat the protocol probe on another network or endpoint."));
        }
        if (deepFailure is not null)
        {
            findings.Insert(0, new DiagnosticFinding(
                "deep-diagnostics-failed",
                "measurement-quality",
                "info",
                "high",
                "Local and path diagnostics did not complete",
                "The Internet transfer phases completed and remain available, but the operating-system deep-probe section failed before it could produce evidence.",
                [new DiagnosticEvidence("deepDiagnostics.status", "Deep diagnostics", "Failed", deepFailure)],
                ["Repeat the same profile. If the section fails again, inspect permissions and the technical error before changing network settings."],
                "Repeat Full or Stress after resolving the local diagnostic error."));
        }

        return report with { Findings = findings };
    }

    internal static IEnumerable<string> Capabilities(ProbeOptions options)
    {
        yield return "endpoint-preflight";
        yield return "network-metadata";
        yield return "http3-probe";
        yield return "http-latency";
        yield return "download-throughput";
        yield return "upload-throughput";
        yield return "loaded-latency";
        if (!string.IsNullOrWhiteSpace(options.InterfaceId)) yield return "source-interface-binding";

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

    private static string SafeError(Exception error)
    {
        var message = string.IsNullOrWhiteSpace(error.Message)
            ? error.GetType().Name
            : $"{error.GetType().Name}: {error.Message}";
        return message.Length <= 320 ? message : $"{message[..317]}...";
    }
}
