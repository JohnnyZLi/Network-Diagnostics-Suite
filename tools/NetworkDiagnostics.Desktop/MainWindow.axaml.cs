using Avalonia.Controls;
using Avalonia.Platform.Storage;
using NetworkDeepProbe.Diagnostics;
using NetworkDeepProbe.Models;
using NetworkDeepProbe.Planning;
using NetworkDiagnostics.Desktop.Models;
using NetworkDiagnostics.Desktop.Monitoring;
using NetworkDiagnostics.Desktop.Navigation;
using NetworkDiagnostics.Desktop.Presentation;
using NetworkDiagnostics.Desktop.Services;
using NetworkDiagnostics.Desktop.Shell;
using NetworkDiagnostics.Desktop.Workspaces;

namespace NetworkDiagnostics.Desktop;

public sealed partial class MainWindow : Window
{
    private static readonly FilePickerFileType JsonReportFileType = new("Network Diagnostics report")
    {
        Patterns = ["*.json"],
        MimeTypes = ["application/json"]
    };

    private readonly DiagnosticRunService diagnosticRunService = new();
    private readonly DesktopSettingsStore settingsStore = new();
    private readonly NavigationService navigationService = new();
    private readonly ActiveRunSession activeRunSession = new();
    private readonly Dictionary<WorkspaceKind, NavigationEntry> lastWorkspaceEntries = new();
    private readonly ReportStore reportStore;
    private readonly ContinuousMonitorService monitoringService;
    private CancellationTokenSource? runCancellation => activeRunSession.CancellationSource;
    private CancellationTokenSource? preflightCancellation;
    private CancellationTokenSource? lanServerCancellation;
    private WorkbenchShell? workbenchShell;
    private TestSetupWorkspace? testSetupWorkspace;
    private TestConfigurationPanel? testConfigurationPanel;
    private RunningTestWorkspace? runningTestWorkspace;
    private TestResultWorkspace? testResultWorkspace;
    private ReportBrowserWorkspace? reportBrowserWorkspace;
    private ReportDetailWorkspace? reportDetailWorkspace;
    private ComparisonWorkspace? comparisonWorkspace;
    private SettingsWorkspace? settingsWorkspace;
    private bool initialized;
    private bool settingsLoaded;
    private bool applyingNavigation;
    private int selectedProfileIndex;
    private int selectedMethodIndex;
    private int selectedInterfaceIndex;
    private int fixtureIndex;
    private int savedReportCount;
    private bool lanServerRunning;
    private TestViewState currentTestState = TestViewState.Setup;
    private ConnectionCheckPresentation currentPresentationValue = ConnectionCheckFixtures.All[0];
    private ConnectionCheckPresentation currentPresentation
    {
        get => currentPresentationValue;
        set
        {
            currentPresentationValue = value;
            SyncRunResultWorkspaces();
        }
    }
    private DesktopSettings settings = new();
    private MonitorWindow monitorWindow = MonitorWindow.FiveMinutes;
    private NetworkDiagnosticsReportV2? currentReport;
    private NetworkDiagnosticsReportV2? comparisonBaselineReport;
    private StoredReport? selectedHistoryReport { get; set; }
    private TestProfileId activeProfile = TestProfileId.ConnectionCheck;
    private TransferMethod activeMethod = TransferMethod.Compare;
    private IReadOnlyList<NetworkInterfaceChoice> interfaceChoices = [];
    private IReadOnlyList<string> interfaceLabels = ["Automatic system routing"];
    private double displayedRunProgress;
    private Guid activeRunNavigationId = Guid.NewGuid();
    private string profileQuestion = "Is the connection working normally?";
    private string profilePurpose = "A lightweight first-party reachability, latency, request-loss, download, and upload check with a clear verdict.";
    private string methodExplanation = "Measures isolated and aggregate behavior separately.";
    private string estimatedTime = "—";
    private string transferCap = "—";
    private string confirmation = "Not required";
    private string profileAvailability = "Connection Check runs the real native engine and saves its report locally when complete.";
    private string preflightNetwork = string.Empty;
    private string preflightEndpoint = string.Empty;
    private string preflightInterface = "Automatic system routing";
    private string testOriginsText = string.Empty;
    private string lanTargetText = string.Empty;
    private string lanPortText = "8765";
    private string lanDurationText = "8";
    private string lanConnectionsText = "4";
    private string lanServerStatus = "Server stopped.";
    private string settingsStatus = string.Empty;

    public MainWindow()
    {
        reportStore = new ReportStore(settingsStore.RootDirectory);
        monitoringService = new ContinuousMonitorService(settingsStore.RootDirectory);
        InitializeComponent();
        InstallTestWorkspace();
        InstallWorkbenchShell();
        initialized = true;
        RenderProfileSelection();
        RenderMethodSelection();
        SyncTestWorkspace();
        ShowArea(DesktopArea.Test);
        ShowTestState(TestViewState.Setup);
        InitializeNavigation();
        Opened += WindowOpened;
        Closed += WindowClosed;
    }

    private async void WindowOpened(object? sender, EventArgs eventArgs)
    {
        initialized = false;
        settingsLoaded = false;
        settings = await settingsStore.LoadAsync();
        ApplyAppearance(settings.Appearance);
        ApplyAccessibilityPreferences();
        monitorWindow = settings.SelectedMonitoringWindow;
        reportStore.Configure(settings.ReportDirectory);
        selectedProfileIndex = ProfileIndex(settings.SelectedProfile);
        selectedMethodIndex = MethodIndex(settings.SelectedTransferMethod);
        testOriginsText = string.Join(Environment.NewLine, settings.ParsedTestOrigins.Select(uri => uri.ToString()));
        lanTargetText = settings.LanTarget ?? string.Empty;
        lanPortText = settings.LanPort.ToString(System.Globalization.CultureInfo.InvariantCulture);
        lanDurationText = settings.LanDurationSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        lanConnectionsText = settings.LanConnections.ToString(System.Globalization.CultureInfo.InvariantCulture);
        PopulateInterfaceChoices();
        initialized = true;
        RenderProfileSelection();
        RenderMethodSelection();
        await RefreshHistoryAsync();
        await RefreshPreflightAsync();
        await InitializeMonitoringAsync();
        await UpdateTrayIntegrationAsync();
        SyncTestWorkspace();
        SyncSettingsWorkspace();
        settingsLoaded = true;
        await RestorePersistedWorkbenchStateAsync();
        RefreshWorkbenchChrome();
    }

    private void WindowClosed(object? sender, EventArgs eventArgs)
    {
        activeRunSession.Dispose();
        preflightCancellation?.Cancel();
        lanServerCancellation?.Cancel();
        if (liveTrayIcon is not null) liveTrayIcon.IsVisible = false;
        _ = monitoringService.DisposeAsync();
    }

    private void PopulateInterfaceChoices()
    {
        interfaceChoices = diagnosticRunService.ListInterfaces();
        interfaceLabels = new[] { "Automatic system routing" }
            .Concat(interfaceChoices.Select(choice =>
            {
                var speed = choice.LinkSpeedMbps is null ? string.Empty : $" · {choice.LinkSpeedMbps:N0} Mbps";
                return $"{choice.Name} · {choice.Type}{speed}";
            }))
            .ToArray();
        selectedInterfaceIndex = interfaceChoices
            .Select((choice, index) => new { choice, index })
            .Where(item => string.Equals(item.choice.Id, settings.InterfaceId, StringComparison.Ordinal))
            .Select(item => item.index + 1)
            .FirstOrDefault();
    }

    private string? SelectedInterfaceId() => selectedInterfaceIndex <= 0
        ? null
        : interfaceChoices.ElementAtOrDefault(selectedInterfaceIndex - 1)?.Id;
}
