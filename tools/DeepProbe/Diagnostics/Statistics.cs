using NetworkDeepProbe.Models;

namespace NetworkDeepProbe.Diagnostics;

internal static class Statistics
{
    public static LatencyStatistics Summarize(IReadOnlyList<double?> samples)
    {
        var valid = samples.Where(value => value.HasValue).Select(value => value!.Value).ToArray();
        var sent = samples.Count;
        var received = valid.Length;
        var lost = sent - received;

        return new LatencyStatistics(
            sent,
            received,
            lost,
            sent == 0 ? 0 : lost * 100d / sent,
            valid.Length == 0 ? null : valid.Min(),
            valid.Length == 0 ? null : valid.Max(),
            valid.Length == 0 ? null : valid.Average(),
            Percentile(valid, 50),
            Percentile(valid, 95),
            ConsecutiveJitter(valid),
            samples);
    }

    public static double? Percentile(IReadOnlyList<double> values, double percentile)
    {
        if (values.Count == 0) return null;
        var sorted = values.OrderBy(value => value).ToArray();
        var bounded = Math.Clamp(percentile, 0, 100);
        var index = bounded / 100d * (sorted.Length - 1);
        var lower = (int)Math.Floor(index);
        var upper = (int)Math.Ceiling(index);
        if (lower == upper) return sorted[lower];
        var weight = index - lower;
        return sorted[lower] * (1 - weight) + sorted[upper] * weight;
    }

    public static double? ConsecutiveJitter(IReadOnlyList<double> values)
    {
        if (values.Count < 2) return null;
        var total = 0d;
        for (var index = 1; index < values.Count; index++)
        {
            total += Math.Abs(values[index] - values[index - 1]);
        }
        return total / (values.Count - 1);
    }
}
