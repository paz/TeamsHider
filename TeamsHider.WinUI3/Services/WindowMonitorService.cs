using System.Diagnostics;
using TeamsHider.Helpers;
using TeamsHider.Models;

namespace TeamsHider.Services;

/// <summary>
/// Background service that monitors for Teams overlay windows and hides them.
/// Uses 2-second polling interval to balance responsiveness and CPU usage.
/// </summary>
public class WindowMonitorService
{
    private readonly SettingsService _settingsService;
    private CancellationTokenSource? _cts;
    private Task? _monitorTask;

    private const string BottomOverlayText = "Meeting compact view";
    private const string TeamsWindowClass = "TeamsWebView";
    private const string TeamsWindowTitle = "| Microsoft Teams";

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
        List<(string title, WindowHelper.DisplayAffinity affinity, IntPtr hwnd)> toHide = new();

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
                toHide.Add((wdwText, affinity, wnd));
            }
            catch
            {
                // Ignored - window may have closed during enumeration
            }
            return true;
        }, IntPtr.Zero);

        // Phase 2: Parse titles to extract participant names
        // Format: "Name1, Name2, Name3 | Microsoft Teams"
        List<(string title, WindowHelper.DisplayAffinity affinity, IntPtr hwnd)> parsed = toHide
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
                (string title, WindowHelper.DisplayAffinity affinity, IntPtr hwnd) firstItem = group.FirstOrDefault();

                // Bottom overlay: Hide if title is "Meeting compact view"
                if (firstItem.title == BottomOverlayText && settings.HideBottomOverlay)
                {
                    (string title, WindowHelper.DisplayAffinity affinity, IntPtr hwnd) overlayItem = group.FirstOrDefault(x =>
                        x.affinity is WindowHelper.DisplayAffinity.Monitor
                            or WindowHelper.DisplayAffinity.ExcludeFromCapture);

                    if (overlayItem.hwnd != IntPtr.Zero)
                    {
                        WindowHelper.ShowWindow((int)overlayItem.hwnd, WindowHelper.SW_HIDE);
                    }
                    continue;
                }

                // Top bar: Hide if multiple windows with same name and overlay affinity
                if (settings.HideTopBar && group.Count() > 1 &&
                    firstItem.affinity is WindowHelper.DisplayAffinity.Monitor
                        or WindowHelper.DisplayAffinity.ExcludeFromCapture)
                {
                    WindowHelper.ShowWindow((int)firstItem.hwnd, WindowHelper.SW_HIDE);
                }
            }
            catch
            {
                // Ignored - window may have closed
            }
        }
    }
}
