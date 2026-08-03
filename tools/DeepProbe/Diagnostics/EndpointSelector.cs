using System.Diagnostics;
using System.Net;
using System.Reflection;
using NetworkDeepProbe.Models;

namespace NetworkDeepProbe.Diagnostics;

internal sealed record EndpointSelectionResult(
    MeasurementEndpoint Selected,
    string SelectionReason,
    IReadOnlyList<EndpointProbeReport> Candidates);

internal static class MeasurementEndpointCatalog
{
    public static MeasurementEndpoint Primary { get; } = new(
        "cloudflare-primary",
        "Johnny Li edge",
        "Cloudflare",
        InternetTransferProbe.DefaultOrigin);

    public static MeasurementEndpoint FromOrigin(Uri origin, int index = 0)
    {
        ArgumentNullException.ThrowIfNull(origin);
        if (origin == InternetTransferProbe.DefaultOrigin) return Primary;
        return new MeasurementEndpoint(
            $"custom-{index + 1}",
            origin.Host,
            "Custom",
            origin,
            index > 0);
    }
}

internal static class EndpointSelector
{
    private const int SamplesPerEndpoint = 2;
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromMilliseconds(1_800);

    public static async Task<EndpointSelectionResult> SelectAsync(
        IReadOnlyList<MeasurementEndpoint> endpoints,
        CancellationToken cancellationToken,
        HttpMessageHandler? handler = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        if (endpoints.Count == 0) throw new ArgumentException("At least one measurement endpoint is required.", nameof(endpoints));
        if (endpoints.Count > 8) throw new ArgumentException("No more than eight measurement endpoints may be probed.", nameof(endpoints));

        ValidateEndpoints(endpoints);
        using var client = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        client.Timeout = Timeout.InfiniteTimeSpan;
        var probes = await Task.WhenAll(endpoints.Select(endpoint => ProbeAsync(client, endpoint, cancellationToken)));
        var available = probes
            .Where(probe => probe.Report.Available && probe.Report.MedianLatencyMs is not null)
            .OrderBy(probe => probe.Report.MedianLatencyMs)
            .ToArray();
        if (available.Length == 0)
        {
            var errors = string.Join("; ", probes.Select(probe => $"{probe.Endpoint.Name}: {probe.Report.Error ?? "unavailable"}"));
            throw new InvalidOperationException($"No measurement endpoint passed preflight. {errors}");
        }

        var selected = available[0];
        var availableCount = probes.Count(probe => probe.Report.Available);
        var reason = availableCount > 1 ? "lowest-latency" : "only-available";
        return new EndpointSelectionResult(selected.Endpoint, reason, probes.Select(probe => probe.Report).ToArray());
    }

    public static MeasurementContextReport CreateContext(
        EndpointSelectionResult selection,
        string engine,
        IEnumerable<string> capabilities)
    {
        ArgumentNullException.ThrowIfNull(selection);
        var selectedProbe = selection.Candidates.Single(candidate => candidate.Id == selection.Selected.Id);
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
        return new MeasurementContextReport(
            "1.0",
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
            selection.Candidates);
    }

    private static async Task<(MeasurementEndpoint Endpoint, EndpointProbeReport Report)> ProbeAsync(
        HttpClient client,
        MeasurementEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        var samples = new List<double>(SamplesPerEndpoint);
        string? error = null;
        for (var sample = 0; sample < SamplesPerEndpoint; sample++)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(ProbeTimeout);
            var started = Stopwatch.GetTimestamp();
            try
            {
                var ping = new Uri(endpoint.Origin, $"api/ping?n={Guid.NewGuid():N}");
                using var request = new HttpRequestMessage(HttpMethod.Get, ping)
                {
                    Version = HttpVersion.Version20,
                    VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
                };
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                if (!response.IsSuccessStatusCode)
                {
                    error = $"HTTP {(int)response.StatusCode}";
                    continue;
                }
                await response.Content.ReadAsByteArrayAsync(timeout.Token);
                samples.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                error = "timed out";
            }
            catch (HttpRequestException requestError)
            {
                error = requestError.Message;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        double? median = samples.Count == 0 ? null : Median(samples);
        return (endpoint, new EndpointProbeReport(
            endpoint.Id,
            endpoint.Name,
            endpoint.Provider,
            endpoint.Origin.ToString(),
            median is not null,
            median,
            median is null ? error ?? "no successful response" : null));
    }

    private static void ValidateEndpoints(IReadOnlyList<MeasurementEndpoint> endpoints)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var endpoint in endpoints)
        {
            if (string.IsNullOrWhiteSpace(endpoint.Id)) throw new ArgumentException("Endpoint IDs cannot be empty.", nameof(endpoints));
            if (!ids.Add(endpoint.Id)) throw new ArgumentException($"Endpoint ID '{endpoint.Id}' is duplicated.", nameof(endpoints));
            if (!endpoint.Origin.IsAbsoluteUri || endpoint.Origin.Scheme is not ("http" or "https"))
            {
                throw new ArgumentException($"Endpoint '{endpoint.Id}' must use an absolute HTTP or HTTPS origin.", nameof(endpoints));
            }
        }
    }

    private static double Median(IReadOnlyList<double> values)
    {
        var ordered = values.Order().ToArray();
        var middle = ordered.Length / 2;
        return ordered.Length % 2 == 0
            ? (ordered[middle - 1] + ordered[middle]) / 2
            : ordered[middle];
    }
}
