using NetworkDiagnostics.Desktop.Navigation;

namespace NetworkDiagnostics.Desktop.Tests;

public sealed class NavigationServiceTests
{
    [Fact]
    public void NavigateBackAndForwardRestoreDestinationsAndState()
    {
        var service = new NavigationService();
        var reportId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var reportState = new NavigationViewState(
            SearchQuery: "wifi",
            SortKey: "date",
            SortDescending: true,
            SelectedReportId: reportId,
            VerticalOffset: 420,
            InspectorOpen: false);

        service.Initialize(new TestSetupDestination());
        service.Navigate(new ReportListDestination(), reportState);
        service.Navigate(new ReportDetailDestination(reportId, "Path"));

        Assert.True(service.CanGoBack);
        Assert.False(service.CanGoForward);
        Assert.IsType<ReportDetailDestination>(service.Current?.Destination);

        Assert.True(service.GoBack());
        Assert.Equal(new ReportListDestination(), service.Current?.Destination);
        Assert.Equal(reportState, service.Current?.ViewState);
        Assert.True(service.CanGoForward);

        Assert.True(service.GoForward());
        var detail = Assert.IsType<ReportDetailDestination>(service.Current?.Destination);
        Assert.Equal(reportId, detail.ReportId);
        Assert.Equal("Path", detail.Section);
    }

    [Fact]
    public void NewNavigationAfterBackClearsForwardHistory()
    {
        var service = new NavigationService();
        service.Initialize(new TestSetupDestination());
        service.Navigate(new ReportListDestination());
        service.Navigate(new SettingsDestination("Privacy"));

        Assert.True(service.GoBack());
        Assert.True(service.CanGoForward);

        service.Navigate(new ComparisonDestination());

        Assert.False(service.CanGoForward);
        Assert.Equal(0, service.ForwardCount);
        Assert.IsType<ComparisonDestination>(service.Current?.Destination);
    }

    [Fact]
    public void ReplaceCurrentDoesNotAddAHistoryEntry()
    {
        var service = new NavigationService();
        service.Initialize(new TestSetupDestination());
        service.Navigate(new ReportListDestination());
        var backCount = service.BackCount;

        service.Navigate(
            new ReportListDestination(),
            new NavigationViewState(SearchQuery: "ethernet"),
            replaceCurrent: true);

        Assert.Equal(backCount, service.BackCount);
        Assert.Equal("ethernet", service.Current?.ViewState.SearchQuery);
    }

    [Fact]
    public void UpdateCurrentStatePreservesDestination()
    {
        var service = new NavigationService();
        var destination = new SettingsDestination("Measurement");
        service.Initialize(destination);

        var updatedState = new NavigationViewState(
            SidebarCompact: true,
            InspectorOpen: false,
            Filters: new Dictionary<string, string> { ["family"] = "IPv6" });
        service.UpdateCurrentState(updatedState);

        Assert.Equal(destination, service.Current?.Destination);
        Assert.Equal(updatedState, service.Current?.ViewState);
    }

    [Fact]
    public void NavigatingToIdenticalEntryIsANoOp()
    {
        var service = new NavigationService();
        var changed = 0;
        service.Changed += (_, _) => changed++;
        var destination = new TestSetupDestination();
        var state = new NavigationViewState(InspectorOpen: false);

        service.Initialize(destination, state);
        service.Navigate(destination, state);

        Assert.Equal(1, changed);
        Assert.Equal(0, service.BackCount);
    }
}
