using System.Text.Json;
using TeamsHider.Models;

namespace TeamsHider.Services;

/// <summary>
/// Manages application settings persistence.
/// Supports migration from legacy settings.conf format.
/// </summary>
public class SettingsService
{
    private const string LegacyConfigFile = "settings.conf";
    private const string ModernConfigFile = "TeamsHider.config.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Current in-memory settings. Updated when settings change.
    /// </summary>
    public AppSettings CurrentSettings { get; private set; } = new();

    /// <summary>
    /// Loads settings from disk, migrating legacy format if needed.
    /// </summary>
    public AppSettings LoadSettings()
    {
        string appDir = AppContext.BaseDirectory;
        string modernPath = Path.Combine(appDir, ModernConfigFile);
        string legacyPath = Path.Combine(appDir, LegacyConfigFile);

        // Priority: Modern config > Legacy config > Defaults
        if (File.Exists(modernPath))
        {
            CurrentSettings = LoadModernConfig(modernPath);
        }
        else if (File.Exists(legacyPath))
        {
            CurrentSettings = MigrateLegacyConfig(legacyPath, modernPath);
        }
        else
        {
            CurrentSettings = CreateDefaultSettings(modernPath);
        }

        return CurrentSettings;
    }

    /// <summary>
    /// Saves current settings to disk.
    /// </summary>
    public void SaveSettings()
    {
        string appDir = AppContext.BaseDirectory;
        string modernPath = Path.Combine(appDir, ModernConfigFile);

        try
        {
            string json = JsonSerializer.Serialize(CurrentSettings, JsonOptions);
            File.WriteAllText(modernPath, json);
        }
        catch
        {
            // Silently fail - settings will apply in memory only
        }
    }

    private AppSettings LoadModernConfig(string path)
    {
        try
        {
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    private AppSettings MigrateLegacyConfig(string legacyPath, string modernPath)
    {
        try
        {
            // Parse legacy JSON format (uses JsonConfig library format)
            // Legacy format: { HideTopBar: true, HideBottomOverlay: true }
            string legacyJson = File.ReadAllText(legacyPath);

            // Legacy format may have unquoted property names - normalize it
            legacyJson = legacyJson.Replace("HideTopBar:", "\"HideTopBar\":");
            legacyJson = legacyJson.Replace("HideBottomOverlay:", "\"HideBottomOverlay\":");

            using JsonDocument doc = JsonDocument.Parse(legacyJson);
            JsonElement root = doc.RootElement;

            AppSettings settings = new()
            {
                HideTopBar = root.TryGetProperty("HideTopBar", out JsonElement topBar) && topBar.GetBoolean(),
                HideBottomOverlay = root.TryGetProperty("HideBottomOverlay", out JsonElement bottomOverlay) && bottomOverlay.GetBoolean(),
                LaunchAtStartup = false // New setting, default off
            };

            // Save to modern format
            string json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(modernPath, json);

            // Rename legacy file as backup
            string backupPath = legacyPath + ".backup";
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }
            File.Move(legacyPath, backupPath);

            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    private AppSettings CreateDefaultSettings(string modernPath)
    {
        AppSettings settings = new();

        try
        {
            string json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(modernPath, json);
        }
        catch
        {
            // Silently fail - use defaults
        }

        return settings;
    }
}
