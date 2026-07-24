using System.Runtime.InteropServices;
using NetworkDeepProbe.Models;

namespace NetworkDeepProbe.Diagnostics;

internal static class ProbeRunner
{
    public static async Task<DeepProbeReport> RunAsync(
        ProbeOptions options,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report("Inspecting active network interfaces");
        var interfaces = InterfaceProbe.Collect(options.IncludeAddresses);

        progress?.Report($"Resolving {options.Target}");
        var destination = await PingProbe.ResolveTargetAsync(options.Target, cancellationToken);

        PingTargetReport? gatewayPing = null;
        if (interfaces.PrimaryGateway is not null)
        {
            progress?.Report("Measuring the default gateway");
            gatewayPing = await PingProbe.RunAsync(
                "Default gateway",
                interfaces.PrimaryGateway,
                Math.Min(options.PingCount, 12),
                options.IncludeAddresses,
                cancellationToken);
        }

        progress?.Report($"Sending {options.PingCount} Internet Control Message Protocol probes");
        var internetPing = await PingProbe.RunAsync(
            options.Target,
            destination,
            options.PingCount,
            true,
            cancellationToken);

        progress?.Report("Tracing the route");
        var traceRoute = await TraceRouteProbe.RunAsync(
            options.Target,
            destination,
            options.MaximumHops,
            options.IncludeAddresses,
            cancellationToken);

        progress?.Report("Testing Domain Name System resolvers");
        var dnsResolvers = await DnsProbe.RunAsync(options.IncludeAddresses, cancellationToken);

        progress?.Report("Estimating the IPv4 path Maximum Transmission Unit");
        var pathMtu = await MtuProbe.RunAsync(options.Target, destination, cancellationToken);

        progress?.Report("Timing common Transport Layer Security endpoints");
        var serviceEndpoints = await EndpointProbe.RunAsync(cancellationToken);

        LanThroughputReport? localLink = null;
        if (options.LanTarget is not null)
        {
            localLink = await LanThroughputClient.RunAsync(
                options.LanTarget,
                options.LanPort,
                options.LanDurationSeconds,
                options.LanConcurrency,
                progress,
                cancellationToken);
        }

        return new DeepProbeReport(
            "1.1",
            DateTimeOffset.UtcNow,
            options.Target,
            RuntimeInformation.OSDescription,
            RuntimeInformation.OSArchitecture.ToString(),
            options.IncludeAddresses,
            interfaces.Reports,
            gatewayPing,
            internetPing,
            traceRoute,
            dnsResolvers,
            pathMtu,
            serviceEndpoints,
            localLink);
    }
}
