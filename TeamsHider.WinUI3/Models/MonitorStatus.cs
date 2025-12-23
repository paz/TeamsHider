namespace TeamsHider.Models;

/// <summary>
/// Represents the current monitoring status.
/// </summary>
public record MonitorStatus
{
    /// <summary>
    /// Whether Teams is currently detected as running.
    /// </summary>
    public bool TeamsDetected { get; init; }

    /// <summary>
    /// Number of Teams windows currently detected.
    /// </summary>
    public int TeamsWindowCount { get; init; }

    /// <summary>
    /// Number of overlays hidden in the last scan.
    /// </summary>
    public int OverlaysHidden { get; init; }

    /// <summary>
    /// Time of the last scan.
    /// </summary>
    public DateTime LastScanTime { get; init; }

    /// <summary>
    /// Gets a human-readable status message.
    /// </summary>
    public string StatusMessage
    {
        get
        {
            if (!TeamsDetected)
                return "Teams not detected";

            if (OverlaysHidden > 0)
                return $"Hiding {OverlaysHidden} overlay{(OverlaysHidden > 1 ? "s" : "")}";

            return $"Monitoring ({TeamsWindowCount} window{(TeamsWindowCount > 1 ? "s" : "")})";
        }
    }

    public static MonitorStatus Initial => new()
    {
        TeamsDetected = false,
        TeamsWindowCount = 0,
        OverlaysHidden = 0,
        LastScanTime = DateTime.MinValue
    };
}
