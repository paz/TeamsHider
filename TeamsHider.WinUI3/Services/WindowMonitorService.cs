using System.Diagnostics;
using TeamsHider.Helpers;
using TeamsHider.Models;

namespace TeamsHider.Services;

/// <summary>
/// Background service that monitors for Teams overlay windows and hides them.
/// Uses 2-second polling interval to balance responsiveness and CPU usage.
/// Tracks hidden windows to restore them when settings change or app exits.
/// </summary>
public class WindowMonitorService : IDisposable
{
    private bool _disposed;
    private readonly SettingsService _settingsService;
    private CancellationTokenSource? _cts;
    private Task? _monitorTask;
    private MonitorStatus _lastStatus = MonitorStatus.Initial;

    // Track hidden windows to restore them when needed
    private readonly HashSet<IntPtr> _hiddenTopBarWindows = new();
    private readonly HashSet<IntPtr> _hiddenBottomOverlayWindows = new();
    private readonly object _hiddenWindowsLock = new();

    private const string BottomOverlayText = "Meeting compact view";
    private const string TeamsWindowClass = "TeamsWebView";
    private const string TeamsWindowTitle = "| Microsoft Teams";

    /// <summary>
    /// Event raised when monitoring status changes.
    /// </summary>
    public event Action<MonitorStatus>? StatusChanged;

    /// <summary>
    /// Gets the current monitoring status.
    /// </summary>
    public MonitorStatus CurrentStatus => _lastStatus;

    public WindowMonitorService(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    /// <summary>
    /// Starts the background monitoring loop.
    /// </summary>
    public void Start()
    {
        _cts = new CancellationTokenSource();
        _monitorTask = Task.Run(() => MonitorLoop(_cts.Token));
    }

    /// <summary>
    /// Stops the background monitoring loop.
    /// </summary>
    public void Stop()
    {
        _cts?.Cancel();
        try
        {
            _monitorTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // Expected when cancellation occurs
        }
    }

    /// <summary>
    /// Disposes of resources used by the service.
    /// Restores all hidden windows before shutting down.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();
        RestoreAllHiddenWindows();
        _cts?.Dispose();
        _cts = null;
    }

    /// <summary>
    /// Restores all windows that were hidden by this service.
    /// Called on app exit or when settings are disabled.
    /// </summary>
    public void RestoreAllHiddenWindows()
    {
        lock (_hiddenWindowsLock)
        {
            foreach (IntPtr hwnd in _hiddenTopBarWindows)
            {
                try { WindowHelper.ShowWindow((int)hwnd, WindowHelper.SW_SHOW); }
                catch { /* Window may no longer exist */ }
            }
            foreach (IntPtr hwnd in _hiddenBottomOverlayWindows)
            {
                try { WindowHelper.ShowWindow((int)hwnd, WindowHelper.SW_SHOW); }
                catch { /* Window may no longer exist */ }
            }
            _hiddenTopBarWindows.Clear();
            _hiddenBottomOverlayWindows.Clear();
        }
    }

    /// <summary>
    /// Restores hidden top bar windows (when HideTopBar is toggled off).
    /// </summary>
    public void RestoreTopBarWindows()
    {
        lock (_hiddenWindowsLock)
        {
            foreach (IntPtr hwnd in _hiddenTopBarWindows)
            {
                try { WindowHelper.ShowWindow((int)hwnd, WindowHelper.SW_SHOW); }
                catch { /* Window may no longer exist */ }
            }
            _hiddenTopBarWindows.Clear();
        }
    }

    /// <summary>
    /// Restores hidden bottom overlay windows (when HideBottomOverlay is toggled off).
    /// </summary>
    public void RestoreBottomOverlayWindows()
    {
        lock (_hiddenWindowsLock)
        {
            foreach (IntPtr hwnd in _hiddenBottomOverlayWindows)
            {
                try { WindowHelper.ShowWindow((int)hwnd, WindowHelper.SW_SHOW); }
                catch { /* Window may no longer exist */ }
            }
            _hiddenBottomOverlayWindows.Clear();
        }
    }

    private async Task MonitorLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                ProcessTeamsWindows();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Monitor error: {ex.Message}");
            }

            try
            {
                await Task.Delay(2000, ct);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private void ProcessTeamsWindows()
    {
        AppSettings settings = _settingsService.CurrentSettings;
        List<(string title, WindowHelper.DisplayAffinity affinity, IntPtr hwnd)> teamsWindows = new();
        int overlaysHidden = 0;

        // If settings disabled, restore windows and exit early
        if (!settings.HideTopBar)
        {
            RestoreTopBarWindows();
        }
        if (!settings.HideBottomOverlay)
        {
            RestoreBottomOverlayWindows();
        }

        // Phase 1: Enumerate all Teams windows with overlay affinity
        WindowHelper.EnumWindows(delegate(IntPtr wnd, IntPtr param)
        {
            try
            {
                // Filter: Must have class "TeamsWebView"
                if (!WindowHelper.GetClassName(wnd).Contains(TeamsWindowClass))
                    return true;

                // Filter: Must have "| Microsoft Teams" in title
                string wdwText = WindowHelper.GetWindowText(wnd);
                if (!wdwText.Contains(TeamsWindowTitle))
                    return true;

                // Capture display affinity (identifies overlay windows)
                WindowHelper.GetWindowDisplayAffinity(wnd, out WindowHelper.DisplayAffinity affinity);
                teamsWindows.Add((wdwText, affinity, wnd));
            }
            catch
            {
                // Ignored - window may have closed during enumeration
            }
            return true;
        }, IntPtr.Zero);

        // Phase 2: Parse titles to extract participant names
        // Format: "Name1, Name2, Name3 | Microsoft Teams"
        List<(string title, WindowHelper.DisplayAffinity affinity, IntPtr hwnd)> parsed = teamsWindows
            .SelectMany(x =>
            {
                string? beforePipe = x.title.Split('|').FirstOrDefault();
                if (string.IsNullOrEmpty(beforePipe))
                    return new[] { (x.title, x.affinity, x.hwnd) };

                return beforePipe
                    .Split(", ", StringSplitOptions.RemoveEmptyEntries)
                    .Select(name => (name.Trim(), x.affinity, x.hwnd));
            })
            .ToList();

        // Phase 3: Hide overlays based on settings
        foreach (IGrouping<string, (string title, WindowHelper.DisplayAffinity affinity, IntPtr hwnd)> group in parsed.GroupBy(x => x.title))
        {
            try
            {
                // Materialize group once to avoid multiple enumerations
                List<(string title, WindowHelper.DisplayAffinity affinity, IntPtr hwnd)> items = group.ToList();
                if (items.Count == 0) continue;

                (string title, WindowHelper.DisplayAffinity affinity, IntPtr hwnd) firstItem = items[0];

                // Bottom overlay: Hide if title is "Meeting compact view"
                if (firstItem.title == BottomOverlayText && settings.HideBottomOverlay)
                {
                    (string title, WindowHelper.DisplayAffinity affinity, IntPtr hwnd) overlayItem = items.FirstOrDefault(x =>
                        x.affinity is WindowHelper.DisplayAffinity.Monitor
                            or WindowHelper.DisplayAffinity.ExcludeFromCapture);

                    if (overlayItem.hwnd != IntPtr.Zero)
                    {
                        WindowHelper.ShowWindow((int)overlayItem.hwnd, WindowHelper.SW_HIDE);
                        lock (_hiddenWindowsLock)
                        {
                            _hiddenBottomOverlayWindows.Add(overlayItem.hwnd);
                        }
                        overlaysHidden++;
                    }
                    continue;
                }

                // Top bar: Hide if multiple windows with same name and overlay affinity
                if (settings.HideTopBar && items.Count > 1 &&
                    firstItem.affinity is WindowHelper.DisplayAffinity.Monitor
                        or WindowHelper.DisplayAffinity.ExcludeFromCapture)
                {
                    WindowHelper.ShowWindow((int)firstItem.hwnd, WindowHelper.SW_HIDE);
                    lock (_hiddenWindowsLock)
                    {
                        _hiddenTopBarWindows.Add(firstItem.hwnd);
                    }
                    overlaysHidden++;
                }
            }
            catch
            {
                // Ignored - window may have closed
            }
        }

        // Count currently tracked hidden windows for accurate status
        lock (_hiddenWindowsLock)
        {
            overlaysHidden = _hiddenTopBarWindows.Count + _hiddenBottomOverlayWindows.Count;
        }

        // Phase 4: Update and report status
        MonitorStatus newStatus = new()
        {
            TeamsDetected = teamsWindows.Count > 0,
            TeamsWindowCount = teamsWindows.Count,
            OverlaysHidden = overlaysHidden,
            LastScanTime = DateTime.Now
        };

        // Only notify if status changed meaningfully
        if (newStatus.TeamsDetected != _lastStatus.TeamsDetected ||
            newStatus.OverlaysHidden != _lastStatus.OverlaysHidden ||
            newStatus.TeamsWindowCount != _lastStatus.TeamsWindowCount)
        {
            _lastStatus = newStatus;
            StatusChanged?.Invoke(newStatus);
        }
        else
        {
            // Update timestamp even if no other changes
            _lastStatus = newStatus;
        }
    }
}
