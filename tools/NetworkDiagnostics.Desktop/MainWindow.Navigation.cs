using Avalonia.Controls;
using Avalonia.Interactivity;
using NetworkDeepProbe.Planning;
using NetworkDiagnostics.Desktop.Models;
using NetworkDiagnostics.Desktop.Services;

namespace NetworkDiagnostics.Desktop;

public sealed partial class MainWindow
{
    private void TestNavClicked(object? sender, RoutedEventArgs eventArgs) => ShowArea(DesktopArea.Test);

    private async void HistoryNavClicked(object? sender, RoutedEventArgs eventArgs)
    {
        await RefreshHistoryAsync();
        ShowArea(DesktopArea.History);
    }

    private void SettingsNavClicked(object? sender, RoutedEventArgs eventArgs) => ShowArea(DesktopArea.Settings);

    private async void ProfileSelectionChanged(object? sender, SelectionChangedEventArgs eventArgs)
    {
        if (!initialized) return;
        RenderProfileSelection();
        settings = settings with { DefaultProfile = DesktopSettings.ContractId(SelectedProfile()) };
        await PersistSettingsAsync();
    }

    private void ConnectionProfileClicked(object? sender, RoutedEventArgs eventArgs) => SelectProfile(0);

    private void QuickProfileClicked(object? sender, RoutedEventArgs eventArgs) => SelectProfile(1);

    private void FullProfileClicked(object? sender, RoutedEventArgs eventArgs) => SelectProfile(2);

    private void StressProfileClicked(object? sender, RoutedEventArgs eventArgs) => SelectProfile(3);

    private void SelectProfile(int index)
    {
        if (ProfileSelector.SelectedIndex == index)
        {
            RenderProfileSelection();
            return;
        }
        ProfileSelector.SelectedIndex = index;
    }

    private void ShowArea(DesktopArea area)
    {
        TestArea.IsVisible = area == DesktopArea.Test;
        HistoryArea.IsVisible = area == DesktopArea.History;
        SettingsArea.IsVisible = area == DesktopArea.Settings;

        SetActiveState(TestNavButton, area == DesktopArea.Test);
        SetActiveState(HistoryNavButton, area == DesktopArea.History);
        SetActiveState(SettingsNavButton, area == DesktopArea.Settings);

        if (area == DesktopArea.Test) ShowTestState(currentTestState);
    }

    private void ShowTestState(TestViewState state)
    {
        currentTestState = state;
        SetupView.IsVisible = state == TestViewState.Setup;
        RunningView.IsVisible = state == TestViewState.Running;
        ResultsView.IsVisible = state == TestViewState.Results;
    }

    private TestProfileId SelectedProfile() => ProfileSelector.SelectedIndex switch
    {
        1 => TestProfileId.Quick,
        2 => TestProfileId.Standard,
        3 => TestProfileId.Extended,
        _ => TestProfileId.ConnectionCheck
    };

    private static int ProfileIndex(TestProfileId profile) => profile switch
    {
        TestProfileId.Quick => 1,
        TestProfileId.Standard => 2,
        TestProfileId.Extended => 3,
        _ => 0
    };

    private static void SetActiveState(Button button, bool active)
    {
        if (active)
        {
            if (!button.Classes.Contains("active")) button.Classes.Add("active");
        }
        else
        {
            button.Classes.Remove("active");
        }
    }
}
