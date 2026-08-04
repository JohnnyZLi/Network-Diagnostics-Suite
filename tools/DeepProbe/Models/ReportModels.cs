namespace NetworkDeepProbe.Models;

public sealed record LatencyStatistics(
    int Sent,
    int Received,
    int Lost,
    double LossPercent,
    double? MinimumMs,
    double? MaximumMs,
    double? MeanMs,
    double? MedianMs,
    double? P95Ms,
    double? JitterMs,
    IReadOnlyList<double?> Samples);

public sealed record PingTargetReport(
    string Label,
    string? Address,
    LatencyStatistics Statistics);

public sealed record TraceHop(
    int Hop,
    string? Address,
    string? Hostname,
    IReadOnlyList<double?> RoundTripsMs,
    bool ReachedDestination,
    bool AddressRedacted);

public sealed record TraceRouteReport(
    string Target,
    string? ResolvedAddress,
    int MaximumHops,
    bool ReachedDestination,
    IReadOnlyList<TraceHop> Hops);

public sealed record DnsResolverReport(
    string Name,
    string Address,
    int Attempts,
    int Successful,
    double? MinimumMs,
    double? MedianMs,
    double? P95Ms,
    double? MaximumMs,
    string? Error);

public sealed record TlsEndpointReport(
    string Name,
    string Host,
    bool Reachable,
    double? DnsMs,
    double? TcpMs,
    double? TlsMs,
    string? TlsProtocol,
    string? ApplicationProtocol,
    string? Error);

public sealed record NetworkInterfaceReport(
    string Name,
    string Description,
    string Type,
    long? LinkSpeedMbps,
    int? Ipv4Mtu,
    bool SupportsIpv4,
    bool SupportsIpv6,
    IReadOnlyList<string>? UnicastAddresses,
    IReadOnlyList<string>? Gateways,
    IReadOnlyList<string>? DnsServers,
    string? Id = null,
    bool Selected = false);

public sealed record PathMtuReport(
    string Target,
    int? PayloadBytes,
    int? EstimatedIpv4Mtu,
    string Status);

public sealed record LanThroughputReport(
    string Target,
    string? ResolvedAddress,
    int Port,
    int DurationMs,
    int Concurrency,
    LatencyStatistics Latency,
    double DownloadMbps,
    long DownloadBytes,
    double UploadMbps,
    long UploadBytes);

public sealed record DeepProbeReport(
    string SchemaVersion,
    DateTimeOffset GeneratedAt,
    string Target,
    string OperatingSystem,
    string Architecture,
    bool IncludesLocalAddresses,
    IReadOnlyList<NetworkInterfaceReport> Interfaces,
    PingTargetReport? GatewayPing,
    PingTargetReport InternetPing,
    TraceRouteReport TraceRoute,
    IReadOnlyList<DnsResolverReport> DnsResolvers,
    PathMtuReport PathMtu,
    IReadOnlyList<TlsEndpointReport> ServiceEndpoints,
    LanThroughputReport? LocalLink,
    WifiDetailsReport? Wifi,
    RoutingDetailsReport? Routing);
