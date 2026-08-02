namespace NetworkDeepProbe.Models;

public sealed record WifiDetailsReport(
    string Status,
    string? InterfaceName,
    string? Ssid,
    int? SignalPercent,
    int? RssiDbm,
    int? Channel,
    string? Band,
    string? Protocol,
    long? ReceiveRateMbps,
    long? TransmitRateMbps,
    string? Security,
    string? Error);

public sealed record RouteEntryReport(
    string Destination,
    string? Gateway,
    string? InterfaceName,
    int? Metric,
    string AddressFamily,
    bool IsDefault);

public sealed record RoutingDetailsReport(
    string Status,
    IReadOnlyList<RouteEntryReport> Entries,
    string? Error);

public sealed record PlatformNetworkDetailsReport(
    WifiDetailsReport Wifi,
    RoutingDetailsReport Routing);
