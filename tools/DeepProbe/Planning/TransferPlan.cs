using System.Text.Json.Serialization;
using NetworkDeepProbe.Contracts;

namespace NetworkDeepProbe.Planning;

public enum TestProfileId
{
    [JsonStringEnumMemberName("connection-check")]
    ConnectionCheck,

    [JsonStringEnumMemberName("quick")]
    Quick,

    [JsonStringEnumMemberName("standard")]
    Standard,

    [JsonStringEnumMemberName("extended")]
    Extended
}

public enum TransferMethod
{
    Compare,
    Single,
    Aggregate
}

public enum DownloadPathPreference
{
    Automatic,
    DirectR2,
    Worker
}

public enum TransferDirection
{
    Download,
    Upload
}

public enum TransferStrategy
{
    Single,
    Aggregate
}

public sealed record TransferStagePlan(
    string Id,
    TransferDirection Direction,
    TransferStrategy Strategy,
    int Connections,
    int DurationMs,
    long CapBytes,
    int Samples = 1);

public sealed record NativeTransferPlan(
    TestProfileId Profile,
    TransferMethod Method,
    string ProfileName,
    int BaseEstimatedSeconds,
    int EstimatedSeconds,
    long TransferCapBytes,
    bool IncludeServices,
    int IdlePingCount,
    int PingIntervalMs,
    IReadOnlyList<TransferStagePlan> DownloadStages,
    IReadOnlyList<TransferStagePlan> UploadStages);

public static class NativeTransferPlanBuilder
{
    public static TestProfileId ParseProfile(string value) => value.Trim().ToLowerInvariant() switch
    {
        "connection-check" or "connectioncheck" or "check" => TestProfileId.ConnectionCheck,
        "quick" => TestProfileId.Quick,
        "full" or "standard" => TestProfileId.Standard,
        "stress" or "extended" => TestProfileId.Extended,
        _ => throw new ArgumentException("Profile must be connection-check, quick, full, or stress.")
    };

    public static TransferMethod ParseMethod(string value) => value.Trim().ToLowerInvariant() switch
    {
        "compare" => TransferMethod.Compare,
        "single" => TransferMethod.Single,
        "aggregate" or "parallel" => TransferMethod.Aggregate,
        _ => throw new ArgumentException("Transfer method must be compare, single, or aggregate.")
    };

    public static DownloadPathPreference ParseDownloadPath(string value) => value.Trim().ToLowerInvariant() switch
    {
        "automatic" or "auto" => DownloadPathPreference.Automatic,
        "direct-r2" or "r2-direct" or "r2" => DownloadPathPreference.DirectR2,
        "worker" or "worker-stream" => DownloadPathPreference.Worker,
        _ => throw new ArgumentException("Download path must be automatic, direct-r2, or worker.")
    };

    public static NativeTransferPlan Build(TestProfileId profile, TransferMethod method)
    {
        var contract = TestProfileContract.Load();
        var definition = contract.Profiles.Single(item => item.Id == ContractId(profile));
        var downloads = BuildDownloads(definition, method);
        var uploads = BuildUploads(definition, method);
        var cap = downloads.Concat(uploads).Sum(stage => stage.CapBytes);
        var measuredMs = downloads.Concat(uploads).Sum(stage => stage.DurationMs);
        var idleMs = definition.IdlePingCount * definition.PingIntervalMs;
        var overheadMs = 2_000 + (definition.IncludeServices ? 5_000 : 0);
        var computedSeconds = Math.Max(5, (int)Math.Ceiling((idleMs + measuredMs + overheadMs) / 5_000d) * 5);

        return new NativeTransferPlan(
            profile,
            method,
            definition.Name,
            definition.EstimatedSeconds,
            Math.Max(definition.EstimatedSeconds, computedSeconds),
            cap,
            definition.IncludeServices,
            definition.IdlePingCount,
            definition.PingIntervalMs,
            downloads,
            uploads);
    }

    private static IReadOnlyList<TransferStagePlan> BuildDownloads(
        TestProfileDefinition profile,
        TransferMethod method)
    {
        if (method == TransferMethod.Single)
        {
            return [Stage("single", TransferDirection.Download, TransferStrategy.Single, 1,
                profile.DownloadDurationMs, profile.DownloadCapBytes, profile.DownloadSamples)];
        }

        if (method == TransferMethod.Aggregate)
        {
            return [Stage("aggregate", TransferDirection.Download, TransferStrategy.Aggregate,
                profile.AggregateDownloadConnections, profile.DownloadDurationMs,
                profile.DownloadCapBytes, profile.DownloadSamples)];
        }

        if (profile.Id == "extended")
        {
            return profile.DownloadScaling.Select(item => Stage(
                $"scale-{item.Connections}",
                TransferDirection.Download,
                item.Connections == 1 ? TransferStrategy.Single : TransferStrategy.Aggregate,
                item.Connections,
                item.DurationMs,
                item.CapBytes)).ToArray();
        }

        return
        [
            Stage("single", TransferDirection.Download, TransferStrategy.Single, 1,
                profile.Comparison.SingleDownloadDurationMs,
                profile.Comparison.SingleDownloadCapBytes),
            Stage("aggregate", TransferDirection.Download, TransferStrategy.Aggregate,
                profile.AggregateDownloadConnections,
                profile.DownloadDurationMs,
                profile.DownloadCapBytes - profile.Comparison.SingleDownloadCapBytes,
                profile.DownloadSamples)
        ];
    }

    private static IReadOnlyList<TransferStagePlan> BuildUploads(
        TestProfileDefinition profile,
        TransferMethod method)
    {
        if (method == TransferMethod.Single)
        {
            return [Stage("single", TransferDirection.Upload, TransferStrategy.Single, 1,
                profile.UploadDurationMs, profile.UploadCapBytes)];
        }

        if (method == TransferMethod.Aggregate)
        {
            return [Stage("aggregate", TransferDirection.Upload, TransferStrategy.Aggregate,
                profile.AggregateUploadConnections, profile.UploadDurationMs,
                profile.UploadCapBytes)];
        }

        if (profile.Comparison.SingleUploadDurationMs <= 0)
        {
            return [Stage("aggregate", TransferDirection.Upload, TransferStrategy.Aggregate,
                profile.AggregateUploadConnections, profile.UploadDurationMs,
                profile.UploadCapBytes)];
        }

        return
        [
            Stage("single", TransferDirection.Upload, TransferStrategy.Single, 1,
                profile.Comparison.SingleUploadDurationMs,
                profile.Comparison.SingleUploadCapBytes),
            Stage("aggregate", TransferDirection.Upload, TransferStrategy.Aggregate,
                profile.AggregateUploadConnections,
                profile.UploadDurationMs,
                profile.UploadCapBytes - profile.Comparison.SingleUploadCapBytes)
        ];
    }

    private static TransferStagePlan Stage(
        string id,
        TransferDirection direction,
        TransferStrategy strategy,
        int connections,
        int durationMs,
        long capBytes,
        int samples = 1)
    {
        return new TransferStagePlan(id, direction, strategy, connections, durationMs, capBytes, samples);
    }

    private static string ContractId(TestProfileId profile) => profile switch
    {
        TestProfileId.ConnectionCheck => "connection-check",
        TestProfileId.Quick => "quick",
        TestProfileId.Standard => "standard",
        TestProfileId.Extended => "extended",
        _ => throw new ArgumentOutOfRangeException(nameof(profile))
    };
}
