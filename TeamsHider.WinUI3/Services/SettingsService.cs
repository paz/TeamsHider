using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
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

    // Regex to match unquoted property names (only at start of line or after { or ,)
    private static readonly Regex UnquotedPropertyRegex = new(
        @"(?<=^|[{,]\s*)(\w+)(?=\s*:)",
        RegexOptions.Compiled | RegexOptions.Multiline);

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
        catch (IOException ex)
        {
            // Settings will apply in memory only
            Debug.WriteLine($"Failed to save settings: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"No permission to save settings: {ex.Message}");
        }
    }

    private AppSettings LoadModernConfig(string path)
    {
        try
        {
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"Failed to parse config file: {ex.Message}");
            return new AppSettings();
        }
        catch (IOException ex)
        {
            Debug.WriteLine($"Failed to read config file: {ex.Message}");
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

            // Use regex to safely quote unquoted property names
            // This only matches property names at valid JSON positions (after { or ,)
            string normalizedJson = UnquotedPropertyRegex.Replace(legacyJson, "\"$1\"");

            using JsonDocument doc = JsonDocument.Parse(normalizedJson);
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
        catch (JsonException ex)
        {
            Debug.WriteLine($"Failed to parse legacy config: {ex.Message}");
            return new AppSettings();
        }
        catch (IOException ex)
        {
            Debug.WriteLine($"Failed to read/write config file: {ex.Message}");
            return new AppSettings();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Unexpected error during config migration: {ex.Message}");
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
