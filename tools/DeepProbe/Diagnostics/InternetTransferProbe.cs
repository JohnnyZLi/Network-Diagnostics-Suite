using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using NetworkDeepProbe.Models;
using NetworkDeepProbe.Planning;

namespace NetworkDeepProbe.Diagnostics;

public static class InternetTransferProbe
{
    public static readonly Uri DefaultOrigin = new("https://network.johnnyli.dev/");
    private const int TimelineIntervalMs = 250;
    private const int LoadedLatencyIntervalMs = 225;
    private const int UploadRequestBytes = 16 * 1024 * 1024;
    private const int StressUploadRequestBytes = 32 * 1024 * 1024;

    public static async Task<NativeInternetTransferReport> RunAsync(
        NativeTransferPlan plan,
        Uri origin,
        IProgress<NativeTransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(origin);
        if (!origin.IsAbsoluteUri || origin.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException("The test origin must be an absolute HTTP or HTTPS URI.", nameof(origin));
        }

        using var transferClient = CreateClient(32);
        using var latencyClient = CreateClient(4);

        progress?.Report(new NativeTransferProgress("idle", "baseline", 0, null, null, 0));
        var idleSamples = await CollectIdleLatencyAsync(
            latencyClient,
            origin,
            plan.IdlePingCount,
            plan.PingIntervalMs,
            progress,
            cancellationToken);
        var idleLatency = Statistics.Summarize(idleSamples);

        var downloadResults = await RunStageSequenceAsync(
            plan.DownloadStages,
            transferClient,
            latencyClient,
            origin,
            idleLatency,
            progress,
            cancellationToken);
        var uploadResults = await RunStageSequenceAsync(
            plan.UploadStages,
            transferClient,
            latencyClient,
            origin,
            idleLatency,
            progress,
            cancellationToken);

        var singleDownload = downloadResults.FirstOrDefault(item => item.Stage.Connections == 1);
        var aggregateDownload = downloadResults
            .Where(item => item.Stage.Connections > 1)
            .OrderByDescending(item => item.Stage.Connections)
            .FirstOrDefault();
        var singleUpload = uploadResults.FirstOrDefault(item => item.Stage.Connections == 1);
        var aggregateUpload = uploadResults
            .Where(item => item.Stage.Connections > 1)
            .OrderByDescending(item => item.Stage.Connections)
            .FirstOrDefault();

        var primaryDownload = plan.Method == TransferMethod.Single
            ? singleDownload
            : aggregateDownload ?? singleDownload;
        var primaryUpload = plan.Method == TransferMethod.Single
            ? singleUpload
            : aggregateUpload ?? singleUpload;
        if (primaryDownload is null || primaryUpload is null)
        {
            throw new InvalidOperationException("The selected transfer plan did not produce both directions.");
        }

        var measurements = new List<NativeFlowMeasurement>();
        if (singleDownload is not null || singleUpload is not null)
        {
            measurements.Add(new NativeFlowMeasurement(
                TransferStrategy.Single,
                1,
                singleDownload?.Throughput,
                singleUpload?.Throughput,
                singleDownload?.Latency,
                singleUpload?.Latency));
        }
        if (aggregateDownload is not null || aggregateUpload is not null)
        {
            measurements.Add(new NativeFlowMeasurement(
                TransferStrategy.Aggregate,
                Math.Max(aggregateDownload?.Stage.Connections ?? 0, aggregateUpload?.Stage.Connections ?? 0),
                aggregateDownload?.Throughput,
                aggregateUpload?.Throughput,
                aggregateDownload?.Latency,
                aggregateUpload?.Latency));
        }

        var scaling = plan.Profile == TestProfileId.Extended && plan.Method == TransferMethod.Compare
            ? downloadResults.Select(item => new NativeFlowScalingPoint(
                item.Stage.Connections,
                item.Throughput,
                item.Latency)).ToArray()
            : [];
        var dataUsed = downloadResults.Concat(uploadResults).Sum(item => item.Throughput.Bytes);

        progress?.Report(new NativeTransferProgress("complete", "complete", 1, null, null, dataUsed));
        return new NativeInternetTransferReport(
            origin.ToString(),
            idleLatency,
            primaryDownload.Throughput,
            primaryUpload.Throughput,
            primaryDownload.Latency,
            primaryUpload.Latency,
            measurements,
            scaling,
            dataUsed);
    }

    private static HttpClient CreateClient(int maximumConnections)
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.None,
            ConnectTimeout = TimeSpan.FromSeconds(5),
            MaxConnectionsPerServer = maximumConnections,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        };
        var client = new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("NetworkDiagnosticsSuite/2.0");
        return client;
    }

    private static async Task<IReadOnlyList<double?>> CollectIdleLatencyAsync(
        HttpClient client,
        Uri origin,
        int count,
        int intervalMs,
        IProgress<NativeTransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        var samples = new List<double?>(count);
        for (var index = 0; index < count; index++)
        {
            var sample = await MeasureHttpLatencyAsync(client, origin, cancellationToken);
            samples.Add(sample);
            progress?.Report(new NativeTransferProgress(
                "idle",
                "baseline",
                (index + 1d) / count,
                null,
                sample,
                0));
            if (index < count - 1) await Task.Delay(intervalMs, cancellationToken);
        }
        return samples;
    }

    private static async Task<IReadOnlyList<StageMeasurement>> RunStageSequenceAsync(
        IReadOnlyList<TransferStagePlan> stages,
        HttpClient transferClient,
        HttpClient latencyClient,
        Uri origin,
        LatencyStatistics idleLatency,
        IProgress<NativeTransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        var results = new List<StageMeasurement>(stages.Count);
        for (var index = 0; index < stages.Count; index++)
        {
            var stage = stages[index];
            var result = await RunStageAsync(
                stage,
                transferClient,
                latencyClient,
                origin,
                idleLatency,
                progress,
                cancellationToken);
            results.Add(result);
        }
        return results;
    }

    private static async Task<StageMeasurement> RunStageAsync(
        TransferStagePlan stage,
        HttpClient transferClient,
        HttpClient latencyClient,
        Uri origin,
        LatencyStatistics idleLatency,
        IProgress<NativeTransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var latencyCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var loadedLatencyTask = CollectLoadedLatencyAsync(
            latencyClient,
            origin,
            latencyCancellation.Token,
            sample => progress?.Report(new NativeTransferProgress(
                stage.Direction.ToString().ToLowerInvariant(),
                stage.Id,
                0,
                null,
                sample,
                0)));

        NativeThroughputSummary throughput;
        try
        {
            throughput = stage.Direction == TransferDirection.Download
                ? await RunDownloadSamplesAsync(stage, transferClient, origin, progress, cancellationToken)
                : await RunUploadSampleAsync(stage, transferClient, origin, progress, cancellationToken);
        }
        finally
        {
            latencyCancellation.Cancel();
        }

        var loadedSamples = await loadedLatencyTask;
        var loadedStatistics = Statistics.Summarize(loadedSamples);
        var increase = idleLatency.MedianMs is null || loadedStatistics.MedianMs is null
            ? null
            : loadedStatistics.MedianMs - idleLatency.MedianMs;
        var latency = new NativeLoadedLatencyReport(loadedStatistics, increase, GradeLoadedLatency(increase));
        return new StageMeasurement(stage, throughput, latency);
    }

    private static async Task<IReadOnlyList<double?>> CollectLoadedLatencyAsync(
        HttpClient client,
        Uri origin,
        CancellationToken cancellationToken,
        Action<double?> onSample)
    {
        var samples = new List<double?>();
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var sample = await MeasureHttpLatencyAsync(client, origin, cancellationToken);
                if (cancellationToken.IsCancellationRequested) break;
                samples.Add(sample);
                onSample(sample);
                await Task.Delay(LoadedLatencyIntervalMs, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The transfer stage completed.
        }
        return samples;
    }

    private static async Task<double?> MeasureHttpLatencyAsync(
        HttpClient client,
        Uri origin,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(1_500);
        var started = Stopwatch.GetTimestamp();
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                new Uri(origin, $"api/ping?n={Guid.NewGuid():N}"));
            request.Version = HttpVersion.Version20;
            request.VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode) return null;
            await response.Content.ReadAsByteArrayAsync(timeout.Token);
            return Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested) throw;
            return null;
        }
    }

    private static async Task<NativeThroughputSummary> RunDownloadSamplesAsync(
        TransferStagePlan stage,
        HttpClient client,
        Uri origin,
        IProgress<NativeTransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        var sampleCount = Math.Max(1, stage.Samples);
        var samples = new List<NativeThroughputSummary>(sampleCount);
        long completedBytes = 0;
        for (var index = 0; index < sampleCount; index++)
        {
            var durationMs = index == sampleCount - 1
                ? stage.DurationMs - (stage.DurationMs / sampleCount) * (sampleCount - 1)
                : stage.DurationMs / sampleCount;
            var capBytes = index == sampleCount - 1
                ? stage.CapBytes - (stage.CapBytes / sampleCount) * (sampleCount - 1)
                : stage.CapBytes / sampleCount;
            var sample = await RunDownloadSampleAsync(
                stage,
                durationMs,
                capBytes,
                client,
                origin,
                (liveMbps, bytes) => progress?.Report(new NativeTransferProgress(
                    "download",
                    stage.Id,
                    (index + Math.Min(1, bytes / (double)Math.Max(capBytes, 1))) / sampleCount,
                    liveMbps,
                    null,
                    completedBytes + bytes)),
                cancellationToken);
            samples.Add(sample);
            completedBytes += sample.Bytes;
        }
        return AggregateSamples(samples);
    }

    private static async Task<NativeThroughputSummary> RunDownloadSampleAsync(
        TransferStagePlan stage,
        int durationMs,
        long capBytes,
        HttpClient client,
        Uri origin,
        Action<double, long> onProgress,
        CancellationToken cancellationToken)
    {
        using var phase = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        phase.CancelAfter(durationMs);
        long bytes = 0;
        var capReached = 0;
        var started = Stopwatch.GetTimestamp();
        var timelineTask = CaptureTimelineAsync(
            () => Interlocked.Read(ref bytes),
            started,
            onProgress,
            phase.Token);

        void AddBytes(int count)
        {
            var total = AddCapped(ref bytes, count, capBytes);
            if (total >= capBytes && Interlocked.Exchange(ref capReached, 1) == 0)
            {
                phase.Cancel();
            }
        }

        var workers = Enumerable.Range(0, stage.Connections)
            .Select(_ => DownloadWorkerAsync(client, origin, AddBytes, phase.Token))
            .ToArray();
        try
        {
            await Task.WhenAll(workers);
        }
        catch (OperationCanceledException) when (phase.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // Normal duration or cap completion.
        }
        finally
        {
            phase.Cancel();
        }
        cancellationToken.ThrowIfCancellationRequested();
        var timeline = await timelineTask;
        var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        return SummarizeThroughput(
            Interlocked.Read(ref bytes),
            elapsed,
            durationMs,
            capReached != 0,
            timeline,
            "single");
    }

    private static async Task DownloadWorkerAsync(
        HttpClient client,
        Uri origin,
        Action<int> onBytes,
        CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(128 * 1024);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    new Uri(origin, $"speed/v4/stream?n={Guid.NewGuid():N}"));
                request.Version = HttpVersion.Version20;
                request.VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
                using var response = await client.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                while (!cancellationToken.IsCancellationRequested)
                {
                    var read = await stream.ReadAsync(buffer, cancellationToken);
                    if (read == 0) break;
                    onBytes(read);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async Task<NativeThroughputSummary> RunUploadSampleAsync(
        TransferStagePlan stage,
        HttpClient client,
        Uri origin,
        IProgress<NativeTransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var phase = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        phase.CancelAfter(stage.DurationMs);
        var requestBytes = stage.DurationMs >= 20_000 ? StressUploadRequestBytes : UploadRequestBytes;
        var claimLock = new object();
        long claimedBytes = 0;
        long transferredBytes = 0;
        var capReached = 0;
        var started = Stopwatch.GetTimestamp();
        var timelineTask = CaptureTimelineAsync(
            () => Interlocked.Read(ref transferredBytes),
            started,
            (liveMbps, bytes) => progress?.Report(new NativeTransferProgress(
                "upload",
                stage.Id,
                Math.Min(1, bytes / (double)Math.Max(stage.CapBytes, 1)),
                liveMbps,
                null,
                bytes)),
            phase.Token);

        int ClaimRequest()
        {
            lock (claimLock)
            {
                var remaining = stage.CapBytes - claimedBytes;
                if (remaining <= 0) return 0;
                var size = (int)Math.Min(requestBytes, remaining);
                claimedBytes += size;
                return size;
            }
        }

        void AddBytes(int count)
        {
            var total = AddCapped(ref transferredBytes, count, stage.CapBytes);
            if (total >= stage.CapBytes && Interlocked.Exchange(ref capReached, 1) == 0)
            {
                phase.Cancel();
            }
        }

        var workers = Enumerable.Range(0, stage.Connections)
            .Select(worker => UploadWorkerAsync(
                client,
                origin,
                worker,
                ClaimRequest,
                AddBytes,
                phase.Token))
            .ToArray();
        try
        {
            await Task.WhenAll(workers);
        }
        catch (OperationCanceledException) when (phase.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // Normal duration or cap completion.
        }
        finally
        {
            phase.Cancel();
        }
        cancellationToken.ThrowIfCancellationRequested();
        var timeline = await timelineTask;
        var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        return SummarizeThroughput(
            Interlocked.Read(ref transferredBytes),
            elapsed,
            stage.DurationMs,
            capReached != 0,
            timeline,
            "single");
    }

    private static async Task UploadWorkerAsync(
        HttpClient client,
        Uri origin,
        int workerIndex,
        Func<int> claimRequest,
        Action<int> onBytes,
        CancellationToken cancellationToken)
    {
        if (workerIndex > 0) await Task.Delay(workerIndex * 40, cancellationToken);
        while (!cancellationToken.IsCancellationRequested)
        {
            var size = claimRequest();
            if (size <= 0) return;
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                new Uri(origin, $"api/upload?n={Guid.NewGuid():N}"));
            request.Version = HttpVersion.Version20;
            request.VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
            request.Content = new GeneratedUploadContent(size, onBytes);
            using var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            if (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(20 + ((workerIndex * 17 + size) % 36), cancellationToken);
            }
        }
    }

    private static async Task<IReadOnlyList<NativeTimelinePoint>> CaptureTimelineAsync(
        Func<long> readBytes,
        long started,
        Action<double, long> onProgress,
        CancellationToken cancellationToken)
    {
        var points = new List<NativeTimelinePoint>();
        long lastBytes = 0;
        var lastTimestamp = started;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimelineIntervalMs, cancellationToken);
                Capture();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Capture();
        }
        return points;

        void Capture()
        {
            var now = Stopwatch.GetTimestamp();
            var interval = Stopwatch.GetElapsedTime(lastTimestamp, now).TotalMilliseconds;
            var total = readBytes();
            var delta = total - lastBytes;
            if (interval >= 100 && delta > 0)
            {
                var mbps = delta * 8d / (interval / 1000d) / 1_000_000d;
                points.Add(new NativeTimelinePoint(
                    Stopwatch.GetElapsedTime(started, now).TotalMilliseconds,
                    mbps));
                onProgress(mbps, total);
            }
            lastBytes = total;
            lastTimestamp = now;
        }
    }

    private static long AddCapped(ref long totalBytes, int count, long capBytes)
    {
        while (true)
        {
            var current = Interlocked.Read(ref totalBytes);
            if (current >= capBytes) return current;
            var accepted = Math.Min(count, capBytes - current);
            var next = current + accepted;
            if (Interlocked.CompareExchange(ref totalBytes, next, current) == current) return next;
        }
    }

    private static NativeThroughputSummary SummarizeThroughput(
        long bytes,
        double durationMs,
        int targetDurationMs,
        bool capReached,
        IReadOnlyList<NativeTimelinePoint> timeline,
        string aggregation)
    {
        var durationSeconds = Math.Max(durationMs / 1000d, 0.001);
        var wholeMbps = bytes * 8d / durationSeconds / 1_000_000d;
        var steadyPoints = timeline.Where(point => point.ElapsedMs >= 1_000).ToArray();
        if (steadyPoints.Length == 0) steadyPoints = timeline.ToArray();
        var rates = steadyPoints.Select(point => point.Mbps).Where(double.IsFinite).ToArray();
        var steadyMbps = rates.Length > 0 ? rates.Average() : wholeMbps;
        var peakMbps = rates.Length > 0 ? rates.Max() : wholeMbps;
        var standardDeviation = rates.Length > 1
            ? Math.Sqrt(rates.Sum(rate => Math.Pow(rate - steadyMbps, 2)) / rates.Length)
            : 0;
        var coefficient = steadyMbps > 0 ? standardDeviation / steadyMbps : 0;
        var stability = Math.Clamp(100 - coefficient * 100, 0, 100);
        double? rampRatio = null;
        if (steadyPoints.Length >= 4)
        {
            var midpoint = steadyPoints[0].ElapsedMs
                + (steadyPoints[^1].ElapsedMs - steadyPoints[0].ElapsedMs) / 2;
            var early = steadyPoints.Where(point => point.ElapsedMs <= midpoint).Select(point => point.Mbps).ToArray();
            var late = steadyPoints.Where(point => point.ElapsedMs > midpoint).Select(point => point.Mbps).ToArray();
            if (early.Length > 0 && late.Length > 0 && early.Average() > 0)
            {
                rampRatio = late.Average() / early.Average();
            }
        }

        var qualification = capReached && durationMs < targetDurationMs * 0.8
            ? "cap-limited"
            : rampRatio > 1.2
                ? "still-ramping"
                : rampRatio < 0.8
                    ? "declining"
                    : coefficient > 0.2
                        ? "unstable"
                        : "qualified";
        var sample = new NativeThroughputSampleSummary(
            1,
            wholeMbps,
            steadyMbps,
            bytes,
            durationMs,
            peakMbps,
            stability,
            rampRatio,
            capReached,
            qualification);
        return new NativeThroughputSummary(
            wholeMbps,
            steadyMbps,
            bytes,
            durationMs,
            peakMbps,
            stability,
            rampRatio,
            capReached,
            qualification,
            timeline,
            aggregation,
            [sample]);
    }

    private static NativeThroughputSummary AggregateSamples(IReadOnlyList<NativeThroughputSummary> samples)
    {
        if (samples.Count == 1) return samples[0];
        var offset = 0d;
        var timeline = new List<NativeTimelinePoint>();
        for (var index = 0; index < samples.Count; index++)
        {
            timeline.AddRange(samples[index].Timeline.Select(point => point with { ElapsedMs = point.ElapsedMs + offset }));
            offset += samples[index].DurationMs;
        }
        var rampRatios = samples.Where(sample => sample.RampRatio is not null).Select(sample => sample.RampRatio!.Value).ToArray();
        var summaries = samples.SelectMany((sample, sampleIndex) => sample.Samples.Select(item => item with { Sample = sampleIndex + 1 })).ToArray();
        return new NativeThroughputSummary(
            Median(samples.Select(sample => sample.Mbps)),
            Median(samples.Select(sample => sample.SteadyMbps)),
            samples.Sum(sample => sample.Bytes),
            samples.Sum(sample => sample.DurationMs),
            samples.Max(sample => sample.PeakMbps),
            Median(samples.Select(sample => sample.StabilityPercent)),
            rampRatios.Length == 0 ? null : Median(rampRatios),
            samples.Any(sample => sample.CapReached),
            WorstQualification(samples.Select(sample => sample.Qualification)),
            timeline,
            "median",
            summaries);
    }

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(value => value).ToArray();
        if (sorted.Length == 0) return 0;
        var middle = sorted.Length / 2;
        return sorted.Length % 2 == 1
            ? sorted[middle]
            : (sorted[middle - 1] + sorted[middle]) / 2;
    }

    private static string WorstQualification(IEnumerable<string> values)
    {
        var rank = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["qualified"] = 0,
            ["unstable"] = 1,
            ["still-ramping"] = 2,
            ["declining"] = 3,
            ["cap-limited"] = 4
        };
        return values.OrderByDescending(value => rank.GetValueOrDefault(value)).FirstOrDefault() ?? "qualified";
    }

    private static string GradeLoadedLatency(double? increaseMs) => increaseMs switch
    {
        null => "—",
        <= 5 => "A+",
        <= 15 => "A",
        <= 30 => "B",
        <= 60 => "C",
        <= 100 => "D",
        _ => "F"
    };

    private sealed record StageMeasurement(
        TransferStagePlan Stage,
        NativeThroughputSummary Throughput,
        NativeLoadedLatencyReport Latency);

    private sealed class GeneratedUploadContent : HttpContent
    {
        private static readonly byte[] Payload = CreatePayload(64 * 1024);
        private readonly int size;
        private readonly Action<int> onBytes;

        public GeneratedUploadContent(int size, Action<int> onBytes)
        {
            this.size = size;
            this.onBytes = onBytes;
            Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        }

        protected override bool TryComputeLength(out long length)
        {
            length = size;
            return true;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            return SerializeToStreamAsync(stream, context, CancellationToken.None);
        }

        protected override async Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context,
            CancellationToken cancellationToken)
        {
            var remaining = size;
            while (remaining > 0)
            {
                var count = Math.Min(Payload.Length, remaining);
                await stream.WriteAsync(Payload.AsMemory(0, count), cancellationToken);
                onBytes(count);
                remaining -= count;
            }
        }

        private static byte[] CreatePayload(int length)
        {
            var bytes = new byte[length];
            uint state = 0x9e3779b9;
            for (var index = 0; index < bytes.Length; index++)
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                bytes[index] = (byte)(state & 0xff);
            }
            return bytes;
        }
    }
}
