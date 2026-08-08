using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace NetworkDiagnostics.Desktop.Workspaces;

public sealed partial class TestSetupWorkspace
{
    private readonly Dictionary<Button, TextBlock> polishedProfileIndicators = new();
    private bool polishedDiagnosticConfiguratorBuilt;

    public void ApplyDiagnosticConfiguratorPolish()
    {
        if (polishedDiagnosticConfiguratorBuilt
            || diagnosticLauncherContent is not ScrollViewer scrollViewer)
        {
            return;
        }

        DetachConfiguratorControl(ProfileGrid);
        DetachConfiguratorControl(SelectedQuestionPanel);
        DetachConfiguratorControl(MethodPanel);
        DetachConfiguratorControl(RunPlanPanel);
        DetachConfiguratorControl(RunActionPanel);
        DetachConfiguratorControl(InterfaceText);
        DetachConfiguratorControl(EndpointText);
        DetachConfiguratorControl(NetworkText);

        ConfigurePolishedProfileRail();
        ConfigurePolishedQuestionPanel();
        ConfigurePolishedMethodPanel();
        ConfigurePolishedRunPlanPanel();

        var profileRail = BuildPolishedProfileRail();
        var configurationCard = BuildPolishedConfigurationCard();
        var actionFooter = BuildPolishedActionFooter();

        var rightColumn = new StackPanel
        {
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Top
        };
        rightColumn.Children.Add(configurationCard);
        rightColumn.Children.Add(actionFooter);

        var configurator = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("246,*"),
            ColumnSpacing = 16,
            Margin = new Thickness(18),
            MaxWidth = 920,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top
        };
        configurator.Children.Add(profileRail);
        Grid.SetColumn(rightColumn, 1);
        configurator.Children.Add(rightColumn);

        scrollViewer.Content = configurator;
        scrollViewer.Padding = new Thickness(0);

        foreach (var button in ProfileButtons())
        {
            button.Click += PolishedConfiguratorProfileClicked;
        }

        LayoutUpdated += PolishedConfiguratorLayoutUpdated;
        polishedDiagnosticConfiguratorBuilt = true;
        RefreshPolishedConfiguratorVisualState();
    }

    private Border BuildPolishedProfileRail()
    {
        var profileLabel = new TextBlock
        {
            Text = "DIAGNOSTIC PROFILE",
            FontSize = 9.5,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = 1.15
        };
        profileLabel.Classes.Add("eyebrow");

        var profileHint = new TextBlock
        {
            Text = "Choose how much evidence to collect.",
            FontSize = 10.5,
            LineHeight = 16,
            TextWrapping = TextWrapping.Wrap
        };
        profileHint.Classes.Add("secondary");

        var profileStack = new StackPanel
        {
            Spacing = 11
        };
        profileStack.Children.Add(profileLabel);
        profileStack.Children.Add(profileHint);
        profileStack.Children.Add(ProfileGrid);

        var rail = new Border
        {
            Padding = new Thickness(14),
            CornerRadius = new CornerRadius(12),
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = profileStack
        };
        rail.Classes.Add("dashboardPanel");
        return rail;
    }

    private Border BuildPolishedConfigurationCard()
    {
        var divider = new Border
        {
            Margin = new Thickness(0, 2, 0, 0)
        };
        divider.Classes.Add("divider");

        var methodCard = new Border
        {
            Padding = new Thickness(14, 13),
            CornerRadius = new CornerRadius(10),
            Child = MethodPanel
        };
        methodCard.Classes.Add("surfaceSubtle");

        var planCard = new Border
        {
            Padding = new Thickness(14, 13),
            CornerRadius = new CornerRadius(10),
            Child = RunPlanPanel
        };
        planCard.Classes.Add("surfaceSubtle");

        var options = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1.08*,0.92*"),
            ColumnSpacing = 12
        };
        options.Children.Add(methodCard);
        Grid.SetColumn(planCard, 1);
        options.Children.Add(planCard);

        var stack = new StackPanel
        {
            Spacing = 14
        };
        stack.Children.Add(SelectedQuestionPanel);
        stack.Children.Add(divider);
        stack.Children.Add(options);

        var card = new Border
        {
            Padding = new Thickness(18, 16),
            CornerRadius = new CornerRadius(12),
            Child = stack
        };
        card.Classes.Add("dashboardPanel");
        return card;
    }

    private Border BuildPolishedActionFooter()
    {
        var routeLabel = new TextBlock
        {
            Text = "CURRENT ROUTE",
            FontSize = 9.5,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = 1.1
        };
        routeLabel.Classes.Add("eyebrow");

        ConfigureRouteText(InterfaceText);
        ConfigureRouteText(EndpointText);
        ConfigureRouteText(NetworkText);

        var routeLines = new StackPanel
        {
            Spacing = 2
        };
        routeLines.Children.Add(InterfaceText);
        routeLines.Children.Add(EndpointText);
        routeLines.Children.Add(NetworkText);

        var routeContent = new StackPanel
        {
            Spacing = 5,
            VerticalAlignment = VerticalAlignment.Center
        };
        routeContent.Children.Add(routeLabel);
        routeContent.Children.Add(routeLines);

        var accentRule = new Border
        {
            Width = 3,
            CornerRadius = new CornerRadius(2),
            VerticalAlignment = VerticalAlignment.Stretch
        };
        accentRule.Classes.Add("indicatorAccent");

        var cancelButton = new Button
        {
            Content = "Cancel",
            MinWidth = 92,
            MinHeight = 40,
            Padding = new Thickness(14, 8),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        cancelButton.Classes.Add("secondary");
        cancelButton.Click += DiagnosticConfiguratorCancelClicked;

        RunButton.MinWidth = 154;
        RunButton.MinHeight = 40;
        RunButton.Padding = new Thickness(16, 8);
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
            ColumnDefinitions = new ColumnDefinitions("3,*,Auto"),
            ColumnSpacing = 12
        };
        footerGrid.Children.Add(accentRule);
        Grid.SetColumn(routeContent, 1);
        footerGrid.Children.Add(routeContent);
        Grid.SetColumn(actions, 2);
        footerGrid.Children.Add(actions);

        var footer = new Border
        {
            Padding = new Thickness(14, 12),
            CornerRadius = new CornerRadius(12),
            Child = footerGrid
        };
        footer.Classes.Add("dashboardPanel");
        return footer;
    }

    private void ConfigurePolishedProfileRail()
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
        ProfileGrid.Margin = new Thickness(0, 3, 0, 0);

        var buttons = ProfileButtons();
        for (var index = 0; index < buttons.Count; index++)
        {
            var button = buttons[index];
            SetGridPosition(button, index, 0);
            button.MinHeight = 61;
            button.Padding = new Thickness(13, 10);
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            button.VerticalContentAlignment = VerticalAlignment.Center;
            button.Opacity = 1;
            InstallPolishedProfileIndicator(button);
        }
    }

    private void InstallPolishedProfileIndicator(Button button)
    {
        if (polishedProfileIndicators.ContainsKey(button)
            || button.Content is not StackPanel originalContent)
        {
            return;
        }

        foreach (var text in originalContent.Children.OfType<TextBlock>())
        {
            if (text.FontWeight >= FontWeight.SemiBold)
            {
                text.FontSize = 12.5;
            }
            else
            {
                text.FontSize = 9.5;
                text.LineHeight = 13;
            }
        }

        var indicator = new TextBlock
        {
            Text = "✓",
            FontSize = 13,
            FontWeight = FontWeight.Bold,
            IsVisible = false,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(8, 0, 0, 0)
        };
        indicator.Classes.Add("eyebrow");

        button.Content = null;
        var contentGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 5
        };
        contentGrid.Children.Add(originalContent);
        Grid.SetColumn(indicator, 1);
        contentGrid.Children.Add(indicator);
        button.Content = contentGrid;
        polishedProfileIndicators[button] = indicator;
    }

    private void ConfigurePolishedQuestionPanel()
    {
        SelectedQuestionPanel.Spacing = 6;
        QuestionText.FontSize = 18;
        QuestionText.LineHeight = 22;
        PurposeText.FontSize = 11;
        PurposeText.LineHeight = 17;
        PurposeText.MaxWidth = 620;
    }

    private void ConfigurePolishedMethodPanel()
    {
        MethodPanel.Spacing = 9;
        MethodDetailText.FontSize = 10;
        MethodDetailText.LineHeight = 15;
        MethodDetailText.MinHeight = 30;

        foreach (var button in new[] { CompareMethodButton, SingleMethodButton, AggregateMethodButton })
        {
            button.MinHeight = 35;
            button.Padding = new Thickness(11, 6);
            button.FontSize = 10.5;
        }
    }

    private void ConfigurePolishedRunPlanPanel()
    {
        RunPlanPanel.Spacing = 9;
        AvailabilityText.FontSize = 10;
        AvailabilityText.LineHeight = 15;
        AvailabilityText.MinHeight = 30;
        EstimatedTimeText.FontSize = 10.5;
        TransferCapText.FontSize = 10.5;
        ConfirmationText.FontSize = 10.5;
    }

    private static void ConfigureRouteText(TextBlock text)
    {
        text.FontSize = 9.5;
        text.LineHeight = 13;
        text.TextWrapping = TextWrapping.NoWrap;
        text.TextTrimming = TextTrimming.CharacterEllipsis;
        text.MaxWidth = 430;
    }

    private void PolishedConfiguratorProfileClicked(object? sender, RoutedEventArgs eventArgs) =>
        Dispatcher.UIThread.Post(RefreshPolishedConfiguratorVisualState);

    private void PolishedConfiguratorLayoutUpdated(object? sender, EventArgs eventArgs) =>
        RefreshPolishedConfiguratorVisualState();

    private void RefreshPolishedConfiguratorVisualState()
    {
        if (!polishedDiagnosticConfiguratorBuilt)
        {
            return;
        }

        foreach (var button in ProfileButtons())
        {
            var selected = button.Classes.Contains("selected");
            button.Opacity = 1;
            button.BorderThickness = selected ? new Thickness(2) : new Thickness(1);
            if (polishedProfileIndicators.TryGetValue(button, out var indicator))
            {
                indicator.IsVisible = selected;
            }
        }

        var profileName = ProfileName(SelectedProfileIndex());
        var runLabel = RunButton.IsEnabled ? $"Run {profileName}" : "Diagnostic running";
        if (!Equals(RunButton.Content, runLabel))
        {
            RunButton.Content = runLabel;
        }
    }

    private static void DetachConfiguratorControl(Control control)
    {
        switch (control.Parent)
        {
            case Panel panel:
                panel.Children.Remove(control);
                break;
            case Border border when ReferenceEquals(border.Child, control):
                border.Child = null;
                break;
            case ContentControl contentControl when ReferenceEquals(contentControl.Content, control):
                contentControl.Content = null;
                break;
        }
    }
}
