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
        await RefreshComparisonHistoryAsync();
        ShowArea(DesktopArea.History);
    }

    private void SettingsNavClicked(object? sender, RoutedEventArgs eventArgs) => ShowArea(DesktopArea.Settings);

    private async void ProfileSelectionChanged(object? sender, SelectionChangedEventArgs eventArgs)
    {
        if (!initialized) return;
        RenderProfileSelection();
        settings = settings with { DefaultProfile = DesktopSettings.ContractId(SelectedProfile()) };
        await PersistSettingsAsync();
        await RefreshPreflightAsync();
    }

    private async void MethodSelectionChanged(object? sender, SelectionChangedEventArgs eventArgs)
    {
        if (!initialized) return;
        RenderMethodSelection();
        settings = settings with { DefaultTransferMethod = DesktopSettings.ContractId(SelectedMethod()) };
        await PersistSettingsAsync();
        RenderProfileSelection();
        await RefreshPreflightAsync();
    }

    private void ConnectionProfileClicked(object? sender, RoutedEventArgs eventArgs) => SelectProfile(0);

    private void QuickProfileClicked(object? sender, RoutedEventArgs eventArgs) => SelectProfile(1);

    private void FullProfileClicked(object? sender, RoutedEventArgs eventArgs) => SelectProfile(2);

    private void StressProfileClicked(object? sender, RoutedEventArgs eventArgs) => SelectProfile(3);

    private void CompareMethodClicked(object? sender, RoutedEventArgs eventArgs) => SelectMethod(0);

    private void SingleMethodClicked(object? sender, RoutedEventArgs eventArgs) => SelectMethod(1);

    private void AggregateMethodClicked(object? sender, RoutedEventArgs eventArgs) => SelectMethod(2);

    private void SelectProfile(int index)
    {
        if (ProfileSelector.SelectedIndex == index)
        {
            RenderProfileSelection();
            return;
        }
        ProfileSelector.SelectedIndex = index;
    }

    private void SelectMethod(int index)
    {
        if (MethodSelector.SelectedIndex == index)
        {
            RenderMethodSelection();
            return;
        }
        MethodSelector.SelectedIndex = index;
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

    private TransferMethod SelectedMethod() => MethodSelector.SelectedIndex switch
    {
        1 => TransferMethod.Single,
        2 => TransferMethod.Aggregate,
        _ => TransferMethod.Compare
    };

    private static int ProfileIndex(TestProfileId profile) => profile switch
    {
        TestProfileId.Quick => 1,
        TestProfileId.Standard => 2,
        TestProfileId.Extended => 3,
        _ => 0
    };

    private static int MethodIndex(TransferMethod method) => method switch
    {
        TransferMethod.Single => 1,
        TransferMethod.Aggregate => 2,
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
