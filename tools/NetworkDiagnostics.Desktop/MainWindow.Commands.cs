using Avalonia.Interactivity;
using NetworkDiagnostics.Desktop.Navigation;
using NetworkDiagnostics.Desktop.Shell;

namespace NetworkDiagnostics.Desktop;

public sealed partial class MainWindow
{
    private void WorkbenchCommandPaletteRequested(object? sender, EventArgs eventArgs)
    {
        if (workbenchShell is null) return;
        if (workbenchShell.CommandPaletteOpen)
        {
            workbenchShell.CloseCommandPalette();
            return;
        }
        workbenchShell.OpenCommandPalette(BuildWorkbenchCommands());
    }

    private async void WorkbenchCommandInvoked(object? sender, CommandInvokedEventArgs eventArgs)
    {
        switch (eventArgs.Command.Id)
        {
            case "navigate.test":
                NavigateToDestination(new TestSetupDestination());
                break;
            case "navigate.reports":
                NavigateToWorkspace(WorkspaceKind.Reports);
                break;
            case "navigate.comparisons":
                NavigateToWorkspace(WorkspaceKind.Comparisons);
                break;
            case "navigate.settings.general":
                NavigateToDestination(new SettingsDestination("General"));
                break;
            case "navigate.settings.monitoring":
                NavigateToDestination(new SettingsDestination("Monitoring"));
                break;
            case "navigate.settings.measurement":
                NavigateToDestination(new SettingsDestination("Measurement"));
                break;
            case "navigate.settings.privacy":
                NavigateToDestination(new SettingsDestination("Privacy & data"));
                break;
            case "navigate.settings.storage":
                NavigateToDestination(new SettingsDestination("Storage"));
                break;
            case "navigate.settings.developer":
                NavigateToDestination(new SettingsDestination("Developer"));
                break;
            case "developer.preview-healthy":
                fixtureIndex = 0;
                PreviewFixture();
                break;
            case "monitor.toggle":
                TestSetupMonitoringToggleRequested(sender, EventArgs.Empty);
                break;
            case "monitor.content-speed":
                await RunContentSpeedTestAsync();
                break;
            case "monitor.peak-speed":
                await RunPeakSpeedTestAsync();
                break;
            case "monitor.copy":
                await CopyMonitoringSummaryAsync();
                break;
            case "monitor.share":
                await ExportMonitoringSnapshotAsync();
                break;
            case "monitor.export":
                await ExportMonitoringHistoryAsync();
                break;
            case "test.run":
                RunClicked(sender, new RoutedEventArgs());
                break;
            case "test.active":
                ReturnToActiveRun();
                break;
            case "reports.import":
                await ImportReportAsync();
                break;
            case "reports.folder":
                reportStore.OpenReportsFolder();
                break;
            case "preflight.refresh":
                await RefreshPreflightAsync();
                SyncTestWorkspace();
                SyncSettingsWorkspace();
                RefreshWorkbenchChrome();
                break;
            case "view.inspector":
                if (workbenchShell is not null)
                {
                    workbenchShell.SetInspectorOpen(!workbenchShell.InspectorOpen);
                    PreserveCurrentNavigationState();
                    await PersistWorkbenchStateAsync();
                }
                break;
            case "navigation.back":
                WorkbenchBackRequested(sender, EventArgs.Empty);
                break;
            case "navigation.forward":
                WorkbenchForwardRequested(sender, EventArgs.Empty);
                break;
        }
    }

    private IReadOnlyList<WorkbenchCommand> BuildWorkbenchCommands()
    {
        var session = activeRunSession.Snapshot;
        var commands = new List<WorkbenchCommand>
        {
            new("navigate.test", "Open network overview", "Show the live score, timelines, alerts, and diagnostic controls.", "home overview monitor score network", "T", Priority: 0),
            new("monitor.toggle", settings.MonitoringEnabled ? "Pause continuous monitoring" : "Start continuous monitoring", "Toggle lightweight reachability and response-time sampling.", "monitor pause resume continuous", Priority: 1),
            new("monitor.content-speed", "Run content-speed test", "Add a low-data content download and upload result to Speed history.", "speed content download upload", Enabled: !session.IsActive, Priority: 2),
            new("monitor.peak-speed", "Run peak-speed test", "Run the Stress profile after its normal data confirmation.", "speed peak capacity stress", Enabled: !session.IsActive, Priority: 3),
            new("monitor.copy", "Copy network summary", "Copy the selected time-window score and component summary.", "share clipboard score summary", Priority: 4),
            new("monitor.share", "Export shareable snapshot", "Create a self-contained HTML network-health snapshot.", "share html snapshot export", Priority: 5),
            new("monitor.export", "Export monitoring history", "Export the selected time window as a privacy-aware CSV file.", "csv history data export", Priority: 6),
            new("test.run", "Run selected diagnostic", "Start the currently selected explicit profile and transfer method.", "start run diagnostic connection", Enabled: !session.IsActive, Priority: 8),
            new("navigate.reports", "Open reports", "Browse, search, label, import, and export saved diagnostic reports.", "history library saved reports", "R", Priority: 10),
            new("reports.import", "Import report JSON", "Import a website or desktop schema 2.0 report.", "json file report import", Priority: 11),
            new("navigate.comparisons", "Open comparisons", "Choose an explicit baseline and candidate report.", "compare baseline candidate trend", "C", Priority: 12),
            new("navigate.settings.general", "Settings: General", "Appearance, interface, tray, background, and accessibility.", "settings interface tray appearance", Priority: 20),
            new("navigate.settings.monitoring", "Settings: Monitoring", "Sampling cadence, speed expectations, and alert threshold.", "settings monitor interval score alert speed", Priority: 21),
            new("navigate.settings.measurement", "Settings: Diagnostics", "Profiles, endpoint candidates, and LAN isolation.", "settings endpoint origin lan server path", Priority: 22),
            new("navigate.settings.privacy", "Settings: Privacy & data", "Local identifiers and remembered data approvals.", "settings privacy identifiers approval data", Priority: 23),
            new("navigate.settings.storage", "Settings: Storage", "Inspect and open the local data directory.", "settings storage folder reports history", Priority: 24),
            new("navigate.settings.developer", "Settings: Developer", "Preview result states without running the engine.", "settings developer fixture preview", Priority: 25),
            new("developer.preview-healthy", "Preview healthy result", "Open the healthy-connection presentation fixture without running a diagnostic.", "developer fixture preview healthy result", Priority: 26),
            new("preflight.refresh", "Refresh connection preflight", "Probe the current interface, endpoint candidates, and network context.", "refresh endpoint latency network interface", Priority: 30),
            new("reports.folder", "Open data folder", "Open the local application-data directory in the system file manager.", "storage directory finder explorer", Priority: 31),
            new("view.inspector", workbenchShell?.InspectorOpen == true ? "Hide information drawer" : "Show information drawer", "Toggle contextual information and controls.", "view panel info details", Priority: 40),
            new("navigation.back", "Go back", "Return to the previous application destination.", "history navigation previous", Enabled: navigationService.CanGoBack, Priority: 45),
            new("navigation.forward", "Go forward", "Advance to the next application destination.", "history navigation next", Enabled: navigationService.CanGoForward, Priority: 46)
        };

        if (session.IsActive || session.ReportId is not null)
        {
            commands.Insert(1, new WorkbenchCommand(
                "test.active",
                session.IsActive ? "Return to active diagnostic" : "Open latest result",
                session.IsActive ? $"{session.Phase} · {session.Detail}" : "Return to the most recently completed diagnostic result.",
                "active running progress result",
                Priority: 1));
        }

        return commands;
    }
}
