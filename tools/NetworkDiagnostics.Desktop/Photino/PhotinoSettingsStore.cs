using System.Text.Json;

namespace NetworkDiagnostics.Desktop;

public enum AppearancePreference
{
    System,
    Light,
    Dark
}

public sealed record PhotinoAppSettings(AppearancePreference Appearance)
{
    public static PhotinoAppSettings Default { get; } = new(AppearancePreference.System);
}

public sealed class PhotinoSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly object gate = new();
    private readonly string settingsPath;

    public PhotinoSettingsStore(string? settingsPath = null)
    {
        this.settingsPath = settingsPath ?? GetDefaultSettingsPath();
    }

    public PhotinoAppSettings Load()
    {
        lock (gate)
        {
            return LoadCore();
        }
    }

    public PhotinoAppSettings SaveAppearance(AppearancePreference appearance)
    {
        lock (gate)
        {
            var settings = LoadCore() with { Appearance = appearance };
            var directory = Path.GetDirectoryName(settingsPath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

            var document = new SettingsDocument(BridgeProtocol.AppearanceId(settings.Appearance));
            var json = JsonSerializer.Serialize(document, JsonOptions);
            var temporaryPath = settingsPath + ".tmp";
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, settingsPath, overwrite: true);
            return settings;
        }
    }

    private PhotinoAppSettings LoadCore()
    {
        if (!File.Exists(settingsPath)) return PhotinoAppSettings.Default;

        try
        {
            var document = JsonSerializer.Deserialize<SettingsDocument>(File.ReadAllText(settingsPath), JsonOptions);
            return new PhotinoAppSettings(BridgeProtocol.ParseAppearance(document?.Appearance));
        }
        catch (JsonException)
        {
            return PhotinoAppSettings.Default;
        }
        catch (IOException)
        {
            return PhotinoAppSettings.Default;
        }
        catch (UnauthorizedAccessException)
        {
            return PhotinoAppSettings.Default;
        }
    }

    private static string GetDefaultSettingsPath()
    {
        var baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            baseDirectory = AppContext.BaseDirectory;
        }

        return Path.Combine(baseDirectory, "NetworkDiagnostics", "desktop-settings.json");
    }

    private sealed record SettingsDocument(string Appearance);
}
