using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using NetworkDeepProbe.Planning;
using NetworkDiagnostics.Desktop.Models;
using NetworkDiagnostics.Desktop.Navigation;
using NetworkDiagnostics.Desktop.Presentation;
using NetworkDiagnostics.Desktop.Services;
using NetworkDiagnostics.Desktop.Shell;

namespace NetworkDiagnostics.Desktop;

public sealed partial class MainWindow
{
    private void InstallWorkbenchShell()
    {
        MinWidth = 720;
        MinHeight = 560;

        if (Content is not Grid legacyRoot)
        {
            return;
        }

        var workspace = legacyRoot.Children.FirstOrDefault(control => Grid.GetRow(control) == 1);
        if (workspace is null)
        {
            return;
        }

        legacyRoot.Children.Remove(workspace);
        Grid.SetRow(workspace, 0);

        workbenchShell = new WorkbenchShell
        {
            WorkspaceContent = workspace
        };
        workbenchShell.BackRequested += WorkbenchBackRequested;
        workbenchShell.ForwardRequested += WorkbenchForwardRequested;
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

    private void TestNavClicked(object? sender, RoutedEventArgs eventArgs) =>
        NavigateToWorkspace(WorkspaceKind.Test);

    private void HistoryNavClicked(object? sender, RoutedEventArgs eventArgs) =>
        NavigateToWorkspace(WorkspaceKind.Reports);

    private void SettingsNavClicked(object? sender, RoutedEventArgs eventArgs) =>
        NavigateToWorkspace(WorkspaceKind.Settings);

    private async void ProfileSelectionChanged(object? sender, SelectionChangedEventArgs eventArgs)
    {
        if (!initialized) return;
        RenderProfileSelection();
        settings = settings with { DefaultProfile = DesktopSettings.ContractId(SelectedProfile()) };
        await PersistSettingsAsync();
        await RefreshPreflightAsync();
        RefreshWorkbenchChrome();
    }

    private async void MethodSelectionChanged(object? sender, SelectionChangedEventArgs eventArgs)
    {
        if (!initialized) return;
        RenderMethodSelection();
        settings = settings with { DefaultTransferMethod = DesktopSettings.ContractId(SelectedMethod()) };
        await PersistSettingsAsync();
        RenderProfileSelection();
        await RefreshPreflightAsync();
        RefreshWorkbenchChrome();
    }

    private void ConnectionProfileClicked(object? sender, RoutedEventArgs eventArgs) => SelectProfile(0);

    private void QuickProfileClicked(object? sender, RoutedEventArgs eventArgs) => SelectProfile(1);

    private void FullProfileClicked(object? sender, RoutedEventArgs eventArgs) => SelectProfile(2);

    private void StressProfileClicked(object? sender, RoutedEventArgs eventArgs) => SelectProfile(3);

    private void CompareMethodClicked(object? sender, RoutedEventArgs eventArgs) => SelectMethod(0);

    private void SingleMethodClicked(object? sender, RoutedEventArgs eventArgs) => SelectMethod(1);

    private void AggregateMethodClicked(object? sender, RoutedEventArgs eventArgs) => SelectMethod(2);

    private void SelectProfile(int index)
    {
        if (ProfileSelector.SelectedIndex == index)
        {
            RenderProfileSelection();
            RefreshWorkbenchChrome();
            return;
        }
        ProfileSelector.SelectedIndex = index;
    }

    private void SelectMethod(int index)
    {
        if (MethodSelector.SelectedIndex == index)
        {
            RenderMethodSelection();
            RefreshWorkbenchChrome();
            return;
        }
        MethodSelector.SelectedIndex = index;
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
                    ShowArea(DesktopArea.Test);
                    ShowTestState(TestViewState.Setup);
                    break;

                case RunningTestDestination:
                    ShowArea(DesktopArea.Test);
                    ShowTestState(TestViewState.Running);
                    break;

                case TestResultDestination result:
                    if (result.ReportId != Guid.Empty)
                    {
                        await LoadReportForNavigationAsync(result.ReportId);
                    }
                    ShowArea(DesktopArea.Test);
                    ShowTestState(TestViewState.Results);
                    break;

                case ReportListDestination:
                    await RefreshHistoryAsync();
                    ShowArea(DesktopArea.History);
                    break;

                case ReportDetailDestination detail:
                    if (!await LoadReportForNavigationAsync(detail.ReportId))
                    {
                        navigationService.Navigate(new ReportListDestination(), replaceCurrent: true);
                        return;
                    }
                    ShowArea(DesktopArea.Test);
                    ShowTestState(TestViewState.Results);
                    break;

                case ComparisonDestination comparison:
                    comparisonBaselineId = comparison.BaselineId;
                    comparisonCandidateId = comparison.CandidateId;
                    await RefreshComparisonHistoryAsync();
                    ShowArea(DesktopArea.History);
                    break;

                case SettingsDestination:
                    ShowArea(DesktopArea.Settings);
                    break;
            }

            RestoreViewState(eventArgs.Current.ViewState);
            RefreshWorkbenchChrome();
        }
        finally
        {
            applyingNavigation = false;
        }
    }

    private async Task<bool> LoadReportForNavigationAsync(Guid reportId)
    {
        if (currentReport?.Run.Id == reportId)
        {
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
        activeProfile = stored.Report.Run.Profile;
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

    private void WorkbenchWorkspaceRequested(object? sender, WorkspaceRequestedEventArgs eventArgs) =>
        NavigateToWorkspace(eventArgs.Workspace);

    private void WorkbenchDestinationRequested(object? sender, DestinationRequestedEventArgs eventArgs) =>
        NavigateToDestination(eventArgs.Destination);

    private void WorkbenchInspectorVisibilityChanged(object? sender, EventArgs eventArgs) =>
        PreserveCurrentNavigationState();

    private void WorkbenchKeyDown(object? sender, KeyEventArgs eventArgs)
    {
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

    private NavigationViewState CaptureCurrentViewState() => new(
        SelectedReportId: selectedHistoryReport?.Report.Run.Id,
        ResultSection: navigationService.Current?.Destination switch
        {
            TestResultDestination result => result.Section,
            ReportDetailDestination detail => detail.Section,
            _ => null
        },
        InspectorOpen: workbenchShell?.InspectorOpen ?? true);

    private void RestoreViewState(NavigationViewState state)
    {
        if (state.SelectedReportId is { } selectedId
            && selectedHistoryReport?.Report.Run.Id != selectedId)
        {
            selectedHistoryReport = null;
        }
        workbenchShell?.SetInspectorOpen(state.InspectorOpen);
    }

    private void ShowArea(DesktopArea area)
    {
        TestArea.IsVisible = area == DesktopArea.Test;
        HistoryArea.IsVisible = area == DesktopArea.History;
        SettingsArea.IsVisible = area == DesktopArea.Settings;

        SetActiveState(TestNavButton, area == DesktopArea.Test);
        SetActiveState(HistoryNavButton, area == DesktopArea.History);
        SetActiveState(SettingsNavButton, area == DesktopArea.Settings);

        if (area == DesktopArea.Test) ShowTestState(currentTestState);
    }

    private void ShowTestState(TestViewState state)
    {
        var previousState = currentTestState;
        currentTestState = state;
        SetupView.IsVisible = state == TestViewState.Setup;
        RunningView.IsVisible = state == TestViewState.Running;
        ResultsView.IsVisible = state == TestViewState.Results;

        if (state == TestViewState.Running && previousState != TestViewState.Running)
        {
            activeRunNavigationId = Guid.NewGuid();
        }

        if (!applyingNavigation && navigationService.Current is not null)
        {
            AppDestination destination = state switch
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

        var interfaceLabel = CompactStatusValue(PreflightInterfaceText.Text);
        var endpointLabel = CompactStatusValue(PreflightEndpointText.Text);
        var networkLabel = CompactStatusValue(PreflightNetworkText.Text);
        var running = currentTestState == TestViewState.Running && runCancellation is not null;
        string activity = running
            ? $"{CompactStatusValue(CurrentPhaseText.Text)} · {displayedRunProgress:0}%"
            : navigationService.Current?.Destination.Workspace switch
            {
                WorkspaceKind.Reports => HistoryCountText.Text ?? "Reports",
                WorkspaceKind.Comparisons => comparisonCandidateId is null ? "Choose reports" : "Comparison ready",
                WorkspaceKind.Settings => "Settings",
                _ => "Ready"
            };

        workbenchShell.SetStatus(interfaceLabel, endpointLabel, networkLabel, activity);
        workbenchShell.SetActiveRun(
            running,
            $"{DiagnosticReportPresenter.ProfileName(activeProfile)} test",
            CompactStatusValue(CurrentPhaseText.Text),
            displayedRunProgress);

        var destination = navigationService.Current?.Destination;
        switch (destination)
        {
            case TestSetupDestination:
                workbenchShell.SetInspectorContent(
                    "Test configuration",
                    "Common test choices stay in the workspace. Endpoint, interface, LAN, and advanced details remain contextual.",
                    $"{DiagnosticReportPresenter.ProfileName(SelectedProfile())} · {MethodName(SelectedMethod())}");
                break;
            case RunningTestDestination:
                workbenchShell.SetInspectorContent(
                    "Active diagnostic",
                    "The run continues when another workspace is opened. Return through the active-test item or navigation history.",
                    CompactStatusValue(CurrentPhaseText.Text));
                break;
            case TestResultDestination:
            case ReportDetailDestination:
                workbenchShell.SetInspectorContent(
                    "Report evidence",
                    "Metadata, configuration, export, and file actions will live here as the Reports vertical slice replaces the legacy result panel.",
                    currentReport is null ? "Preview" : DiagnosticReportPresenter.ProfileName(currentReport.Run.Profile));
                break;
            case ReportListDestination:
                workbenchShell.SetInspectorContent(
                    "Report browser",
                    "Search, sorting, selection, labels, export, deletion, and quick preview will share this persistent list context.",
                    HistoryCountText.Text);
                break;
            case ComparisonDestination:
                workbenchShell.SetInspectorContent(
                    "Comparison context",
                    "Baseline and candidate remain explicit. Compatibility warnings are evidence, not blockers.",
                    comparisonCandidateId is null ? "Select two reports" : "Baseline and candidate selected");
                break;
            case SettingsDestination settingsDestination:
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

    private TestProfileId SelectedProfile() => ProfileSelector.SelectedIndex switch
    {
        1 => TestProfileId.Quick,
        2 => TestProfileId.Standard,
        3 => TestProfileId.Extended,
        _ => TestProfileId.ConnectionCheck
    };

    private TransferMethod SelectedMethod() => MethodSelector.SelectedIndex switch
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

    private static void SetActiveState(Button button, bool active)
    {
        if (active)
        {
            if (!button.Classes.Contains("active")) button.Classes.Add("active");
        }
        else
        {
            button.Classes.Remove("active");
        }
    }
}
