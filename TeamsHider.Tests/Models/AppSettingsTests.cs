using System.Text.Json;
using TeamsHider.Tests;

namespace TeamsHider.Tests.Models;

/// <summary>
/// Tests for AppSettings model serialization and defaults.
/// </summary>
public class AppSettingsTests
{
    [Fact]
    public void DefaultSettings_HaveCorrectValues()
    {
        // Arrange & Act
        AppSettings settings = new();

        // Assert
        Assert.True(settings.HideTopBar);
        Assert.True(settings.HideBottomOverlay);
        Assert.False(settings.LaunchAtStartup);
    }

    [Fact]
    public void Settings_SerializeToJson_Correctly()
    {
        // Arrange
        AppSettings settings = new()
        {
            HideTopBar = false,
            HideBottomOverlay = true,
            LaunchAtStartup = true
        };

        // Act
        string json = JsonSerializer.Serialize(settings);
        AppSettings? deserialized = JsonSerializer.Deserialize<AppSettings>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.False(deserialized.HideTopBar);
        Assert.True(deserialized.HideBottomOverlay);
        Assert.True(deserialized.LaunchAtStartup);
    }

    [Fact]
    public void Settings_DeserializeWithMissingProperties_UseDefaults()
    {
        // Arrange - JSON missing LaunchAtStartup
        string json = """{"HideTopBar":false,"HideBottomOverlay":true}""";

        // Act
        AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(json);

        // Assert
        Assert.NotNull(settings);
        Assert.False(settings.HideTopBar);
        Assert.True(settings.HideBottomOverlay);
        Assert.False(settings.LaunchAtStartup); // Default value when not in JSON
    }

    [Fact]
    public void Settings_DeserializeEmptyJson_UseDefaults()
    {
        // Arrange
        string json = "{}";

        // Act
        AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(json);

        // Assert
        Assert.NotNull(settings);
        // When deserializing empty JSON, bool properties get C# default (false), not property initializer
        Assert.False(settings.HideTopBar);
        Assert.False(settings.HideBottomOverlay);
        Assert.False(settings.LaunchAtStartup);
    }

    [Fact]
    public void Settings_ExtensionData_PreservesUnknownProperties()
    {
        // Arrange - JSON with extra property
        string json = """{"HideTopBar":true,"HideBottomOverlay":true,"FutureProperty":"value"}""";

        // Act
        AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(json);
        string reserialized = JsonSerializer.Serialize(settings);

        // Assert
        Assert.NotNull(settings);
        Assert.Contains("FutureProperty", reserialized);
    }
}

