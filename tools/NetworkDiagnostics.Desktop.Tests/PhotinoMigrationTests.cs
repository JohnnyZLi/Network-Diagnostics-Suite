using System.Text.Json;
using NetworkDeepProbe.Planning;
using Xunit;

namespace NetworkDiagnostics.Desktop.Tests;

public sealed class PhotinoMigrationTests
{
    [Theory]
    [InlineData("connection-check", TestProfileId.ConnectionCheck, "connection-check")]
    [InlineData("quick", TestProfileId.Quick, "quick")]
    [InlineData("full", TestProfileId.Standard, "full")]
    [InlineData("standard", TestProfileId.Standard, "full")]
    [InlineData("stress", TestProfileId.Extended, "stress")]
    [InlineData("extended", TestProfileId.Extended, "stress")]
    public void ProfileContractMapsBrowserIdsToNativeProfiles(
        string contractId,
        TestProfileId expected,
        string normalizedId)
    {
        using var document = JsonDocument.Parse($$"""{"profile":"{{contractId}}"}""");

        var profile = BridgeProtocol.ParseProfile(document.RootElement);

        Assert.Equal(expected, profile);
        Assert.Equal(normalizedId, BridgeProtocol.ProfileId(profile));
    }

    [Theory]
    [InlineData("compare", TransferMethod.Compare, "compare")]
    [InlineData("single", TransferMethod.Single, "single")]
    [InlineData("aggregate", TransferMethod.Aggregate, "aggregate")]
    public void TransferMethodContractMapsBrowserIdsToNativeMethods(
        string contractId,
        TransferMethod expected,
        string normalizedId)
    {
        using var document = JsonDocument.Parse($$"""{"method":"{{contractId}}"}""");

        var method = BridgeProtocol.ParseTransferMethod(document.RootElement);

        Assert.Equal(expected, method);
        Assert.Equal(normalizedId, BridgeProtocol.MethodId(method));
    }

    [Fact]
    public void MissingPayloadValuesUseLowRiskDefaults()
    {
        using var document = JsonDocument.Parse("{}");

        Assert.Equal(TestProfileId.ConnectionCheck, BridgeProtocol.ParseProfile(document.RootElement));
        Assert.Equal(TransferMethod.Compare, BridgeProtocol.ParseTransferMethod(document.RootElement));
    }
}
