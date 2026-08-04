using NetworkDeepProbe.Models;

namespace NetworkDeepProbe.Diagnostics;

internal sealed record AdvancedEvidenceResult(
    LoadedPathLocalizationReport? LoadLocalization,
    DualStackReport DualStack,
    NetworkChangeReport NetworkChange,
    HostResourceReport HostResources);

internal sealed class AdvancedEvidenceSession : IAsyncDisposable
{
    private readonly Uri origin;
    private readonly string? interfaceId;
    private readonly bool includeLocalIdentifiers;
    private readonly CapturedNetworkState before;
    private readonly LoadedPathLatencyCollector? localization;
    private readonly HostResourceMonitor hostMonitor;
    private readonly Task<DualStackReport> dualStackTask;
    private readonly Task<bool> captivePortalTask;
    private readonly object transferEvidenceGate = new();
    private Task<LoadedPathLocalizationReport?>? transferEvidenceTask;
    private bool completed;

    private AdvancedEvidenceSession(
        Uri origin,
        string? interfaceId,
        bool includeLocalIdentifiers,
        CapturedNetworkState before,
        LoadedPathLatencyCollector? localization,
        HostResourceMonitor hostMonitor,
        Task<DualStackReport> dualStackTask,
        Task<bool> captivePortalTask)
    {
        this.origin = origin;
        this.interfaceId = interfaceId;
        this.includeLocalIdentifiers = includeLocalIdentifiers;
        this.before = before;
        this.localization = localization;
        this.hostMonitor = hostMonitor;
        this.dualStackTask = dualStackTask;
        this.captivePortalTask = captivePortalTask;
    }

    public static async Task<AdvancedEvidenceSession> StartAsync(
        Uri origin,
        string? interfaceId,
        bool includeLocalIdentifiers,
        bool enableLoadLocalization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(origin);
        var before = NetworkStateProbe.Capture(origin, interfaceId, includeLocalIdentifiers);
        var hostMonitor = HostResourceMonitor.Start(includeLocalIdentifiers);
        var dualStackTask = DualStackProbe.RunAsync(origin, cancellationToken);
        var captivePortalTask = NetworkStateProbe.CheckCaptivePortalAsync(origin, cancellationToken);
        LoadedPathLatencyCollector? localization = null;
        try
        {
            if (enableLoadLocalization)
            {
                localization = await LoadedPathLatencyCollector.CreateAsync(
                    origin,
                    before.GatewayAddress,
                    includeLocalIdentifiers,
                    cancellationToken);
                localization.Start();
            }

            return new AdvancedEvidenceSession(
                origin,
                interfaceId,
                includeLocalIdentifiers,
                before,
                localization,
                hostMonitor,
                dualStackTask,
                captivePortalTask);
        }
        catch
        {
            if (localization is not null) await localization.DisposeAsync();
            await hostMonitor.DisposeAsync();
            await ObserveAsync(dualStackTask);
            await ObserveAsync(captivePortalTask);
            throw;
        }
    }

    public void SetPhase(string phase)
    {
        if (phase == "complete")
        {
            _ = EnsureTransferEvidenceTask();
            return;
        }
        localization?.SetPhase(phase);
    }

    public async Task<AdvancedEvidenceResult> CompleteAsync(CancellationToken cancellationToken)
    {
        if (completed) throw new InvalidOperationException("Advanced evidence has already been completed.");
        completed = true;
        var loadLocalization = await EnsureTransferEvidenceTask().WaitAsync(cancellationToken);
        var hostResources = await hostMonitor.StopAsync(cancellationToken);
        var dualStack = await dualStackTask.WaitAsync(cancellationToken);
        var captivePortal = await captivePortalTask.WaitAsync(cancellationToken);
        var after = NetworkStateProbe.Capture(origin, interfaceId, includeLocalIdentifiers);
        var networkChange = NetworkStateProbe.Compare(before, after, captivePortal);
        return new AdvancedEvidenceResult(loadLocalization, dualStack, networkChange, hostResources);
    }

    public async ValueTask DisposeAsync()
    {
        await hostMonitor.DisposeAsync();
        if (localization is not null) await localization.DisposeAsync();
    }

    private Task<LoadedPathLocalizationReport?> EnsureTransferEvidenceTask()
    {
        lock (transferEvidenceGate)
        {
            return transferEvidenceTask ??= StopTransferEvidenceAsync();
        }
    }

    private async Task<LoadedPathLocalizationReport?> StopTransferEvidenceAsync()
    {
        return localization is null
            ? null
            : await localization.StopAsync(CancellationToken.None);
    }

    private static async Task ObserveAsync(Task task)
    {
        try
        {
            await task;
        }
        catch
        {
            // The original startup failure remains the exception returned to the caller.
        }
    }
}
