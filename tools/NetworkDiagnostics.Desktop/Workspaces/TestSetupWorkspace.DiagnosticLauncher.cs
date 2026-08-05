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
            Text = "Choose a diagnostic profile",
            FontSize = 20,
            FontWeight = FontWeight.SemiBold
        };
        var description = new TextBlock
        {
            Text = "Select the depth of evidence, transfer method, and run plan without moving the Home layout.",
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

        launcherHost.Child = CreateDiagnosticLauncherButton();
        diagnosticLauncherInstalled = true;
    }

    private Button CreateDiagnosticLauncherButton()
    {
        var title = new TextBlock
        {
            Text = "Run a deeper diagnostic",
            FontSize = 14,
            FontWeight = FontWeight.SemiBold
        };
        var description = new TextBlock
        {
            Text = "Path, service, scaling, and local-network tests.",
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

        var action = new TextBlock
        {
            Text = "CHOOSE DIAGNOSTIC",
            FontSize = 10,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = 0.8,
            VerticalAlignment = VerticalAlignment.Center
        };
        action.Classes.Add("eyebrow");
        var arrow = new TextBlock
        {
            Text = "›",
            FontSize = 24,
            LineHeight = 24,
            VerticalAlignment = VerticalAlignment.Center
        };
        var actionRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            VerticalAlignment = VerticalAlignment.Center
        };
        actionRow.Children.Add(action);
        actionRow.Children.Add(arrow);

        var layout = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 18
        };
        layout.Children.Add(copy);
        Grid.SetColumn(actionRow, 1);
        layout.Children.Add(actionRow);

        var button = new Button
        {
            MinHeight = 82,
            Padding = new Thickness(18, 13),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(14),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = layout
        };
        button.Classes.Add("ghost");
        button.Click += DiagnosticLauncherClicked;
        return button;
    }

    private void DiagnosticLauncherClicked(object? sender, RoutedEventArgs eventArgs) =>
        DiagnosticLauncherRequested?.Invoke(this, EventArgs.Empty);
}
