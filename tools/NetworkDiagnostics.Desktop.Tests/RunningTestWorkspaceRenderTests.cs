using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using NetworkDeepProbe.Planning;
using NetworkDiagnostics.Desktop.Services;
using NetworkDiagnostics.Desktop.Workspaces;
using Xunit;

namespace NetworkDiagnostics.Desktop.Tests;

public sealed class RunningTestWorkspaceRenderTests
{
    [AvaloniaFact]
    public void TelemetryUpdatesReusePhaseAndEvidenceControls()
    {
        var workspace = new RunningTestWorkspace();
        var phaseItems = Assert.IsType<ItemsControl>(workspace.FindControl<ItemsControl>("PhaseItems"));
        var eventPanel = Assert.IsType<StackPanel>(workspace.FindControl<StackPanel>("EventPanel"));
        var runId = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow;
        var firstEvent = new ActiveRunEvent(
            startedAt,
            "Latency",
            "Measuring idle response",
            22,
            LiveLatencyMs: 24.5);
        var firstSnapshot = new ActiveRunSnapshot(
            runId,
            ActiveRunStatus.Running,
            TestProfileId.Quick,
            TransferMethod.Compare,
            startedAt,
            "Latency",
            "Measuring idle response",
            22,
            LiveLatencyMs: 24.5);

        workspace.Render(firstSnapshot, [firstEvent]);

        var phaseRows = phaseItems.Items.Cast<object>().ToArray();
        var firstEvidenceRow = eventPanel.Children[0];

        workspace.Render(firstSnapshot with
        {
            Progress = 24,
            LiveLatencyMs = 23.8
        }, [firstEvent]);

        Assert.Equal(phaseRows.Length, phaseItems.ItemCount);
        Assert.All(phaseRows, row => Assert.Contains(row, phaseItems.Items.Cast<object>()));
        Assert.Same(firstEvidenceRow, eventPanel.Children[0]);

        var secondEvent = new ActiveRunEvent(
            startedAt.AddSeconds(1),
            "Download",
            "Measuring transfer capacity",
            34,
            LiveMbps: 92.4,
            BytesTransferred: 4_000_000);
        workspace.Render(firstSnapshot with
        {
            Phase = "Download",
            Detail = "Measuring transfer capacity",
            Progress = 34,
            LiveMbps = 92.4,
            BytesTransferred = 4_000_000
        }, [firstEvent, secondEvent]);

        Assert.Equal(2, eventPanel.Children.Count);
        Assert.Same(firstEvidenceRow, eventPanel.Children[1]);
    }
}
