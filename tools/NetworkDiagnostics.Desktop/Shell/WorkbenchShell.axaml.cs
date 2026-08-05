using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using NetworkDiagnostics.Desktop.Navigation;

namespace NetworkDiagnostics.Desktop.Shell;

public sealed partial class WorkbenchShell : UserControl
{
    private const string GenericSettingsDetail = "Settings are organized by purpose and participate in the same back and forward history as every other workspace.";
    private bool inspectorRequested = true;

    public WorkbenchShell()
    {
        InitializeComponent();
        SizeChanged += ShellSizeChanged;
        CommandPaletteControl.CommandInvoked += CommandPaletteInvoked;
    }

    public event EventHandler? BackRequested;

    public event EventHandler? ForwardRequested;

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
        RenderBreadcrumbs(entry.Destination.Breadcrumbs);
        SelectWorkspace(entry.Destination.Workspace);
        SetInspectorOpen(entry.ViewState.InspectorOpen);

        InspectorWorkspaceText.Text = entry.Destination.Workspace.ToString();
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
        inspectorRequested = open;
        ApplyResponsiveLayout(Bounds.Width);
    }

    public void OpenCommandPalette(IReadOnlyList<WorkbenchCommand> commands) =>
        CommandPaletteControl.Open(commands);

    public void CloseCommandPalette() => CommandPaletteControl.Close();

    private void BackClicked(object? sender, RoutedEventArgs eventArgs) =>
        BackRequested?.Invoke(this, EventArgs.Empty);

    private void ForwardClicked(object? sender, RoutedEventArgs eventArgs) =>
        ForwardRequested?.Invoke(this, EventArgs.Empty);

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
        inspectorRequested = !InspectorBorder.IsVisible;
        ApplyResponsiveLayout(Bounds.Width);
        InspectorVisibilityChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ShellSizeChanged(object? sender, SizeChangedEventArgs eventArgs) =>
        ApplyResponsiveLayout(eventArgs.NewSize.Width);

    private void ApplyResponsiveLayout(double width)
    {
        var compactSidebar = width < 1080;
        var showInspector = inspectorRequested && width >= 760;

        ShellGrid.ColumnDefinitions[0].Width = new GridLength(compactSidebar ? 164 : 188);
        ShellGrid.ColumnDefinitions[2].Width = new GridLength(showInspector ? 272 : 0);

        ProductNameText.IsVisible = !compactSidebar;
        ProductModeText.IsVisible = !compactSidebar;
        TestWorkspaceLabel.IsVisible = true;
        ReportsWorkspaceLabel.IsVisible = true;
        ComparisonsWorkspaceLabel.IsVisible = true;
        SettingsWorkspaceLabel.IsVisible = true;
        CommandHintText.IsVisible = !compactSidebar;
        ActiveRunTitleText.IsVisible = true;
        ActiveRunDetailText.IsVisible = !compactSidebar;

        InspectorBorder.IsVisible = showInspector;
        InspectorToggleButton.Content = showInspector ? "Hide info" : "Inspector";
    }

    private void RenderBreadcrumbs(IReadOnlyList<BreadcrumbSegment> breadcrumbs)
    {
        BreadcrumbPanel.Children.Clear();

        for (var index = 0; index < breadcrumbs.Count; index++)
        {
            if (index > 0)
            {
                var separator = new TextBlock
                {
                    Text = "/",
                    FontSize = 11,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                };
                separator.Classes.Add("shellMuted");
                BreadcrumbPanel.Children.Add(separator);
            }

            var segment = breadcrumbs[index];
            var button = new Button
            {
                Content = segment.Label,
                Tag = segment.Destination,
                IsEnabled = segment.Destination is not null
            };
            button.Classes.Add("breadcrumb");
            button.Click += BreadcrumbClicked;
            BreadcrumbPanel.Children.Add(button);
        }
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
                "Set the starting profile, transfer method, and interface for new diagnostics. Existing and active tests keep their own configuration.")
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
