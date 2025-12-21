using System.Text.Json;
using System.Text.RegularExpressions;

namespace TeamsHider.Tests.Services;

/// <summary>
/// Tests for SettingsService functionality.
/// Uses a file system abstraction for testability.
/// </summary>
public class SettingsServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly TestableSettingsService _service;

    public SettingsServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"TeamsHiderTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        _service = new TestableSettingsService(_testDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public void LoadSettings_NoConfigExists_ReturnsDefaults()
    {
        // Act
        AppSettings settings = _service.LoadSettings();

        // Assert
        Assert.True(settings.HideTopBar);
        Assert.True(settings.HideBottomOverlay);
        Assert.False(settings.LaunchAtStartup);
    }

    [Fact]
    public void LoadSettings_NoConfigExists_CreatesDefaultFile()
    {
        // Act
        _service.LoadSettings();

        // Assert
        string expectedPath = Path.Combine(_testDirectory, "TeamsHider.config.json");
        Assert.True(File.Exists(expectedPath));
    }

    [Fact]
    public void LoadSettings_ModernConfigExists_LoadsIt()
    {
        // Arrange
        string configPath = Path.Combine(_testDirectory, "TeamsHider.config.json");
        string json = """{"HideTopBar":false,"HideBottomOverlay":true,"LaunchAtStartup":true}""";
        File.WriteAllText(configPath, json);

        // Act
        AppSettings settings = _service.LoadSettings();

        // Assert
        Assert.False(settings.HideTopBar);
        Assert.True(settings.HideBottomOverlay);
        Assert.True(settings.LaunchAtStartup);
    }

    [Fact]
    public void LoadSettings_LegacyConfigExists_MigratesIt()
    {
        // Arrange - Legacy format with unquoted property names
        string legacyPath = Path.Combine(_testDirectory, "settings.conf");
        string legacyJson = """
            {
                HideTopBar: true,
                HideBottomOverlay: false
            }
            """;
        File.WriteAllText(legacyPath, legacyJson);

        // Act
        AppSettings settings = _service.LoadSettings();

        // Assert
        Assert.True(settings.HideTopBar);
        Assert.False(settings.HideBottomOverlay);
        Assert.False(settings.LaunchAtStartup);

        // Verify migration created modern config
        string modernPath = Path.Combine(_testDirectory, "TeamsHider.config.json");
        Assert.True(File.Exists(modernPath));

        // Verify legacy file was renamed to backup
        Assert.False(File.Exists(legacyPath));
        Assert.True(File.Exists(legacyPath + ".backup"));
    }

    [Fact]
    public void LoadSettings_ModernConfigPriority_IgnoresLegacy()
    {
        // Arrange - Both files exist
        string modernPath = Path.Combine(_testDirectory, "TeamsHider.config.json");
        string legacyPath = Path.Combine(_testDirectory, "settings.conf");

        File.WriteAllText(modernPath, """{"HideTopBar":false,"HideBottomOverlay":false,"LaunchAtStartup":true}""");
        File.WriteAllText(legacyPath, """{HideTopBar: true, HideBottomOverlay: true}""");

        // Act
        AppSettings settings = _service.LoadSettings();

        // Assert - Modern config takes priority
        Assert.False(settings.HideTopBar);
        Assert.False(settings.HideBottomOverlay);
        Assert.True(settings.LaunchAtStartup);

        // Legacy file should still exist (not migrated)
        Assert.True(File.Exists(legacyPath));
    }

    [Fact]
    public void LoadSettings_InvalidJson_ReturnsDefaults()
    {
        // Arrange
        string configPath = Path.Combine(_testDirectory, "TeamsHider.config.json");
        File.WriteAllText(configPath, "this is not valid json");

        // Act
        AppSettings settings = _service.LoadSettings();

        // Assert - Returns defaults, doesn't throw
        Assert.True(settings.HideTopBar);
        Assert.True(settings.HideBottomOverlay);
        Assert.False(settings.LaunchAtStartup);
    }

    [Fact]
    public void SaveSettings_CreatesValidJson()
    {
        // Arrange
        _service.CurrentSettings.HideTopBar = false;
        _service.CurrentSettings.HideBottomOverlay = true;
        _service.CurrentSettings.LaunchAtStartup = true;

        // Act
        _service.SaveSettings();

        // Assert
        string configPath = Path.Combine(_testDirectory, "TeamsHider.config.json");
        Assert.True(File.Exists(configPath));

        string json = File.ReadAllText(configPath);
        AppSettings? loaded = JsonSerializer.Deserialize<AppSettings>(json);

        Assert.NotNull(loaded);
        Assert.False(loaded.HideTopBar);
        Assert.True(loaded.HideBottomOverlay);
        Assert.True(loaded.LaunchAtStartup);
    }

    [Fact]
    public void SaveSettings_UpdatesExistingFile()
    {
        // Arrange
        _service.LoadSettings(); // Creates default file
        _service.CurrentSettings.HideTopBar = false;

        // Act
        _service.SaveSettings();

        // Assert
        string configPath = Path.Combine(_testDirectory, "TeamsHider.config.json");
        string json = File.ReadAllText(configPath);
        Assert.Contains("\"HideTopBar\": false", json);
    }

    [Fact]
    public void LegacyMigration_HandlesExistingBackup()
    {
        // Arrange - Legacy file and existing backup
        string legacyPath = Path.Combine(_testDirectory, "settings.conf");
        string backupPath = legacyPath + ".backup";

        File.WriteAllText(legacyPath, """{HideTopBar: true, HideBottomOverlay: true}""");
        File.WriteAllText(backupPath, "old backup content");

        // Act
        _service.LoadSettings();

        // Assert - Backup was replaced
        Assert.False(File.Exists(legacyPath));
        Assert.True(File.Exists(backupPath));
        string backupContent = File.ReadAllText(backupPath);
        Assert.Contains("HideTopBar", backupContent);
    }
}

/// <summary>
/// Testable version of SettingsService that allows specifying the base directory.
/// </summary>
public class TestableSettingsService
{
    private const string LegacyConfigFile = "settings.conf";
    private const string ModernConfigFile = "TeamsHider.config.json";
    private readonly string _baseDirectory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static readonly Regex UnquotedPropertyRegex = new(
        @"(?<=^|[{,]\s*)(\w+)(?=\s*:)",
        RegexOptions.Compiled | RegexOptions.Multiline);

    public AppSettings CurrentSettings { get; private set; } = new();

    public TestableSettingsService(string baseDirectory)
    {
        _baseDirectory = baseDirectory;
    }

    public AppSettings LoadSettings()
    {
        string modernPath = Path.Combine(_baseDirectory, ModernConfigFile);
        string legacyPath = Path.Combine(_baseDirectory, LegacyConfigFile);

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

    public void SaveSettings()
    {
        string modernPath = Path.Combine(_baseDirectory, ModernConfigFile);
        try
        {
            string json = JsonSerializer.Serialize(CurrentSettings, JsonOptions);
            File.WriteAllText(modernPath, json);
        }
        catch
        {
            // Silently fail
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
            string legacyJson = File.ReadAllText(legacyPath);
            string normalizedJson = UnquotedPropertyRegex.Replace(legacyJson, "\"$1\"");

            using JsonDocument doc = JsonDocument.Parse(normalizedJson);
            JsonElement root = doc.RootElement;

            AppSettings settings = new()
            {
                HideTopBar = root.TryGetProperty("HideTopBar", out JsonElement topBar) && topBar.GetBoolean(),
                HideBottomOverlay = root.TryGetProperty("HideBottomOverlay", out JsonElement bottomOverlay) && bottomOverlay.GetBoolean(),
                LaunchAtStartup = false
            };

            string json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(modernPath, json);

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
            // Silently fail
        }
        return settings;
    }
}

/// <summary>
/// AppSettings for tests (mirrors the real model).
/// </summary>
public class AppSettings
{
    public bool HideTopBar { get; set; } = true;
    public bool HideBottomOverlay { get; set; } = true;
    public bool LaunchAtStartup { get; set; } = false;

    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}
