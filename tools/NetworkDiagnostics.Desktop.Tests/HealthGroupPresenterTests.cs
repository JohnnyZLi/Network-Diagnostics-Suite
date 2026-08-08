using NetworkDiagnostics.Desktop.Presentation;
using Xunit;

namespace NetworkDiagnostics.Desktop.Tests;

public sealed class HealthGroupPresenterTests
{
    [Fact]
    public void HealthyFixtureMapsMetricsIntoThreeHealthGroups()
    {
        var groups = HealthGroupPresenter.Build(ConnectionCheckFixtures.All[0]);

        Assert.Equal(3, groups.Count);
        Assert.Contains(groups.Single(group => group.Kind == HealthGroupKind.Responsiveness).Metrics,
            metric => metric.Label == "Latency");
        Assert.Contains(groups.Single(group => group.Kind == HealthGroupKind.Reliability).Metrics,
            metric => metric.Label == "Packet loss");
        Assert.Equal(2, groups.Single(group => group.Kind == HealthGroupKind.Throughput).Metrics.Count);
        Assert.All(groups, group => Assert.Equal(HealthGroupTone.Positive, group.Tone));
    }

    [Fact]
    public void ProblematicFixtureLinksFindingsToAffectedGroups()
    {
        var groups = HealthGroupPresenter.Build(ConnectionCheckFixtures.All[1]);

        Assert.Equal(HealthGroupTone.Attention,
            groups.Single(group => group.Kind == HealthGroupKind.Responsiveness).Tone);
        Assert.Equal(HealthGroupTone.Attention,
            groups.Single(group => group.Kind == HealthGroupKind.Reliability).Tone);
        Assert.Equal(HealthGroupTone.Neutral,
            groups.Single(group => group.Kind == HealthGroupKind.Throughput).Tone);
    }

    [Fact]
    public void UnavailableTransferIsPresentedAsNotMeasured()
    {
        var throughput = HealthGroupPresenter.Build(ConnectionCheckFixtures.All[3])
            .Single(group => group.Kind == HealthGroupKind.Throughput);

        Assert.Equal("Not measured", throughput.State);
        Assert.Equal(HealthGroupTone.Neutral, throughput.Tone);
        Assert.All(throughput.Metrics, metric => Assert.False(metric.WasMeasured));
    }
}
