using System.Text.Json.Serialization;

namespace TeamsHider.Models;

/// <summary>
/// Application settings model.
/// Serialized to JSON for persistence.
/// </summary>
public class AppSettings
{
    /// <summary>
    /// Hide the top bar overlay during screen sharing.
    /// </summary>
    public bool HideTopBar { get; set; } = true;

    /// <summary>
    /// Hide the bottom overlay when call is minimized.
    /// </summary>
    public bool HideBottomOverlay { get; set; } = true;

    /// <summary>
    /// Launch TeamsHider at Windows startup.
    /// </summary>
    public bool LaunchAtStartup { get; set; } = false;

    /// <summary>
    /// Indicates first launch has been completed.
    /// Used to show welcome balloon on initial run.
    /// </summary>
    public bool FirstLaunchCompleted { get; set; } = false;

    /// <summary>
    /// Extensibility for future settings without breaking compatibility.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}
