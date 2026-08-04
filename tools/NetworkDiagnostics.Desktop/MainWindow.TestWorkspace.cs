using Avalonia.Controls;
using Avalonia.Interactivity;
using NetworkDiagnostics.Desktop.Navigation;
using NetworkDiagnostics.Desktop.Presentation;
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
        testSetupWorkspace.SettingsRequested += TestSetupSettingsRequested;

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

    private void TestSetupSettingsRequested(object? sender, EventArgs eventArgs) =>
        NavigateToDestination(new SettingsDestination("Measurement"));

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

    private void TestConfigurationInterfaceRequested(object? sender, IndexRequestedEventArgs eventArgs)
    {
        if (InterfaceSelector.SelectedIndex != eventArgs.Index)
        {
            InterfaceSelector.SelectedIndex = eventArgs.Index;
        }
        SyncTestWorkspace();
    }

    private async void TestConfigurationIdentifiersChanged(object? sender, EventArgs eventArgs)
    {
        if (testConfigurationPanel is null) return;
        var includeIdentifiers = testConfigurationPanel.IncludeIdentifiers;
        IncludeIdentifiersCheckBox.IsChecked = includeIdentifiers;
        settings = settings with { IncludeLocalIdentifiers = includeIdentifiers };
        await PersistSettingsAsync();
        await RefreshPreflightAsync();
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
        SyncTestWorkspace();
        SyncRunResultWorkspaces();
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
        currentTestState = Models.TestViewState.Results;
        SetupView.IsVisible = false;
        RunningView.IsVisible = false;
        ResultsView.IsVisible = true;
        SyncRunResultWorkspaces();

        var destination = new TestResultDestination(reportId);
        lastWorkspaceEntries[WorkspaceKind.Test] = new NavigationEntry(
            destination,
            new NavigationViewState(InspectorOpen: workbenchShell?.InspectorOpen ?? true));

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
        var interfaceLabels = InterfaceSelector.Items
            .OfType<ComboBoxItem>()
            .Select(item => item.Content?.ToString() ?? "Interface")
            .ToArray();

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
            CompactStatusValue(PreflightInterfaceText.Text),
            CompactStatusValue(PreflightEndpointText.Text),
            CompactStatusValue(PreflightNetworkText.Text),
            snapshot.IsActive,
            $"{profileName} · {methodName}",
            snapshot.Detail,
            snapshot.Progress));

        testConfigurationPanel.Render(new TestConfigurationModel(
            interfaceLabels,
            InterfaceSelector.SelectedIndex,
            settings.IncludeLocalIdentifiers,
            CompactStatusValue(PreflightEndpointText.Text),
            CompactStatusValue(PreflightNetworkText.Text)));

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
