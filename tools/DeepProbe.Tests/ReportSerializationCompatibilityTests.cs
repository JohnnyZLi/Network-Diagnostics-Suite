using System.Text.Json;
using System.Text.Json.Serialization;
using NetworkDeepProbe.Planning;

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
}
