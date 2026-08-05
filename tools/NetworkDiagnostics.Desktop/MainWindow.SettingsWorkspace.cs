using System.Globalization;
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
        settingsWorkspace.SaveGeneralRequested += SettingsSaveGeneralRequested;
        settingsWorkspace.SaveMonitoringRequested += SettingsSaveMonitoringRequested;
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
            settings.StartInBackground,
            settings.LiveTrayEnabled,
            settings.ReduceMotion,
            settings.IncreaseContrast,
            PlatformFeatureStatus(),
            settings.MonitoringEnabled,
            MonitoringIntervalIndex(settings.MonitoringIntervalSeconds),
            ContentCadenceIndex(settings.ContentSpeedCadenceHours),
            settings.ExpectedDownloadMbps.ToString("0.###", CultureInfo.InvariantCulture),
            settings.ExpectedUploadMbps.ToString("0.###", CultureInfo.InvariantCulture),
            settings.MonitoringAlertScoreThreshold.ToString(CultureInfo.InvariantCulture),
            monitoringService.Snapshot.StatusMessage,
            settings.IncludeLocalIdentifiers,
            testOriginsText,
            lanTargetText,
            lanPortText,
            lanDurationText,
            lanConnectionsText,
            lanServerStatus,
            lanServerRunning,
            settingsStore.RootDirectory,
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

    private async void SettingsSaveGeneralRequested(object? sender, EventArgs eventArgs)
    {
        if (settingsWorkspace is null) return;
        settings = settings with
        {
            StartInBackground = settingsWorkspace.StartInBackground,
            LiveTrayEnabled = settingsWorkspace.LiveTrayEnabled,
            ReduceMotion = settingsWorkspace.ReduceMotion,
            IncreaseContrast = settingsWorkspace.IncreaseContrast
        };
        ApplyAccessibilityPreferences();
        settingsStatus = "Application settings saved.";
        await PersistSettingsAsync();
        await UpdateTrayIntegrationAsync();
        SyncSettingsWorkspace("General");
    }

    private async void SettingsSaveMonitoringRequested(object? sender, EventArgs eventArgs)
    {
        if (settingsWorkspace is null) return;
        if (!TryPositiveDouble(settingsWorkspace.ExpectedDownload, out var expectedDownload)
            || !TryPositiveDouble(settingsWorkspace.ExpectedUpload, out var expectedUpload)
            || !int.TryParse(settingsWorkspace.AlertThreshold, NumberStyles.Integer, CultureInfo.InvariantCulture, out var threshold)
            || threshold is < 1 or > 100)
        {
            settingsStatus = "Enter positive expected speeds and an alert threshold from 1 to 100.";
            SyncSettingsWorkspace("Monitoring");
            return;
        }

        settings = settings with
        {
            MonitoringEnabled = settingsWorkspace.MonitoringEnabled,
            MonitoringIntervalSeconds = MonitoringIntervalSeconds(settingsWorkspace.MonitoringIntervalIndex),
            ContentSpeedCadenceHours = ContentCadenceHours(settingsWorkspace.ContentCadenceIndex),
            ExpectedDownloadMbps = expectedDownload,
            ExpectedUploadMbps = expectedUpload,
            MonitoringAlertScoreThreshold = threshold
        };
        await PersistSettingsAsync();
        await monitoringService.UpdateOptionsAsync(settings.ToMonitorOptions());
        settingsStatus = settings.MonitoringEnabled
            ? "Continuous monitoring settings saved and active."
            : "Continuous monitoring is paused.";
        SyncSettingsWorkspace("Monitoring");
        SyncTestWorkspace();
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
        await monitoringService.UpdateOptionsAsync(settings.ToMonitorOptions());
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

    private void ApplyAccessibilityPreferences()
    {
        Classes.Set("increasedContrast", settings.IncreaseContrast);
        Classes.Set("reducedMotion", settings.ReduceMotion);
    }

    private string PlatformFeatureStatus()
    {
        var tray = settings.LiveTrayEnabled
            ? "Live menu-bar/system-tray status is enabled."
            : "Live menu-bar/system-tray status is off.";
        var startup = settings.StartInBackground
            ? "The app may open without activating its main window."
            : "The main window opens normally.";
        return $"{tray} {startup}";
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

    private static int MonitoringIntervalIndex(int seconds) => seconds switch
    {
        <= 2 => 0,
        <= 5 => 1,
        <= 10 => 2,
        <= 30 => 3,
        _ => 4
    };

    private static int MonitoringIntervalSeconds(int index) => index switch
    {
        0 => 2,
        2 => 10,
        3 => 30,
        4 => 60,
        _ => 5
    };

    private static int ContentCadenceIndex(int hours) => hours switch
    {
        1 => 0,
        4 => 1,
        6 => 2,
        24 => 3,
        _ => 4
    };

    private static int ContentCadenceHours(int index) => index switch
    {
        0 => 1,
        1 => 4,
        2 => 6,
        3 => 24,
        _ => 0
    };

    private static bool TryPositiveDouble(string value, out double parsed) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)
        && parsed > 0;

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
