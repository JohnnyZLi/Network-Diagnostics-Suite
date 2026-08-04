using System.Text.Json;
using System.Text.Json.Serialization;
using NetworkDeepProbe.Planning;
using NetworkDeepProbe.Models;

namespace NetworkDeepProbe.Tests;

public sealed class ReportSerializationCompatibilityTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    [Theory]
    [InlineData(TestProfileId.ConnectionCheck, "\"connection-check\"")]
    [InlineData(TestProfileId.Quick, "\"quick\"")]
    [InlineData(TestProfileId.Standard, "\"standard\"")]
    [InlineData(TestProfileId.Extended, "\"extended\"")]
    public void ProfileIdentifiersRemainStable(TestProfileId profile, string expectedJson)
    {
        Assert.Equal(expectedJson, JsonSerializer.Serialize(profile, JsonOptions));
    }
    [Fact]
    public void MeasurementContextSerializesInterfaceNetworkAndHttp3Evidence()
    {
        var context = new MeasurementContextReport(
            "1.1",
            "network-diagnostics-native",
            "2.0.0",
            ["endpoint-preflight", "http3-probe"],
            new MeasurementEndpointReport("primary", "Primary", "Cloudflare", "https://example.com/", "lowest-latency", 12),
            [new EndpointProbeReport("primary", "Primary", "Cloudflare", "https://example.com/", true, 12, null)],
            new SelectedInterfaceReport("id", "en0", "Wi-Fi", "Wireless80211", 1200, "bound", null),
            new NetworkMetadataReport("LAX", "Example", 64500, "HTTP/2", "TLS 1.3", "IPv6"),
            new Http3ProbeReport(true, true, "HTTP/3.0", 18, null));

        var json = JsonSerializer.Serialize(context, JsonOptions);

        Assert.Contains("\"contractVersion\":\"1.1\"", json, StringComparison.Ordinal);
        Assert.Contains("\"selectedInterface\"", json, StringComparison.Ordinal);
        Assert.Contains("\"network\"", json, StringComparison.Ordinal);
        Assert.Contains("\"http3\"", json, StringComparison.Ordinal);
    }

}
