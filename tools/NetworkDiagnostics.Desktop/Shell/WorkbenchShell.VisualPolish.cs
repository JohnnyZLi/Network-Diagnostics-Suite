using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using NetworkDiagnostics.Desktop.Navigation;

namespace NetworkDiagnostics.Desktop.Shell;

public sealed partial class WorkbenchShell
{
    private Button? settingsUtilityButton;
    private Panel? toolbarActions;
    private Control? primaryNavigation;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        base.OnAttachedToVisualTree(eventArgs);
        EnsureOverlay();
        ResolveHeaderContainers();
        EnsureSettingsUtility();
        HidePrimaryPageNavigation();
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
        EnsureSettingsUtility();
        HidePrimaryPageNavigation();
        if (settingsUtilityButton is not null)
        {
            settingsUtilityButton.Content = Bounds.Width < 960 ? "⚙" : "Settings";
        }
        SimplifyContextLabel();
    }

    private void ResolveHeaderContainers()
    {
        toolbarActions ??= CommandToolbarButton.GetLogicalParent() as Panel;
        primaryNavigation ??= TestWorkspaceButton.GetLogicalParent()?.GetLogicalParent() as Control;
    }

    private void EnsureSettingsUtility()
    {
        if (settingsUtilityButton is not null || toolbarActions is null) return;
        settingsUtilityButton = new Button
        {
            Content = "Settings",
            MinWidth = 34
        };
        settingsUtilityButton.Classes.Add("utilityKey");
        settingsUtilityButton.Click += (_, _) =>
            WorkspaceRequested?.Invoke(this, new WorkspaceRequestedEventArgs(WorkspaceKind.Settings));
        toolbarActions.Children.Insert(0, settingsUtilityButton);
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
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        label.Classes.Add("shellMuted");
        BreadcrumbPanel.Children.Add(label);
    }
}
