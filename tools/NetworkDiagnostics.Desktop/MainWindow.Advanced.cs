using Avalonia.Interactivity;
using NetworkDeepProbe.Models;

namespace NetworkDiagnostics.Desktop;

public sealed partial class MainWindow
{
    private void SelectInterface(int index) => _ = SelectInterfaceAsync(index);

    private async Task SelectInterfaceAsync(int index)
    {
        index = Math.Clamp(index, 0, interfaceChoices.Count);
        var changed = selectedInterfaceIndex != index;
        selectedInterfaceIndex = index;
        if (initialized && changed)
        {
            settings = settings with { InterfaceId = SelectedInterfaceId() };
            await PersistSettingsAsync();
            await RefreshPreflightAsync();
        }
        SyncTestWorkspace();
        SyncSettingsWorkspace();
        RefreshWorkbenchChrome();
    }

    private async Task RefreshPreflightAsync()
    {
        preflightCancellation?.Cancel();
        preflightCancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        preflightCancellation = cancellation;

        preflightNetwork = "Reading network…";
        preflightEndpoint = "Selecting endpoint…";
        preflightInterface = "Resolving interface…";
        SyncPreflightPresentation();
        try
        {
            var result = await diagnosticRunService.PreflightAsync(
                SelectedProfile(),
                SelectedMethod(),
                settings,
                cancellation.Token);
            if (cancellation.IsCancellationRequested) return;
            RenderPreflight(result.Measurement);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Replaced by a newer preflight request or the window closed.
        }
        catch (Exception error)
        {
            if (cancellation.IsCancellationRequested) return;
            preflightNetwork = "Not measured";
            preflightEndpoint = error.Message;
            preflightInterface = settings.InterfaceId is null
                ? "Automatic system routing"
                : "Selected interface unavailable";
            SyncPreflightPresentation();
        }
        finally
        {
            if (ReferenceEquals(preflightCancellation, cancellation)) preflightCancellation = null;
            cancellation.Dispose();
        }
    }

    private void RenderPreflight(MeasurementContextReport measurement)
    {
        var endpoint = measurement.SelectedEndpoint;
        var candidates = measurement.EndpointCandidates.Count;
        preflightEndpoint = $"{endpoint.Name} · {endpoint.Provider} · {endpoint.PreflightLatencyMs?.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) ?? "—"} ms · {candidates} candidate{(candidates == 1 ? string.Empty : "s")}";
        var network = measurement.Network;
        preflightNetwork = network is null
            ? "Network metadata unavailable"
            : string.Join(" · ", new[]
            {
                network.Network,
                network.Asn is null ? null : $"AS{network.Asn}",
                network.Edge,
                network.Protocol,
                network.TlsVersion,
                network.IpVersion
            }.Where(value => !string.IsNullOrWhiteSpace(value)));
        preflightInterface = measurement.SelectedInterface is null
            ? "Automatic system routing"
            : $"{measurement.SelectedInterface.Name} · {measurement.SelectedInterface.Type}{(measurement.SelectedInterface.LinkSpeedMbps is null ? string.Empty : $" · {measurement.SelectedInterface.LinkSpeedMbps:N0} Mbps")}";
        SyncPreflightPresentation();
    }

    private void SyncPreflightPresentation()
    {
        SyncTestWorkspace();
        SyncSettingsWorkspace();
        RefreshWorkbenchChrome();
    }

    private async Task StartLanServerAsync()
    {
        if (lanServerCancellation is not null) return;
        if (!int.TryParse(lanPortText, out var port) || port is < 1024 or > 65535)
        {
            lanServerStatus = "Enter a LAN port between 1024 and 65535.";
            SyncSettingsWorkspace("Measurement");
            return;
        }

        var cancellation = new CancellationTokenSource();
        lanServerCancellation = cancellation;
        lanServerRunning = true;
        lanServerStatus = "Starting LAN server…";
        SyncSettingsWorkspace("Measurement");
        var progress = new Progress<string>(message =>
        {
            lanServerStatus = message;
            SyncSettingsWorkspace("Measurement");
        });
        try
        {
            await diagnosticRunService.RunLanServerAsync(port, progress, cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            lanServerStatus = "LAN server stopped.";
        }
        catch (Exception error)
        {
            lanServerStatus = $"LAN server failed: {error.Message}";
        }
        finally
        {
            if (ReferenceEquals(lanServerCancellation, cancellation)) lanServerCancellation = null;
            lanServerRunning = false;
            cancellation.Dispose();
            SyncSettingsWorkspace("Measurement");
        }
    }

    private void StopLanServer() => lanServerCancellation?.Cancel();

    private async void StartLanServerClicked(object? sender, RoutedEventArgs eventArgs) =>
        await StartLanServerAsync();

    private void StopLanServerClicked(object? sender, RoutedEventArgs eventArgs) =>
        StopLanServer();
}
