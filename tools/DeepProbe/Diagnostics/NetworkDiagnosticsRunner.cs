using System.Text.Json;
using System.Text.Json.Serialization;
using NetworkDeepProbe.Models;
using NetworkDeepProbe.Planning;

namespace NetworkDeepProbe.Diagnostics;

public sealed record NativeDiagnosticRunOptions(
    TestProfileId Profile = TestProfileId.Quick,
    TransferMethod TransferMethod = TransferMethod.Compare,
    string Target = "1.1.1.1",
    int PingCount = 20,
    int MaximumHops = 30,
    bool IncludeAddresses = false,
    Uri? TestOrigin = null,
    string? LanTarget = null,
    int LanPort = 8765,
    int LanDurationSeconds = 8,
    int LanConnections = 4,
    string ProducerApplication = "desktop",
    string? ProducerVersion = null);

public sealed record NativeRunProgress(
    string Phase,
    string Stage,
    string Message,
    double Fraction,
    double? LiveMbps,
    double? LiveLatencyMs,
    long BytesTransferred);

public static class NetworkDiagnosticsRunner
{
    public static NativeTransferPlan DescribePlan(TestProfileId profile, TransferMethod method) =>
        NativeTransferPlanBuilder.Build(profile, method);

    public static async Task<NetworkDiagnosticsReportV2> RunAsync(
        NativeDiagnosticRunOptions options,
        IProgress<NativeRunProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Validate(options);
        var output = Path.Combine(Path.GetTempPath(), $"network-report-{Guid.NewGuid():N}.json");
        var probeOptions = new ProbeOptions(
            options.Target,
            output,
            options.PingCount,
            options.MaximumHops,
            options.IncludeAddresses,
            options.LanTarget,
            options.LanPort,
            options.LanDurationSeconds,
            options.LanConnections,
            false,
            true,
            options.Profile,
            options.TransferMethod,
            options.TestOrigin ?? InternetTransferProbe.DefaultOrigin,
            false);
        var messageProgress = progress is null
            ? null
            : new Progress<string>(message => progress.Report(new NativeRunProgress(
                "diagnostics", "diagnostics", message, 0, null, null, 0)));
        var transferProgress = progress is null
            ? null
            : new Progress<NativeTransferProgress>(current => progress.Report(new NativeRunProgress(
                current.Phase,
                current.Stage,
                ProgressMessage(current),
                current.Fraction,
                current.LiveMbps,
                current.LiveLatencyMs,
                current.BytesTransferred)));

        var report = await FullDiagnosticRunner.RunAsync(
            probeOptions,
            messageProgress,
            transferProgress,
            cancellationToken);
        return report with
        {
            Producer = new ReportProducer(
                options.ProducerApplication,
                options.ProducerVersion,
                "network-diagnostics-native")
        };
    }

    private static string ProgressMessage(NativeTransferProgress progress) => progress.Phase switch
    {
        "idle" => "Measuring first-party HTTP latency",
        "download" => $"Measuring {progress.Stage} download",
        "upload" => $"Measuring {progress.Stage} upload",
        "complete" => "Internet transfer measurements complete",
        _ => $"Running {progress.Stage}"
    };

    private static void Validate(NativeDiagnosticRunOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Target)) throw new ArgumentException("A diagnostic target is required.", nameof(options));
        if (options.PingCount is < 5 or > 100) throw new ArgumentOutOfRangeException(nameof(options), "Ping count must be between 5 and 100.");
        if (options.MaximumHops is < 5 or > 64) throw new ArgumentOutOfRangeException(nameof(options), "Maximum hops must be between 5 and 64.");
        if (options.LanPort is < 1024 or > 65535) throw new ArgumentOutOfRangeException(nameof(options), "LAN port must be between 1024 and 65535.");
        if (options.LanDurationSeconds is < 3 or > 30) throw new ArgumentOutOfRangeException(nameof(options), "LAN duration must be between 3 and 30 seconds.");
        if (options.LanConnections is < 1 or > 16) throw new ArgumentOutOfRangeException(nameof(options), "LAN connections must be between 1 and 16.");
        if (string.IsNullOrWhiteSpace(options.ProducerApplication)) throw new ArgumentException("A producer application is required.", nameof(options));
    }
}

public static class NetworkDiagnosticsJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string Serialize(NetworkDiagnosticsReportV2 report) =>
        JsonSerializer.Serialize(report, Options);

    public static NetworkDiagnosticsReportV2 Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip,
            MaxDepth = 128
        });
        var root = document.RootElement;
        if (BrowserReportAdapter.Matches(root))
        {
            return BrowserReportAdapter.Deserialize(json, Options);
        }

        if (!root.TryGetProperty("schemaVersion", out var schemaVersion))
        {
            throw new InvalidDataException("The JSON is neither a website diagnostic export nor a versioned Network Diagnostics report.");
        }
        if (!string.Equals(schemaVersion.GetString(), "2.0", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Unsupported report schema '{schemaVersion.GetString()}'.");
        }

        var report = JsonSerializer.Deserialize<NetworkDiagnosticsReportV2>(json, Options)
            ?? throw new InvalidDataException("The report JSON did not contain a schema 2.0 report.");
        return report;
    }

    public static async Task<NetworkDiagnosticsReportV2> ReadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Deserialize(await File.ReadAllTextAsync(path, cancellationToken));
    }

    public static async Task WriteAsync(
        string path,
        NetworkDiagnosticsReportV2 report,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(path, Serialize(report), cancellationToken);
    }
}
