using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NetworkDiagnostics.Desktop.Workspaces;

public sealed partial class SettingsWorkspace : UserControl
{
    private bool applyingModel;
    private string currentSection = "General";

    public SettingsWorkspace()
    {
        InitializeComponent();
        ShowSection(currentSection);
    }

    public event EventHandler<SettingsSectionRequestedEventArgs>? SectionRequested;
    public event EventHandler<SettingsIndexRequestedEventArgs>? ProfileRequested;
    public event EventHandler<SettingsIndexRequestedEventArgs>? MethodRequested;
    public event EventHandler<SettingsIndexRequestedEventArgs>? AppearanceRequested;
    public event EventHandler<SettingsIndexRequestedEventArgs>? InterfaceRequested;
    public event EventHandler<SettingsBooleanChangedEventArgs>? IdentifiersChanged;
    public event EventHandler? SaveGeneralRequested;
    public event EventHandler? SaveMonitoringRequested;
    public event EventHandler? RefreshPreflightRequested;
    public event EventHandler? SaveMeasurementRequested;
    public event EventHandler? StartLanServerRequested;
    public event EventHandler? StopLanServerRequested;
    public event EventHandler? ResetApprovalsRequested;
    public event EventHandler? OpenReportsFolderRequested;
    public event EventHandler<SettingsIndexRequestedEventArgs>? PreviewRequested;

    public string Origins => OriginsTextBox.Text ?? string.Empty;
    public string LanTarget => LanTargetTextBox.Text ?? string.Empty;
    public string LanPort => LanPortTextBox.Text ?? string.Empty;
    public string LanDuration => LanDurationTextBox.Text ?? string.Empty;
    public string LanConnections => LanConnectionsTextBox.Text ?? string.Empty;
    public bool MonitoringEnabled => MonitoringEnabledCheckBox.IsChecked == true;
    public int MonitoringIntervalIndex => Math.Max(0, MonitoringIntervalComboBox.SelectedIndex);
    public int ContentCadenceIndex => Math.Max(0, ContentCadenceComboBox.SelectedIndex);
    public string ExpectedDownload => ExpectedDownloadTextBox.Text ?? string.Empty;
    public string ExpectedUpload => ExpectedUploadTextBox.Text ?? string.Empty;
    public string AlertThreshold => AlertThresholdTextBox.Text ?? string.Empty;
    public bool StartInBackground => StartInBackgroundCheckBox.IsChecked == true;
    public bool LiveTrayEnabled => LiveTrayCheckBox.IsChecked == true;
    public bool ReduceMotion => ReduceMotionCheckBox.IsChecked == true;
    public bool IncreaseContrast => IncreaseContrastCheckBox.IsChecked == true;

    public void Render(SettingsWorkspaceModel model)
    {
        applyingModel = true;
        try
        {
            ProfileComboBox.SelectedIndex = model.ProfileIndex;
            MethodComboBox.SelectedIndex = model.MethodIndex;
            AppearanceComboBox.SelectedIndex = model.AppearanceIndex;

            InterfaceComboBox.Items.Clear();
            foreach (var label in model.InterfaceLabels)
            {
                InterfaceComboBox.Items.Add(new ComboBoxItem { Content = label });
            }
            InterfaceComboBox.SelectedIndex = Math.Clamp(model.InterfaceIndex, 0, Math.Max(0, model.InterfaceLabels.Count - 1));

            StartInBackgroundCheckBox.IsChecked = model.StartInBackground;
            LiveTrayCheckBox.IsChecked = model.LiveTrayEnabled;
            ReduceMotionCheckBox.IsChecked = model.ReduceMotion;
            IncreaseContrastCheckBox.IsChecked = model.IncreaseContrast;
            PlatformFeatureStatusText.Text = model.PlatformFeatureStatus;

            MonitoringEnabledCheckBox.IsChecked = model.MonitoringEnabled;
            MonitoringIntervalComboBox.SelectedIndex = Math.Clamp(model.MonitoringIntervalIndex, 0, 4);
            ContentCadenceComboBox.SelectedIndex = Math.Clamp(model.ContentCadenceIndex, 0, 4);
            ExpectedDownloadTextBox.Text = model.ExpectedDownload;
            ExpectedUploadTextBox.Text = model.ExpectedUpload;
            AlertThresholdTextBox.Text = model.AlertThreshold;
            MonitoringStatusText.Text = model.MonitoringStatus;

            IdentifiersCheckBox.IsChecked = model.IncludeIdentifiers;
            OriginsTextBox.Text = model.Origins;
            LanTargetTextBox.Text = model.LanTarget;
            LanPortTextBox.Text = model.LanPort;
            LanDurationTextBox.Text = model.LanDuration;
            LanConnectionsTextBox.Text = model.LanConnections;
            LanServerStatusText.Text = model.LanServerStatus;
            StopLanServerButton.IsEnabled = model.LanServerRunning;
            ReportsDirectoryText.Text = model.ReportsDirectory;
            ApprovalSummaryText.Text = model.ApprovalSummary;
            MeasurementStatusText.Text = model.Status;
            PrivacyStatusText.Text = model.Status;
            FixtureComboBox.SelectedIndex = Math.Clamp(model.FixtureIndex, 0, 4);
            ShowSection(model.Section);
        }
        finally
        {
            applyingModel = false;
        }
    }

    public void ShowSection(string? section)
    {
        currentSection = NormalizeSection(section);
        SetSelected(GeneralButton, currentSection == "General");
        SetSelected(MonitoringButton, currentSection == "Monitoring");
        SetSelected(MeasurementButton, currentSection == "Measurement");
        SetSelected(PrivacyButton, currentSection == "Privacy & data");
        SetSelected(StorageButton, currentSection == "Storage");
        SetSelected(DeveloperButton, currentSection == "Developer");

        GeneralPanel.IsVisible = currentSection == "General";
        MonitoringPanel.IsVisible = currentSection == "Monitoring";
        MeasurementPanel.IsVisible = currentSection == "Measurement";
        PrivacyPanel.IsVisible = currentSection == "Privacy & data";
        StoragePanel.IsVisible = currentSection == "Storage";
        DeveloperPanel.IsVisible = currentSection == "Developer";

        var copy = currentSection switch
        {
            "Monitoring" => ("MONITORING", "Continuous monitoring", "Configure background sampling, scheduled speed checks, score expectations, and alert thresholds."),
            "Measurement" => ("DIAGNOSTICS", "Diagnostic defaults", "Configure new-run defaults, measurement endpoints, and optional trusted-LAN isolation."),
            "Privacy & data" => ("PRIVACY", "Privacy and approvals", "Control local identifiers in exported data and remembered approval for high-data diagnostic runs."),
            "Storage" => ("STORAGE", "Local data", "Review where reports, monitoring history, and alerts are stored and how data leaves the app."),
            "Developer" => ("DEVELOPER", "Preview states", "Exercise result presentations without starting a live network measurement or writing a report."),
            _ => ("GENERAL", "Application", "Choose appearance, measurement interface, startup behavior, live status, and accessibility preferences.")
        };
        SectionEyebrowText.Text = copy.Item1;
        SectionTitleText.Text = copy.Item2;
        SectionDetailText.Text = copy.Item3;
    }

    private void SectionClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is not Button { Tag: string section }) return;
        ShowSection(section);
        SectionRequested?.Invoke(this, new SettingsSectionRequestedEventArgs(currentSection));
    }

    private void ProfileSelectionChanged(object? sender, SelectionChangedEventArgs eventArgs)
    {
        if (!applyingModel && ProfileComboBox.SelectedIndex >= 0)
        {
            ProfileRequested?.Invoke(this, new SettingsIndexRequestedEventArgs(ProfileComboBox.SelectedIndex));
        }
    }

    private void MethodSelectionChanged(object? sender, SelectionChangedEventArgs eventArgs)
    {
        if (!applyingModel && MethodComboBox.SelectedIndex >= 0)
        {
            MethodRequested?.Invoke(this, new SettingsIndexRequestedEventArgs(MethodComboBox.SelectedIndex));
        }
    }

    private void AppearanceSelectionChanged(object? sender, SelectionChangedEventArgs eventArgs)
    {
        if (!applyingModel && AppearanceComboBox.SelectedIndex >= 0)
        {
            AppearanceRequested?.Invoke(this, new SettingsIndexRequestedEventArgs(AppearanceComboBox.SelectedIndex));
        }
    }

    private void InterfaceSelectionChanged(object? sender, SelectionChangedEventArgs eventArgs)
    {
        if (!applyingModel && InterfaceComboBox.SelectedIndex >= 0)
        {
            InterfaceRequested?.Invoke(this, new SettingsIndexRequestedEventArgs(InterfaceComboBox.SelectedIndex));
        }
    }

    private void IdentifiersClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (!applyingModel)
        {
            IdentifiersChanged?.Invoke(this, new SettingsBooleanChangedEventArgs(IdentifiersCheckBox.IsChecked == true));
        }
    }

    private void SaveGeneralClicked(object? sender, RoutedEventArgs eventArgs) =>
        SaveGeneralRequested?.Invoke(this, EventArgs.Empty);

    private void SaveMonitoringClicked(object? sender, RoutedEventArgs eventArgs) =>
        SaveMonitoringRequested?.Invoke(this, EventArgs.Empty);

    private void RefreshPreflightClicked(object? sender, RoutedEventArgs eventArgs) =>
        RefreshPreflightRequested?.Invoke(this, EventArgs.Empty);

    private void SaveMeasurementClicked(object? sender, RoutedEventArgs eventArgs) =>
        SaveMeasurementRequested?.Invoke(this, EventArgs.Empty);

    private void StartLanServerClicked(object? sender, RoutedEventArgs eventArgs) =>
        StartLanServerRequested?.Invoke(this, EventArgs.Empty);

    private void StopLanServerClicked(object? sender, RoutedEventArgs eventArgs) =>
        StopLanServerRequested?.Invoke(this, EventArgs.Empty);

    private void ResetApprovalsClicked(object? sender, RoutedEventArgs eventArgs) =>
        ResetApprovalsRequested?.Invoke(this, EventArgs.Empty);

    private void OpenReportsFolderClicked(object? sender, RoutedEventArgs eventArgs) =>
        OpenReportsFolderRequested?.Invoke(this, EventArgs.Empty);

    private void PreviewClicked(object? sender, RoutedEventArgs eventArgs) =>
        PreviewRequested?.Invoke(this, new SettingsIndexRequestedEventArgs(Math.Max(0, FixtureComboBox.SelectedIndex)));

    private static string NormalizeSection(string? section) => section?.Trim() switch
    {
        "Monitoring" => "Monitoring",
        "Measurement" or "Diagnostics" => "Measurement",
        "Privacy & data" or "Privacy" or "Data" => "Privacy & data",
        "Storage" => "Storage",
        "Developer" => "Developer",
        _ => "General"
    };

    private static void SetSelected(Button button, bool selected)
    {
        if (selected)
        {
            if (!button.Classes.Contains("selected")) button.Classes.Add("selected");
        }
        else
        {
            button.Classes.Remove("selected");
        }
    }
}

public sealed record SettingsWorkspaceModel(
    string Section,
    int ProfileIndex,
    int MethodIndex,
    int AppearanceIndex,
    IReadOnlyList<string> InterfaceLabels,
    int InterfaceIndex,
    bool StartInBackground,
    bool LiveTrayEnabled,
    bool ReduceMotion,
    bool IncreaseContrast,
    string PlatformFeatureStatus,
    bool MonitoringEnabled,
    int MonitoringIntervalIndex,
    int ContentCadenceIndex,
    string ExpectedDownload,
    string ExpectedUpload,
    string AlertThreshold,
    string MonitoringStatus,
    bool IncludeIdentifiers,
    string Origins,
    string LanTarget,
    string LanPort,
    string LanDuration,
    string LanConnections,
    string LanServerStatus,
    bool LanServerRunning,
    string ReportsDirectory,
    string ApprovalSummary,
    string Status,
    int FixtureIndex);

public sealed class SettingsSectionRequestedEventArgs(string section) : EventArgs
{
    public string Section { get; } = section;
}

public sealed class SettingsIndexRequestedEventArgs(int index) : EventArgs
{
    public int Index { get; } = index;
}

public sealed class SettingsBooleanChangedEventArgs(bool value) : EventArgs
{
    public bool Value { get; } = value;
}
