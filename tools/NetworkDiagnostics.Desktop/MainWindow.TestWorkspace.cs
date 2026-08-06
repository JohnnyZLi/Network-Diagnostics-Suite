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
        runningTestWorkspace = new RunningTestWorkspace();
        testResultWorkspace = new TestResultWorkspace();
        SetupView.Content = testSetupWorkspace;
        RunningView.Content = runningTestWorkspace;
        ResultsView.Content = testResultWorkspace;

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

        runningTestWorkspace.StopRequested += RunningWorkspaceStopRequested;
        testResultWorkspace.RunAgainRequested += TestResultRunAgainRequested;
        testResultWorkspace.QuickRequested += TestResultQuickRequested;
        testResultWorkspace.ExportRequested += TestResultExportRequested;
        testResultWorkspace.ReportsRequested += TestResultReportsRequested;
        testResultWorkspace.CompareRequested += TestResultCompareRequested;

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

    private void RunningWorkspaceStopRequested(object? sender, EventArgs eventArgs) =>
        StopClicked(sender, new RoutedEventArgs());

    private void TestResultRunAgainRequested(object? sender, EventArgs eventArgs) =>
        RunAgainClicked(sender, new RoutedEventArgs());

    private void TestResultQuickRequested(object? sender, EventArgs eventArgs) =>
        ChooseQuickClicked(sender, new RoutedEventArgs());

    private void TestResultExportRequested(object? sender, EventArgs eventArgs) =>
        ExportReportClicked(sender, new RoutedEventArgs());

    private void TestResultReportsRequested(object? sender, EventArgs eventArgs) =>
        NavigateToDestination(new ReportListDestination());

    private void TestResultCompareRequested(object? sender, EventArgs eventArgs)
    {
        if (currentReport is not null)
        {
            NavigateToDestination(new ComparisonDestination(currentReport.Run.Id));
        }
    }

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
            // Live telemetry updates a fixed dashboard tile and the compatibility
            // running renderer. Neither path rebuilds hidden workspaces or the full
            // monitoring surface, preserving the bounded-memory behavior.
            testSetupWorkspace?.RenderActiveRunSnapshot(snapshot);
            runningTestWorkspace?.Render(snapshot, activeRunSession.Events);
            RefreshWorkbenchChrome();
            return;
        }

        if (snapshot.Status == ActiveRunStatus.Completed && snapshot.ReportId is not null)
        {
            // Keep the final live layout mounted until the report sheet owns the
            // interaction. Restoring the two idle choice cards here caused a visible
            // dashboard reflow immediately before the backdrop and result appeared.
            testSetupWorkspace?.HoldCompletedRunTile(snapshot);
            SyncRunResultWorkspaces();
            RefreshWorkbenchChrome();
            return;
        }

        // Cancelled and failed runs return to the normal test choices immediately.
        // SyncTestWorkspace already calls SyncRunResultWorkspaces, so do not invoke
        // it a second time here.
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
        SyncRunResultWorkspaces();
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

        SyncRunResultWorkspaces();
    }

    private void SyncRunResultWorkspaces()
    {
        var snapshot = activeRunSession.Snapshot;
        runningTestWorkspace?.Render(snapshot, activeRunSession.Events);
        var section = navigationService.Current?.Destination is TestResultDestination result
            ? result.Section
            : "Overview";
        testResultWorkspace?.Render(currentPresentation, currentReport, snapshot, section);
    }
}
