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

    public static int ParseRequiredInt(JsonElement payload, string propertyName, int minimum, int maximum)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        if (minimum > maximum) throw new ArgumentOutOfRangeException(nameof(minimum));
        if (payload.ValueKind == JsonValueKind.Object
            && payload.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var parsed)
            && parsed >= minimum
            && parsed <= maximum)
        {
            return parsed;
        }
        throw new ArgumentException(
            $"Bridge field '{propertyName}' must be an integer between {minimum} and {maximum}.",
            propertyName);
    }

    public static double ParseRequiredDouble(JsonElement payload, string propertyName, double minimum, double maximum)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        if (!double.IsFinite(minimum) || !double.IsFinite(maximum) || minimum > maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(minimum));
        }
        if (payload.ValueKind == JsonValueKind.Object
            && payload.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetDouble(out var parsed)
            && double.IsFinite(parsed)
            && parsed >= minimum
            && parsed <= maximum)
        {
            return parsed;
        }
        throw new ArgumentException(
            $"Bridge field '{propertyName}' must be a number between {minimum:0.##} and {maximum:0.##}.",
            propertyName);
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
