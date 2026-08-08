using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using NetworkDeepProbe.Models;
using NetworkDeepProbe.Planning;
using NetworkDiagnostics.Desktop.Monitoring;
using NetworkDiagnostics.Desktop.Presentation;
using NetworkDiagnostics.Desktop.Services;
using NetworkDiagnostics.Desktop.Workspaces;
using Xunit;

namespace NetworkDiagnostics.Desktop.Tests;

public sealed class TestSetupWorkspaceMemoryTests
{
    [AvaloniaFact]
    public void MonitoringRefreshesReuseVisualControlsAndRemainMemoryBounded()
    {
        var workspace = new TestSetupWorkspace();
        var window = new Window
        {
            Width = 1200,
            Height = 800,
            Content = workspace
        };

        try
        {
            window.Show();
            var presentation = CreatePresentation();

            for (var index = 0; index < 100; index++)
            {
                workspace.RenderMonitoringSnapshot(presentation with { LastUpdated = $"Warmup {index}" });
            }

            var responsiveness = Assert.IsType<Grid>(workspace.FindControl<Grid>("ResponsivenessTimelineGrid"));
            var reliability = Assert.IsType<Grid>(workspace.FindControl<Grid>("ReliabilityTimelineGrid"));
            var alerts = Assert.IsType<StackPanel>(workspace.FindControl<StackPanel>("AlertsPanel"));
            var speedMetrics = Assert.IsType<Grid>(workspace.FindControl<Grid>("SpeedMetricsGrid"));

            var responsivenessControls = responsiveness.Children.ToArray();
            var reliabilityControls = reliability.Children.ToArray();
            var alertControls = alerts.Children.ToArray();
            var speedMetricControls = speedMetrics.Children.ToArray();

            CollectFully();
            var baselineBytes = GC.GetTotalMemory(forceFullCollection: true);

            for (var index = 0; index < 5_000; index++)
            {
                workspace.RenderMonitoringSnapshot(presentation with { LastUpdated = $"Update {index}" });
            }

            CollectFully();
            var finalBytes = GC.GetTotalMemory(forceFullCollection: true);

            AssertSameControls(responsivenessControls, responsiveness.Children);
            AssertSameControls(reliabilityControls, reliability.Children);
            AssertSameControls(alertControls, alerts.Children);
            AssertSameControls(speedMetricControls, speedMetrics.Children);

            var retainedGrowth = finalBytes - baselineBytes;
            Assert.True(
                retainedGrowth < 32L * 1024 * 1024,
                $"Monitoring refreshes retained {retainedGrowth / 1024d / 1024d:0.0} MB.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void CompletedRunKeepsTheLiveTileMountedUntilTheReportSheetOpens()
    {
        var workspace = new TestSetupWorkspace();
        var window = new Window
        {
            Width = 1200,
            Height = 800,
            Content = workspace
        };

        try
        {
            window.Show();
            workspace.HoldCompletedRunTile(new ActiveRunSnapshot(
                Guid.NewGuid(),
                ActiveRunStatus.Completed,
                TestProfileId.ConnectionCheck,
                TransferMethod.Aggregate,
                DateTimeOffset.UtcNow,
                "Complete",
                "Diagnostic completed and saved.",
                100,
                182,
                34.2,
                64_000_000,
                Guid.NewGuid()));

            var texts = workspace.GetVisualDescendants().OfType<TextBlock>().ToArray();
            var buttons = workspace.GetVisualDescendants().OfType<Button>().ToArray();

            Assert.Contains(texts, text => text.Text == "DIAGNOSTIC COMPLETE");
            Assert.Contains(texts, text => text.Text == "Preparing the saved result…");
            var stop = Assert.Single(
                buttons,
                button => string.Equals(button.Content?.ToString(), "Stop test", StringComparison.Ordinal));
            Assert.False(stop.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    private static void AssertSameControls(
        IReadOnlyList<Control> expected,
        IReadOnlyList<Control> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var index = 0; index < expected.Count; index++)
        {
            Assert.Same(expected[index], actual[index]);
        }
    }

    private static NetworkExperiencePresentation CreatePresentation()
    {
        var now = DateTimeOffset.UtcNow;
        var samples = Enumerable.Range(0, 72)
            .Select(index => new MonitorSample(
                now - TimeSpan.FromSeconds((71 - index) * 5),
                MonitorSampleState.Responsive,
                18 + index % 12,
                2 + index % 3,
                8 + index % 4,
                24 + index % 7,
                0,
                "Ethernet",
                "test-network"))
            .ToArray();

        var responsiveness = new ExperienceComponentPresentation(
            "Responsiveness",
            96,
            ExperienceBand.Excellent,
            "Excellent",
            "Interactive traffic is responding quickly.",
            [
                ("Typical latency", "24 ms"),
                ("Typical jitter", "3 ms"),
                ("DNS", "9 ms"),
                ("Time to first byte", "28 ms")
            ]);
        var reliability = new ExperienceComponentPresentation(
            "Reliability",
            100,
            ExperienceBand.Excellent,
            "Excellent",
            "No outages or laggy periods were observed.",
            [
                ("Availability", "100.0%"),
                ("Responsive", "100.0%"),
                ("Laggy", "0.0%"),
                ("Unresponsive", "0.0%")
            ]);
        var speed = new ExperienceComponentPresentation(
            "Speed",
            100,
            ExperienceBand.Excellent,
            "Excellent",
            "Recent content speed is close to the configured expectation.",
            [
                ("Content download", "340 Mbps"),
                ("Content upload", "75 Mbps"),
                ("Expected download", "100 Mbps"),
                ("Expected upload", "20 Mbps")
            ]);
        var alerts = new[]
        {
            new MonitorAlert(
                Guid.NewGuid(),
                now,
                MonitorAlertKind.Recovery,
                MonitorAlertSeverity.Information,
                "Connection recovered",
                "The monitor can reach the endpoint again.")
        };

        return new NetworkExperiencePresentation(
            99,
            ExperienceBand.Excellent,
            "Excellent",
            "Rock solid and ready to go.",
            "Test workstation",
            "Ethernet",
            "Updated just now",
            true,
            MonitorWindow.FiveMinutes,
            responsiveness,
            reliability,
            speed,
            samples,
            alerts,
            1);
    }

    private static void CollectFully()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}
