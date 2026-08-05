using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace NetworkDiagnostics.Desktop.Shell;

public sealed partial class WorkbenchShell
{
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        base.OnAttachedToVisualTree(eventArgs);
        EnsureOverlay();
        ReportsWorkspaceButton.IsVisible = false;
        ComparisonsWorkspaceButton.IsVisible = false;
        TestWorkspaceLabel.Text = "Control center";
        SettingsWorkspaceLabel.Text = "Settings";
        LayoutUpdated += VisualPolishLayoutUpdated;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        LayoutUpdated -= VisualPolishLayoutUpdated;
        base.OnDetachedFromVisualTree(eventArgs);
    }

    private void VisualPolishLayoutUpdated(object? sender, EventArgs eventArgs)
    {
        ReportsWorkspaceButton.IsVisible = false;
        ComparisonsWorkspaceButton.IsVisible = false;
        TestWorkspaceLabel.Text = Bounds.Width < 960 ? "Home" : "Control center";
        SimplifyContextLabel();
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
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        label.Classes.Add("shellMuted");
        BreadcrumbPanel.Children.Add(label);
    }
}
