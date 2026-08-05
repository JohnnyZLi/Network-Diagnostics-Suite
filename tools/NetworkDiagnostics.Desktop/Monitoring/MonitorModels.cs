namespace NetworkDiagnostics.Desktop.Monitoring;

public enum MonitorWindow
{
    OneMinute,
    FiveMinutes,
    OneHour,
    TwentyFourHours
}

public enum MonitorSampleState
{
    Responsive,
    Laggy,
    Unresponsive,
    Inactive
}

public enum MonitorAlertKind
{
    Outage,
    Recovery,
    Degradation,
    NetworkChange,
    BandwidthChange
}

public enum MonitorAlertSeverity
{
    Information,
    Warning,
    Critical
}

public sealed record MonitorSample(
    DateTimeOffset Timestamp,
    MonitorSampleState State,
    double? LatencyMs,
    double? JitterMs,
    double? DnsMs,
    double? TimeToFirstByteMs,
    double PacketLossPercent,
    string InterfaceName,
    string NetworkSignature,
    double? DownloadMbps = null,
    double? UploadMbps = null,
    bool IsSpeedMeasurement = false);

public sealed record MonitorAlert(
    Guid Id,
    DateTimeOffset Timestamp,
    MonitorAlertKind Kind,
    MonitorAlertSeverity Severity,
    string Title,
    string Detail,
    bool IsRead = false);

public sealed record MonitorSnapshot(
    bool IsRunning,
    DateTimeOffset? StartedAt,
    DateTimeOffset? LastSampleAt,
    IReadOnlyList<MonitorSample> Samples,
    IReadOnlyList<MonitorAlert> Alerts,
    string StatusMessage)
{
    public static MonitorSnapshot Stopped { get; } = new(
        false,
        null,
        null,
        [],
        [],
        "Monitoring is off");
}

public sealed record MonitorOptions(
    bool Enabled,
    Uri Endpoint,
    TimeSpan Interval,
    int AlertScoreThreshold,
    double ExpectedDownloadMbps,
    double ExpectedUploadMbps,
    int ContentSpeedCadenceHours);

public sealed class MonitorSnapshotChangedEventArgs(MonitorSnapshot snapshot) : EventArgs
{
    public MonitorSnapshot Snapshot { get; } = snapshot;
}

public sealed class MonitorContentSpeedDueEventArgs(DateTimeOffset dueAt) : EventArgs
{
    public DateTimeOffset DueAt { get; } = dueAt;
}

public static class MonitorWindowExtensions
{
    public static TimeSpan Duration(this MonitorWindow window) => window switch
    {
        MonitorWindow.OneMinute => TimeSpan.FromMinutes(1),
        MonitorWindow.FiveMinutes => TimeSpan.FromMinutes(5),
        MonitorWindow.OneHour => TimeSpan.FromHours(1),
        _ => TimeSpan.FromHours(24)
    };

    public static string ContractId(this MonitorWindow window) => window switch
    {
        MonitorWindow.OneMinute => "1m",
        MonitorWindow.FiveMinutes => "5m",
        MonitorWindow.OneHour => "1h",
        _ => "24h"
    };

    public static MonitorWindow Parse(string? value) => value switch
    {
        "1m" => MonitorWindow.OneMinute,
        "5m" => MonitorWindow.FiveMinutes,
        "1h" => MonitorWindow.OneHour,
        _ => MonitorWindow.TwentyFourHours
    };
}
