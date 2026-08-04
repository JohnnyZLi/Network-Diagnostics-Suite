using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using NetworkDeepProbe.Models;

namespace NetworkDeepProbe.Diagnostics;

internal sealed record MeasurementPreflightResult(
    EndpointSelectionResult EndpointSelection,
    ResolvedNetworkBinding? Binding,
    MeasurementContextReport Measurement);

internal static class MeasurementPreflight
{
    public static async Task<MeasurementPreflightResult> RunAsync(
        IReadOnlyList<Uri> origins,
        string? interfaceId,
        bool includeAddresses,
        string engine,
        IEnumerable<string> capabilities,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(origins);
        var binding = NetworkBindingResolver.Resolve(interfaceId);
        var endpoints = origins
            .DistinctBy(origin => origin.AbsoluteUri, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .Select((origin, index) => MeasurementEndpointCatalog.FromOrigin(origin, index))
            .ToArray();
        var selection = await EndpointSelector.SelectAsync(endpoints, cancellationToken, sourceAddress: binding?.SourceAddress);
        var metadataTask = EndpointMetadataProbe.RunAsync(selection.Selected.Origin, binding?.SourceAddress, cancellationToken);
        var http3Task = Http3Probe.RunAsync(selection.Selected.Origin, cancellationToken);
        await Task.WhenAll(metadataTask, http3Task);

        var selectedProbe = selection.Candidates.Single(candidate => candidate.Id == selection.Selected.Id);
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
        var measurement = new MeasurementContextReport(
            "1.1",
            engine,
            version,
            capabilities.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            new MeasurementEndpointReport(
                selection.Selected.Id,
                selection.Selected.Name,
                selection.Selected.Provider,
                selection.Selected.Origin.ToString(),
                selection.SelectionReason,
                selectedProbe.MedianLatencyMs),
            selection.Candidates,
            NetworkBindingResolver.CreateReport(binding, includeAddresses),
            await metadataTask,
            await http3Task);
        return new MeasurementPreflightResult(selection, binding, measurement);
    }
}

internal static class EndpointMetadataProbe
{
    private sealed record MetadataResponse(
        [property: JsonPropertyName("edge")] string? Edge,
        [property: JsonPropertyName("network")] string? Network,
        [property: JsonPropertyName("asn")] int? Asn,
        [property: JsonPropertyName("protocol")] string? Protocol,
        [property: JsonPropertyName("tlsVersion")] string? TlsVersion,
        [property: JsonPropertyName("ipVersion")] string? IpVersion);

    public static async Task<NetworkMetadataReport?> RunAsync(
        Uri origin,
        IPAddress? sourceAddress,
        CancellationToken cancellationToken)
    {
        using var client = BoundHttpClientFactory.Create(2, sourceAddress);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            var response = await client.GetFromJsonAsync<MetadataResponse>(
                new Uri(origin, $"api/meta?n={Guid.NewGuid():N}"),
                timeout.Token);
            return response is null
                ? null
                : new NetworkMetadataReport(
                    response.Edge,
                    response.Network,
                    response.Asn,
                    response.Protocol,
                    response.TlsVersion,
                    response.IpVersion);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }
}

internal static class Http3Probe
{
    public static async Task<Http3ProbeReport> RunAsync(Uri origin, CancellationToken cancellationToken)
    {
        if (!string.Equals(origin.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return new Http3ProbeReport(false, false, null, null, "HTTP/3 requires HTTPS.");
        }

        using var client = new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.None,
            ConnectTimeout = TimeSpan.FromSeconds(4),
            PooledConnectionLifetime = TimeSpan.FromMinutes(1)
        })
        {
            Timeout = Timeout.InfiniteTimeSpan,
            DefaultRequestVersion = HttpVersion.Version30,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact
        };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        var started = Stopwatch.GetTimestamp();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(origin, $"api/ping?n={Guid.NewGuid():N}"))
            {
                Version = HttpVersion.Version30,
                VersionPolicy = HttpVersionPolicy.RequestVersionExact
            };
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            await response.Content.ReadAsByteArrayAsync(timeout.Token);
            var negotiated = $"HTTP/{response.Version.Major}.{response.Version.Minor}";
            return new Http3ProbeReport(
                true,
                response.IsSuccessStatusCode && response.Version.Major >= 3,
                negotiated,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new Http3ProbeReport(true, false, null, null, "HTTP/3 probe timed out.");
        }
        catch (Exception error) when (error is HttpRequestException or PlatformNotSupportedException or NotSupportedException)
        {
            var message = string.IsNullOrWhiteSpace(error.Message) ? error.GetType().Name : error.Message;
            return new Http3ProbeReport(true, false, null, null, message.Length <= 240 ? message : $"{message[..237]}...");
        }
    }
}
