using NetworkDeepProbe.Diagnostics;
using Photino.NET;

namespace NetworkDiagnostics.Desktop;

public sealed partial class PhotinoDesktopBridge
{
    private void StartLanClient(PhotinoWindow sender, BridgeRequest request)
    {
        var target = BridgeProtocol.ParseOptionalString(request.Payload, "target");
        if (string.IsNullOrWhiteSpace(target)) throw new ArgumentException("A LAN server target is required.");
        var port = BridgeProtocol.ParseRequiredInt(request.Payload, "port", 1024, 65535);
        var durationSeconds = BridgeProtocol.ParseRequiredInt(request.Payload, "durationSeconds", 3, 30);
        var connections = BridgeProtocol.ParseRequiredInt(request.Payload, "connections", 1, 16);
        var interfaceId = BridgeProtocol.ParseOptionalString(request.Payload, "interfaceId");
        var clientRunId = Guid.NewGuid();

        lock (lanServerGate)
        {
            if (lanClientTask is { IsCompleted: false })
            {
                throw new InvalidOperationException("A LAN throughput test is already running.");
            }
            lanClientCancellation?.Dispose();
            lanClientCancellation = new CancellationTokenSource();
            var cancellation = lanClientCancellation;
            var progress = new Progress<string>(message =>
            {
                var targetWindow = window;
                if (targetWindow is not null && !disposed)
                {
                    SendEvent(targetWindow, "lan.client.progress", new { runId = clientRunId, message });
                }
            });
            lanClientTask = Task.Run(async () =>
            {
                try
                {
                    var report = await NetworkDiagnosticsRunner.RunLanThroughputAsync(
                        target,
                        port,
                        durationSeconds,
                        connections,
                        interfaceId,
                        progress,
                        cancellation.Token);
                    var targetWindow = window;
                    if (targetWindow is not null && !disposed)
                    {
                        SendEvent(targetWindow, "lan.client.completed", new { runId = clientRunId, report });
                    }
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                    var targetWindow = window;
                    if (targetWindow is not null && !disposed)
                    {
                        SendEvent(targetWindow, "lan.client.cancelled", new { runId = clientRunId });
                    }
                }
                catch (Exception error)
                {
                    var targetWindow = window;
                    if (targetWindow is not null && !disposed)
                    {
                        SendEvent(targetWindow, "lan.client.failed", new
                        {
                            runId = clientRunId,
                            message = SafeMessage(error)
                        });
                    }
                }
            });
        }

        SendResponse(sender, request.Id, true, new
        {
            runId = clientRunId,
            target,
            port,
            durationSeconds,
            connections
        });
    }

    private void CancelLanClient(PhotinoWindow sender, string? requestId)
    {
        bool cancelled;
        lock (lanServerGate)
        {
            cancelled = lanClientTask is { IsCompleted: false };
            lanClientCancellation?.Cancel();
        }
        SendResponse(sender, requestId, true, new { cancelled });
    }
}
