using Avalonia.Controls;
using Avalonia.Interactivity;
using NetworkDeepProbe.Models;

namespace NetworkDiagnostics.Desktop;

public sealed partial class MainWindow
{
    private async void InterfaceSelectionChanged(object? sender, SelectionChangedEventArgs eventArgs)
    {
        if (!initialized) return;
        var interfaceId = (InterfaceSelector.SelectedItem as ComboBoxItem)?.Tag as string;
        settings = settings with { InterfaceId = interfaceId };
        await PersistSettingsAsync();
        await RefreshPreflightAsync();
    }

    private async void RefreshPreflightClicked(object? sender, RoutedEventArgs eventArgs) =>
        await RefreshPreflightAsync();

    private async Task RefreshPreflightAsync()
    {
        preflightCancellation?.Cancel();
        preflightCancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        preflightCancellation = cancellation;

        PreflightStatusText.Text = "Checking endpoint candidates, network metadata, and HTTP/3…";
        PreflightNetworkText.Text = "Reading network…";
        PreflightEndpointText.Text = "Selecting endpoint…";
        PreflightInterfaceText.Text = "Resolving interface…";
        PreflightHttp3Text.Text = "Testing protocol…";
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
            PreflightStatusText.Text = "Pre-test path check unavailable. The Run button remains available and will retry.";
            PreflightNetworkText.Text = "Not measured";
            PreflightEndpointText.Text = error.Message;
            PreflightInterfaceText.Text = settings.InterfaceId is null ? "Automatic system routing" : "Selected interface unavailable";
            PreflightHttp3Text.Text = "Not measured";
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
        PreflightStatusText.Text = "Ready. This path information was measured before the test starts.";
        PreflightEndpointText.Text = $"{endpoint.Name} · {endpoint.Provider} · {endpoint.PreflightLatencyMs?.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) ?? "—"} ms · {candidates} candidate{(candidates == 1 ? string.Empty : "s")}";
        var network = measurement.Network;
        PreflightNetworkText.Text = network is null
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
        PreflightInterfaceText.Text = measurement.SelectedInterface is null
            ? "Automatic system routing"
            : $"{measurement.SelectedInterface.Name} · {measurement.SelectedInterface.Type}{(measurement.SelectedInterface.LinkSpeedMbps is null ? string.Empty : $" · {measurement.SelectedInterface.LinkSpeedMbps:N0} Mbps")}";
        PreflightHttp3Text.Text = measurement.Http3 switch
        {
            { Supported: true } http3 => $"Available · {http3.NegotiatedProtocol} · {http3.DurationMs:0.0} ms",
            { Attempted: true } http3 => $"Not available · {http3.Error ?? "exact-version request failed"}",
            _ => "Not measured"
        };
    }

    private async void StartLanServerClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (lanServerCancellation is not null) return;
        if (!int.TryParse(LanPortTextBox.Text, out var port) || port is < 1024 or > 65535)
        {
            LanServerStatusText.Text = "Enter a LAN port between 1024 and 65535.";
            return;
        }

        var cancellation = new CancellationTokenSource();
        lanServerCancellation = cancellation;
        StartLanServerButton.IsEnabled = false;
        StopLanServerButton.IsEnabled = true;
        LanServerStatusText.Text = "Starting LAN server…";
        var progress = new Progress<string>(message => LanServerStatusText.Text = message);
        try
        {
            await diagnosticRunService.RunLanServerAsync(port, progress, cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            LanServerStatusText.Text = "LAN server stopped.";
        }
        catch (Exception error)
        {
            LanServerStatusText.Text = $"LAN server failed: {error.Message}";
        }
        finally
        {
            if (ReferenceEquals(lanServerCancellation, cancellation)) lanServerCancellation = null;
            StartLanServerButton.IsEnabled = true;
            StopLanServerButton.IsEnabled = false;
            cancellation.Dispose();
        }
    }

    private void StopLanServerClicked(object? sender, RoutedEventArgs eventArgs) =>
        lanServerCancellation?.Cancel();
}
