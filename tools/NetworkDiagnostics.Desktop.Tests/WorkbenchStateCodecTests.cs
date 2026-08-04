using NetworkDiagnostics.Desktop.Navigation;
using NetworkDiagnostics.Desktop.Services;
using Xunit;

namespace NetworkDiagnostics.Desktop.Tests;

public sealed class WorkbenchStateCodecTests
{
    [Fact]
    public void ReportBrowserStateRoundTripsSearchSortSelectionAndInspector()
    {
        var selected = Guid.NewGuid();
        var entry = new NavigationEntry(
            new ReportListDestination(),
            new NavigationViewState(
                SearchQuery: "frontier quick",
                SortKey: "profile",
                SortDescending: true,
                SelectedReportId: selected,
                InspectorOpen: false));

        var restored = WorkbenchStateCodec.Restore(WorkbenchStateCodec.Capture(entry));

        Assert.IsType<ReportListDestination>(restored.Destination);
        Assert.Equal("frontier quick", restored.ViewState.SearchQuery);
        Assert.Equal("profile", restored.ViewState.SortKey);
        Assert.True(restored.ViewState.SortDescending);
        Assert.Equal(selected, restored.ViewState.SelectedReportId);
        Assert.False(restored.ViewState.InspectorOpen);
    }

    [Fact]
    public void SettingsSectionRoundTrips()
    {
        var entry = new NavigationEntry(
            new SettingsDestination("Privacy & data"),
            new NavigationViewState(InspectorOpen: true));

        var restored = WorkbenchStateCodec.Restore(WorkbenchStateCodec.Capture(entry));

        var destination = Assert.IsType<SettingsDestination>(restored.Destination);
        Assert.Equal("Privacy & data", destination.Section);
        Assert.True(restored.ViewState.InspectorOpen);
    }

    [Fact]
    public void ComparisonRoundTripsExplicitBaselineAndCandidate()
    {
        var baseline = Guid.NewGuid();
        var candidate = Guid.NewGuid();
        var entry = new NavigationEntry(
            new ComparisonDestination(baseline, candidate),
            new NavigationViewState());

        var restored = WorkbenchStateCodec.Restore(WorkbenchStateCodec.Capture(entry));

        var destination = Assert.IsType<ComparisonDestination>(restored.Destination);
        Assert.Equal(baseline, destination.BaselineId);
        Assert.Equal(candidate, destination.CandidateId);
    }

    [Fact]
    public void RunningDestinationRestoresToSafeTestSetup()
    {
        var entry = new NavigationEntry(
            new RunningTestDestination(Guid.NewGuid()),
            new NavigationViewState(InspectorOpen: false));

        var restored = WorkbenchStateCodec.Restore(WorkbenchStateCodec.Capture(entry));

        Assert.IsType<TestSetupDestination>(restored.Destination);
        Assert.False(restored.ViewState.InspectorOpen);
    }
}
