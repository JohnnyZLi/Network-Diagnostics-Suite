using Avalonia.Controls;
using Avalonia.Platform.Storage;
using NetworkDeepProbe.Models;
using NetworkDeepProbe.Planning;
using NetworkDiagnostics.Desktop.Models;
using NetworkDiagnostics.Desktop.Presentation;
using NetworkDiagnostics.Desktop.Services;

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
    private readonly ReportStore reportStore;
    private CancellationTokenSource? runCancellation;
    private bool initialized;
    private TestViewState currentTestState = TestViewState.Setup;
    private ConnectionCheckPresentation currentPresentation = ConnectionCheckFixtures.All[0];
    private DesktopSettings settings = new();
    private NetworkDiagnosticsReportV2? currentReport;
    private TestProfileId activeProfile = TestProfileId.ConnectionCheck;
    private double displayedRunProgress;

    public MainWindow()
    {
        reportStore = new ReportStore(settingsStore.RootDirectory);
        InitializeComponent();
        ProfileSelector.SelectedIndex = 0;
        FixtureSelector.SelectedIndex = 0;
        initialized = true;
        RenderProfileSelection();
        RenderPresentation(currentPresentation);
        ShowArea(DesktopArea.Test);
        ShowTestState(TestViewState.Setup);
        Opened += WindowOpened;
    }

    private async void WindowOpened(object? sender, EventArgs eventArgs)
    {
        initialized = false;
        settings = await settingsStore.LoadAsync();
        reportStore.Configure(settings.ReportDirectory);
        ProfileSelector.SelectedIndex = ProfileIndex(settings.SelectedProfile);
        IncludeIdentifiersCheckBox.IsChecked = settings.IncludeLocalIdentifiers;
        TestOriginTextBox.Text = settings.TestOrigin ?? string.Empty;
        ReportsFolderText.Text = reportStore.ReportsDirectory;
        initialized = true;
        RenderProfileSelection();
        await RefreshHistoryAsync();
    }
}
