using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace NetworkDiagnostics.Desktop.Workspaces;

public sealed partial class TestSetupWorkspace
{
    private Control? diagnosticLauncherContent;
    private bool diagnosticLauncherInstalled;

    public event EventHandler? DiagnosticLauncherRequested;

    public event EventHandler<IndexRequestedEventArgs>? DiagnosticRunRequested;

    public Control? DiagnosticLauncherContent => diagnosticLauncherContent;

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

        var title = new TextBlock
        {
            Text = "Customize a diagnostic",
            FontSize = 20,
            FontWeight = FontWeight.SemiBold
        };
        var description = new TextBlock
        {
            Text = "Choose a profile, transfer method, interface, and run plan without moving the Home layout.",
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap
        };
        description.Classes.Add("secondary");

        var intro = new StackPanel { Spacing = 4 };
        intro.Children.Add(title);
        intro.Children.Add(description);

        var overlayBody = new StackPanel
        {
            Margin = new Thickness(24, 20, 24, 28),
            Spacing = 18
        };
        overlayBody.Children.Add(intro);
        overlayBody.Children.Add(diagnosticContent);

        diagnosticLauncherContent = new ScrollViewer
        {
            Content = overlayBody,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
        };

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

    private void QuickLauncherClicked(object? sender, RoutedEventArgs eventArgs) =>
        DiagnosticRunRequested?.Invoke(this, new IndexRequestedEventArgs(1));

    private void FullLauncherClicked(object? sender, RoutedEventArgs eventArgs) =>
        DiagnosticRunRequested?.Invoke(this, new IndexRequestedEventArgs(2));

    private void StressLauncherClicked(object? sender, RoutedEventArgs eventArgs) =>
        DiagnosticRunRequested?.Invoke(this, new IndexRequestedEventArgs(3));

    private void MoreLauncherClicked(object? sender, RoutedEventArgs eventArgs) =>
        DiagnosticLauncherRequested?.Invoke(this, EventArgs.Empty);
}
