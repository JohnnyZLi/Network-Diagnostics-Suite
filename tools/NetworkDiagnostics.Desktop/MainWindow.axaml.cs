using Avalonia.Controls;
using Avalonia.Platform.Storage;
using NetworkDeepProbe.Diagnostics;
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
    private CancellationTokenSource? preflightCancellation;
    private CancellationTokenSource? lanServerCancellation;
    private bool initialized;
    private TestViewState currentTestState = TestViewState.Setup;
    private ConnectionCheckPresentation currentPresentation = ConnectionCheckFixtures.All[0];
    private DesktopSettings settings = new();
    private NetworkDiagnosticsReportV2? currentReport;
    private NetworkDiagnosticsReportV2? comparisonBaselineReport;
    private TestProfileId activeProfile = TestProfileId.ConnectionCheck;
    private TransferMethod activeMethod = TransferMethod.Compare;
    private IReadOnlyList<NetworkInterfaceChoice> interfaceChoices = [];
    private double displayedRunProgress;

    public MainWindow()
    {
        reportStore = new ReportStore(settingsStore.RootDirectory);
        InitializeComponent();
        ProfileSelector.SelectedIndex = 0;
        MethodSelector.SelectedIndex = 0;
        FixtureSelector.SelectedIndex = 0;
        initialized = true;
        RenderProfileSelection();
        RenderMethodSelection();
        RenderPresentation(currentPresentation);
        ShowArea(DesktopArea.Test);
        ShowTestState(TestViewState.Setup);
        Opened += WindowOpened;
        Closed += WindowClosed;
    }

    private async void WindowOpened(object? sender, EventArgs eventArgs)
    {
        initialized = false;
        settings = await settingsStore.LoadAsync();
        reportStore.Configure(settings.ReportDirectory);
        ProfileSelector.SelectedIndex = ProfileIndex(settings.SelectedProfile);
        MethodSelector.SelectedIndex = MethodIndex(settings.SelectedTransferMethod);
        IncludeIdentifiersCheckBox.IsChecked = settings.IncludeLocalIdentifiers;
        TestOriginTextBox.Text = string.Join(Environment.NewLine, settings.ParsedTestOrigins.Select(uri => uri.ToString()));
        LanTargetTextBox.Text = settings.LanTarget ?? string.Empty;
        LanPortTextBox.Text = settings.LanPort.ToString(System.Globalization.CultureInfo.InvariantCulture);
        LanDurationTextBox.Text = settings.LanDurationSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        LanConnectionsTextBox.Text = settings.LanConnections.ToString(System.Globalization.CultureInfo.InvariantCulture);
        ReportsFolderText.Text = reportStore.ReportsDirectory;
        PopulateInterfaceSelector();
        initialized = true;
        RenderProfileSelection();
        RenderMethodSelection();
        await RefreshHistoryAsync();
        await RefreshPreflightAsync();
    }

    private void WindowClosed(object? sender, EventArgs eventArgs)
    {
        runCancellation?.Cancel();
        preflightCancellation?.Cancel();
        lanServerCancellation?.Cancel();
    }

    private void PopulateInterfaceSelector()
    {
        interfaceChoices = diagnosticRunService.ListInterfaces();
        InterfaceSelector.Items.Clear();
        InterfaceSelector.Items.Add(new ComboBoxItem { Content = "Automatic system routing", Tag = null });
        foreach (var choice in interfaceChoices)
        {
            var speed = choice.LinkSpeedMbps is null ? string.Empty : $" · {choice.LinkSpeedMbps:N0} Mbps";
            InterfaceSelector.Items.Add(new ComboBoxItem
            {
                Content = $"{choice.Name} · {choice.Type}{speed}",
                Tag = choice.Id
            });
        }
        var selectedIndex = interfaceChoices
            .Select((choice, index) => new { choice, index })
            .Where(item => string.Equals(item.choice.Id, settings.InterfaceId, StringComparison.Ordinal))
            .Select(item => item.index + 1)
            .FirstOrDefault();
        InterfaceSelector.SelectedIndex = selectedIndex;
    }
}
