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
    string? ProducerVersion = null,
    IReadOnlyList<Uri>? TestOrigins = null,
    string? InterfaceId = null,
    DownloadPathPreference DownloadPath = DownloadPathPreference.Automatic);

public sealed record NativePreflightOptions(
    TestProfileId Profile = TestProfileId.ConnectionCheck,
    TransferMethod TransferMethod = TransferMethod.Compare,
    IReadOnlyList<Uri>? TestOrigins = null,
    string? InterfaceId = null,
    bool IncludeAddresses = false,
    DownloadPathPreference DownloadPath = DownloadPathPreference.Automatic);

public sealed record NativePreflightResult(
    MeasurementContextReport Measurement,
    IReadOnlyList<NetworkInterfaceChoice> Interfaces,
    NativeDownloadPathStatus DownloadPath);

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

    public static IReadOnlyList<NetworkInterfaceChoice> ListInterfaces() =>
        NetworkBindingResolver.ListChoices();

    public static async Task<NativePreflightResult> PreflightAsync(
        NativePreflightOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var probeOptions = CreateProbeOptions(
            options.Profile,
            options.TransferMethod,
            options.IncludeAddresses,
            options.TestOrigins,
            options.InterfaceId,
            null,
            8765,
            8,
            4,
            options.DownloadPath);
        var preflight = await MeasurementPreflight.RunAsync(
            probeOptions.CandidateOrigins,
            probeOptions.InterfaceId,
            probeOptions.IncludeAddresses,
            "network-diagnostics-native",
            FullDiagnosticRunner.Capabilities(probeOptions),
            cancellationToken);
        var downloadPath = await InternetTransferProbe.ProbeDownloadPathAsync(
            preflight.EndpointSelection.Selected.Origin,
            options.DownloadPath,
            cancellationToken);
        return new NativePreflightResult(preflight.Measurement, ListInterfaces(), downloadPath);
    }

    public static async Task<NetworkDiagnosticsReportV2> RunAsync(
        NativeDiagnosticRunOptions options,
        IProgress<NativeRunProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Validate(options);
        var probeOptions = CreateProbeOptions(
            options.Profile,
            options.TransferMethod,
            options.IncludeAddresses,
            options.TestOrigins ?? (options.TestOrigin is null ? null : [options.TestOrigin]),
            options.InterfaceId,
            options.LanTarget,
            options.LanPort,
            options.LanDurationSeconds,
            options.LanConnections,
            options.DownloadPath,
            options.Target,
            options.PingCount,
            options.MaximumHops);
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

    public static Task RunLanServerAsync(
        int port,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (port is < 1024 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        return LanThroughputServer.RunAsync(port, progress, cancellationToken);
    }

    public static IReadOnlyList<string> ListLanServerAddresses() =>
        LanThroughputServer.GetLocalAddresses();

    public static Task<LanThroughputReport> RunLanThroughputAsync(
        string target,
        int port,
        int durationSeconds,
        int connections,
        string? interfaceId = null,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        if (port is < 1024 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        if (durationSeconds is < 3 or > 30) throw new ArgumentOutOfRangeException(nameof(durationSeconds));
        if (connections is < 1 or > 16) throw new ArgumentOutOfRangeException(nameof(connections));
        var binding = NetworkBindingResolver.Resolve(interfaceId);
        return LanThroughputClient.RunAsync(
            target.Trim(),
            port,
            durationSeconds,
            connections,
            progress,
            cancellationToken,
            binding?.SourceAddress);
    }

    private static ProbeOptions CreateProbeOptions(
        TestProfileId profile,
        TransferMethod method,
        bool includeAddresses,
        IReadOnlyList<Uri>? origins,
        string? interfaceId,
        string? lanTarget,
        int lanPort,
        int lanDurationSeconds,
        int lanConnections,
        DownloadPathPreference downloadPath = DownloadPathPreference.Automatic,
        string target = "1.1.1.1",
        int pingCount = 20,
        int maximumHops = 30)
    {
        var candidates = (origins ?? [])
            .DistinctBy(origin => origin.AbsoluteUri, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToArray();
        var primary = candidates.FirstOrDefault() ?? InternetTransferProbe.DefaultOrigin;
        return new ProbeOptions(
            target,
            Path.Combine(Path.GetTempPath(), $"network-report-{Guid.NewGuid():N}.json"),
            pingCount,
            maximumHops,
            includeAddresses,
            lanTarget,
            lanPort,
            lanDurationSeconds,
            lanConnections,
            false,
            true,
            profile,
            method,
            primary,
            false,
            candidates.Skip(1).ToArray(),
            interfaceId,
            downloadPath);
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
        if ((options.TestOrigins?.Count ?? 0) > 8) throw new ArgumentException("No more than eight endpoint candidates may be configured.", nameof(options));
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

        return JsonSerializer.Deserialize<NetworkDiagnosticsReportV2>(json, Options)
            ?? throw new InvalidDataException("The report JSON did not contain a schema 2.0 report.");
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
