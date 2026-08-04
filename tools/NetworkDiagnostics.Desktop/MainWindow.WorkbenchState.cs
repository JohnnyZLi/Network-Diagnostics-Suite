using NetworkDiagnostics.Desktop.Navigation;
using NetworkDiagnostics.Desktop.Workspaces;

namespace NetworkDiagnostics.Desktop;

public sealed partial class MainWindow
{
    private Task RestorePersistedWorkbenchStateAsync()
    {
        var restored = WorkbenchStateCodec.Restore(settings.Workbench);
        navigationService.Navigate(restored.Destination, restored.ViewState, replaceCurrent: true);
        return Task.CompletedTask;
    }

    private async Task PersistWorkbenchStateAsync()
    {
        if (!settingsLoaded || navigationService.Current is null) return;

        var viewState = CaptureCurrentViewState();
        navigationService.UpdateCurrentState(viewState);
        var entry = navigationService.Current;
        if (entry is null) return;

        settings = settings with { Workbench = WorkbenchStateCodec.Capture(entry) };
        await PersistSettingsAsync();
    }

    private async void ReportBrowserPersistenceStateChanged(object? sender, ReportBrowserStateChangedEventArgs eventArgs)
    {
        if (navigationService.Current?.Destination is not ReportListDestination) return;
        navigationService.UpdateCurrentState(new NavigationViewState(
            SearchQuery: eventArgs.State.SearchQuery,
            SortKey: eventArgs.State.SortKey,
            SortDescending: eventArgs.State.SortDescending,
            SelectedReportId: eventArgs.State.SelectedReportId,
            InspectorOpen: workbenchShell?.InspectorOpen ?? true));
        await PersistWorkbenchStateAsync();
    }
}
