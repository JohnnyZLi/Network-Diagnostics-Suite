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

    [Theory]
    [InlineData("system", AppearancePreference.System, "system")]
    [InlineData("light", AppearancePreference.Light, "light")]
    [InlineData("dark", AppearancePreference.Dark, "dark")]
    public void AppearanceContractMapsBrowserIdsToNativePreferences(
        string contractId,
        AppearancePreference expected,
        string normalizedId)
    {
        using var document = JsonDocument.Parse($$"""{"appearance":"{{contractId}}"}""");

        var appearance = BridgeProtocol.ParseAppearance(document.RootElement);

        Assert.Equal(expected, appearance);
        Assert.Equal(normalizedId, BridgeProtocol.AppearanceId(appearance));
    }

    [Fact]
    public void ReportIdContractParsesGuidPayloads()
    {
        var expected = Guid.NewGuid();
        using var document = JsonDocument.Parse($$"""{"id":"{{expected}}"}""");

        var reportId = BridgeProtocol.ParseRequiredGuid(document.RootElement, "id");

        Assert.Equal(expected, reportId);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"id\":\"not-a-guid\"}")]
    [InlineData("{\"id\":17}")]
    public void ReportIdContractRejectsInvalidPayloads(string json)
    {
        using var document = JsonDocument.Parse(json);

        var error = Assert.Throws<ArgumentException>(() =>
            BridgeProtocol.ParseRequiredGuid(document.RootElement, "id"));

        Assert.Contains("valid report ID", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReportAnnotationContractParsesOptionalLabelAndTags()
    {
        using var document = JsonDocument.Parse("""
            {
              "label": "Home baseline",
              "tags": ["wifi", "evening", "wifi"]
            }
            """);

        Assert.Equal("Home baseline", BridgeProtocol.ParseOptionalString(document.RootElement, "label"));
        Assert.Equal(["wifi", "evening", "wifi"], BridgeProtocol.ParseStringArray(document.RootElement, "tags"));
    }

    [Theory]
    [InlineData("{\"tags\":\"wifi\"}")]
    [InlineData("{\"tags\":[\"wifi\",17]}")]
    public void ReportAnnotationContractRejectsInvalidTags(string json)
    {
        using var document = JsonDocument.Parse(json);

        var error = Assert.Throws<ArgumentException>(() =>
            BridgeProtocol.ParseStringArray(document.RootElement, "tags"));

        Assert.Contains("strings", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MissingPayloadValuesUseLowRiskDefaults()
    {
        using var document = JsonDocument.Parse("{}");

        Assert.Equal(TestProfileId.ConnectionCheck, BridgeProtocol.ParseProfile(document.RootElement));
        Assert.Equal(TransferMethod.Compare, BridgeProtocol.ParseTransferMethod(document.RootElement));
        Assert.Equal(AppearancePreference.System, BridgeProtocol.ParseAppearance(document.RootElement));
        Assert.Null(BridgeProtocol.ParseOptionalString(document.RootElement, "label"));
        Assert.Empty(BridgeProtocol.ParseStringArray(document.RootElement, "tags"));
    }

    [Fact]
    public void AppearancePreferencePersistsAcrossStoreInstances()
    {
        var directory = Path.Combine(Path.GetTempPath(), "network-diagnostics-photino-tests", Guid.NewGuid().ToString("N"));
        var settingsPath = Path.Combine(directory, "desktop-settings.json");

        try
        {
            var store = new PhotinoSettingsStore(settingsPath);
            Assert.Equal(AppearancePreference.System, store.Load().Appearance);

            var saved = store.SaveAppearance(AppearancePreference.Dark);
            Assert.Equal(AppearancePreference.Dark, saved.Appearance);

            var reloaded = new PhotinoSettingsStore(settingsPath).Load();
            Assert.Equal(AppearancePreference.Dark, reloaded.Appearance);
            Assert.DoesNotContain(".tmp", Directory.EnumerateFiles(directory).Single());
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
