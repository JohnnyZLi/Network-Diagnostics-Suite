using Avalonia.Interactivity;
using Avalonia.Threading;
using NetworkDiagnostics.Desktop.Monitoring;
using NetworkDiagnostics.Desktop.Presentation;
using NetworkDiagnostics.Desktop.Workspaces;

namespace NetworkDiagnostics.Desktop;

public sealed partial class MainWindow
{
    private bool monitoringSubscribed;
    private int monitoringUiUpdateQueued;

    private async Task InitializeMonitoringAsync()
    {
        if (!monitoringSubscribed)
        {
            monitoringService.SnapshotChanged += MonitoringSnapshotChanged;
            monitoringService.ContentSpeedDue += MonitoringContentSpeedDue;
            monitoringSubscribed = true;
        }

        await monitoringService.StartAsync(settings.ToMonitorOptions());
    }

    private NetworkExperiencePresentation CurrentNetworkExperience() =>
        NetworkExperiencePresenter.Build(
            monitoringService.Snapshot,
            settings.ToMonitorOptions(),
            monitorWindow);

    private void MonitoringSnapshotChanged(object? sender, MonitorSnapshotChangedEventArgs eventArgs)
    {
        // Monitoring can publish while the UI thread is busy. Keep at most one
        // dashboard refresh queued and always read the service's latest snapshot
        // when that refresh executes. The old path queued a complete workspace
        // rebuild for every sample, including hidden report/result surfaces.
        if (Interlocked.Exchange(ref monitoringUiUpdateQueued, 1) != 0)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                testSetupWorkspace?.RenderMonitoringSnapshot(CurrentNetworkExperience());
                UpdateTrayPresentation();
                RefreshWorkbenchChrome();
            }
            finally
            {
                Volatile.Write(ref monitoringUiUpdateQueued, 0);
            }
        }, DispatcherPriority.Background);
    }

    private void MonitoringContentSpeedDue(object? sender, MonitorContentSpeedDueEventArgs eventArgs)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            if (!settings.MonitoringEnabled
                || settings.ContentSpeedCadenceHours <= 0
                || activeRunSession.Snapshot.IsActive)
            {
                return;
            }

            await RunContentSpeedTestAsync();
        });
    }

    private async void TestSetupMonitorWindowRequested(object? sender, MonitorWindowRequestedEventArgs eventArgs)
    {
        monitorWindow = eventArgs.Window;
        settings = settings with { MonitoringWindow = eventArgs.Window.ContractId() };
        await PersistSettingsAsync();
        SyncTestWorkspace();
    }

    private async void TestSetupMonitoringToggleRequested(object? sender, EventArgs eventArgs)
    {
        settings = settings with { MonitoringEnabled = !settings.MonitoringEnabled };
        await PersistSettingsAsync();
        await monitoringService.UpdateOptionsAsync(settings.ToMonitorOptions());
        UpdateTrayPresentation();
        SyncTestWorkspace();
    }

    private async void TestSetupContentSpeedRequested(object? sender, EventArgs eventArgs) =>
        await RunContentSpeedTestAsync();

    private async void TestSetupPeakSpeedRequested(object? sender, EventArgs eventArgs) =>
        await RunPeakSpeedTestAsync();

    private async void TestSetupMarkAlertsReadRequested(object? sender, EventArgs eventArgs)
    {
        await monitoringService.MarkAllAlertsReadAsync();
        SyncTestWorkspace();
    }

    private async void TestSetupClearAlertsRequested(object? sender, EventArgs eventArgs)
    {
        await monitoringService.ClearAlertsAsync();
        SyncTestWorkspace();
    }

    private async Task RunContentSpeedTestAsync()
    {
        if (activeRunSession.Snapshot.IsActive)
        {
            ReturnToActiveRun();
            return;
        }

        await SelectProfileAsync(0);
        await SelectMethodAsync(2);
        RunClicked(this, new RoutedEventArgs());
    }

    private async Task RunPeakSpeedTestAsync()
    {
        if (activeRunSession.Snapshot.IsActive)
        {
            ReturnToActiveRun();
            return;
        }

        await SelectProfileAsync(3);
        await SelectMethodAsync(2);
        RunClicked(this, new RoutedEventArgs());
    }

    private async Task RecordCurrentReportForMonitoringAsync()
    {
        if (currentReport is null) return;
        await monitoringService.RecordDiagnosticAsync(currentReport);
        UpdateTrayPresentation();
    }
}
