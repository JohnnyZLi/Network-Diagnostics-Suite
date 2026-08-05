using NetworkDiagnostics.Desktop.Monitoring;
using Xunit;

namespace NetworkDiagnostics.Desktop.Tests;

public sealed class MonitorHistoryStoreTests
{
    [Fact]
    public async Task SamplesAndAlertsRoundTripLocally()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var root = CreateTemporaryRoot();
        try
        {
            var store = new MonitorHistoryStore(root);
            var sample = new MonitorSample(
                DateTimeOffset.UtcNow,
                MonitorSampleState.Responsive,
                24,
                3,
                7,
                24,
                0,
                "Wi-Fi",
                "network");
            var alert = new MonitorAlert(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                MonitorAlertKind.Degradation,
                MonitorAlertSeverity.Warning,
                "Network score dropped",
                "The five-minute score fell below its threshold.");

            await store.AppendSampleAsync(sample, cancellationToken);
            await store.SaveAlertsAsync([alert], cancellationToken);

            var samples = await store.LoadSamplesAsync(cancellationToken);
            var alerts = await store.LoadAlertsAsync(cancellationToken);

            Assert.Single(samples);
            Assert.Equal(sample.NetworkSignature, samples[0].NetworkSignature);
            Assert.Single(alerts);
            Assert.Equal(alert.Id, alerts[0].Id);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Fact]
    public async Task InvalidSampleLineDoesNotDiscardValidHistory()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var root = CreateTemporaryRoot();
        try
        {
            var store = new MonitorHistoryStore(root);
            var sample = new MonitorSample(
                DateTimeOffset.UtcNow,
                MonitorSampleState.Responsive,
                18,
                2,
                6,
                18,
                0,
                "Ethernet",
                "network");
            await store.AppendSampleAsync(sample, cancellationToken);
            await File.AppendAllTextAsync(
                store.SamplesPath,
                "{interrupted write" + Environment.NewLine,
                cancellationToken);

            var samples = await store.LoadSamplesAsync(cancellationToken);

            Assert.Single(samples);
            Assert.Equal("Ethernet", samples[0].InterfaceName);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Fact]
    public async Task PruneRemovesHistoryOutsideRetentionWindow()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var root = CreateTemporaryRoot();
        try
        {
            var store = new MonitorHistoryStore(root);
            var recent = new MonitorSample(
                DateTimeOffset.UtcNow,
                MonitorSampleState.Responsive,
                20,
                2,
                8,
                20,
                0,
                "Wi-Fi",
                "recent");
            var stale = recent with
            {
                Timestamp = DateTimeOffset.UtcNow.AddDays(-8),
                NetworkSignature = "stale"
            };

            await store.PruneAsync([stale, recent], cancellationToken);
            var samples = await store.LoadSamplesAsync(cancellationToken);

            Assert.Single(samples);
            Assert.Equal("recent", samples[0].NetworkSignature);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    private static string CreateTemporaryRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "network-diagnostics-monitor-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteTemporaryRoot(string root)
    {
        try
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
        catch (IOException)
        {
            // Temporary test data can be cleaned by the operating system if a handle closes late.
        }
    }
}
