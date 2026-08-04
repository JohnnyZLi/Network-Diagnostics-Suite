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
            new("navigate.test", "New diagnostic", "Open the Test workspace and choose a profile.", "test check quick full stress new", "T", Priority: 0),
            new("test.run", "Run selected diagnostic", "Start the currently selected profile and transfer method.", "start run diagnostic connection", Enabled: !session.IsActive, Priority: 2),
            new("navigate.reports", "Open reports", "Browse, search, label, import, and export saved reports.", "history library saved reports", "R", Priority: 5),
            new("reports.import", "Import report JSON", "Import a website or desktop schema 2.0 report.", "json file report import", Priority: 8),
            new("navigate.comparisons", "Open comparisons", "Choose an explicit baseline and candidate report.", "compare baseline candidate trend", "C", Priority: 10),
            new("navigate.settings.general", "Settings: General", "Defaults for profile, transfer method, and interface.", "settings defaults interface", Priority: 20),
            new("navigate.settings.measurement", "Settings: Measurement", "Endpoint candidates and LAN isolation.", "settings endpoint origin lan server path", Priority: 21),
            new("navigate.settings.privacy", "Settings: Privacy & data", "Local identifiers and remembered data approvals.", "settings privacy identifiers approval data", Priority: 22),
            new("navigate.settings.storage", "Settings: Storage", "Inspect and open the local report directory.", "settings storage folder reports", Priority: 23),
            new("navigate.settings.developer", "Settings: Developer", "Preview result states without running the engine.", "settings developer fixture preview", Priority: 24),
            new("preflight.refresh", "Refresh connection preflight", "Probe the current interface, endpoint candidates, and network context.", "refresh endpoint latency network interface", Priority: 30),
            new("reports.folder", "Open reports folder", "Open the local report directory in the system file manager.", "storage directory finder explorer", Priority: 31),
            new("view.inspector", workbenchShell?.InspectorOpen == true ? "Hide inspector" : "Show inspector", "Toggle contextual information and controls.", "view panel info sidebar", Priority: 40),
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
