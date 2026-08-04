using System.Diagnostics;
using System.Net.NetworkInformation;
using NetworkDeepProbe.Models;

namespace NetworkDeepProbe.Diagnostics;

internal sealed class HostResourceMonitor : IAsyncDisposable
{
    private readonly Process process;
    private readonly DateTimeOffset startedAt;
    private readonly TimeSpan processorTimeAtStart;
    private readonly long managedMemoryBefore;
    private readonly IReadOnlyDictionary<string, InterfaceCounterSnapshot> interfaceBefore;
    private readonly TcpCounterSnapshot tcpBefore;
    private readonly CancellationTokenSource cancellation = new();
    private readonly Task sampleTask;
    private readonly bool includeLocalIdentifiers;
    private long peakWorkingSet;
    private bool stopped;
    private bool disposed;

    private HostResourceMonitor(bool includeLocalIdentifiers)
    {
        this.includeLocalIdentifiers = includeLocalIdentifiers;
        process = Process.GetCurrentProcess();
        process.Refresh();
        startedAt = DateTimeOffset.UtcNow;
        processorTimeAtStart = process.TotalProcessorTime;
        managedMemoryBefore = GC.GetTotalMemory(false);
        interfaceBefore = CaptureInterfaceCounters();
        tcpBefore = CaptureTcpCounters();
        peakWorkingSet = process.WorkingSet64;
        sampleTask = Task.Run(SampleLoopAsync);
    }

    public static HostResourceMonitor Start(bool includeLocalIdentifiers) => new(includeLocalIdentifiers);

    public async Task<HostResourceReport> StopAsync(CancellationToken cancellationToken)
    {
        if (stopped) throw new InvalidOperationException("Host monitoring has already stopped.");
        stopped = true;
        await cancellation.CancelAsync();
        await AwaitSampleTaskAsync(cancellationToken);

        process.Refresh();
        var elapsed = Math.Max(0.001, (DateTimeOffset.UtcNow - startedAt).TotalSeconds);
        var processorSeconds = Math.Max(0, (process.TotalProcessorTime - processorTimeAtStart).TotalSeconds);
        var cpuPercent = Math.Clamp(
            processorSeconds / elapsed / Math.Max(1, Environment.ProcessorCount) * 100,
            0,
            100);
        var after = CaptureInterfaceCounters();
        var deltas = new List<InterfaceCounterDelta>();
        var index = 0;
        foreach (var pair in after.OrderBy(item => item.Value.Name, StringComparer.Ordinal))
        {
            if (!interfaceBefore.TryGetValue(pair.Key, out var before)) continue;
            index++;
            var current = pair.Value;
            deltas.Add(new InterfaceCounterDelta(
                includeLocalIdentifiers ? current.Id : $"interface-{index}",
                includeLocalIdentifiers ? current.Name : $"Interface {index}",
                NonNegative(current.BytesReceived - before.BytesReceived),
                NonNegative(current.BytesSent - before.BytesSent),
                NonNegative(current.IncomingErrors - before.IncomingErrors),
                NonNegative(current.OutgoingErrors - before.OutgoingErrors),
                NonNegative(current.IncomingDiscards - before.IncomingDiscards),
                NonNegative(current.OutgoingDiscards - before.OutgoingDiscards)));
        }

        var tcpAfter = CaptureTcpCounters();
        var tcpSent = NonNegative(tcpAfter.SegmentsSent - tcpBefore.SegmentsSent);
        var tcpRetransmitted = NonNegative(tcpAfter.SegmentsResent - tcpBefore.SegmentsResent);
        var retransmissionPercent = tcpSent == 0 ? null : tcpRetransmitted / (double)tcpSent * 100;
        var memoryInfo = GC.GetGCMemoryInfo();
        var potentialBottleneck = cpuPercent >= 85
            || retransmissionPercent >= 1
            || deltas.Any(item =>
                item.IncomingErrors > 0
                || item.OutgoingErrors > 0
                || item.IncomingDiscards > 0
                || item.OutgoingDiscards > 0);
        return new HostResourceReport(
            cpuPercent,
            peakWorkingSet,
            managedMemoryBefore,
            GC.GetTotalMemory(false),
            deltas,
            potentialBottleneck,
            tcpSent,
            tcpRetransmitted,
            retransmissionPercent,
            memoryInfo.MemoryLoadBytes,
            memoryInfo.HighMemoryLoadThresholdBytes);
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        if (!stopped)
        {
            stopped = true;
            await cancellation.CancelAsync();
            await AwaitSampleTaskAsync(CancellationToken.None);
        }
        cancellation.Dispose();
        process.Dispose();
    }

    private async Task SampleLoopAsync()
    {
        while (!cancellation.IsCancellationRequested)
        {
            try
            {
                process.Refresh();
                var workingSet = process.WorkingSet64;
                if (workingSet > peakWorkingSet) Interlocked.Exchange(ref peakWorkingSet, workingSet);
                await Task.Delay(250, cancellation.Token);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                break;
            }
            catch (InvalidOperationException)
            {
                break;
            }
        }
    }

    private async Task AwaitSampleTaskAsync(CancellationToken cancellationToken)
    {
        try
        {
            await sampleTask.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested || cancellationToken.IsCancellationRequested)
        {
            if (cancellationToken.IsCancellationRequested && !cancellation.IsCancellationRequested) throw;
        }
    }

    private static IReadOnlyDictionary<string, InterfaceCounterSnapshot> CaptureInterfaceCounters()
    {
        var result = new Dictionary<string, InterfaceCounterSnapshot>(StringComparer.Ordinal);
        NetworkInterface[] interfaces;
        try
        {
            interfaces = NetworkInterface.GetAllNetworkInterfaces();
        }
        catch (NetworkInformationException)
        {
            return result;
        }
        catch (PlatformNotSupportedException)
        {
            return result;
        }

        foreach (var networkInterface in interfaces)
        {
            try
            {
                var statistics = networkInterface.GetIPStatistics();
                var outgoingDiscards = OperatingSystem.IsMacOS()
                    ? 0
                    : statistics.OutgoingPacketsDiscarded;
                result[networkInterface.Id] = new InterfaceCounterSnapshot(
                    networkInterface.Id,
                    networkInterface.Name,
                    statistics.BytesReceived,
                    statistics.BytesSent,
                    statistics.IncomingPacketsWithErrors,
                    statistics.OutgoingPacketsWithErrors,
                    statistics.IncomingPacketsDiscarded,
                    outgoingDiscards);
            }
            catch (NetworkInformationException)
            {
                // Unsupported or disappearing interfaces remain absent from the delta.
            }
            catch (PlatformNotSupportedException)
            {
                // The rest of the report remains valid without this interface's counters.
            }
        }
        return result;
    }

    private static TcpCounterSnapshot CaptureTcpCounters()
    {
        try
        {
            var properties = IPGlobalProperties.GetIPGlobalProperties();
            var ipv4 = properties.GetTcpIPv4Statistics();
            long sent = ipv4.SegmentsSent;
            long resent = ipv4.SegmentsResent;
            try
            {
                var ipv6 = properties.GetTcpIPv6Statistics();
                sent += ipv6.SegmentsSent;
                resent += ipv6.SegmentsResent;
            }
            catch (Exception error) when (error is NetworkInformationException or PlatformNotSupportedException)
            {
                // IPv4 statistics remain useful when IPv6 counters are unavailable.
            }
            return new TcpCounterSnapshot(sent, resent);
        }
        catch (Exception error) when (error is NetworkInformationException or PlatformNotSupportedException)
        {
            return new TcpCounterSnapshot(0, 0);
        }
    }

    private static long NonNegative(long value) => Math.Max(0, value);

    private sealed record InterfaceCounterSnapshot(
        string Id,
        string Name,
        long BytesReceived,
        long BytesSent,
        long IncomingErrors,
        long OutgoingErrors,
        long IncomingDiscards,
        long OutgoingDiscards);

    private sealed record TcpCounterSnapshot(long SegmentsSent, long SegmentsResent);
}
