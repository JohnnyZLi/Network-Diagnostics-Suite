using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetworkDiagnostics.Desktop.Monitoring;

public sealed class MonitorHistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    private static readonly TimeSpan Retention = TimeSpan.FromDays(7);
    private readonly SemaphoreSlim gate = new(1, 1);

    public MonitorHistoryStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        RootDirectory = Path.Combine(rootDirectory, "monitoring");
        SamplesPath = Path.Combine(RootDirectory, "samples.jsonl");
        AlertsPath = Path.Combine(RootDirectory, "alerts.json");
    }

    public string RootDirectory { get; }

    public string SamplesPath { get; }

    public string AlertsPath { get; }

    public async Task<IReadOnlyList<MonitorSample>> LoadSamplesAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(SamplesPath)) return [];

            var cutoff = DateTimeOffset.UtcNow - Retention;
            var samples = new List<MonitorSample>();
            await foreach (var line in File.ReadLinesAsync(SamplesPath, cancellationToken))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var sample = JsonSerializer.Deserialize<MonitorSample>(line, JsonOptions);
                    if (sample is not null && sample.Timestamp >= cutoff)
                    {
                        samples.Add(sample);
                    }
                }
                catch (JsonException)
                {
                    // Keep valid history even if one line was interrupted during a prior write.
                }
            }

            return samples
                .OrderBy(sample => sample.Timestamp)
                .ToArray();
        }
        catch (IOException)
        {
            return [];
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyList<MonitorAlert>> LoadAlertsAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(AlertsPath)) return [];
            var json = await File.ReadAllTextAsync(AlertsPath, cancellationToken);
            return (JsonSerializer.Deserialize<MonitorAlert[]>(json, JsonOptions) ?? [])
                .OrderByDescending(alert => alert.Timestamp)
                .Take(200)
                .ToArray();
        }
        catch (Exception error) when (error is IOException or JsonException)
        {
            return [];
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task AppendSampleAsync(MonitorSample sample, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(RootDirectory);
            var line = JsonSerializer.Serialize(sample, JsonOptions) + Environment.NewLine;
            await File.AppendAllTextAsync(SamplesPath, line, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SaveAlertsAsync(IEnumerable<MonitorAlert> alerts, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(alerts);
        await gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(RootDirectory);
            var ordered = alerts
                .OrderByDescending(alert => alert.Timestamp)
                .Take(200)
                .ToArray();
            var temporary = AlertsPath + ".tmp";
            await File.WriteAllTextAsync(
                temporary,
                JsonSerializer.Serialize(ordered, JsonOptions),
                cancellationToken);
            File.Move(temporary, AlertsPath, true);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task PruneAsync(IEnumerable<MonitorSample> samples, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(samples);
        await gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(RootDirectory);
            var cutoff = DateTimeOffset.UtcNow - Retention;
            var retained = samples
                .Where(sample => sample.Timestamp >= cutoff)
                .OrderBy(sample => sample.Timestamp)
                .ToArray();
            var temporary = SamplesPath + ".tmp";
            await using (var stream = File.Create(temporary))
            await using (var writer = new StreamWriter(stream))
            {
                foreach (var sample in retained)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await writer.WriteLineAsync(JsonSerializer.Serialize(sample, JsonOptions));
                }
            }
            File.Move(temporary, SamplesPath, true);
        }
        finally
        {
            gate.Release();
        }
    }
}
