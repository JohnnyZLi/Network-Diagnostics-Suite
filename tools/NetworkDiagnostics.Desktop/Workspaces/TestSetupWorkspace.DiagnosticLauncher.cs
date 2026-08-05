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
    private bool diagnosticLauncherInstalled;

    public event EventHandler? DiagnosticLauncherRequested;

    public event EventHandler<IndexRequestedEventArgs>? DiagnosticRunRequested;

    public Control? DiagnosticLauncherContent => diagnosticLauncherContent;

    public void PrepareDiagnosticLauncherLayout()
    {
        InstallDiagnosticLauncher();

        ConfigureDiagnosticLayout(900);
        ProfileGrid.ColumnSpacing = 10;
        ProfileGrid.RowSpacing = 10;
        ProfileGrid.Margin = new Thickness(0, 2, 0, 0);

        DiagnosticDetailsGrid.ColumnDefinitions.Clear();
        DiagnosticDetailsGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1.08, GridUnitType.Star)));
        DiagnosticDetailsGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(0.92, GridUnitType.Star)));
        DiagnosticDetailsGrid.RowDefinitions.Clear();
        DiagnosticDetailsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        DiagnosticDetailsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        DiagnosticDetailsGrid.ColumnSpacing = 26;
        DiagnosticDetailsGrid.RowSpacing = 20;

        SetGridPosition(SelectedQuestionPanel, 0, 0);
        SetGridPosition(RunPlanPanel, 0, 1);
        SetGridPosition(MethodPanel, 1, 0);
        SetGridPosition(RunActionPanel, 1, 1);

        foreach (var button in ProfileButtons())
        {
            button.MinHeight = 74;
            button.Padding = new Thickness(14, 11);
            button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        }

        RunActionPanel.MinWidth = 0;
        RunActionPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
        RunButton.MinWidth = 0;
        RunButton.HorizontalAlignment = HorizontalAlignment.Stretch;
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
        if (diagnosticContent is Border diagnosticBorder)
        {
            diagnosticBorder.BorderThickness = new Thickness(0);
            diagnosticBorder.Padding = new Thickness(0);
        }

        var description = new TextBlock
        {
            Text = "Choose a profile, transfer method, and run plan. Interface and endpoint details are reviewed before the test starts.",
            FontSize = 11,
            LineHeight = 17,
            TextWrapping = TextWrapping.Wrap
        };
        description.Classes.Add("secondary");

        var overlayBody = new StackPanel
        {
            Margin = new Thickness(22, 18, 22, 24),
            Spacing = 14
        };
        overlayBody.Children.Add(description);
        overlayBody.Children.Add(diagnosticContent);

        diagnosticLauncherContent = new ScrollViewer
        {
            Content = overlayBody,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
        };

        EnsureDiagnosticSelectionBadges();
        foreach (var button in ProfileButtons())
        {
            button.Click += DiagnosticProfileVisualStateChanged;
        }

        launcherHost.Child = CreateDiagnosticLauncherSurface();
        diagnosticLauncherInstalled = true;
    }

    private Control CreateDiagnosticLauncherSurface()
    {
        var title = new TextBlock
        {
            Text = "Run a deeper diagnostic",
            FontSize = 14,
            FontWeight = FontWeight.SemiBold
        };
        var description = new TextBlock
        {
            Text = "Start with saved defaults, or open the full configuration.",
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap
        };
        description.Classes.Add("muted");

        var copy = new StackPanel
        {
            Spacing = 3,
            VerticalAlignment = VerticalAlignment.Center
        };
        copy.Children.Add(title);
        copy.Children.Add(description);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 7,
            VerticalAlignment = VerticalAlignment.Center
        };
        actions.Children.Add(CreateLauncherAction("Quick", "primary", QuickLauncherClicked, 78));
        actions.Children.Add(CreateLauncherAction("Full", "secondary", FullLauncherClicked, 74));
        actions.Children.Add(CreateLauncherAction("Stress", "secondary", StressLauncherClicked, 82));
        actions.Children.Add(CreateLauncherAction("More…", "ghost", MoreLauncherClicked, 78));

        var layout = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 18,
            MinHeight = 82,
            Margin = new Thickness(18, 0)
        };
        layout.Children.Add(copy);
        Grid.SetColumn(actions, 1);
        layout.Children.Add(actions);
        return layout;
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
        Dispatcher.UIThread.Post(RefreshDiagnosticProfileVisuals);

    private void RefreshDiagnosticProfileVisuals()
    {
        EnsureDiagnosticSelectionBadges();
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

    private void QuickLauncherClicked(object? sender, RoutedEventArgs eventArgs) =>
        DiagnosticRunRequested?.Invoke(this, new IndexRequestedEventArgs(1));

    private void FullLauncherClicked(object? sender, RoutedEventArgs eventArgs) =>
        DiagnosticRunRequested?.Invoke(this, new IndexRequestedEventArgs(2));

    private void StressLauncherClicked(object? sender, RoutedEventArgs eventArgs) =>
        DiagnosticRunRequested?.Invoke(this, new IndexRequestedEventArgs(3));

    private void MoreLauncherClicked(object? sender, RoutedEventArgs eventArgs) =>
        DiagnosticLauncherRequested?.Invoke(this, EventArgs.Empty);
}
