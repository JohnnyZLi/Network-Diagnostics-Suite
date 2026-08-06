using Avalonia.Controls;
using Avalonia.Input;
using NetworkDeepProbe.Planning;
using NetworkDiagnostics.Desktop.Models;
using NetworkDiagnostics.Desktop.Navigation;
using NetworkDiagnostics.Desktop.Presentation;
using NetworkDiagnostics.Desktop.Services;
using NetworkDiagnostics.Desktop.Shell;
using NetworkDiagnostics.Desktop.Workspaces;

namespace NetworkDiagnostics.Desktop;

public sealed partial class MainWindow
{
    private void InstallWorkbenchShell()
    {
        MinWidth = 720;
        MinHeight = 560;

        if (Content is not Grid workspaceGrid)
        {
            return;
        }

        reportBrowserWorkspace = new ReportBrowserWorkspace { IsVisible = false };
        reportDetailWorkspace = new ReportDetailWorkspace { IsVisible = false };
        comparisonWorkspace = new ComparisonWorkspace { IsVisible = false };

        workspaceGrid.Children.Add(reportBrowserWorkspace);
        workspaceGrid.Children.Add(reportDetailWorkspace);
        workspaceGrid.Children.Add(comparisonWorkspace);
        InstallSettingsWorkspace(workspaceGrid);

        reportBrowserWorkspace.ImportRequested += ReportBrowserImportRequested;
        reportBrowserWorkspace.OpenFolderRequested += ReportBrowserOpenFolderRequested;
        reportBrowserWorkspace.OpenReportRequested += ReportBrowserOpenReportRequested;
        reportBrowserWorkspace.CompareReportRequested += ReportBrowserCompareReportRequested;
        reportBrowserWorkspace.EditReportRequested += ReportBrowserEditReportRequested;
        reportBrowserWorkspace.StateChanged += ReportBrowserStateChanged;
        reportBrowserWorkspace.StateChanged += ReportBrowserPersistenceStateChanged;

        reportDetailWorkspace.BackRequested += ReportDetailBackRequested;
        reportDetailWorkspace.CompareRequested += ReportDetailCompareRequested;
        reportDetailWorkspace.EditRequested += ReportDetailEditRequested;
        reportDetailWorkspace.ExportRequested += ReportDetailExportRequested;

        comparisonWorkspace.ClearRequested += ComparisonWorkspaceClearRequested;
        comparisonWorkspace.BaselineRequested += ComparisonWorkspaceBaselineRequested;
        comparisonWorkspace.CandidateRequested += ComparisonWorkspaceCandidateRequested;
        comparisonWorkspace.OpenReportRequested += ComparisonWorkspaceOpenReportRequested;
        comparisonWorkspace.EditReportRequested += ComparisonWorkspaceEditReportRequested;

        workbenchShell = new WorkbenchShell
        {
            WorkspaceContent = workspaceGrid
        };
        workbenchShell.BackRequested += WorkbenchBackRequested;
        workbenchShell.ForwardRequested += WorkbenchForwardRequested;
        workbenchShell.ActiveRunRequested += WorkbenchActiveRunRequested;
        workbenchShell.CommandPaletteRequested += WorkbenchCommandPaletteRequested;
        workbenchShell.CommandInvoked += WorkbenchCommandInvoked;
        workbenchShell.WorkspaceRequested += WorkbenchWorkspaceRequested;
        workbenchShell.DestinationRequested += WorkbenchDestinationRequested;
        workbenchShell.InspectorVisibilityChanged += WorkbenchInspectorVisibilityChanged;

        Content = workbenchShell;
        KeyDown += WorkbenchKeyDown;
        PointerPressed += WorkbenchPointerPressed;
    }

    private void InitializeNavigation()
    {
        navigationService.Changed += NavigationChanged;
        navigationService.Initialize(new TestSetupDestination(), CaptureCurrentViewState());
    }

    private void SelectProfile(int index) => _ = SelectProfileAsync(index);

    private async Task SelectProfileAsync(int index)
    {
        index = Math.Clamp(index, 0, 3);
        var changed = selectedProfileIndex != index;
        selectedProfileIndex = index;
        RenderProfileSelection();

        if (initialized && changed)
        {
            settings = settings with { DefaultProfile = DesktopSettings.ContractId(SelectedProfile()) };
            await PersistSettingsAsync();
            await RefreshPreflightAsync();
        }

        SyncTestWorkspace();
        SyncSettingsWorkspace();
        RefreshWorkbenchChrome();
    }

    private void SelectMethod(int index) => _ = SelectMethodAsync(index);

    private async Task SelectMethodAsync(int index)
    {
        index = Math.Clamp(index, 0, 2);
        var changed = selectedMethodIndex != index;
        selectedMethodIndex = index;
        RenderMethodSelection();
        RenderProfileSelection();

        if (initialized && changed)
        {
            settings = settings with { DefaultTransferMethod = DesktopSettings.ContractId(SelectedMethod()) };
            await PersistSettingsAsync();
            await RefreshPreflightAsync();
        }

        SyncTestWorkspace();
        SyncSettingsWorkspace();
        RefreshWorkbenchChrome();
    }

    private async void NavigationChanged(object? sender, NavigationChangedEventArgs eventArgs)
    {
        applyingNavigation = true;
        try
        {
            lastWorkspaceEntries[eventArgs.Current.Destination.Workspace] = eventArgs.Current;
            workbenchShell?.SetNavigation(
                eventArgs.Current,
                navigationService.CanGoBack,
                navigationService.CanGoForward);

            switch (eventArgs.Current.Destination)
            {
                case TestSetupDestination:
                    ShowControlCenterUnderlay();
                    workbenchShell?.CloseOverlay();
                    workbenchShell?.SelectControlCenter();
                    SyncTestWorkspace();
                    break;

                case RunningTestDestination runningDestination:
                    var activeRun = activeRunSession.Snapshot;
                    if (!activeRun.IsActive || activeRun.RunId != runningDestination.RunId)
                    {
                        AppDestination replacement = activeRun.ReportId is { } completedReportId
                            ? new TestResultDestination(completedReportId)
                            : new TestSetupDestination();
                        navigationService.Navigate(replacement, replaceCurrent: true);
                        return;
                    }

                    activeRunNavigationId = activeRun.RunId;
                    ShowControlCenterUnderlay();
                    workbenchShell?.CloseOverlay();
                    workbenchShell?.SelectControlCenter();
                    SyncTestWorkspace();
                    Avalonia.Threading.Dispatcher.UIThread.Post(
                        () => testSetupWorkspace?.BringActiveRunIntoView(),
                        Avalonia.Threading.DispatcherPriority.Loaded);
                    break;

                case TestResultDestination result:
                    ShowControlCenterUnderlay();
                    workbenchShell?.SelectControlCenter();

                    if (result.ReportId == Guid.Empty)
                    {
                        reportDetailWorkspace?.RenderCurrentPreview(currentPresentation);
                    }
                    else if (!await LoadReportForNavigationAsync(result.ReportId)
                             || selectedHistoryReport is null)
                    {
                        navigationService.Navigate(new TestSetupDestination(), replaceCurrent: true);
                        return;
                    }
                    else
                    {
                        reportDetailWorkspace?.RenderCurrent(selectedHistoryReport, currentPresentation);
                    }

                    if (reportDetailWorkspace is not null)
                    {
                        workbenchShell?.OpenOverlay(
                            "Diagnostic result",
                            reportDetailWorkspace,
                            maxWidth: 1160,
                            maxHeight: 820,
                            stretchWidth: true,
                            showHeader: false);
                    }
                    break;

                case ReportListDestination:
                    workbenchShell?.CloseOverlay();
                    await RefreshHistoryAsync(eventArgs.Current.ViewState);
                    ShowWorkspaceSurface(reportBrowserWorkspace);
                    break;

                case ReportDetailDestination detail:
                    ShowControlCenterUnderlay();
                    workbenchShell?.SelectControlCenter();
                    if (!await LoadReportForNavigationAsync(detail.ReportId))
                    {
                        navigationService.Navigate(new ReportListDestination(), replaceCurrent: true);
                        return;
                    }
                    if (selectedHistoryReport is not null && reportDetailWorkspace is not null)
                    {
                        reportDetailWorkspace.Render(selectedHistoryReport, currentPresentation);
                        workbenchShell?.OpenOverlay(
                            "Saved diagnostic",
                            reportDetailWorkspace,
                            maxWidth: 1160,
                            maxHeight: 820,
                            stretchWidth: true,
                            showHeader: false);
                    }
                    break;

                case ComparisonDestination comparison:
                    workbenchShell?.CloseOverlay();
                    ShowControlCenterUnderlay();
                    workbenchShell?.SelectControlCenter();
                    comparisonBaselineId = comparison.BaselineId;
                    comparisonCandidateId = comparison.CandidateId;
                    await RefreshComparisonHistoryAsync();
                    testSetupWorkspace?.OpenInlineComparison();
                    break;

                case SettingsDestination settingsDestination:
                    ShowControlCenterUnderlay();
                    workbenchShell?.SelectControlCenter();
                    SyncSettingsWorkspace(settingsDestination.Section);
                    if (settingsWorkspace is not null)
                    {
                        workbenchShell?.OpenOverlay(
                            "Settings",
                            settingsWorkspace,
                            maxWidth: 1100,
                            maxHeight: 760,
                            stretchWidth: true);
                    }
                    break;
            }

            RestoreViewState(eventArgs.Current.ViewState);
            RefreshWorkbenchChrome();
        }
        finally
        {
            applyingNavigation = false;
        }

        await PersistWorkbenchStateAsync();
    }

    private async Task<bool> LoadReportForNavigationAsync(Guid reportId)
    {
        if (selectedHistoryReport?.Report.Run.Id == reportId)
        {
            currentReport = selectedHistoryReport.Report;
            if (!activeRunSession.Snapshot.IsActive)
            {
                activeProfile = selectedHistoryReport.Report.Run.Profile;
            }
            currentPresentation = DiagnosticReportPresenter.FromReport(selectedHistoryReport.Report);
            return true;
        }

        var stored = (await reportStore.ListAsync())
            .FirstOrDefault(item => item.Report.Run.Id == reportId);
        if (stored is null)
        {
            return false;
        }

        selectedHistoryReport = stored;
        currentReport = stored.Report;
        if (!activeRunSession.Snapshot.IsActive)
        {
            activeProfile = stored.Report.Run.Profile;
        }
        currentPresentation = DiagnosticReportPresenter.FromReport(stored.Report);
        RenderPresentation(currentPresentation);
        return true;
    }

    private void NavigateToWorkspace(WorkspaceKind workspace)
    {
        PreserveCurrentNavigationState();

        if (lastWorkspaceEntries.TryGetValue(workspace, out var previous))
        {
            navigationService.Navigate(previous.Destination, previous.ViewState);
            return;
        }

        navigationService.Navigate(DefaultDestination(workspace), CaptureCurrentViewState());
    }

    private void NavigateToDestination(AppDestination destination)
    {
        PreserveCurrentNavigationState();
        navigationService.Navigate(destination, CaptureCurrentViewState());
    }

    private void WorkbenchBackRequested(object? sender, EventArgs eventArgs)
    {
        PreserveCurrentNavigationState();
        navigationService.GoBack();
    }

    private void WorkbenchForwardRequested(object? sender, EventArgs eventArgs)
    {
        PreserveCurrentNavigationState();
        navigationService.GoForward();
    }

    private void WorkbenchActiveRunRequested(object? sender, EventArgs eventArgs) =>
        ReturnToActiveRun();

    private void WorkbenchWorkspaceRequested(object? sender, WorkspaceRequestedEventArgs eventArgs) =>
        NavigateToWorkspace(eventArgs.Workspace);

    private void WorkbenchDestinationRequested(object? sender, DestinationRequestedEventArgs eventArgs) =>
        NavigateToDestination(eventArgs.Destination);

    private async void WorkbenchInspectorVisibilityChanged(object? sender, EventArgs eventArgs)
    {
        PreserveCurrentNavigationState();
        await PersistWorkbenchStateAsync();
    }

    private void WorkbenchKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        var commandPalette = eventArgs.Key == Key.K
            && (eventArgs.KeyModifiers.HasFlag(KeyModifiers.Meta)
                || eventArgs.KeyModifiers.HasFlag(KeyModifiers.Control));
        if (commandPalette)
        {
            WorkbenchCommandPaletteRequested(sender, EventArgs.Empty);
            eventArgs.Handled = true;
            return;
        }

        if (eventArgs.Key == Key.Escape && workbenchShell?.CommandPaletteOpen == true)
        {
            workbenchShell.CloseCommandPalette();
            eventArgs.Handled = true;
            return;
        }

        var back = eventArgs.Key == Key.Left && eventArgs.KeyModifiers.HasFlag(KeyModifiers.Alt)
            || eventArgs.Key == Key.OemOpenBrackets && eventArgs.KeyModifiers.HasFlag(KeyModifiers.Meta)
            || eventArgs.Key == Key.BrowserBack;
        var forward = eventArgs.Key == Key.Right && eventArgs.KeyModifiers.HasFlag(KeyModifiers.Alt)
            || eventArgs.Key == Key.OemCloseBrackets && eventArgs.KeyModifiers.HasFlag(KeyModifiers.Meta)
            || eventArgs.Key == Key.BrowserForward;

        if (back)
        {
            WorkbenchBackRequested(sender, EventArgs.Empty);
            eventArgs.Handled = true;
        }
        else if (forward)
        {
            WorkbenchForwardRequested(sender, EventArgs.Empty);
            eventArgs.Handled = true;
        }
    }

    private void WorkbenchPointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        var updateKind = eventArgs.GetCurrentPoint(this).Properties.PointerUpdateKind;
        if (updateKind == PointerUpdateKind.XButton1Pressed)
        {
            WorkbenchBackRequested(sender, EventArgs.Empty);
            eventArgs.Handled = true;
        }
        else if (updateKind == PointerUpdateKind.XButton2Pressed)
        {
            WorkbenchForwardRequested(sender, EventArgs.Empty);
            eventArgs.Handled = true;
        }
    }

    private void PreserveCurrentNavigationState()
    {
        if (navigationService.Current is null) return;
        navigationService.UpdateCurrentState(CaptureCurrentViewState());
    }

    private NavigationViewState CaptureCurrentViewState()
    {
        var inspectorOpen = workbenchShell?.InspectorOpen ?? true;
        if (navigationService.Current?.Destination is ReportListDestination
            && reportBrowserWorkspace is not null)
        {
            var browserState = reportBrowserWorkspace.CaptureState();
            return new NavigationViewState(
                SearchQuery: browserState.SearchQuery,
                SortKey: browserState.SortKey,
                SortDescending: browserState.SortDescending,
                SelectedReportId: browserState.SelectedReportId,
                InspectorOpen: inspectorOpen);
        }

        return new NavigationViewState(
            SelectedReportId: selectedHistoryReport?.Report.Run.Id,
            ResultSection: navigationService.Current?.Destination switch
            {
                TestResultDestination result => result.Section,
                ReportDetailDestination detail => detail.Section,
                SettingsDestination settingsDestination => settingsDestination.Section,
                _ => null
            },
            InspectorOpen: inspectorOpen);
    }

    private void RestoreViewState(NavigationViewState state)
    {
        workbenchShell?.SetInspectorOpen(state.InspectorOpen);
    }

    private void ShowArea(DesktopArea area)
    {
        HideRedesignedWorkspaces();
        TestArea.IsVisible = area == DesktopArea.Test;
        if (area == DesktopArea.Test) ShowTestState(currentTestState);
    }

    private void ShowWorkspaceSurface(Control? surface)
    {
        TestArea.IsVisible = false;
        HideRedesignedWorkspaces();
        if (surface is not null) surface.IsVisible = true;
    }

    private void HideRedesignedWorkspaces()
    {
        if (reportBrowserWorkspace is not null) reportBrowserWorkspace.IsVisible = false;
        if (reportDetailWorkspace is not null) reportDetailWorkspace.IsVisible = false;
        if (comparisonWorkspace is not null) comparisonWorkspace.IsVisible = false;
        if (settingsWorkspace is not null) settingsWorkspace.IsVisible = false;
    }

    private void ShowTestState(TestViewState state)
    {
        var previousState = currentTestState;
        var requestedState = state;

        // Running and completed diagnostics are control-center states. There is one
        // mounted test surface for the entire idle -> running -> result handoff.
        currentTestState = TestViewState.Setup;
        SetupView.IsVisible = true;

        if (requestedState == TestViewState.Running && previousState != TestViewState.Running)
        {
            activeRunNavigationId = activeRunSession.Snapshot.IsActive
                ? activeRunSession.Snapshot.RunId
                : Guid.NewGuid();
        }

        if (!applyingNavigation && navigationService.Current is not null)
        {
            AppDestination destination = requestedState switch
            {
                TestViewState.Running => new RunningTestDestination(activeRunNavigationId),
                TestViewState.Results => new TestResultDestination(currentReport?.Run.Id ?? Guid.Empty),
                _ => new TestSetupDestination()
            };
            NavigateToDestination(destination);
        }

        RefreshWorkbenchChrome();
    }

    private void RefreshWorkbenchChrome()
    {
        if (workbenchShell is null) return;

        var interfaceLabel = CompactStatusValue(preflightInterface);
        var endpointLabel = CompactStatusValue(preflightEndpoint);
        var networkLabel = CompactStatusValue(preflightNetwork);
        var session = activeRunSession.Snapshot;
        var running = session.IsActive;
        string activity = running
            ? $"{CompactStatusValue(session.Detail)} · {session.Progress:0}%"
            : navigationService.Current?.Destination.Workspace switch
            {
                WorkspaceKind.Reports => reportBrowserWorkspace is null
                    ? $"{savedReportCount} reports"
                    : $"{reportBrowserWorkspace.VisibleReportCount} reports",
                WorkspaceKind.Comparisons => comparisonCandidateId is null ? "Choose reports" : "Comparison ready",
                WorkspaceKind.Settings => "Settings",
                _ => "Ready"
            };

        workbenchShell.SetStatus(interfaceLabel, endpointLabel, networkLabel, activity);
        workbenchShell.SetActiveRun(
            running,
            $"{DiagnosticReportPresenter.ProfileName(session.Profile)} test",
            $"{session.Phase} · {session.Detail}",
            session.Progress);
        workbenchShell.SetInspectorBody(null);

        var destination = navigationService.Current?.Destination;
        switch (destination)
        {
            case TestSetupDestination:
                SyncTestWorkspace();
                workbenchShell.SetInspectorContent(
                    "Test configuration",
                    "Keep common choices in the setup workspace. Interface, privacy, preflight, endpoints, LAN isolation, and approvals remain contextual.",
                    $"{DiagnosticReportPresenter.ProfileName(SelectedProfile())} · {MethodName(SelectedMethod())}");
                workbenchShell.SetInspectorBody(testConfigurationPanel);
                break;
            case RunningTestDestination:
                workbenchShell.SetInspectorContent(
                    "Active diagnostic",
                    "The live diagnostic stays in the Tests & diagnostics tile while the rest of the control center remains available.",
                    CompactStatusValue(session.Detail));
                break;
            case TestResultDestination:
                workbenchShell.SetInspectorContent(
                    "Test result",
                    "The completed result opens over the persistent control center and shares its viewer with saved diagnostics.",
                    currentReport is null ? "Preview" : DiagnosticReportPresenter.ProfileName(currentReport.Run.Profile));
                break;
            case ReportDetailDestination:
                workbenchShell.SetInspectorContent(
                    "Saved report",
                    selectedHistoryReport is null
                        ? "The selected report is unavailable."
                        : ReportComparisonService.ContextLabel(selectedHistoryReport.Report),
                    selectedHistoryReport?.Label ?? currentPresentation.Label);
                break;
            case ReportListDestination:
                workbenchShell.SetInspectorContent(
                    "Report browser",
                    "Search and sorting are preserved in navigation history. Select a row for report, annotation, and comparison actions.",
                    reportBrowserWorkspace is null ? "Reports" : $"{reportBrowserWorkspace.VisibleReportCount} visible");
                break;
            case ComparisonDestination:
                workbenchShell.SetInspectorContent(
                    "Comparison context",
                    "Baseline and candidate remain explicit. Context differences produce cautions instead of silently invalidating the comparison.",
                    comparisonCandidateId is null ? "Select two reports" : "Baseline and candidate selected");
                break;
            case SettingsDestination settingsDestination:
                SyncSettingsWorkspace(settingsDestination.Section);
                workbenchShell.SetInspectorContent(
                    settingsDestination.Section,
                    "Settings are organized by purpose and participate in the same back and forward history as every other workspace.",
                    settingsDestination.Section);
                break;
        }
    }

    private static AppDestination DefaultDestination(WorkspaceKind workspace) => workspace switch
    {
        WorkspaceKind.Reports => new ReportListDestination(),
        WorkspaceKind.Comparisons => new ComparisonDestination(),
        WorkspaceKind.Settings => new SettingsDestination(),
        _ => new TestSetupDestination()
    };

    private static string CompactStatusValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var firstLine = value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? string.Empty;
        return firstLine.Length <= 42 ? firstLine : $"{firstLine[..39]}…";
    }

    private TestProfileId SelectedProfile() => selectedProfileIndex switch
    {
        1 => TestProfileId.Quick,
        2 => TestProfileId.Standard,
        3 => TestProfileId.Extended,
        _ => TestProfileId.ConnectionCheck
    };

    private TransferMethod SelectedMethod() => selectedMethodIndex switch
    {
        1 => TransferMethod.Single,
        2 => TransferMethod.Aggregate,
        _ => TransferMethod.Compare
    };

    private static int ProfileIndex(TestProfileId profile) => profile switch
    {
        TestProfileId.Quick => 1,
        TestProfileId.Standard => 2,
        TestProfileId.Extended => 3,
        _ => 0
    };

    private static int MethodIndex(TransferMethod method) => method switch
    {
        TransferMethod.Single => 1,
        TransferMethod.Aggregate => 2,
        _ => 0
    };
}
