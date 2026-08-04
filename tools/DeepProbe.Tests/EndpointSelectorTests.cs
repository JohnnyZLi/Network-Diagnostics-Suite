using System.Net;
using NetworkDeepProbe.Diagnostics;
using NetworkDeepProbe.Models;

namespace NetworkDeepProbe.Tests;

public sealed class EndpointSelectorTests
{
    [Fact]
    public async Task SelectsTheLowestLatencyAvailableEndpoint()
    {
        var endpoints = new[]
        {
            new MeasurementEndpoint("slow", "Slow", "Provider A", new Uri("https://slow.example/")),
            new MeasurementEndpoint("fast", "Fast", "Provider B", new Uri("https://fast.example/"), true)
        };
        using var handler = new EndpointHandler(request => request.RequestUri?.Host == "fast.example"
            ? (HttpStatusCode.OK, 2)
            : (HttpStatusCode.OK, 25));

        var selection = await EndpointSelector.SelectAsync(endpoints, CancellationToken.None, handler);

        Assert.Equal("fast", selection.Selected.Id);
        Assert.Equal("lowest-latency", selection.SelectionReason);
        Assert.All(selection.Candidates, candidate => Assert.True(candidate.Available));
    }

    [Fact]
    public async Task FallsBackWhenTheFasterEndpointIsUnavailable()
    {
        var endpoints = new[]
        {
            new MeasurementEndpoint("available", "Available", "Provider A", new Uri("https://available.example/")),
            new MeasurementEndpoint("broken", "Broken", "Provider B", new Uri("https://broken.example/"), true)
        };
        using var handler = new EndpointHandler(request => request.RequestUri?.Host == "broken.example"
            ? (HttpStatusCode.ServiceUnavailable, 1)
            : (HttpStatusCode.OK, 3));

        var selection = await EndpointSelector.SelectAsync(endpoints, CancellationToken.None, handler);

        Assert.Equal("available", selection.Selected.Id);
        Assert.Equal("only-available", selection.SelectionReason);
        Assert.False(Assert.Single(selection.Candidates, candidate => candidate.Id == "broken").Available);
    }

    private sealed class EndpointHandler(Func<HttpRequestMessage, (HttpStatusCode Status, int DelayMs)> response) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var result = response(request);
            await Task.Delay(result.DelayMs, cancellationToken);
            return new HttpResponseMessage(result.Status)
            {
                Content = new StringContent("ok"),
                RequestMessage = request
            };
        }
    }
}
