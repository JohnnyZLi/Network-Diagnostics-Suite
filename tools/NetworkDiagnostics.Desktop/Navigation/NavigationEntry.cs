namespace NetworkDiagnostics.Desktop.Navigation;

public sealed record NavigationViewState(
    string? SearchQuery = null,
    string? SortKey = null,
    bool SortDescending = false,
    Guid? SelectedReportId = null,
    double VerticalOffset = 0,
    string? ResultSection = null,
    bool SidebarCompact = false,
    bool InspectorOpen = true,
    IReadOnlyDictionary<string, string>? Filters = null,
    IReadOnlyDictionary<string, bool>? ExpandedSections = null,
    IReadOnlyDictionary<string, double>? ColumnWidths = null);

public sealed record NavigationEntry(
    AppDestination Destination,
    NavigationViewState ViewState);

public sealed class NavigationChangedEventArgs(NavigationEntry current) : EventArgs
{
    public NavigationEntry Current { get; } = current;
}
