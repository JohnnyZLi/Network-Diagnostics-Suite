using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using NetworkDiagnostics.Desktop.Presentation;

namespace NetworkDiagnostics.Desktop;

public sealed partial class MainWindow
{
    private const string TrayIconPng = "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAAyElEQVR4nO1XyxGFMAiMjsXYkZ1YhJ3Ykd3oVWMIuxAHnZGr2c/jxQVT+iu4Ogtom6ddejYuK8UJH66Jesz0T4mjONWAVRzFV1skgWutZTEUEXPBUHzxL/CKS+dLvDcDLcQZE+oltIqj+MFDfv41VqMXUN4eidSShBI3FERPFm1ACxY2uL7XgXAD2m1n34bwDqhJhQ4eZkCdz7qCyJuSKb1gH7gZQKeYVTznL3aghQl0qr53I9IImaoZDt8HoCBquRHlFf5l9NcB86KFjpJMpEEAAAAASUVORK5CYII=";

    private TrayIcon? liveTrayIcon;
    private NativeMenuItem? trayScoreItem;
    private NativeMenuItem? trayMonitoringItem;
    private bool platformIntegrationInitialized;
    private bool allowWindowClose;

    private async Task UpdateTrayIntegrationAsync()
    {
        if (!platformIntegrationInitialized)
        {
            Closing += MainWindowClosing;
            platformIntegrationInitialized = true;
        }

        if (!settings.LiveTrayEnabled)
        {
            if (liveTrayIcon is not null) liveTrayIcon.IsVisible = false;
            ShowInTaskbar = true;
            return;
        }

        EnsureTrayIcon();
        liveTrayIcon!.IsVisible = true;
        ShowInTaskbar = true;
        UpdateTrayPresentation();

        if (settings.StartInBackground && IsVisible)
        {
            await Dispatcher.UIThread.InvokeAsync(Hide);
        }
    }

    private void EnsureTrayIcon()
    {
        if (liveTrayIcon is not null || Application.Current is null) return;

        var iconBytes = Convert.FromBase64String(TrayIconPng);
        var iconStream = new MemoryStream(iconBytes, writable: false);
        trayScoreItem = new NativeMenuItem("Network score —") { IsEnabled = false };
        trayMonitoringItem = new NativeMenuItem("Pause monitoring");
        trayMonitoringItem.Click += TrayMonitoringClicked;
        var showItem = new NativeMenuItem("Show Network Diagnostics");
        showItem.Click += TrayShowClicked;
        var quitItem = new NativeMenuItem("Quit");
        quitItem.Click += TrayQuitClicked;

        var menu = new NativeMenu();
        menu.Items.Add(trayScoreItem);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(showItem);
        menu.Items.Add(trayMonitoringItem);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(quitItem);

        liveTrayIcon = new TrayIcon
        {
            Icon = new WindowIcon(iconStream),
            ToolTipText = "Network Diagnostics",
            Menu = menu,
            IsVisible = true
        };
        liveTrayIcon.Clicked += TrayIconClicked;
        TrayIcon.SetIcons(Application.Current, new TrayIcons { liveTrayIcon });
    }

    private void UpdateTrayPresentation()
    {
        if (liveTrayIcon is null) return;
        var experience = CurrentNetworkExperience();
        var score = experience.Score?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "—";
        liveTrayIcon.ToolTipText = $"Network score {score} · {experience.Status}";
        if (trayScoreItem is not null)
        {
            trayScoreItem.Header = $"Network score {score} · {experience.Status}";
        }
        if (trayMonitoringItem is not null)
        {
            trayMonitoringItem.Header = settings.MonitoringEnabled ? "Pause monitoring" : "Resume monitoring";
        }
    }

    private void MainWindowClosing(object? sender, WindowClosingEventArgs eventArgs)
    {
        if (!allowWindowClose && settings.LiveTrayEnabled)
        {
            eventArgs.Cancel = true;
            Hide();
            ShowInTaskbar = false;
        }
    }

    private void TrayIconClicked(object? sender, EventArgs eventArgs) => ShowMainWindowFromTray();

    private void TrayShowClicked(object? sender, EventArgs eventArgs) => ShowMainWindowFromTray();

    private async void TrayMonitoringClicked(object? sender, EventArgs eventArgs)
    {
        settings = settings with { MonitoringEnabled = !settings.MonitoringEnabled };
        await PersistSettingsAsync();
        await monitoringService.UpdateOptionsAsync(settings.ToMonitorOptions());
        UpdateTrayPresentation();
        SyncTestWorkspace();
    }

    private void TrayQuitClicked(object? sender, EventArgs eventArgs)
    {
        allowWindowClose = true;
        if (liveTrayIcon is not null) liveTrayIcon.IsVisible = false;
        Close();
    }

    private void ShowMainWindowFromTray()
    {
        ShowInTaskbar = true;
        if (!IsVisible) Show();
        WindowState = WindowState.Normal;
        Activate();
    }
}
