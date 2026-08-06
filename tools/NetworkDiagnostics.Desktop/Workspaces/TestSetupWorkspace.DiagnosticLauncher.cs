using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace NetworkDiagnostics.Desktop.Workspaces;

public sealed partial class TestSetupWorkspace
{
    private readonly Dictionary<Button, TextBlock> diagnosticSelectionBadges = new();
    private Control? diagnosticLauncherContent;
    private Button? quickLauncherButton;
    private Button? fullLauncherButton;
    private Button? stressLauncherButton;
    private Button? moreLauncherButton;
    private Button? launcherRunButton;
    private TextBlock? launcherSelectedProfileText;
    private TextBlock? launcherSummaryText;
    private bool diagnosticLauncherInstalled;
    private bool diagnosticConfiguratorBuilt;
    private int pendingLauncherProfileIndex = 1;

    public event EventHandler? DiagnosticLauncherRequested;
    public event EventHandler? DiagnosticLauncherDismissRequested;

    public Control? DiagnosticLauncherContent => diagnosticLauncherContent;

    public void PrepareDiagnosticLauncherLayout()
    {
        InstallDiagnosticLauncher();
        BuildDiagnosticConfigurator();
        RefreshDiagnosticProfileVisuals();
    }

    private void InstallDiagnosticLauncher()
    {
        if (diagnosticLauncherInstalled
            || DiagnosticExpander.Parent is not Border launcherHost
            || DiagnosticExpander.Content is not Control diagnosticContent)
        {
            return;
        }

        DiagnosticExpander.Content = null;
        diagnosticContent.IsVisible = false;

        diagnosticLauncherContent = new ScrollViewer
        {
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
        };

        foreach (var button in ProfileButtons())
        {
            button.Click += DiagnosticProfileVisualStateChanged;
        }

        launcherHost.Child = CreateDiagnosticLauncherSurface();
        LayoutUpdated += DiagnosticLauncherLayoutUpdated;
        diagnosticLauncherInstalled = true;
        SyncDiagnosticLauncherState();
    }

    private void BuildDiagnosticConfigurator()
    {
        if (diagnosticConfiguratorBuilt || diagnosticLauncherContent is not ScrollViewer scrollViewer)
        {
            return;
        }

        DetachFromPanel(ProfileGrid);
        DetachFromPanel(SelectedQuestionPanel);
        DetachFromPanel(MethodPanel);
        DetachFromPanel(RunPlanPanel);
        DetachFromPanel(RunActionPanel);

        ConfigureProfileRail();
        ConfigureQuestionPanel();
        ConfigureMethodPanel();
        ConfigureRunPlanPanel();
        ConfigureRunActionPanel();

        var profileLabel = new TextBlock
        {
            Text = "PROFILE",
            FontSize = 9,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = 1.1
        };
        profileLabel.Classes.Add("eyebrow");

        var profileHint = new TextBlock
        {
            Text = "Choose the evidence depth for this run.",
            FontSize = 10,
            LineHeight = 15,
            TextWrapping = TextWrapping.Wrap
        };
        profileHint.Classes.Add("muted");

        var profileStack = new StackPanel { Spacing = 10 };
        profileStack.Children.Add(profileLabel);
        profileStack.Children.Add(profileHint);
        profileStack.Children.Add(ProfileGrid);

        var profileRail = new Border
        {
            Padding = new Thickness(14),
            CornerRadius = new CornerRadius(12),
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = profileStack
        };
        profileRail.Classes.Add("surfaceSubtle");

        var questionCard = CreateConfiguratorCard(SelectedQuestionPanel, 16, 14);
        var methodCard = CreateConfiguratorCard(MethodPanel, 15, 13);
        var planCard = CreateConfiguratorCard(RunPlanPanel, 15, 13);

        var optionGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1.1*,0.9*"),
            ColumnSpacing = 12
        };
        optionGrid.Children.Add(methodCard);
        Grid.SetColumn(planCard, 1);
        optionGrid.Children.Add(planCard);

        var detailStack = new StackPanel { Spacing = 12 };
        detailStack.Children.Add(questionCard);
        detailStack.Children.Add(optionGrid);
        detailStack.Children.Add(RunActionPanel);

        var configurator = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("232,*"),
            ColumnSpacing = 16,
            Margin = new Thickness(20),
            MaxWidth = 900,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top
        };
        configurator.Children.Add(profileRail);
        Grid.SetColumn(detailStack, 1);
        configurator.Children.Add(detailStack);

        scrollViewer.Content = configurator;
        diagnosticConfiguratorBuilt = true;
    }

    private void ConfigureProfileRail()
    {
        ProfileGrid.ColumnDefinitions.Clear();
        ProfileGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        ProfileGrid.RowDefinitions.Clear();
        for (var index = 0; index < 4; index++)
        {
            ProfileGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }
        ProfileGrid.ColumnSpacing = 0;
        ProfileGrid.RowSpacing = 7;
        ProfileGrid.Margin = new Thickness(0, 2, 0, 0);

        var buttons = ProfileButtons();
        for (var index = 0; index < buttons.Count; index++)
        {
            var button = buttons[index];
            SetGridPosition(button, index, 0);
            button.MinHeight = 56;
            button.Padding = new Thickness(12, 9);
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        }
    }

    private void ConfigureQuestionPanel()
    {
        SelectedQuestionPanel.Spacing = 5;
        QuestionText.FontSize = 16;
        PurposeText.FontSize = 10;
        PurposeText.LineHeight = 15;
    }

    private void ConfigureMethodPanel()
    {
        MethodPanel.Spacing = 8;
        MethodDetailText.FontSize = 9;
        MethodDetailText.LineHeight = 14;
    }

    private void ConfigureRunPlanPanel()
    {
        RunPlanPanel.Spacing = 8;
        AvailabilityText.FontSize = 9;
        AvailabilityText.LineHeight = 14;
    }

    private void ConfigureRunActionPanel()
    {
        Control? routeContext = RunActionPanel.Children.Count > 1
            ? RunActionPanel.Children[1]
            : null;

        RunActionPanel.Children.Clear();
        RunActionPanel.Spacing = 0;
        RunActionPanel.MinWidth = 0;
        RunActionPanel.HorizontalAlignment = HorizontalAlignment.Stretch;

        if (routeContext is StackPanel routeStack)
        {
            routeStack.Spacing = 2;
        }

        var routeLabel = new TextBlock
        {
            Text = "CURRENT ROUTE",
            FontSize = 9,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = 1.1
        };
        routeLabel.Classes.Add("eyebrow");

        var routeArea = new StackPanel
        {
            Spacing = 5,
            VerticalAlignment = VerticalAlignment.Center
        };
        routeArea.Children.Add(routeLabel);
        if (routeContext is not null)
        {
            routeArea.Children.Add(routeContext);
        }

        var cancelButton = new Button
        {
            Content = "Cancel",
            MinWidth = 92,
            MinHeight = 38,
            Padding = new Thickness(14, 7),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        cancelButton.Classes.Add("secondary");
        cancelButton.Click += DiagnosticConfiguratorCancelClicked;

        RunButton.MinWidth = 150;
        RunButton.MinHeight = 38;
        RunButton.Padding = new Thickness(16, 7);
        RunButton.HorizontalAlignment = HorizontalAlignment.Right;

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };
        actions.Children.Add(cancelButton);
        actions.Children.Add(RunButton);

        var footerGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 18
        };
        footerGrid.Children.Add(routeArea);
        Grid.SetColumn(actions, 1);
        footerGrid.Children.Add(actions);

        var footer = new Border
        {
            Padding = new Thickness(15, 13),
            CornerRadius = new CornerRadius(12),
            Child = footerGrid
        };
        footer.Classes.Add("accentSurface");
        RunActionPanel.Children.Add(footer);
    }

    private static Border CreateConfiguratorCard(Control content, double horizontalPadding, double verticalPadding)
    {
        var border = new Border
        {
            Padding = new Thickness(horizontalPadding, verticalPadding),
            CornerRadius = new CornerRadius(12),
            Child = content
        };
        border.Classes.Add("dashboardPanel");
        return border;
    }

    private static void DetachFromPanel(Control control)
    {
        if (control.Parent is Panel panel)
        {
            panel.Children.Remove(control);
        }
    }

    private Control CreateDiagnosticLauncherSurface()
    {
        var title = new TextBlock
        {
            Text = "Run a deeper diagnostic",
            FontSize = 15,
            FontWeight = FontWeight.SemiBold
        };
        var description = new TextBlock
        {
            Text = "Choose a preset, review the plan, then run it.",
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap
        };
        description.Classes.Add("muted");

        var heading = new StackPanel { Spacing = 3 };
        heading.Children.Add(title);
        heading.Children.Add(description);

        quickLauncherButton = CreateLauncherSelector("Quick", "Fast baseline", 1);
        fullLauncherButton = CreateLauncherSelector("Full", "Broad evidence", 2);
        stressLauncherButton = CreateLauncherSelector("Stress", "Sustained load", 3);

        var presets = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*"),
            ColumnSpacing = 8,
            MaxWidth = 560,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        presets.Children.Add(quickLauncherButton);
        Grid.SetColumn(fullLauncherButton, 1);
        presets.Children.Add(fullLauncherButton);
        Grid.SetColumn(stressLauncherButton, 2);
        presets.Children.Add(stressLauncherButton);

        var presetSection = new StackPanel
        {
            Spacing = 13,
            VerticalAlignment = VerticalAlignment.Center
        };
        presetSection.Children.Add(heading);
        presetSection.Children.Add(presets);

        var selectedLabel = new TextBlock
        {
            Text = "SELECTED PLAN",
            FontSize = 9,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = 1.1
        };
        selectedLabel.Classes.Add("eyebrow");

        launcherSelectedProfileText = new TextBlock
        {
            Text = "Quick",
            FontSize = 18,
            FontWeight = FontWeight.SemiBold
        };
        launcherSummaryText = new TextBlock
        {
            Text = "Loading run plan…",
            FontSize = 10,
            LineHeight = 16,
            MinHeight = 32,
            VerticalAlignment = VerticalAlignment.Top,
            TextWrapping = TextWrapping.Wrap
        };
        launcherSummaryText.Classes.Add("muted");

        moreLauncherButton = CreateLauncherAction("Customize…", "secondary", MoreLauncherClicked, 108);
        launcherRunButton = CreateLauncherAction("Run Quick", "primary", LauncherRunClicked, 116);

        var actionButtons = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 8,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        actionButtons.Children.Add(moreLauncherButton);
        Grid.SetColumn(launcherRunButton, 1);
        actionButtons.Children.Add(launcherRunButton);

        var planContent = new StackPanel { Spacing = 8 };
        planContent.Children.Add(selectedLabel);
        planContent.Children.Add(launcherSelectedProfileText);
        planContent.Children.Add(launcherSummaryText);
        planContent.Children.Add(actionButtons);

        var planPanel = new Border
        {
            Padding = new Thickness(16, 14),
            CornerRadius = new CornerRadius(12),
            MinHeight = 148,
            Child = planContent,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        planPanel.Classes.Add("dashboardPanel");

        var layout = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,360"),
            ColumnSpacing = 18,
            Margin = new Thickness(18, 16),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        layout.Children.Add(presetSection);
        Grid.SetColumn(planPanel, 1);
        layout.Children.Add(planPanel);
        return layout;
    }

    private Button CreateLauncherSelector(string label, string detail, int profileIndex)
    {
        var labelText = new TextBlock
        {
            Text = label,
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
        content.Children.Add(labelText);
        content.Children.Add(detailText);

        var button = new Button
        {
            Content = content,
            Tag = profileIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
            MinWidth = 0,
            MinHeight = 64,
            Padding = new Thickness(13, 10),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        button.Classes.Add("profileChoice");
        button.Click += LauncherProfileClicked;
        return button;
    }

    private static Button CreateLauncherAction(
        string label,
        string styleClass,
        EventHandler<RoutedEventArgs> clickHandler,
        double minWidth)
    {
        var button = new Button
        {
            Content = label,
            MinWidth = minWidth,
            MinHeight = 36,
            Padding = new Thickness(13, 7),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        button.Classes.Add(styleClass);
        button.Click += clickHandler;
        return button;
    }

    private IReadOnlyList<Button> ProfileButtons() =>
    [
        ConnectionProfileButton,
        QuickProfileButton,
        FullProfileButton,
        StressProfileButton
    ];

    private void EnsureDiagnosticSelectionBadges()
    {
        foreach (var button in ProfileButtons())
        {
            if (diagnosticSelectionBadges.ContainsKey(button)
                || button.Content is not StackPanel content)
            {
                continue;
            }

            var badge = new TextBlock
            {
                Text = "SELECTED",
                FontSize = 8,
                FontWeight = FontWeight.SemiBold,
                LetterSpacing = 1.1,
                IsVisible = false
            };
            badge.Classes.Add("eyebrow");
            content.Children.Insert(0, badge);
            diagnosticSelectionBadges[button] = badge;
        }
    }

    private void DiagnosticProfileVisualStateChanged(object? sender, RoutedEventArgs eventArgs) =>
        Dispatcher.UIThread.Post(() =>
        {
            RefreshDiagnosticProfileVisuals();
            SyncDiagnosticLauncherState();
        });

    private void RefreshDiagnosticProfileVisuals()
    {
        foreach (var button in ProfileButtons())
        {
            var selected = button.Classes.Contains("selected");
            button.BorderThickness = selected ? new Thickness(2) : new Thickness(1);
            button.Opacity = selected ? 1 : 0.88;
            if (diagnosticSelectionBadges.TryGetValue(button, out var badge))
            {
                badge.IsVisible = selected;
            }
        }
    }

    private void DiagnosticLauncherLayoutUpdated(object? sender, EventArgs eventArgs) =>
        SyncDiagnosticLauncherState();

    private void SyncDiagnosticLauncherState()
    {
        if (quickLauncherButton is null
            || fullLauncherButton is null
            || stressLauncherButton is null
            || moreLauncherButton is null
            || launcherRunButton is null
            || launcherSelectedProfileText is null
            || launcherSummaryText is null)
        {
            return;
        }

        var selectedProfileIndex = SelectedProfileIndex();
        if (selectedProfileIndex is >= 1 and <= 3)
        {
            pendingLauncherProfileIndex = selectedProfileIndex;
        }

        SetSelected(quickLauncherButton, selectedProfileIndex == 1);
        SetSelected(fullLauncherButton, selectedProfileIndex == 2);
        SetSelected(stressLauncherButton, selectedProfileIndex == 3);
        ApplyCustomizeButtonStyle(selectedProfileIndex == 0);

        var profileName = ProfileName(selectedProfileIndex);
        launcherSelectedProfileText.Text = profileName;

        var summary = $"{EstimatedTimeText.Text} · {TransferCapText.Text}";
        if (!string.IsNullOrWhiteSpace(ConfirmationText.Text)
            && !ConfirmationText.Text.Equals("No", StringComparison.OrdinalIgnoreCase)
            && !ConfirmationText.Text.Equals("Not required", StringComparison.OrdinalIgnoreCase))
        {
            summary += $"\nConfirmation {ConfirmationText.Text.ToLowerInvariant()}";
        }

        if (launcherSummaryText.Text != summary)
        {
            launcherSummaryText.Text = summary;
        }

        var controlsEnabled = RunButton.IsEnabled;
        var runLabel = controlsEnabled ? $"Run {profileName}" : "Diagnostic running";
        if (!Equals(launcherRunButton.Content, runLabel))
        {
            launcherRunButton.Content = runLabel;
        }

        SetEnabled(launcherRunButton, controlsEnabled);
        SetEnabled(quickLauncherButton, controlsEnabled);
        SetEnabled(fullLauncherButton, controlsEnabled);
        SetEnabled(stressLauncherButton, controlsEnabled);
        SetEnabled(moreLauncherButton, controlsEnabled);
    }

    private void ApplyCustomizeButtonStyle(bool customProfileSelected)
    {
        if (moreLauncherButton is null) return;

        var wantedClass = customProfileSelected ? "primary" : "secondary";
        var unwantedClass = customProfileSelected ? "secondary" : "primary";
        moreLauncherButton.Classes.Remove(unwantedClass);
        if (!moreLauncherButton.Classes.Contains(wantedClass))
        {
            moreLauncherButton.Classes.Add(wantedClass);
        }
    }

    private static void SetEnabled(Control control, bool enabled)
    {
        if (control.IsEnabled != enabled)
        {
            control.IsEnabled = enabled;
        }
    }

    private int SelectedProfileIndex()
    {
        if (QuickProfileButton.Classes.Contains("selected")) return 1;
        if (FullProfileButton.Classes.Contains("selected")) return 2;
        if (StressProfileButton.Classes.Contains("selected")) return 3;
        if (ConnectionProfileButton.Classes.Contains("selected")) return 0;
        return pendingLauncherProfileIndex;
    }

    private static string ProfileName(int profileIndex) => profileIndex switch
    {
        0 => "Connection Check",
        2 => "Full",
        3 => "Stress",
        _ => "Quick"
    };

    private void LauncherProfileClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is not Button { Tag: string value }
            || !int.TryParse(value, out var profileIndex))
        {
            return;
        }

        pendingLauncherProfileIndex = profileIndex;
        SetSelected(ConnectionProfileButton, false);
        SetSelected(QuickProfileButton, profileIndex == 1);
        SetSelected(FullProfileButton, profileIndex == 2);
        SetSelected(StressProfileButton, profileIndex == 3);
        SetSelected(quickLauncherButton!, profileIndex == 1);
        SetSelected(fullLauncherButton!, profileIndex == 2);
        SetSelected(stressLauncherButton!, profileIndex == 3);
        ApplyCustomizeButtonStyle(false);

        var profileName = ProfileName(profileIndex);
        launcherSelectedProfileText!.Text = profileName;
        if (!Equals(launcherRunButton!.Content, $"Run {profileName}"))
        {
            launcherRunButton.Content = $"Run {profileName}";
        }
        launcherSummaryText!.Text = "Loading saved run plan…";
        ProfileRequested?.Invoke(this, new IndexRequestedEventArgs(profileIndex));
    }

    private void DiagnosticConfiguratorCancelClicked(object? sender, RoutedEventArgs eventArgs) =>
        DiagnosticLauncherDismissRequested?.Invoke(this, EventArgs.Empty);

    private void LauncherRunClicked(object? sender, RoutedEventArgs eventArgs) =>
        RunRequested?.Invoke(this, EventArgs.Empty);

    private void MoreLauncherClicked(object? sender, RoutedEventArgs eventArgs) =>
        DiagnosticLauncherRequested?.Invoke(this, EventArgs.Empty);
}
