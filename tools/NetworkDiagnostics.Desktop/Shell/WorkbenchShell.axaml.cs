using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using NetworkDiagnostics.Desktop.Navigation;

namespace NetworkDiagnostics.Desktop.Shell;

public sealed partial class WorkbenchShell : UserControl
{
    private const string GenericSettingsDetail = "Settings are organized by purpose and participate in the same back and forward history as every other workspace.";
    private const double InspectorMinimumWidth = 1180;
    private const string StableProductContext = "Local diagnostics";
    private bool inspectorRequested;
    private WorkspaceKind currentWorkspace = WorkspaceKind.Test;

    public WorkbenchShell()
    {
        InitializeComponent();
        SizeChanged += ShellSizeChanged;
        CommandPaletteControl.CommandInvoked += CommandPaletteInvoked;
    }

    public event EventHandler? BackRequested;

    public event EventHandler? ForwardRequested;

    public event EventHandler? HomeRequested;

    public event EventHandler? ActiveRunRequested;

    public event EventHandler? CommandPaletteRequested;

    public event EventHandler<CommandInvokedEventArgs>? CommandInvoked;

    public event EventHandler<WorkspaceRequestedEventArgs>? WorkspaceRequested;

    public event EventHandler<DestinationRequestedEventArgs>? DestinationRequested;

    public event EventHandler? InspectorVisibilityChanged;

    public object? WorkspaceContent
    {
        get => WorkspaceHost.Content;
        set
        {
            if (value is Control control
                && control.GetLogicalParent() is ContentControl parent
                && ReferenceEquals(parent.Content, control))
            {
                parent.Content = null;
            }

            WorkspaceHost.Content = value;
        }
    }

    public bool InspectorOpen => InspectorBorder.IsVisible;

    public bool CommandPaletteOpen => CommandPaletteControl.IsOpen;

    public void SetNavigation(
        NavigationEntry entry,
        bool canGoBack,
        bool canGoForward)
    {
        BackButton.IsEnabled = canGoBack;
        ForwardButton.IsEnabled = canGoForward;
        currentWorkspace = entry.Destination.Workspace;
        RenderProductContext();
        SelectWorkspace(currentWorkspace);

        if (currentWorkspace != WorkspaceKind.Test || !entry.ViewState.InspectorOpen)
        {
            inspectorRequested = false;
        }
        ApplyResponsiveLayout(Bounds.Width);

        InspectorWorkspaceText.Text = currentWorkspace == WorkspaceKind.Test
            ? "Overview"
            : currentWorkspace.ToString();
        InspectorSelectionText.Text = SelectionLabel(entry.Destination);
    }

    public void SetInspectorContent(
        string title,
        string detail,
        string? selection = null)
    {
        var resolved = ResolveInspectorCopy(title, detail);
        InspectorTitleText.Text = resolved.Title;
        InspectorDetailText.Text = resolved.Detail;
        if (!string.IsNullOrWhiteSpace(selection))
        {
            InspectorSelectionText.Text = selection;
        }
    }

    public void SetInspectorBody(Control? content) => InspectorBodyHost.Content = content;

    public void SetStatus(
        string? interfaceLabel,
        string? endpointLabel,
        string? networkLabel,
        string activityLabel)
    {
        StatusInterfaceText.Text = $"Interface · {Fallback(interfaceLabel, "Automatic")}";
        StatusEndpointText.Text = $"Endpoint · {Fallback(endpointLabel, "Checking")}";
        StatusNetworkText.Text = $"Network · {Fallback(networkLabel, "Unknown")}";
        StatusActivityText.Text = activityLabel;
    }

    public void SetActiveRun(
        bool visible,
        string title,
        string detail,
        double progress)
    {
        ActiveRunPanel.IsVisible = visible;
        ActiveRunTitleText.Text = title;
        ActiveRunDetailText.Text = detail;
        ActiveRunProgress.Value = Math.Clamp(progress, 0, 100);
    }

    public void SetInspectorOpen(bool open)
    {
        inspectorRequested = open && currentWorkspace == WorkspaceKind.Test;
        ApplyResponsiveLayout(Bounds.Width);
    }

    public void RefreshResponsiveChrome() => ApplyResponsiveLayout(Bounds.Width);

    public void OpenCommandPalette(IReadOnlyList<WorkbenchCommand> commands) =>
        CommandPaletteControl.Open(commands);

    public void CloseCommandPalette() => CommandPaletteControl.Close();

    private void BackClicked(object? sender, RoutedEventArgs eventArgs) =>
        BackRequested?.Invoke(this, EventArgs.Empty);

    private void ForwardClicked(object? sender, RoutedEventArgs eventArgs) =>
        ForwardRequested?.Invoke(this, EventArgs.Empty);

    private void HomeClicked(object? sender, RoutedEventArgs eventArgs) =>
        HomeRequested?.Invoke(this, EventArgs.Empty);

    private void SettingsClicked(object? sender, RoutedEventArgs eventArgs) =>
        WorkspaceRequested?.Invoke(this, new WorkspaceRequestedEventArgs(WorkspaceKind.Settings));

    private void ActiveRunClicked(object? sender, RoutedEventArgs eventArgs) =>
        ActiveRunRequested?.Invoke(this, EventArgs.Empty);

    private void CommandsClicked(object? sender, RoutedEventArgs eventArgs) =>
        CommandPaletteRequested?.Invoke(this, EventArgs.Empty);

    private void CommandPaletteInvoked(object? sender, CommandInvokedEventArgs eventArgs) =>
        CommandInvoked?.Invoke(this, eventArgs);

    private void WorkspaceClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is not Button { Tag: string workspaceName }
            || !Enum.TryParse<WorkspaceKind>(workspaceName, out var workspace))
        {
            return;
        }

        WorkspaceRequested?.Invoke(this, new WorkspaceRequestedEventArgs(workspace));
    }

    private void BreadcrumbClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button { Tag: AppDestination destination })
        {
            DestinationRequested?.Invoke(this, new DestinationRequestedEventArgs(destination));
        }
    }

    private void InspectorClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (currentWorkspace != WorkspaceKind.Test || Bounds.Width < InspectorMinimumWidth || OverlayOpen) return;
        inspectorRequested = !InspectorBorder.IsVisible;
        ApplyResponsiveLayout(Bounds.Width);
        InspectorVisibilityChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ShellSizeChanged(object? sender, SizeChangedEventArgs eventArgs) =>
        ApplyResponsiveLayout(eventArgs.NewSize.Width);

    private void ApplyResponsiveLayout(double width)
    {
        var compact = width < 960;
        var inspectorEligible = currentWorkspace == WorkspaceKind.Test
            && width >= InspectorMinimumWidth
            && !OverlayOpen;
        var showInspector = inspectorEligible && inspectorRequested;

        InspectorBorder.IsVisible = showInspector;
        InspectorToggleButton.IsVisible = width >= 960;
        InspectorToggleButton.IsEnabled = inspectorEligible;
        ProductStack.IsVisible = width >= 880;
        ActiveRunDetailText.IsVisible = width >= 1180;
        HomeButton.Content = compact ? "⌂" : "Home";
        HomeButton.MinWidth = compact ? 34 : 52;
        SettingsToolbarButton.Content = compact ? "⚙" : "Settings";
        SettingsToolbarButton.MinWidth = compact ? 34 : 58;
        CommandToolbarButton.Content = compact ? "⌘K" : "Commands  ⌘K";
        InspectorToggleButton.Content = showInspector ? "Close info" : "Info";
        TestWorkspaceLabel.Text = compact ? "Home" : "Overview";
        ReportsWorkspaceLabel.Text = "History";
        ComparisonsWorkspaceLabel.Text = "Compare";
        SettingsWorkspaceLabel.Text = "Settings";
    }

    private void RenderProductContext()
    {
        if (BreadcrumbPanel.Children.Count == 1
            && BreadcrumbPanel.Children[0] is TextBlock current
            && string.Equals(current.Text, StableProductContext, StringComparison.Ordinal))
        {
            return;
        }

        BreadcrumbPanel.Children.Clear();
        var text = new TextBlock
        {
            Text = StableProductContext,
            FontSize = 8,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
            MaxWidth = 148
        };
        text.Classes.Add("shellMuted");
        BreadcrumbPanel.Children.Add(text);
    }

    private void SelectWorkspace(WorkspaceKind workspace)
    {
        SetSelected(TestWorkspaceButton, workspace == WorkspaceKind.Test);
        SetSelected(ReportsWorkspaceButton, workspace == WorkspaceKind.Reports);
        SetSelected(ComparisonsWorkspaceButton, workspace == WorkspaceKind.Comparisons);
        SetSelected(SettingsWorkspaceButton, workspace == WorkspaceKind.Settings);
    }

    private static void SetSelected(Button button, bool selected)
    {
        if (selected)
        {
            if (!button.Classes.Contains("selected")) button.Classes.Add("selected");
        }
        else
        {
            button.Classes.Remove("selected");
        }
    }

    private static (string Title, string Detail) ResolveInspectorCopy(string title, string detail)
    {
        if (!string.Equals(detail, GenericSettingsDetail, StringComparison.Ordinal))
        {
            return (title, detail);
        }

        return title switch
        {
            "Measurement" => (
                "Measurement path",
                "Manage endpoint selection and optional trusted-LAN isolation. Saved changes apply to future diagnostics, not a run already in progress."),
            "Privacy & data" => (
                "Privacy controls",
                "Choose whether saved reports include supported local identifiers and clear remembered approvals when you want higher-data profiles to ask again."),
            "Storage" => (
                "Local report storage",
                "Reports remain in the local application directory until you explicitly export them. Open the folder to inspect or back up the JSON files."),
            "Developer" => (
                "Presentation previews",
                "Preview terminal result states without network activity, changing measurement settings, or saving a diagnostic report."),
            _ => (
                "Application defaults",
                "Set monitoring, appearance, and diagnostic defaults. Existing and active tests keep their own configuration.")
        };
    }

    private static string SelectionLabel(AppDestination destination) => destination switch
    {
        RunningTestDestination running => running.RunId.ToString("N")[..8],
        TestResultDestination result => result.Section,
        ReportDetailDestination detail => detail.Section,
        ComparisonDestination comparison when comparison.BaselineId is not null && comparison.CandidateId is not null => "Two reports",
        ComparisonDestination comparison when comparison.BaselineId is not null => "Baseline selected",
        SettingsDestination settings => settings.Section,
        TestSetupDestination => "Live monitor",
        _ => "None"
    };

    private static string Fallback(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}

public sealed class WorkspaceRequestedEventArgs(WorkspaceKind workspace) : EventArgs
{
    public WorkspaceKind Workspace { get; } = workspace;
}

public sealed class DestinationRequestedEventArgs(AppDestination destination) : EventArgs
{
    public AppDestination Destination { get; } = destination;
}
