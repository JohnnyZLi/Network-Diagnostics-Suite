using Avalonia.Interactivity;
using NetworkDiagnostics.Desktop.Navigation;
using NetworkDiagnostics.Desktop.Presentation;
using NetworkDiagnostics.Desktop.Services;
using NetworkDiagnostics.Desktop.Workspaces;

namespace NetworkDiagnostics.Desktop;

public sealed partial class MainWindow
{
    private void InstallTestWorkspace()
    {
        testSetupWorkspace = new TestSetupWorkspace();
        testConfigurationPanel = new TestConfigurationPanel();
        SetupView.Content = testSetupWorkspace;

        testSetupWorkspace.ProfileRequested += TestSetupProfileRequested;
        testSetupWorkspace.MethodRequested += TestSetupMethodRequested;
        testSetupWorkspace.RunRequested += TestSetupRunRequested;
        testSetupWorkspace.ActiveRunRequested += TestSetupActiveRunRequested;
        testSetupWorkspace.ActiveRunStopRequested += TestSetupActiveRunStopRequested;
        testSetupWorkspace.SettingsRequested += TestSetupSettingsRequested;
        testSetupWorkspace.MonitorWindowRequested += TestSetupMonitorWindowRequested;
        testSetupWorkspace.MonitoringToggleRequested += TestSetupMonitoringToggleRequested;
        testSetupWorkspace.ContentSpeedRequested += TestSetupContentSpeedRequested;
        testSetupWorkspace.PeakSpeedRequested += TestSetupPeakSpeedRequested;
        testSetupWorkspace.MarkAlertsReadRequested += TestSetupMarkAlertsReadRequested;
        testSetupWorkspace.ClearAlertsRequested += TestSetupClearAlertsRequested;
        WireControlCenterEvents();

        testConfigurationPanel.InterfaceRequested += TestConfigurationInterfaceRequested;
        testConfigurationPanel.IdentifiersChanged += TestConfigurationIdentifiersChanged;
        testConfigurationPanel.RefreshRequested += TestConfigurationRefreshRequested;
        testConfigurationPanel.SettingsRequested += TestSetupSettingsRequested;

        activeRunSession.Changed += ActiveRunSessionChanged;
    }

    private void TestSetupProfileRequested(object? sender, IndexRequestedEventArgs eventArgs) =>
        SelectProfile(eventArgs.Index);

    private void TestSetupMethodRequested(object? sender, IndexRequestedEventArgs eventArgs) =>
        SelectMethod(eventArgs.Index);

    private void TestSetupRunRequested(object? sender, EventArgs eventArgs) =>
        RunClicked(sender, new RoutedEventArgs());

    private void TestSetupActiveRunRequested(object? sender, EventArgs eventArgs) =>
        ReturnToActiveRun();

    private void TestSetupActiveRunStopRequested(object? sender, EventArgs eventArgs) =>
        StopClicked(sender, new RoutedEventArgs());

    private void TestSetupSettingsRequested(object? sender, EventArgs eventArgs) =>
        NavigateToDestination(new SettingsDestination("General"));

    private async void TestConfigurationInterfaceRequested(object? sender, IndexRequestedEventArgs eventArgs) =>
        await SelectInterfaceAsync(eventArgs.Index);

    private async void TestConfigurationIdentifiersChanged(object? sender, EventArgs eventArgs)
    {
        if (testConfigurationPanel is null) return;
        await SaveIdentifiersSettingAsync(testConfigurationPanel.IncludeIdentifiers);
        SyncTestWorkspace();
        RefreshWorkbenchChrome();
    }

    private async void TestConfigurationRefreshRequested(object? sender, EventArgs eventArgs)
    {
        await RefreshPreflightAsync();
        SyncTestWorkspace();
        RefreshWorkbenchChrome();
    }

    private void ActiveRunSessionChanged(object? sender, EventArgs eventArgs)
    {
        var snapshot = activeRunSession.Snapshot;
        if (snapshot.IsActive)
        {
            // The live tile is the only visual run renderer. Updating fixed controls
            // here avoids hidden workspace churn while preserving the bounded-memory path.
            testSetupWorkspace?.RenderActiveRunSnapshot(snapshot);
            RefreshWorkbenchChrome();
            return;
        }

        if (snapshot.Status == ActiveRunStatus.Completed && snapshot.ReportId is not null)
        {
            // Keep both the completed live tile and the existing active-run header
            // presentation stable until the fully rendered report sheet is mounted.
            // Refreshing chrome here would hide the header chip one frame too early.
            testSetupWorkspace?.HoldCompletedRunTile(snapshot);
            return;
        }

        // Cancelled and failed runs return to the normal choices immediately.
        SyncTestWorkspace();
        RefreshWorkbenchChrome();
    }

    private void ReturnToActiveRun()
    {
        var snapshot = activeRunSession.Snapshot;
        if (snapshot.IsActive)
        {
            NavigateToDestination(new RunningTestDestination(snapshot.RunId));
            return;
        }

        if (snapshot.ReportId is { } reportId)
        {
            NavigateToDestination(new TestResultDestination(reportId));
        }
    }

    private void PresentRunOutcome(Guid reportId)
    {
        ShowControlCenterUnderlay();
        _ = RecordCurrentReportForMonitoringAsync();

        var destination = new TestResultDestination(reportId);
        lastWorkspaceEntries[WorkspaceKind.Test] = new NavigationEntry(
            destination,
            new NavigationViewState(InspectorOpen: workbenchShell?.InspectorOpen ?? false));

        if (navigationService.Current?.Destination is RunningTestDestination)
        {
            NavigateToDestination(destination);
        }
        else
        {
            RefreshWorkbenchChrome();
        }
    }

    private void SyncTestWorkspace()
    {
        if (testSetupWorkspace is null || testConfigurationPanel is null)
        {
            return;
        }

        var snapshot = activeRunSession.Snapshot;
        var profileName = DiagnosticReportPresenter.ProfileName(snapshot.IsActive ? snapshot.Profile : SelectedProfile());
        var methodName = MethodName(snapshot.IsActive ? snapshot.Method : SelectedMethod());

        testSetupWorkspace.Render(new TestSetupWorkspaceModel(
            selectedProfileIndex,
            selectedMethodIndex,
            profileQuestion,
            profilePurpose,
            methodExplanation,
            estimatedTime,
            transferCap,
            confirmation,
            profileAvailability,
            CompactStatusValue(preflightInterface),
            CompactStatusValue(preflightEndpoint),
            CompactStatusValue(preflightNetwork),
            snapshot.IsActive,
            $"{profileName} · {methodName}",
            snapshot.Detail,
            snapshot.Progress,
            CurrentNetworkExperience()));
        testSetupWorkspace.SetActiveRunTileState(snapshot);
        testSetupWorkspace.RefreshModelDependentVisuals();
        SyncControlCenterSections();

        testConfigurationPanel.Render(new TestConfigurationModel(
            interfaceLabels,
            selectedInterfaceIndex,
            settings.IncludeLocalIdentifiers,
            CompactStatusValue(preflightEndpoint),
            CompactStatusValue(preflightNetwork)));
    }
}
