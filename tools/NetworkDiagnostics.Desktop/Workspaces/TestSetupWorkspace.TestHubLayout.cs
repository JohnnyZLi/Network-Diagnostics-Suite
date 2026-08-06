using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace NetworkDiagnostics.Desktop.Workspaces;

public sealed partial class TestSetupWorkspace
{
    private Border? testHubHost;
    private StackPanel? testHubRoot;
    private Grid? testHubTilesGrid;
    private Border? testHubSpeedTile;
    private Border? testHubDiagnosticTile;
    private Grid? testHubDiagnosticGrid;
    private StackPanel? testHubDiagnosticPresetSection;
    private Border? testHubPlanPanel;
    private Button? testHubContentButton;
    private Button? testHubPeakButton;
    private bool testHubInstalled;
    private bool legacySpeedActionsHidden;

    private void CaptureTestHubHost()
    {
        if (testHubHost is null && DiagnosticExpander.Parent is Border host)
        {
            testHubHost = host;
        }
    }

    private void InstallTestHubLayout()
    {
        if (testHubInstalled
            || testHubHost is null
            || quickLauncherButton is null
            || fullLauncherButton is null
            || stressLauncherButton is null
            || moreLauncherButton is null
            || launcherRunButton is null
            || launcherSelectedProfileText is null
            || launcherSummaryText is null)
        {
            return;
        }

        HideLegacySpeedActions();

        foreach (var control in new Control[]
        {
            quickLauncherButton,
            fullLauncherButton,
            stressLauncherButton,
            moreLauncherButton,
            launcherRunButton,
            launcherSelectedProfileText,
            launcherSummaryText
        })
        {
            DetachTestHubControl(control);
        }

        ConfigureLauncherPreset(quickLauncherButton);
        ConfigureLauncherPreset(fullLauncherButton);
        ConfigureLauncherPreset(stressLauncherButton);

        testHubContentButton = CreateSpeedTestButton(
            "Content test",
            "Everyday delivery",
            "secondary",
            TestHubContentClicked);
        testHubPeakButton = CreateSpeedTestButton(
            "Peak test",
            "Maximum capacity",
            "primary",
            TestHubPeakClicked);

        var outerHeading = new StackPanel { Spacing = 3 };
        outerHeading.Children.Add(new TextBlock
        {
            Text = "Tests & diagnostics",
            FontSize = 15,
            FontWeight = FontWeight.SemiBold
        });
        var outerDescription = new TextBlock
        {
            Text = "Measure throughput or collect broader evidence about the connection.",
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap
        };
        outerDescription.Classes.Add("muted");
        outerHeading.Children.Add(outerDescription);

        testHubSpeedTile = BuildSpeedTestsTile();
        testHubDiagnosticTile = BuildDiagnosticsTile();

        testHubTilesGrid = new Grid
        {
            ColumnSpacing = 12,
            RowSpacing = 12,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        testHubTilesGrid.Children.Add(testHubSpeedTile);
        testHubTilesGrid.Children.Add(testHubDiagnosticTile);

        testHubRoot = new StackPanel
        {
            Spacing = 12,
            Margin = new Thickness(18, 16),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        testHubRoot.Children.Add(outerHeading);
        testHubRoot.Children.Add(testHubTilesGrid);
        testHubRoot.SizeChanged += TestHubRootSizeChanged;

        testHubHost.Child = testHubRoot;
        testHubInstalled = true;
        ApplyTestHubResponsiveLayout(testHubRoot.Bounds.Width);
        SyncTestHubLayout();
    }

    private Border BuildSpeedTestsTile()
    {
        var label = new TextBlock { Text = "SPEED TESTS" };
        label.Classes.Add("eyebrow");

        var title = new TextBlock
        {
            Text = "Measure throughput",
            FontSize = 17,
            FontWeight = FontWeight.SemiBold
        };

        var detail = new TextBlock
        {
            Text = "Check ordinary content delivery or push the connection toward its maximum capacity.",
            FontSize = 10,
            LineHeight = 15,
            TextWrapping = TextWrapping.Wrap
        };
        detail.Classes.Add("muted");

        var actions = new StackPanel { Spacing = 8 };
        actions.Children.Add(testHubContentButton!);
        actions.Children.Add(testHubPeakButton!);

        var content = new StackPanel { Spacing = 9 };
        content.Children.Add(label);
        content.Children.Add(title);
        content.Children.Add(detail);
        content.Children.Add(actions);

        var tile = new Border
        {
            Padding = new Thickness(16, 15),
            CornerRadius = new CornerRadius(12),
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = content
        };
        tile.Classes.Add("surfaceSubtle");
        return tile;
    }

    private Border BuildDiagnosticsTile()
    {
        var label = new TextBlock { Text = "DIAGNOSTICS" };
        label.Classes.Add("eyebrow");

        var title = new TextBlock
        {
            Text = "Investigate connection health",
            FontSize = 17,
            FontWeight = FontWeight.SemiBold
        };

        var detail = new TextBlock
        {
            Text = "Choose how much evidence to collect, review the saved plan, then start the run.",
            FontSize = 10,
            LineHeight = 15,
            TextWrapping = TextWrapping.Wrap
        };
        detail.Classes.Add("muted");

        var presetGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*"),
            ColumnSpacing = 8,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        presetGrid.Children.Add(quickLauncherButton!);
        Grid.SetColumn(fullLauncherButton!, 1);
        presetGrid.Children.Add(fullLauncherButton!);
        Grid.SetColumn(stressLauncherButton!, 2);
        presetGrid.Children.Add(stressLauncherButton!);

        testHubDiagnosticPresetSection = new StackPanel { Spacing = 10 };
        testHubDiagnosticPresetSection.Children.Add(label);
        testHubDiagnosticPresetSection.Children.Add(title);
        testHubDiagnosticPresetSection.Children.Add(detail);
        testHubDiagnosticPresetSection.Children.Add(presetGrid);

        var selectedLabel = new TextBlock { Text = "SELECTED PLAN" };
        selectedLabel.Classes.Add("eyebrow");

        launcherSelectedProfileText!.FontSize = 17;
        launcherSelectedProfileText.FontWeight = FontWeight.SemiBold;
        launcherSummaryText!.FontSize = 10;
        launcherSummaryText.LineHeight = 15;
        launcherSummaryText.MinHeight = 31;

        moreLauncherButton!.MinHeight = 36;
        moreLauncherButton.Padding = new Thickness(12, 7);
        moreLauncherButton.HorizontalContentAlignment = HorizontalAlignment.Center;
        launcherRunButton!.MinHeight = 36;
        launcherRunButton.Padding = new Thickness(13, 7);
        launcherRunButton.HorizontalContentAlignment = HorizontalAlignment.Center;

        var actions = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 8,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        actions.Children.Add(moreLauncherButton);
        Grid.SetColumn(launcherRunButton, 1);
        actions.Children.Add(launcherRunButton);

        var planContent = new StackPanel { Spacing = 8 };
        planContent.Children.Add(selectedLabel);
        planContent.Children.Add(launcherSelectedProfileText);
        planContent.Children.Add(launcherSummaryText);
        planContent.Children.Add(actions);

        testHubPlanPanel = new Border
        {
            Padding = new Thickness(15, 14),
            CornerRadius = new CornerRadius(11),
            MinHeight = 146,
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = planContent
        };
        testHubPlanPanel.Classes.Add("dashboardPanel");

        testHubDiagnosticGrid = new Grid
        {
            ColumnSpacing = 14,
            RowSpacing = 12,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        testHubDiagnosticGrid.Children.Add(testHubDiagnosticPresetSection);
        testHubDiagnosticGrid.Children.Add(testHubPlanPanel);

        var tile = new Border
        {
            Padding = new Thickness(16, 15),
            CornerRadius = new CornerRadius(12),
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = testHubDiagnosticGrid
        };
        tile.Classes.Add("surfaceSubtle");
        return tile;
    }

    private static Button CreateSpeedTestButton(
        string title,
        string detail,
        string styleClass,
        EventHandler<RoutedEventArgs> handler)
    {
        var titleText = new TextBlock
        {
            Text = title,
            FontSize = 11,
            FontWeight = FontWeight.SemiBold
        };
        var detailText = new TextBlock
        {
            Text = detail,
            FontSize = 9,
            TextWrapping = TextWrapping.Wrap
        };
        detailText.Classes.Add(styleClass == "primary" ? "onAccentSecondary" : "muted");

        var content = new StackPanel { Spacing = 2 };
        content.Children.Add(titleText);
        content.Children.Add(detailText);

        var button = new Button
        {
            Content = content,
            MinHeight = 55,
            Padding = new Thickness(13, 9),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        button.Classes.Add(styleClass);
        button.Click += handler;
        return button;
    }

    private static void ConfigureLauncherPreset(Button button)
    {
        button.MinWidth = 0;
        button.MinHeight = 61;
        button.Padding = new Thickness(12, 9);
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        button.VerticalContentAlignment = VerticalAlignment.Center;
    }

    private static void DetachTestHubControl(Control control)
    {
        if (control.Parent is Panel panel)
        {
            panel.Children.Remove(control);
        }
    }

    private void HideLegacySpeedActions()
    {
        if (legacySpeedActionsHidden) return;

        var legacyContentButton = this.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(button => string.Equals(
                button.Content as string,
                "Run content test",
                StringComparison.Ordinal));
        if (legacyContentButton?.Parent is Panel legacyPanel)
        {
            legacyPanel.IsVisible = false;
            legacySpeedActionsHidden = true;
        }
    }

    private void TestHubRootSizeChanged(object? sender, SizeChangedEventArgs eventArgs) =>
        ApplyTestHubResponsiveLayout(eventArgs.NewSize.Width);

    private void ApplyTestHubResponsiveLayout(double width)
    {
        if (!testHubInstalled
            || testHubTilesGrid is null
            || testHubSpeedTile is null
            || testHubDiagnosticTile is null
            || testHubDiagnosticGrid is null
            || testHubDiagnosticPresetSection is null
            || testHubPlanPanel is null)
        {
            return;
        }

        var resolvedWidth = width > 0 ? width : Bounds.Width;
        var stackTiles = resolvedWidth < 760;

        testHubTilesGrid.ColumnDefinitions.Clear();
        testHubTilesGrid.RowDefinitions.Clear();
        if (stackTiles)
        {
            testHubTilesGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            testHubTilesGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            testHubTilesGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            SetGridPosition(testHubSpeedTile, 0, 0);
            SetGridPosition(testHubDiagnosticTile, 1, 0);
        }
        else
        {
            testHubTilesGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(280)));
            testHubTilesGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            testHubTilesGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            SetGridPosition(testHubSpeedTile, 0, 0);
            SetGridPosition(testHubDiagnosticTile, 0, 1);
        }

        var diagnosticWidth = stackTiles
            ? resolvedWidth
            : Math.Max(0, resolvedWidth - 292);
        var stackDiagnostic = diagnosticWidth < 660;

        testHubDiagnosticGrid.ColumnDefinitions.Clear();
        testHubDiagnosticGrid.RowDefinitions.Clear();
        if (stackDiagnostic)
        {
            testHubDiagnosticGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            testHubDiagnosticGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            testHubDiagnosticGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            SetGridPosition(testHubDiagnosticPresetSection, 0, 0);
            SetGridPosition(testHubPlanPanel, 1, 0);
        }
        else
        {
            testHubDiagnosticGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1.15, GridUnitType.Star)));
            testHubDiagnosticGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(0.85, GridUnitType.Star)));
            testHubDiagnosticGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            SetGridPosition(testHubDiagnosticPresetSection, 0, 0);
            SetGridPosition(testHubPlanPanel, 0, 1);
        }
    }

    private void SyncTestHubLayout()
    {
        if (!testHubInstalled) return;
        HideLegacySpeedActions();

        var controlsEnabled = RunButton.IsEnabled;
        if (testHubContentButton is not null) testHubContentButton.IsEnabled = controlsEnabled;
        if (testHubPeakButton is not null) testHubPeakButton.IsEnabled = controlsEnabled;
    }

    private void TestHubContentClicked(object? sender, RoutedEventArgs eventArgs) =>
        ContentSpeedRequested?.Invoke(this, EventArgs.Empty);

    private void TestHubPeakClicked(object? sender, RoutedEventArgs eventArgs) =>
        PeakSpeedRequested?.Invoke(this, EventArgs.Empty);
}
