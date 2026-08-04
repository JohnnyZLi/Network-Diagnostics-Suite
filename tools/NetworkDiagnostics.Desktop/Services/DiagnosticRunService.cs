using System.Reflection;
using NetworkDeepProbe.Diagnostics;
using NetworkDeepProbe.Models;
using NetworkDeepProbe.Planning;

namespace NetworkDiagnostics.Desktop.Services;

public sealed class DiagnosticRunService
{
    private static readonly string? ApplicationVersion =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3);

    public IReadOnlyList<NetworkInterfaceChoice> ListInterfaces() =>
        NetworkDiagnosticsRunner.ListInterfaces();

    public Task<NativePreflightResult> PreflightAsync(
        TestProfileId profile,
        TransferMethod method,
        DesktopSettings settings,
        CancellationToken cancellationToken)
    {
        return NetworkDiagnosticsRunner.PreflightAsync(
            new NativePreflightOptions(
                Profile: profile,
                TransferMethod: method,
                TestOrigins: settings.ParsedTestOrigins,
                InterfaceId: settings.InterfaceId,
                IncludeAddresses: settings.IncludeLocalIdentifiers),
            cancellationToken);
    }

    public Task<NetworkDiagnosticsReportV2> RunAsync(
        TestProfileId profile,
        TransferMethod method,
        DesktopSettings settings,
        IProgress<NativeRunProgress> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);
        return NetworkDiagnosticsRunner.RunAsync(
            new NativeDiagnosticRunOptions(
                Profile: profile,
                TransferMethod: method,
                IncludeAddresses: settings.IncludeLocalIdentifiers,
                TestOrigins: settings.ParsedTestOrigins,
                InterfaceId: settings.InterfaceId,
                LanTarget: string.IsNullOrWhiteSpace(settings.LanTarget) ? null : settings.LanTarget.Trim(),
                LanPort: settings.LanPort,
                LanDurationSeconds: settings.LanDurationSeconds,
                LanConnections: settings.LanConnections,
                ProducerApplication: "desktop",
                ProducerVersion: ApplicationVersion),
            progress,
            cancellationToken);
    }

    public Task RunLanServerAsync(
        int port,
        IProgress<string> progress,
        CancellationToken cancellationToken) =>
        NetworkDiagnosticsRunner.RunLanServerAsync(port, progress, cancellationToken);
}
