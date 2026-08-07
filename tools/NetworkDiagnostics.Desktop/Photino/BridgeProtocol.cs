using System.Text.Json;
using NetworkDeepProbe.Planning;

namespace NetworkDiagnostics.Desktop;

public static class BridgeProtocol
{
    public static TestProfileId ParseProfile(JsonElement payload)
    {
        var value = ReadString(payload, "profile")?.Trim().ToLowerInvariant();
        return value switch
        {
            "quick" => TestProfileId.Quick,
            "full" or "standard" => TestProfileId.Standard,
            "stress" or "extended" => TestProfileId.Extended,
            _ => TestProfileId.ConnectionCheck
        };
    }

    public static TransferMethod ParseTransferMethod(JsonElement payload)
    {
        var value = ReadString(payload, "method")?.Trim().ToLowerInvariant();
        return value switch
        {
            "single" => TransferMethod.Single,
            "aggregate" => TransferMethod.Aggregate,
            _ => TransferMethod.Compare
        };
    }

    public static string ProfileId(TestProfileId profile) => profile switch
    {
        TestProfileId.ConnectionCheck => "connection-check",
        TestProfileId.Quick => "quick",
        TestProfileId.Standard => "full",
        TestProfileId.Extended => "stress",
        _ => "connection-check"
    };

    public static string MethodId(TransferMethod method) => method switch
    {
        TransferMethod.Single => "single",
        TransferMethod.Aggregate => "aggregate",
        _ => "compare"
    };

    private static string? ReadString(JsonElement payload, string propertyName)
    {
        if (payload.ValueKind != JsonValueKind.Object
            || !payload.TryGetProperty(propertyName, out var value)
            || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
    }
}
