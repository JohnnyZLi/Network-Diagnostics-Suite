using System.Reflection;
using NetworkDeepProbe.Diagnostics;
using NetworkDeepProbe.Models;
using NetworkDeepProbe.Planning;

namespace NetworkDiagnostics.Desktop.Services;

public sealed class DiagnosticRunService
{
    private static readonly string? ApplicationVersion =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3);

    public Task<NetworkDiagnosticsReportV2> RunAsync(
        TestProfileId profile,
        bool includeLocalIdentifiers,
        Uri? testOrigin,
        IProgress<NativeRunProgress> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);
        return NetworkDiagnosticsRunner.RunAsync(
            new NativeDiagnosticRunOptions(
                Profile: profile,
                TransferMethod: TransferMethod.Compare,
                IncludeAddresses: includeLocalIdentifiers,
                TestOrigin: testOrigin,
                ProducerApplication: "desktop",
                ProducerVersion: ApplicationVersion),
            progress,
            cancellationToken);
    }
}
