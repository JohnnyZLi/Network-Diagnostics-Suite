using Avalonia.Controls;
using Avalonia.Interactivity;
using NetworkDiagnostics.Desktop.Navigation;
using NetworkDiagnostics.Desktop.Workspaces;

namespace NetworkDiagnostics.Desktop;

public sealed partial class MainWindow
{
    private void InstallSettingsWorkspace(Grid workspaceGrid)
    {
        settingsWorkspace = new SettingsWorkspace { IsVisible = false };
        workspaceGrid.Children.Add(settingsWorkspace);

        settingsWorkspace.SectionRequested += SettingsSectionRequested;
        settingsWorkspace.ProfileRequested += SettingsProfileRequested;
        settingsWorkspace.MethodRequested += SettingsMethodRequested;
        settingsWorkspace.InterfaceRequested += SettingsInterfaceRequested;
        settingsWorkspace.IdentifiersChanged += SettingsIdentifiersChanged;
        settingsWorkspace.RefreshPreflightRequested += SettingsRefreshPreflightRequested;
        settingsWorkspace.SaveMeasurementRequested += SettingsSaveMeasurementRequested;
        settingsWorkspace.StartLanServerRequested += SettingsStartLanServerRequested;
        settingsWorkspace.StopLanServerRequested += SettingsStopLanServerRequested;
        settingsWorkspace.ResetApprovalsRequested += SettingsResetApprovalsRequested;
        settingsWorkspace.OpenReportsFolderRequested += SettingsOpenReportsFolderRequested;
        settingsWorkspace.PreviewRequested += SettingsPreviewRequested;
    }

    private void SyncSettingsWorkspace(string? section = null)
    {
        if (settingsWorkspace is null) return;

        var interfaceLabels = InterfaceSelector.Items
            .OfType<ComboBoxItem>()
            .Select(item => item.Content?.ToString() ?? "Interface")
            .ToArray();
        var currentSection = section
            ?? (navigationService.Current?.Destination as SettingsDestination)?.Section
            ?? "General";

        settingsWorkspace.Render(new SettingsWorkspaceModel(
            currentSection,
            ProfileSelector.SelectedIndex,
            MethodSelector.SelectedIndex,
            interfaceLabels,
            Math.Max(0, InterfaceSelector.SelectedIndex),
            settings.IncludeLocalIdentifiers,
            TestOriginTextBox.Text ?? string.Empty,
            LanTargetTextBox.Text ?? string.Empty,
            LanPortTextBox.Text ?? settings.LanPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            LanDurationTextBox.Text ?? settings.LanDurationSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
            LanConnectionsTextBox.Text ?? settings.LanConnections.ToString(System.Globalization.CultureInfo.InvariantCulture),
            LanServerStatusText.Text ?? "Server stopped.",
            StopLanServerButton.IsEnabled,
            reportStore.ReportsDirectory,
            ApprovalSummary(),
            SettingsStatusText.Text ?? string.Empty,
            Math.Max(0, FixtureSelector.SelectedIndex)));
    }

    private void SettingsSectionRequested(object? sender, SettingsSectionRequestedEventArgs eventArgs) =>
        NavigateToDestination(new SettingsDestination(eventArgs.Section));

    private void SettingsProfileRequested(object? sender, SettingsIndexRequestedEventArgs eventArgs) =>
        SelectProfile(eventArgs.Index);

    private void SettingsMethodRequested(object? sender, SettingsIndexRequestedEventArgs eventArgs) =>
        SelectMethod(eventArgs.Index);

    private void SettingsInterfaceRequested(object? sender, SettingsIndexRequestedEventArgs eventArgs)
    {
        if (InterfaceSelector.SelectedIndex != eventArgs.Index)
        {
            InterfaceSelector.SelectedIndex = eventArgs.Index;
        }
        SyncSettingsWorkspace();
    }

    private async void SettingsIdentifiersChanged(object? sender, SettingsBooleanChangedEventArgs eventArgs)
    {
        IncludeIdentifiersCheckBox.IsChecked = eventArgs.Value;
        await SaveIdentifiersSettingAsync(eventArgs.Value);
        SyncSettingsWorkspace();
        RefreshWorkbenchChrome();
    }

    private async void SettingsRefreshPreflightRequested(object? sender, EventArgs eventArgs)
    {
        await RefreshPreflightAsync();
        SyncSettingsWorkspace();
        RefreshWorkbenchChrome();
    }

    private async void SettingsSaveMeasurementRequested(object? sender, EventArgs eventArgs)
    {
        if (settingsWorkspace is null) return;
        CopyMeasurementSettingsFromWorkspace();
        await SaveAdvancedSettingsAsync();
        SyncSettingsWorkspace("Measurement");
        RefreshWorkbenchChrome();
    }

    private void SettingsStartLanServerRequested(object? sender, EventArgs eventArgs)
    {
        CopyMeasurementSettingsFromWorkspace();
        StartLanServerClicked(sender, new RoutedEventArgs());
        SyncSettingsWorkspace("Measurement");
    }

    private void SettingsStopLanServerRequested(object? sender, EventArgs eventArgs)
    {
        StopLanServerClicked(sender, new RoutedEventArgs());
        SyncSettingsWorkspace("Measurement");
    }

    private async void SettingsResetApprovalsRequested(object? sender, EventArgs eventArgs)
    {
        await ResetApprovalsAsync();
        SyncSettingsWorkspace("Privacy & data");
        RefreshWorkbenchChrome();
    }

    private void SettingsOpenReportsFolderRequested(object? sender, EventArgs eventArgs) =>
        reportStore.OpenReportsFolder();

    private void SettingsPreviewRequested(object? sender, SettingsIndexRequestedEventArgs eventArgs)
    {
        FixtureSelector.SelectedIndex = eventArgs.Index;
        PreviewFixtureClicked(sender, new RoutedEventArgs());
    }

    private void CopyMeasurementSettingsFromWorkspace()
    {
        if (settingsWorkspace is null) return;
        TestOriginTextBox.Text = settingsWorkspace.Origins;
        LanTargetTextBox.Text = settingsWorkspace.LanTarget;
        LanPortTextBox.Text = settingsWorkspace.LanPort;
        LanDurationTextBox.Text = settingsWorkspace.LanDuration;
        LanConnectionsTextBox.Text = settingsWorkspace.LanConnections;
    }

    private string ApprovalSummary()
    {
        var full = settings.FullApprovedCapBytes <= 0
            ? "Full asks before its next high-data run"
            : $"Full approved through {ApprovalSize(settings.FullApprovedCapBytes)}";
        var stress = settings.StressApprovedCapBytes <= 0
            ? "Stress asks before its next high-data run"
            : $"Stress approved through {ApprovalSize(settings.StressApprovedCapBytes)}";
        return $"{full}. {stress}.";
    }

    private static string ApprovalSize(long bytes) => bytes >= 1_000_000_000
        ? $"{bytes / 1_000_000_000d:0.###} GB"
        : $"{bytes / 1_000_000d:0} MB";
}
