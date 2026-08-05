using NetworkDiagnostics.Desktop.Monitoring;
using NetworkDiagnostics.Desktop.Presentation;
using Xunit;

namespace NetworkDiagnostics.Desktop.Tests;

public sealed class NetworkExperiencePresenterTests
{
    private static readonly MonitorOptions Options = new(
        true,
        new Uri("https://network.johnnyli.dev/"),
        TimeSpan.FromSeconds(5),
        70,
        100,
        20,
        1);

    [Fact]
    public void NoSamplesRemainUnavailableInsteadOfScoringZero()
    {
        var presentation = NetworkExperiencePresenter.Build(
            MonitorSnapshot.Stopped,
            Options,
            MonitorWindow.FiveMinutes);

        Assert.Null(presentation.Score);
        Assert.Equal(ExperienceBand.Unavailable, presentation.Band);
        Assert.Null(presentation.Responsiveness.Score);
        Assert.Null(presentation.Reliability.Score);
        Assert.Null(presentation.Speed.Score);
    }

    [Fact]
    public void HealthySamplesAndExpectedSpeedProduceExcellentScore()
    {
        var now = DateTimeOffset.UtcNow;
        var samples = Enumerable.Range(0, 6)
            .Select(index => Sample(now.AddSeconds(-25 + index * 5), 20, 2))
            .Append(SpeedSample(now, 100, 20))
            .ToArray();
        var snapshot = new MonitorSnapshot(true, now.AddMinutes(-1), now, samples, [], "Monitoring is active");

        var presentation = NetworkExperiencePresenter.Build(snapshot, Options, MonitorWindow.FiveMinutes);

        Assert.Equal(100, presentation.Score);
        Assert.Equal(ExperienceBand.Excellent, presentation.Band);
        Assert.Equal(100, presentation.Responsiveness.Score);
        Assert.Equal(100, presentation.Reliability.Score);
        Assert.Equal(100, presentation.Speed.Score);
    }

    [Fact]
    public void MissingSpeedIsReweightedRatherThanTreatedAsZero()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = new MonitorSnapshot(
            true,
            now.AddMinutes(-1),
            now,
            [Sample(now.AddSeconds(-10), 20, 2), Sample(now.AddSeconds(-5), 20, 2)],
            [],
            "Monitoring is active");

        var presentation = NetworkExperiencePresenter.Build(snapshot, Options, MonitorWindow.FiveMinutes);

        Assert.Equal(100, presentation.Score);
        Assert.Null(presentation.Speed.Score);
    }

    [Fact]
    public void OutageSamplesReduceReliabilityAndRemainVisibleInTimeline()
    {
        var now = DateTimeOffset.UtcNow;
        var samples = new[]
        {
            Sample(now.AddSeconds(-15), 20, 2),
            Sample(now.AddSeconds(-10), 20, 2),
            new MonitorSample(now.AddSeconds(-5), MonitorSampleState.Unresponsive, null, null, null, null, 100, "Wi-Fi", "network"),
            Sample(now, 20, 2)
        };
        var snapshot = new MonitorSnapshot(true, now.AddMinutes(-1), now, samples, [], "Monitoring is active");

        var presentation = NetworkExperiencePresenter.Build(snapshot, Options, MonitorWindow.FiveMinutes);

        Assert.NotNull(presentation.Reliability.Score);
        Assert.True(presentation.Reliability.Score < 50);
        Assert.Contains(presentation.Timeline, sample => sample.State == MonitorSampleState.Unresponsive);
    }

    [Fact]
    public void SelectedWindowExcludesOlderHeartbeatSamples()
    {
        var now = DateTimeOffset.UtcNow;
        var samples = new[]
        {
            new MonitorSample(now.AddMinutes(-10), MonitorSampleState.Unresponsive, null, null, null, null, 100, "Wi-Fi", "network"),
            Sample(now.AddSeconds(-20), 20, 2)
        };
        var snapshot = new MonitorSnapshot(true, now.AddHours(-1), now, samples, [], "Monitoring is active");

        var presentation = NetworkExperiencePresenter.Build(snapshot, Options, MonitorWindow.OneMinute);

        Assert.Single(presentation.Timeline);
        Assert.Equal(MonitorSampleState.Responsive, presentation.Timeline[0].State);
        Assert.Equal(100, presentation.Reliability.Score);
    }

    private static MonitorSample Sample(DateTimeOffset timestamp, double latency, double jitter) => new(
        timestamp,
        MonitorSampleState.Responsive,
        latency,
        jitter,
        8,
        latency,
        0,
        "Wi-Fi",
        "network");

    private static MonitorSample SpeedSample(DateTimeOffset timestamp, double download, double upload) => new(
        timestamp,
        MonitorSampleState.Responsive,
        20,
        2,
        8,
        20,
        0,
        "Wi-Fi",
        "network",
        download,
        upload,
        true);
}
