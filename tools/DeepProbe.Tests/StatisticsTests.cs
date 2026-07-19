using NetworkDeepProbe.Diagnostics;

namespace NetworkDeepProbe.Tests;

public sealed class StatisticsTests
{
    [Fact]
    public void SummarizeCountsMissingRepliesAsLoss()
    {
        var summary = Statistics.Summarize([10, null, 20, null]);

        Assert.Equal(4, summary.Sent);
        Assert.Equal(2, summary.Received);
        Assert.Equal(50, summary.LossPercent);
        Assert.Equal(15, summary.MedianMs);
    }

    [Fact]
    public void PercentileInterpolatesWithoutChangingInput()
    {
        double[] values = [40, 10, 30, 20];

        Assert.Equal(25, Statistics.Percentile(values, 50));
        Assert.Equal(38.5, Statistics.Percentile(values, 95));
        Assert.Equal([40, 10, 30, 20], values);
    }

    [Fact]
    public void ConsecutiveJitterUsesAdjacentDifferences()
    {
        Assert.Equal(5, Statistics.ConsecutiveJitter([10, 16, 13, 19]));
        Assert.Null(Statistics.ConsecutiveJitter([10]));
    }
}
