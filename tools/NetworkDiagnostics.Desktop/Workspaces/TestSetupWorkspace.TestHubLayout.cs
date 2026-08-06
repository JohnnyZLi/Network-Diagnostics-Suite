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
    private Grid? testHubSpeedFooterGrid;
    private Grid? testHubDiagnosticFooterGrid;
    private StackPanel? testHubSpeedActions;
    private StackPanel? testHubDiagnosticActions;
    private Button? testHubContentButton;
    private Button? testHubPeakButton;
    private Button? testHubSpeedRunButton;
    private TextBlock? testHubSpeedSelectedText;
    private TextBlock? testHubSpeedSummaryText;
    private bool testHubInstalled;
    private bool legacySpeedActionsHidden;
    private int selectedSpeedTestIndex;

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

        testHubContentButton = CreateHubChoiceButton(
            "Content",
            "Everyday delivery",
            "ContentSpeedChoice",
            TestHubContentSelected);
        testHubPeakButton = CreateHubChoiceButton(
            "Peak",
            "Maximum capacity",
            "PeakSpeedChoice",
            TestHubPeakSelected);
        testHubSpeedRunButton = CreateLauncherAction(
            "Run content test",
            "primary",
            TestHubSpeedRunClicked,
            150);
        testHubSpeedRunButton.Name = "RunSelectedSpeedTestButton";

        var outerHeading = new StackPanel { Spacing = 3 };
        outerHeading.Children.Add(new TextBlock
        {
            Text = "Tests & diagnostics",
            FontSize = 15,
            FontWeight = FontWeight.SemiBold
        });
        var outerDescription = new TextBlock
        {
            Text = "Choose a focused speed measurement or a broader diagnostic run.",
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
        var header = BuildTestHubHeader(
            "SPEED TESTS",
            "Measure throughput",
            "Choose ordinary content delivery or a maximum-capacity measurement.");

        var choices = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 8,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        choices.Children.Add(testHubContentButton!);
        Grid.SetColumn(testHubPeakButton!, 1);
        choices.Children.Add(testHubPeakButton!);

        testHubSpeedSelectedText = CreateSelectedPlanTitle("Content test");
        testHubSpeedSummaryText = CreateSelectedPlanSummary("Everyday delivery · lower data use");
        var selection = BuildSelectionSummary(testHubSpeedSelectedText, testHubSpeedSummaryText);

        testHubSpeedActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        testHubSpeedActions.Children.Add(testHubSpeedRunButton!);

        testHubSpeedFooterGrid = BuildTestHubFooter(selection, testHubSpeedActions);

        var content = new StackPanel { Spacing = 13 };
        content.Children.Add(header);
        content.Children.Add(choices);
        content.Children.Add(CreateTestHubDivider());
        content.Children.Add(testHubSpeedFooterGrid);

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
        var header = BuildTestHubHeader(
            "DIAGNOSTICS",
            "Investigate connection health",
            "Choose a preset for the amount of evidence and sustained load required.");

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

        launcherSelectedProfileText!.FontSize = 12;
        launcherSelectedProfileText.FontWeight = FontWeight.SemiBold;
        launcherSelectedProfileText.TextTrimming = TextTrimming.CharacterEllipsis;
        launcherSummaryText!.FontSize = 9.5;
        launcherSummaryText.LineHeight = 14;
        launcherSummaryText.MinHeight = 0;
        launcherSummaryText.MaxLines = 2;
        launcherSummaryText.TextTrimming = TextTrimming.CharacterEllipsis;

        moreLauncherButton!.MinHeight = 36;
        moreLauncherButton.MinWidth = 108;
        moreLauncherButton.Padding = new Thickness(12, 7);
        moreLauncherButton.HorizontalContentAlignment = HorizontalAlignment.Center;
        launcherRunButton!.MinHeight = 36;
        launcherRunButton.MinWidth = 136;
        launcherRunButton.Padding = new Thickness(13, 7);
        launcherRunButton.HorizontalContentAlignment = HorizontalAlignment.Center;

        testHubDiagnosticActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        testHubDiagnosticActions.Children.Add(moreLauncherButton);
        testHubDiagnosticActions.Children.Add(launcherRunButton);

        var selection = BuildSelectionSummary(launcherSelectedProfileText, launcherSummaryText);
        testHubDiagnosticFooterGrid = BuildTestHubFooter(selection, testHubDiagnosticActions);

        var content = new StackPanel { Spacing = 13 };
        content.Children.Add(header);
        content.Children.Add(presetGrid);
        content.Children.Add(CreateTestHubDivider());
        content.Children.Add(testHubDiagnosticFooterGrid);

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

    private static StackPanel BuildTestHubHeader(string eyebrow, string title, string detail)
    {
        var label = new TextBlock { Text = eyebrow };
        label.Classes.Add("eyebrow");

        var titleText = new TextBlock
        {
            Text = title,
            FontSize = 17,
            FontWeight = FontWeight.SemiBold
        };

        var detailText = new TextBlock
        {
            Text = detail,
            FontSize = 10,
            LineHeight = 15,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 30
        };
        detailText.Classes.Add("muted");

        var header = new StackPanel { Spacing = 6 };
        header.Children.Add(label);
        header.Children.Add(titleText);
        header.Children.Add(detailText);
        return header;
    }

    private static Grid BuildTestHubFooter(Control selection, Control actions)
    {
        var footer = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 14,
            RowSpacing = 9,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        footer.Children.Add(selection);
        Grid.SetColumn(actions, 1);
        footer.Children.Add(actions);
        return footer;
    }

    private static StackPanel BuildSelectionSummary(TextBlock title, TextBlock summary)
    {
        var selectedLabel = new TextBlock
        {
            Text = "Selected",
            FontSize = 9,
            FontWeight = FontWeight.Medium
        };
        selectedLabel.Classes.Add("muted");

        var stack = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center
        };
        stack.Children.Add(selectedLabel);
        stack.Children.Add(title);
        stack.Children.Add(summary);
        return stack;
    }

    private static TextBlock CreateSelectedPlanTitle(string text) => new()
    {
        Text = text,
        FontSize = 12,
        FontWeight = FontWeight.SemiBold,
        TextTrimming = TextTrimming.CharacterEllipsis
    };

    private static TextBlock CreateSelectedPlanSummary(string text)
    {
        var summary = new TextBlock
        {
            Text = text,
            FontSize = 9.5,
            LineHeight = 14,
            TextWrapping = TextWrapping.Wrap,
            MaxLines = 2,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        summary.Classes.Add("muted");
        return summary;
    }

    private static Border CreateTestHubDivider()
    {
        var divider = new Border { Height = 1 };
        divider.Classes.Add("divider");
        return divider;
    }

    private static Button CreateHubChoiceButton(
        string title,
        string detail,
        string name,
        EventHandler<RoutedEventArgs> handler)
    {
        var titleText = new TextBlock
        {
            Text = title,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold
        };
        var detailText = new TextBlock
        {
            Text = detail,
            FontSize = 9,
            TextWrapping = TextWrapping.Wrap
        };
        detailText.Classes.Add("muted");

        var content = new StackPanel { Spacing = 3 };
        content.Children.Add(titleText);
        content.Children.Add(detailText);

        var button = new Button
        {
            Name = name,
            Content = content,
            MinHeight = 61,
            Padding = new Thickness(12, 9),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        button.Classes.Add("profileChoice");
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
            || testHubSpeedFooterGrid is null
            || testHubDiagnosticFooterGrid is null
            || testHubSpeedActions is null
            || testHubDiagnosticActions is null)
        {
            return;
        }

        var resolvedWidth = width > 0 ? width : Bounds.Width;
        var stackTiles = resolvedWidth < 900;

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
            testHubTilesGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(0.82, GridUnitType.Star)));
            testHubTilesGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1.68, GridUnitType.Star)));
            testHubTilesGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            SetGridPosition(testHubSpeedTile, 0, 0);
            SetGridPosition(testHubDiagnosticTile, 0, 1);
        }

        var speedWidth = stackTiles ? resolvedWidth : resolvedWidth * 0.328;
        ConfigureTestHubFooter(testHubSpeedFooterGrid, testHubSpeedActions, speedWidth < 440);

        var diagnosticWidth = stackTiles ? resolvedWidth : resolvedWidth * 0.672;
        ConfigureTestHubFooter(testHubDiagnosticFooterGrid, testHubDiagnosticActions, diagnosticWidth < 690);
    }

    private static void ConfigureTestHubFooter(Grid footer, StackPanel actions, bool stacked)
    {
        footer.ColumnDefinitions.Clear();
        footer.RowDefinitions.Clear();
        if (stacked)
        {
            footer.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            footer.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            footer.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var selection = footer.Children[0];
            SetGridPosition(selection, 0, 0);
            SetGridPosition(actions, 1, 0);
            actions.HorizontalAlignment = HorizontalAlignment.Stretch;
            foreach (var button in actions.Children.OfType<Button>())
            {
                button.HorizontalAlignment = HorizontalAlignment.Stretch;
            }
        }
        else
        {
            footer.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            footer.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            footer.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var selection = footer.Children[0];
            SetGridPosition(selection, 0, 0);
            SetGridPosition(actions, 0, 1);
            actions.HorizontalAlignment = HorizontalAlignment.Right;
            foreach (var button in actions.Children.OfType<Button>())
            {
                button.HorizontalAlignment = HorizontalAlignment.Center;
            }
        }
    }

    private void SyncTestHubLayout()
    {
        if (!testHubInstalled) return;
        HideLegacySpeedActions();

        var controlsEnabled = RunButton.IsEnabled;
        SetEnabled(testHubContentButton!, controlsEnabled);
        SetEnabled(testHubPeakButton!, controlsEnabled);
        SetEnabled(testHubSpeedRunButton!, controlsEnabled);

        SetSelected(testHubContentButton!, selectedSpeedTestIndex == 0);
        SetSelected(testHubPeakButton!, selectedSpeedTestIndex == 1);

        var speedTitle = selectedSpeedTestIndex == 0 ? "Content test" : "Peak test";
        var speedSummary = selectedSpeedTestIndex == 0
            ? "Everyday delivery · lower data use"
            : "Maximum capacity · higher data use";
        SetTextStable(testHubSpeedSelectedText!, speedTitle);
        SetTextStable(testHubSpeedSummaryText!, speedSummary);

        var speedRunLabel = controlsEnabled ? $"Run {speedTitle.ToLowerInvariant()}" : "Diagnostic running";
        if (!Equals(testHubSpeedRunButton!.Content, speedRunLabel))
        {
            testHubSpeedRunButton.Content = speedRunLabel;
        }
    }

    private void TestHubContentSelected(object? sender, RoutedEventArgs eventArgs)
    {
        selectedSpeedTestIndex = 0;
        SyncTestHubLayout();
    }

    private void TestHubPeakSelected(object? sender, RoutedEventArgs eventArgs)
    {
        selectedSpeedTestIndex = 1;
        SyncTestHubLayout();
    }

    private void TestHubSpeedRunClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (selectedSpeedTestIndex == 0)
        {
            ContentSpeedRequested?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            PeakSpeedRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}