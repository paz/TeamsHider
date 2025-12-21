using System.Text.Json.Serialization;

namespace TeamsHider.Tests;

/// <summary>
/// Shared AppSettings model for testing (mirrors the real model).
/// </summary>
public class AppSettings
{
    public bool HideTopBar { get; set; } = true;
    public bool HideBottomOverlay { get; set; } = true;
    public bool LaunchAtStartup { get; set; } = false;

    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}
