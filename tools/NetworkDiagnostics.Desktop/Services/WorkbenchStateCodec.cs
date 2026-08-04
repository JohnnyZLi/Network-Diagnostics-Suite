using NetworkDiagnostics.Desktop.Navigation;

namespace NetworkDiagnostics.Desktop.Services;

public sealed record DesktopWorkbenchState(
    string Destination = "test-setup",
    Guid? PrimaryId = null,
    Guid? SecondaryId = null,
    string? Section = null,
    string? SearchQuery = null,
    string? SortKey = null,
    bool SortDescending = false,
    Guid? SelectedReportId = null,
    bool InspectorOpen = true);

public static class WorkbenchStateCodec
{
    public static DesktopWorkbenchState Capture(NavigationEntry entry)
    {
        var destination = entry.Destination switch
        {
            TestResultDestination result when result.ReportId != Guid.Empty => "test-result",
            ReportListDestination => "report-list",
            ReportDetailDestination => "report-detail",
            ComparisonDestination => "comparison",
            SettingsDestination => "settings",
            _ => "test-setup"
        };

        var primaryId = entry.Destination switch
        {
            TestResultDestination result when result.ReportId != Guid.Empty => result.ReportId,
            ReportDetailDestination detail => detail.ReportId,
            ComparisonDestination comparison => comparison.BaselineId,
            _ => null
        };
        var secondaryId = entry.Destination is ComparisonDestination comparisonDestination
            ? comparisonDestination.CandidateId
            : null;
        var section = entry.Destination switch
        {
            TestResultDestination result => result.Section,
            ReportDetailDestination detail => detail.Section,
            SettingsDestination settings => settings.Section,
            _ => entry.ViewState.ResultSection
        };

        return new DesktopWorkbenchState(
            destination,
            primaryId,
            secondaryId,
            section,
            entry.ViewState.SearchQuery,
            entry.ViewState.SortKey,
            entry.ViewState.SortDescending,
            entry.ViewState.SelectedReportId,
            entry.ViewState.InspectorOpen);
    }

    public static NavigationEntry Restore(DesktopWorkbenchState? state)
    {
        state ??= new DesktopWorkbenchState();
        AppDestination destination = state.Destination switch
        {
            "test-result" when state.PrimaryId is { } resultId =>
                new TestResultDestination(resultId, NormalizeSection(state.Section)),
            "report-list" => new ReportListDestination(),
            "report-detail" when state.PrimaryId is { } reportId =>
                new ReportDetailDestination(reportId, NormalizeSection(state.Section)),
            "comparison" => new ComparisonDestination(state.PrimaryId, state.SecondaryId),
            "settings" => new SettingsDestination(NormalizeSettingsSection(state.Section)),
            _ => new TestSetupDestination()
        };

        var viewState = new NavigationViewState(
            SearchQuery: state.SearchQuery,
            SortKey: state.SortKey,
            SortDescending: state.SortDescending,
            SelectedReportId: state.SelectedReportId,
            ResultSection: state.Section,
            InspectorOpen: state.InspectorOpen);
        return new NavigationEntry(destination, viewState);
    }

    private static string NormalizeSection(string? section) =>
        string.IsNullOrWhiteSpace(section) ? "Overview" : section.Trim();

    private static string NormalizeSettingsSection(string? section) => section?.Trim() switch
    {
        "Measurement" => "Measurement",
        "Privacy & data" or "Privacy" or "Data" => "Privacy & data",
        "Storage" => "Storage",
        "Developer" => "Developer",
        _ => "General"
    };
}
