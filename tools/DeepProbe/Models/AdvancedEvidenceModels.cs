namespace NetworkDeepProbe.Models;

public sealed record LoadedPathTargetReport(
    string Id,
    string Label,
    string? Address,
    LatencyStatistics Idle,
    LatencyStatistics Download,
    LatencyStatistics Upload);

public sealed record LoadedPathLocalizationReport(
    string Status,
    IReadOnlyList<LoadedPathTargetReport> Targets,
    string? LikelyBoundary,
    string Summary);

public sealed record AddressFamilyProbeReport(
    string Family,
    bool AddressAvailable,
    string? Address,
    bool PingAvailable,
    double? PingMedianMs,
    bool TcpReachable,
    double? TcpConnectMs,
    string? Error,
    bool TlsReachable = false,
    double? TlsHandshakeMs = null,
    string? TlsProtocol = null,
    string? ApplicationProtocol = null,
    bool HttpReachable = false,
    double? HttpResponseMs = null,
    int? HttpStatusCode = null);

public sealed record DualStackReport(
    AddressFamilyProbeReport Ipv4,
    AddressFamilyProbeReport Ipv6,
    string PreferredFamily,
    bool Nat64Suspected,
    string Status,
    double? DnsResolutionMs = null,
    int Ipv4AddressCount = 0,
    int Ipv6AddressCount = 0,
    string? ParallelConnectWinner = null,
    double? ParallelConnectDifferenceMs = null);

public sealed record NetworkStateSnapshot(
    string? InterfaceId,
    string? InterfaceName,
    string? Gateway,
    IReadOnlyList<string> AddressFamilies,
    string? Proxy,
    IReadOnlyList<string> TunnelInterfaces);

public sealed record NetworkChangeReport(
    NetworkStateSnapshot Before,
    NetworkStateSnapshot After,
    bool Changed,
    IReadOnlyList<string> Changes,
    bool CaptivePortalSuspected);

public sealed record InterfaceCounterDelta(
    string InterfaceId,
    string Name,
    long BytesReceived,
    long BytesSent,
    long IncomingErrors,
    long OutgoingErrors,
    long IncomingDiscards,
    long OutgoingDiscards);

public sealed record HostResourceReport(
    double ProcessCpuPercent,
    long PeakWorkingSetBytes,
    long ManagedMemoryBeforeBytes,
    long ManagedMemoryAfterBytes,
    IReadOnlyList<InterfaceCounterDelta> Interfaces,
    bool PotentialClientBottleneck);

public sealed record ReportAnnotations(
    string? Label,
    IReadOnlyList<string> Tags);
