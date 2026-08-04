using NetworkDiagnostics.Desktop.Services;

namespace NetworkDiagnostics.Desktop.Workspaces;

public sealed class StoredReportEventArgs(StoredReport report) : EventArgs
{
    public StoredReport Report { get; } = report;
}

public sealed record ReportBrowserState(
    string SearchQuery,
    string SortKey,
    bool SortDescending,
    Guid? SelectedReportId);

public sealed class ReportBrowserStateChangedEventArgs(ReportBrowserState state) : EventArgs
{
    public ReportBrowserState State { get; } = state;
}
