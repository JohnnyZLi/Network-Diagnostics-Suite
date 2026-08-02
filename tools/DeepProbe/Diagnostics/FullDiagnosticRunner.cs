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
        var startedAt = DateTimeOffset.UtcNow;
        var plan = NativeTransferPlanBuilder.Build(options.Profile, options.TransferMethod);
        var lastStage = string.Empty;
        var transferProgress = new Progress<NativeTransferProgress>(current =>
        {
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
            options.TestOrigin,
            transferProgress,
            cancellationToken);
        var deepDiagnostics = await ProbeRunner.RunAsync(options, progress, cancellationToken);
        var completedAt = DateTimeOffset.UtcNow;

        return new NetworkDiagnosticsReportV2(
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
            deepDiagnostics.LocalLink);
    }
}
