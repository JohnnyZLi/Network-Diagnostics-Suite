using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using NetworkDiagnostics.Desktop.Navigation;
using NetworkDiagnostics.Desktop.Presentation;
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
        settingsWorkspace.AppearanceRequested += SettingsAppearanceRequested;
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

        var currentSection = section
            ?? (navigationService.Current?.Destination as SettingsDestination)?.Section
            ?? "General";

        settingsWorkspace.Render(new SettingsWorkspaceModel(
            currentSection,
            selectedProfileIndex,
            selectedMethodIndex,
            AppearanceIndex(settings.Appearance),
            interfaceLabels,
            selectedInterfaceIndex,
            settings.IncludeLocalIdentifiers,
            testOriginsText,
            lanTargetText,
            lanPortText,
            lanDurationText,
            lanConnectionsText,
            lanServerStatus,
            lanServerRunning,
            reportStore.ReportsDirectory,
            ApprovalSummary(),
            settingsStatus,
            fixtureIndex));
    }

    private void SettingsSectionRequested(object? sender, SettingsSectionRequestedEventArgs eventArgs) =>
        NavigateToDestination(new SettingsDestination(eventArgs.Section));

    private void SettingsProfileRequested(object? sender, SettingsIndexRequestedEventArgs eventArgs) =>
        SelectProfile(eventArgs.Index);

    private void SettingsMethodRequested(object? sender, SettingsIndexRequestedEventArgs eventArgs) =>
        SelectMethod(eventArgs.Index);

    private async void SettingsAppearanceRequested(object? sender, SettingsIndexRequestedEventArgs eventArgs)
    {
        var appearance = AppearanceId(eventArgs.Index);
        settings = settings with { Appearance = appearance };
        ApplyAppearance(appearance);
        await PersistSettingsAsync();
        SyncSettingsWorkspace("General");
        RefreshWorkbenchChrome();
    }

    private async void SettingsInterfaceRequested(object? sender, SettingsIndexRequestedEventArgs eventArgs) =>
        await SelectInterfaceAsync(eventArgs.Index);

    private async void SettingsIdentifiersChanged(object? sender, SettingsBooleanChangedEventArgs eventArgs)
    {
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

    private async void SettingsStartLanServerRequested(object? sender, EventArgs eventArgs)
    {
        CopyMeasurementSettingsFromWorkspace();
        await StartLanServerAsync();
    }

    private void SettingsStopLanServerRequested(object? sender, EventArgs eventArgs) =>
        StopLanServer();

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
        fixtureIndex = Math.Clamp(eventArgs.Index, 0, ConnectionCheckFixtures.All.Count - 1);
        PreviewFixture();
    }

    private void CopyMeasurementSettingsFromWorkspace()
    {
        if (settingsWorkspace is null) return;
        testOriginsText = settingsWorkspace.Origins;
        lanTargetText = settingsWorkspace.LanTarget;
        lanPortText = settingsWorkspace.LanPort;
        lanDurationText = settingsWorkspace.LanDuration;
        lanConnectionsText = settingsWorkspace.LanConnections;
    }

    private static void ApplyAppearance(string? appearance)
    {
        if (Application.Current is not { } app) return;

        app.RequestedThemeVariant = appearance?.Trim().ToLowerInvariant() switch
        {
            "light" => ThemeVariant.Light,
            "dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }

    private static int AppearanceIndex(string? appearance) => appearance?.Trim().ToLowerInvariant() switch
    {
        "light" => 1,
        "dark" => 2,
        _ => 0
    };

    private static string AppearanceId(int index) => index switch
    {
        1 => "light",
        2 => "dark",
        _ => "system"
    };

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
