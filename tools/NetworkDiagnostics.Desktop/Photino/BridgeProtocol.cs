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

    public static AppearancePreference ParseAppearance(JsonElement payload)
        => ParseAppearance(ReadString(payload, "appearance"));

    public static AppearancePreference ParseAppearance(string? value)
        => value?.Trim().ToLowerInvariant() switch
        {
            "light" => AppearancePreference.Light,
            "dark" => AppearancePreference.Dark,
            _ => AppearancePreference.System
        };

    public static Guid ParseRequiredGuid(JsonElement payload, string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        var value = ReadString(payload, propertyName);
        if (Guid.TryParse(value, out var parsed)) return parsed;
        throw new ArgumentException($"Bridge field '{propertyName}' must contain a valid report ID.", propertyName);
    }

    public static bool ParseRequiredBool(JsonElement payload, string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        if (payload.ValueKind == JsonValueKind.Object
            && payload.TryGetProperty(propertyName, out var value)
            && value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return value.GetBoolean();
        }
        throw new ArgumentException($"Bridge field '{propertyName}' must contain a boolean value.", propertyName);
    }

    public static string? ParseOptionalString(JsonElement payload, string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        return ReadString(payload, propertyName);
    }

    public static IReadOnlyList<string> ParseStringArray(JsonElement payload, string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        if (payload.ValueKind != JsonValueKind.Object
            || !payload.TryGetProperty(propertyName, out var value)
            || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return [];
        }
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException($"Bridge field '{propertyName}' must be an array of strings.", propertyName);
        }

        var values = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException($"Bridge field '{propertyName}' must contain only strings.", propertyName);
            }
            values.Add(item.GetString() ?? string.Empty);
        }
        return values;
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

    public static string AppearanceId(AppearancePreference appearance) => appearance switch
    {
        AppearancePreference.Light => "light",
        AppearancePreference.Dark => "dark",
        _ => "system"
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
