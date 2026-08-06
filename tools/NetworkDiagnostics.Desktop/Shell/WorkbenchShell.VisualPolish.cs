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
    private Button? homeNavigationButton;
    private Button? settingsUtilityButton;
    private Panel? toolbarActions;
    private Control? primaryNavigation;
    private bool activeRunLayoutPolished;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        base.OnAttachedToVisualTree(eventArgs);
        EnsureOverlay();
        ResolveHeaderContainers();
        EnsureHomeNavigation();
        EnsureSettingsUtility();
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

    private void VisualPolishLayoutUpdated(object? sender, EventArgs eventArgs)
    {
        ResolveHeaderContainers();
        EnsureHomeNavigation();
        EnsureSettingsUtility();
        HidePrimaryPageNavigation();
        if (homeNavigationButton is not null)
        {
            homeNavigationButton.Content = Bounds.Width < 960 ? "⌂" : "Home";
        }
        if (settingsUtilityButton is not null)
        {
            settingsUtilityButton.Content = Bounds.Width < 960 ? "⚙" : "Settings";
        }
        PolishHeaderUtilities();
        PolishActiveRunLayout();
        SimplifyContextLabel();
    }

    private void ResolveHeaderContainers()
    {
        toolbarActions ??= CommandToolbarButton.GetLogicalParent() as Panel;
        primaryNavigation ??= TestWorkspaceButton.GetLogicalParent()?.GetLogicalParent() as Control;
    }

    private void EnsureHomeNavigation()
    {
        if (homeNavigationButton is not null) return;
        if (BackButton.GetLogicalParent()?.GetLogicalParent() is not Panel productNavigation) return;

        homeNavigationButton = new Button
        {
            Content = "Home",
            MinWidth = 54
        };
        homeNavigationButton.Classes.Add("utilityKey");
        ToolTip.SetTip(homeNavigationButton, "Return to the live network overview");
        homeNavigationButton.Click += (_, _) =>
            DestinationRequested?.Invoke(
                this,
                new DestinationRequestedEventArgs(new TestSetupDestination()));

        productNavigation.Children.Insert(1, homeNavigationButton);
    }

    private void EnsureSettingsUtility()
    {
        if (settingsUtilityButton is not null || toolbarActions is null) return;
        settingsUtilityButton = new Button
        {
            Content = "Settings",
            MinWidth = 58
        };
        settingsUtilityButton.Classes.Add("utilityKey");
        settingsUtilityButton.Click += (_, _) =>
            WorkspaceRequested?.Invoke(this, new WorkspaceRequestedEventArgs(WorkspaceKind.Settings));
        toolbarActions.Children.Insert(0, settingsUtilityButton);
    }

    private void PolishHeaderUtilities()
    {
        if (homeNavigationButton is not null)
        {
            PolishUtilityButton(homeNavigationButton, Bounds.Width < 960 ? 34 : 54);
        }

        if (settingsUtilityButton is not null)
        {
            PolishUtilityButton(settingsUtilityButton, Bounds.Width < 960 ? 34 : 58);
        }

        PolishUtilityButton(CommandToolbarButton, Bounds.Width < 960 ? 42 : 84);

        InspectorToggleButton.Width = double.NaN;
        PolishUtilityButton(InspectorToggleButton, 46);
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
            }

            Grid.SetColumn(ActiveRunProgress, 0);
            Grid.SetRow(ActiveRunProgress, 1);
            ActiveRunProgress.Height = 3;
            ActiveRunProgress.Margin = new Thickness(0, 4, 0, 0);
            ActiveRunProgress.HorizontalAlignment = HorizontalAlignment.Stretch;
            ActiveRunProgress.VerticalAlignment = VerticalAlignment.Center;
            ActiveRunProgress.IsHitTestVisible = false;

            ActiveRunTitleText.TextWrapping = TextWrapping.NoWrap;
            ActiveRunTitleText.TextTrimming = TextTrimming.CharacterEllipsis;
            ActiveRunDetailText.TextWrapping = TextWrapping.NoWrap;
            ActiveRunDetailText.TextTrimming = TextTrimming.CharacterEllipsis;

            ActiveRunPanel.Height = 42;
            ActiveRunPanel.MinHeight = 42;
            ActiveRunPanel.Padding = new Thickness(10, 5);
            ActiveRunPanel.VerticalContentAlignment = VerticalAlignment.Center;
            activeRunLayoutPolished = true;
        }

        var targetWidth = Bounds.Width < 1100 ? 180d : 250d;
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
        button.Padding = new Thickness(11, 0);
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

    private void SimplifyContextLabel()
    {
        var labels = BreadcrumbPanel.Children
            .OfType<Button>()
            .Select(button => button.Content?.ToString())
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Cast<string>()
            .ToArray();
        if (labels.Length == 0) return;

        var selected = labels[^1];
        if (selected is "Overview" or "Evidence" && labels.Length > 1)
        {
            selected = labels[^2];
        }

        selected = selected switch
        {
            "Test" or "Overview" => "Live control center",
            "Reports" => "Diagnostics library",
            "Comparisons" => "Comparison",
            _ => selected
        };

        if (BreadcrumbPanel.Children.Count == 1
            && BreadcrumbPanel.Children[0] is TextBlock current
            && string.Equals(current.Text, selected, StringComparison.Ordinal))
        {
            return;
        }

        BreadcrumbPanel.Children.Clear();
        var label = new TextBlock
        {
            Text = selected,
            FontSize = 9,
            VerticalAlignment = VerticalAlignment.Center
        };
        label.Classes.Add("shellMuted");
        BreadcrumbPanel.Children.Add(label);
    }
}
