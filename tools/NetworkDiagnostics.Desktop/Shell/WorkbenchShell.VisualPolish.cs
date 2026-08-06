using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using NetworkDiagnostics.Desktop.Navigation;

namespace NetworkDiagnostics.Desktop.Shell;

public sealed partial class WorkbenchShell
{
    private Control? primaryNavigation;
    private bool activeRunLayoutPolished;
    private bool persistentNavigationWired;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        base.OnAttachedToVisualTree(eventArgs);
        EnsureOverlay();
        WirePersistentNavigation();
        ResolveHeaderContainers();
        HidePrimaryPageNavigation();
        PolishHeaderUtilities();
        PolishActiveRunLayout();
        LayoutUpdated += VisualPolishLayoutUpdated;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        LayoutUpdated -= VisualPolishLayoutUpdated;
        base.OnDetachedFromVisualTree(eventArgs);
    }

    private void WirePersistentNavigation()
    {
        if (persistentNavigationWired) return;
        persistentNavigationWired = true;
        HomeRequested += (_, _) =>
            DestinationRequested?.Invoke(
                this,
                new DestinationRequestedEventArgs(new TestSetupDestination()));
    }

    private void VisualPolishLayoutUpdated(object? sender, EventArgs eventArgs)
    {
        ResolveHeaderContainers();
        HidePrimaryPageNavigation();
        PolishHeaderUtilities();
        PolishActiveRunLayout();
    }

    private void ResolveHeaderContainers() =>
        primaryNavigation ??= TestWorkspaceButton.GetLogicalParent()?.GetLogicalParent() as Control;

    private void PolishHeaderUtilities()
    {
        PolishUtilityButton(HomeButton, Bounds.Width < 960 ? 34 : 52);
        PolishUtilityButton(SettingsToolbarButton, Bounds.Width < 960 ? 34 : 58);
        PolishUtilityButton(CommandToolbarButton, Bounds.Width < 960 ? 42 : 84);

        InspectorToggleButton.Width = double.NaN;
        PolishUtilityButton(InspectorToggleButton, 44);
    }

    private void PolishActiveRunLayout()
    {
        if (!activeRunLayoutPolished && ActiveRunPanel.Content is Grid grid)
        {
            grid.ColumnDefinitions.Clear();
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            grid.RowDefinitions.Clear();
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            grid.ColumnSpacing = 0;
            grid.RowSpacing = 0;

            var textStack = grid.Children.OfType<StackPanel>().FirstOrDefault();
            if (textStack is not null)
            {
                Grid.SetColumn(textStack, 0);
                Grid.SetRow(textStack, 0);
                textStack.Spacing = 0;
            }

            Grid.SetColumn(ActiveRunProgress, 0);
            Grid.SetRow(ActiveRunProgress, 1);
            ActiveRunProgress.Height = 2;
            ActiveRunProgress.Margin = new Thickness(0, 3, 0, 0);
            ActiveRunProgress.HorizontalAlignment = HorizontalAlignment.Stretch;
            ActiveRunProgress.VerticalAlignment = VerticalAlignment.Center;
            ActiveRunProgress.IsHitTestVisible = false;

            ActiveRunTitleText.FontSize = 9;
            ActiveRunDetailText.FontSize = 8;
            ActiveRunTitleText.TextWrapping = TextWrapping.NoWrap;
            ActiveRunTitleText.TextTrimming = TextTrimming.CharacterEllipsis;
            ActiveRunDetailText.TextWrapping = TextWrapping.NoWrap;
            ActiveRunDetailText.TextTrimming = TextTrimming.CharacterEllipsis;

            ActiveRunPanel.Height = 34;
            ActiveRunPanel.MinHeight = 34;
            ActiveRunPanel.Padding = new Thickness(10, 4);
            ActiveRunPanel.VerticalContentAlignment = VerticalAlignment.Center;
            activeRunLayoutPolished = true;
        }

        var compact = Bounds.Width < 960;
        var showDetail = Bounds.Width >= 1260;
        ActiveRunDetailText.IsVisible = showDetail;
        ActiveRunPanel.Padding = compact
            ? new Thickness(8, 4)
            : new Thickness(10, 4);

        var targetWidth = showDetail
            ? 224d
            : compact
                ? 148d
                : 176d;
        if (Math.Abs(ActiveRunPanel.Width - targetWidth) > 0.5)
        {
            ActiveRunPanel.Width = targetWidth;
        }
    }

    private static void PolishUtilityButton(Button button, double minimumWidth)
    {
        button.Height = 30;
        button.MinHeight = 30;
        button.MinWidth = minimumWidth;
        button.Padding = new Thickness(10, 0);
        button.CornerRadius = new CornerRadius(9);
        button.FontSize = 9;
        button.FontWeight = FontWeight.Medium;
        button.HorizontalContentAlignment = HorizontalAlignment.Center;
        button.VerticalContentAlignment = VerticalAlignment.Center;
    }

    private void HidePrimaryPageNavigation()
    {
        if (primaryNavigation is not null) primaryNavigation.IsVisible = false;
        TestWorkspaceButton.IsVisible = false;
        ReportsWorkspaceButton.IsVisible = false;
        ComparisonsWorkspaceButton.IsVisible = false;
        SettingsWorkspaceButton.IsVisible = false;
    }
}
