using NetworkDiagnostics.Desktop.Monitoring;

namespace NetworkDiagnostics.Desktop.Presentation;

public enum ExperienceBand
{
    Excellent,
    Good,
    Fair,
    Degraded,
    Poor,
    Unavailable
}

public sealed record ExperienceComponentPresentation(
    string Title,
    int? Score,
    ExperienceBand Band,
    string Status,
    string Summary,
    IReadOnlyList<(string Label, string Value)> Metrics);

public sealed record NetworkExperiencePresentation(
    int? Score,
    ExperienceBand Band,
    string Status,
    string Summary,
    string DeviceName,
    string InterfaceName,
    string LastUpdated,
    bool MonitoringEnabled,
    MonitorWindow Window,
    ExperienceComponentPresentation Responsiveness,
    ExperienceComponentPresentation Reliability,
    ExperienceComponentPresentation Speed,
    IReadOnlyList<MonitorSample> Timeline,
    IReadOnlyList<MonitorAlert> Alerts,
    int UnreadAlertCount);

public static class NetworkExperiencePresenter
{
    private const int MaximumTimelinePoints = 72;

    public static NetworkExperiencePresentation Build(
        MonitorSnapshot snapshot,
        MonitorOptions options,
        MonitorWindow window)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(options);

        var now = DateTimeOffset.UtcNow;
        var cutoff = now - window.Duration();
        var heartbeatSamples = snapshot.Samples
            .Where(sample => !sample.IsSpeedMeasurement && sample.Timestamp >= cutoff)
            .OrderBy(sample => sample.Timestamp)
            .ToArray();
        var successfulHeartbeatHistory = snapshot.Samples
            .Where(sample => !sample.IsSpeedMeasurement)
            .Where(sample => sample.State is MonitorSampleState.Responsive or MonitorSampleState.Laggy)
            .OrderBy(sample => sample.Timestamp)
            .ToArray();
        var hasSuccessfulBaseline = successfulHeartbeatHistory.Length > 0;
        var endpointUnavailable = snapshot.IsRunning
            && heartbeatSamples.Length > 0
            && !hasSuccessfulBaseline
            && heartbeatSamples.All(sample => sample.State is MonitorSampleState.Unresponsive or MonitorSampleState.Inactive);
        var presentationSamples = endpointUnavailable
            ? heartbeatSamples.Select(NeutralizePreBaselineFailure).ToArray()
            : heartbeatSamples;
        var latestSpeed = snapshot.Samples
            .Where(sample => sample.IsSpeedMeasurement && sample.Timestamp >= now - TimeSpan.FromHours(24))
            .OrderByDescending(sample => sample.Timestamp)
            .FirstOrDefault();

        var responsiveness = BuildResponsiveness(presentationSamples);
        var reliability = BuildReliability(presentationSamples);
        var speed = BuildSpeed(latestSpeed, options);
        var score = endpointUnavailable
            ? null
            : WeightedOverall(responsiveness.Score, reliability.Score, speed.Score);
        var band = Band(score);
        var latest = heartbeatSamples.LastOrDefault() ?? snapshot.Samples.OrderBy(sample => sample.Timestamp).LastOrDefault();
        var timeline = Downsample(presentationSamples, MaximumTimelinePoints);
        var firstSuccessfulSample = successfulHeartbeatHistory.FirstOrDefault()?.Timestamp;
        var alerts = snapshot.Alerts
            .Where(alert => alert.Timestamp >= cutoff)
            .Where(alert => firstSuccessfulSample is not null && alert.Timestamp >= firstSuccessfulSample)
            .OrderByDescending(alert => alert.Timestamp)
            .Take(12)
            .ToArray();

        return new NetworkExperiencePresentation(
            score,
            band,
            endpointUnavailable ? "Monitor unavailable" : StatusFor(band),
            endpointUnavailable
                ? "The monitoring endpoint could not be reached. This does not yet prove that the local connection is down."
                : SummaryFor(band, snapshot.IsRunning, heartbeatSamples.Length),
            Environment.MachineName,
            latest?.InterfaceName ?? "Automatic routing",
            latest is null
                ? "No measurements yet"
                : endpointUnavailable
                    ? $"Endpoint check failed {RelativeTime(latest.Timestamp)}"
                    : $"Updated {RelativeTime(latest.Timestamp)}",
            snapshot.IsRunning,
            window,
            responsiveness,
            reliability,
            speed,
            timeline,
            alerts,
            alerts.Count(alert => !alert.IsRead));
    }

    private static MonitorSample NeutralizePreBaselineFailure(MonitorSample sample) =>
        sample.State == MonitorSampleState.Unresponsive
            ? sample with
            {
                State = MonitorSampleState.Inactive,
                PacketLossPercent = 0
            }
            : sample;

    private static ExperienceComponentPresentation BuildResponsiveness(IReadOnlyList<MonitorSample> samples)
    {
        var activeSamples = samples
            .Where(sample => sample.State is MonitorSampleState.Responsive or MonitorSampleState.Laggy or MonitorSampleState.Unresponsive)
            .ToArray();
        var measured = activeSamples
            .Where(sample => sample.State is MonitorSampleState.Responsive or MonitorSampleState.Laggy)
            .Where(sample => sample.LatencyMs is not null)
            .ToArray();
        if (measured.Length == 0)
        {
            return Unavailable("Responsiveness", "Waiting for response-time samples", "Latency, jitter, DNS, and time to first byte will appear after the endpoint responds.");
        }

        var latency = Median(measured.Select(sample => sample.LatencyMs!.Value));
        var jitterValues = measured.Where(sample => sample.JitterMs is not null).Select(sample => sample.JitterMs!.Value).ToArray();
        var jitter = jitterValues.Length == 0 ? 0 : Median(jitterValues);
        var loss = activeSamples.Length == 0 ? 0 : activeSamples.Average(sample => sample.PacketLossPercent);
        var latencyScore = ScoreLowerIsBetter(latency, [(20, 100), (50, 82), (100, 58), (250, 24), (500, 0)]);
        var jitterScore = ScoreLowerIsBetter(jitter, [(5, 100), (20, 82), (50, 52), (100, 18), (200, 0)]);
        var lossScore = Math.Clamp(100 - loss * 18, 0, 100);
        var score = (int)Math.Round(latencyScore * 0.68 + jitterScore * 0.22 + lossScore * 0.10);
        var band = Band(score);
        var latestDns = measured.LastOrDefault(sample => sample.DnsMs is not null)?.DnsMs;
        var latestTtfb = measured.LastOrDefault(sample => sample.TimeToFirstByteMs is not null)?.TimeToFirstByteMs;

        return new ExperienceComponentPresentation(
            "Responsiveness",
            score,
            band,
            StatusFor(band),
            latency <= 50
                ? "Interactive traffic is responding quickly."
                : latency <= 100
                    ? "Response time is noticeable but still usable."
                    : "High response time may affect calls, games, and browsing.",
            [
                ("Typical latency", Milliseconds(latency)),
                ("Typical jitter", Milliseconds(jitter)),
                ("DNS", latestDns is null ? "—" : Milliseconds(latestDns.Value)),
                ("Time to first byte", latestTtfb is null ? "—" : Milliseconds(latestTtfb.Value))
            ]);
    }

    private static ExperienceComponentPresentation BuildReliability(IReadOnlyList<MonitorSample> samples)
    {
        var activeSamples = samples
            .Where(sample => sample.State is MonitorSampleState.Responsive or MonitorSampleState.Laggy or MonitorSampleState.Unresponsive)
            .ToArray();
        if (activeSamples.Length == 0)
        {
            return Unavailable("Reliability", "Waiting for availability samples", "Responsive, laggy, and outage periods will appear after a monitoring baseline is established.");
        }

        var responsive = activeSamples.Count(sample => sample.State == MonitorSampleState.Responsive);
        var laggy = activeSamples.Count(sample => sample.State == MonitorSampleState.Laggy);
        var unresponsive = activeSamples.Count(sample => sample.State == MonitorSampleState.Unresponsive);
        var active = responsive + laggy + unresponsive;
        var availability = (responsive + laggy) / (double)active * 100;
        var responsivePercent = responsive / (double)active * 100;
        var laggyPercent = laggy / (double)active * 100;
        var unresponsivePercent = unresponsive / (double)active * 100;
        var averageLoss = activeSamples.Average(sample => sample.PacketLossPercent);
        var score = (int)Math.Round(Math.Clamp(
            availability - unresponsivePercent * 1.5 - laggyPercent * 0.18 - averageLoss * 2.2,
            0,
            100));
        var band = Band(score);

        return new ExperienceComponentPresentation(
            "Reliability",
            score,
            band,
            StatusFor(band),
            unresponsive == 0
                ? laggy == 0 ? "No outages or laggy periods were observed." : "The connection stayed online with some laggy periods."
                : $"{unresponsive} monitoring sample{(unresponsive == 1 ? "" : "s")} could not reach the endpoint.",
            [
                ("Availability", $"{availability:0.0}%"),
                ("Responsive", $"{responsivePercent:0.0}%"),
                ("Laggy", $"{laggyPercent:0.0}%"),
                ("Unresponsive", $"{unresponsivePercent:0.0}%")
            ]);
    }

    private static ExperienceComponentPresentation BuildSpeed(MonitorSample? speed, MonitorOptions options)
    {
        if (speed?.DownloadMbps is null && speed?.UploadMbps is null)
        {
            return Unavailable("Speed", "No recent content-speed result", "Run a content test to add download and upload history without using a peak test.");
        }

        var download = speed.DownloadMbps ?? 0;
        var upload = speed.UploadMbps ?? 0;
        var expectedDownload = Math.Max(1, options.ExpectedDownloadMbps);
        var expectedUpload = Math.Max(1, options.ExpectedUploadMbps);
        var downloadScore = Math.Clamp(download / expectedDownload * 100, 0, 100);
        var uploadScore = Math.Clamp(upload / expectedUpload * 100, 0, 100);
        var score = (int)Math.Round(downloadScore * 0.65 + uploadScore * 0.35);
        var band = Band(score);

        return new ExperienceComponentPresentation(
            "Speed",
            score,
            band,
            StatusFor(band),
            score >= 80
                ? "Recent content speed is close to the configured expectation."
                : "Recent content speed is below the configured expectation.",
            [
                ("Content download", Megabits(download)),
                ("Content upload", Megabits(upload)),
                ("Expected download", Megabits(expectedDownload)),
                ("Expected upload", Megabits(expectedUpload))
            ]);
    }

    private static ExperienceComponentPresentation Unavailable(string title, string status, string summary) => new(
        title,
        null,
        ExperienceBand.Unavailable,
        status,
        summary,
        []);

    private static int? WeightedOverall(int? responsiveness, int? reliability, int? speed)
    {
        var values = new List<(int Score, double Weight)>();
        if (responsiveness is not null) values.Add((responsiveness.Value, 0.45));
        if (reliability is not null) values.Add((reliability.Value, 0.35));
        if (speed is not null) values.Add((speed.Value, 0.20));
        if (values.Count == 0) return null;
        var totalWeight = values.Sum(value => value.Weight);
        return (int)Math.Round(values.Sum(value => value.Score * value.Weight) / totalWeight);
    }

    private static ExperienceBand Band(int? score) => score switch
    {
        null => ExperienceBand.Unavailable,
        >= 90 => ExperienceBand.Excellent,
        >= 80 => ExperienceBand.Good,
        >= 70 => ExperienceBand.Fair,
        >= 50 => ExperienceBand.Degraded,
        _ => ExperienceBand.Poor
    };

    private static string StatusFor(ExperienceBand band) => band switch
    {
        ExperienceBand.Excellent => "Excellent",
        ExperienceBand.Good => "Good",
        ExperienceBand.Fair => "Fair",
        ExperienceBand.Degraded => "Degraded",
        ExperienceBand.Poor => "Poor",
        _ => "Not enough data"
    };

    private static string SummaryFor(ExperienceBand band, bool running, int sampleCount)
    {
        if (!running) return "Continuous monitoring is paused. Saved history remains available.";
        if (sampleCount == 0) return "Monitoring has started and is collecting the first response-time samples.";
        return band switch
        {
            ExperienceBand.Excellent => "Rock solid and ready to go.",
            ExperienceBand.Good => "The connection is working well with minor room for improvement.",
            ExperienceBand.Fair => "The connection is usable, but one or more components are inconsistent.",
            ExperienceBand.Degraded => "Performance issues are likely to be noticeable during normal use.",
            ExperienceBand.Poor => "The connection needs attention.",
            _ => "More measurements are needed before a network score is available."
        };
    }

    private static IReadOnlyList<MonitorSample> Downsample(IReadOnlyList<MonitorSample> samples, int maximum)
    {
        if (samples.Count <= maximum) return samples.ToArray();
        var result = new List<MonitorSample>(maximum);
        var bucketSize = samples.Count / (double)maximum;
        for (var index = 0; index < maximum; index++)
        {
            var start = (int)Math.Floor(index * bucketSize);
            var end = Math.Min(samples.Count, (int)Math.Floor((index + 1) * bucketSize));
            if (end <= start) end = Math.Min(samples.Count, start + 1);
            var bucket = samples.Skip(start).Take(end - start).ToArray();
            result.Add(bucket
                .OrderByDescending(sample => Severity(sample.State))
                .ThenByDescending(sample => sample.LatencyMs ?? 0)
                .First());
        }
        return result;
    }

    private static int Severity(MonitorSampleState state) => state switch
    {
        MonitorSampleState.Unresponsive => 3,
        MonitorSampleState.Laggy => 2,
        MonitorSampleState.Responsive => 1,
        _ => 0
    };

    private static double ScoreLowerIsBetter(double value, IReadOnlyList<(double Threshold, double Score)> points)
    {
        if (value <= points[0].Threshold) return points[0].Score;
        for (var index = 1; index < points.Count; index++)
        {
            var previous = points[index - 1];
            var current = points[index];
            if (value <= current.Threshold)
            {
                var progress = (value - previous.Threshold) / (current.Threshold - previous.Threshold);
                return previous.Score + (current.Score - previous.Score) * progress;
            }
        }
        return points[^1].Score;
    }

    private static double Median(IEnumerable<double> values)
    {
        var ordered = values.Order().ToArray();
        if (ordered.Length == 0) return 0;
        var middle = ordered.Length / 2;
        return ordered.Length % 2 == 0
            ? (ordered[middle - 1] + ordered[middle]) / 2
            : ordered[middle];
    }

    private static string Milliseconds(double value) => $"{value:0.#} ms";

    private static string Megabits(double value) => $"{value:0.#} Mbps";

    private static string RelativeTime(DateTimeOffset timestamp)
    {
        var elapsed = DateTimeOffset.UtcNow - timestamp;
        if (elapsed < TimeSpan.FromSeconds(10)) return "just now";
        if (elapsed < TimeSpan.FromMinutes(1)) return $"{Math.Max(1, (int)elapsed.TotalSeconds)} seconds ago";
        if (elapsed < TimeSpan.FromHours(1)) return $"{Math.Max(1, (int)elapsed.TotalMinutes)} minutes ago";
        return timestamp.ToLocalTime().ToString("h:mm tt");
    }
}
