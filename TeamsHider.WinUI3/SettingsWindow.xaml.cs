using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Runtime.InteropServices;
using TeamsHider.Models;
using TeamsHider.Services;
using Windows.Graphics;
using WinRT.Interop;

namespace TeamsHider;

/// <summary>
/// Settings flyout for TeamsHider.
/// Positioned at cursor location with Mica backdrop.
/// Auto-closes when focus is lost for flyout-like behavior.
/// </summary>
public sealed partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly Action? _onSettingsChanged;
    private readonly Action? _onExitRequested;
    private bool _isInitializing = true;

    // Colors for status indicator
    private static readonly SolidColorBrush GreenBrush = new(Colors.LimeGreen);
    private static readonly SolidColorBrush OrangeBrush = new(Colors.Orange);
    private static readonly SolidColorBrush GrayBrush = new(Colors.Gray);

    // Win32 interop for cursor position
    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    public SettingsWindow(SettingsService settingsService, Action? onSettingsChanged = null, Action? onExitRequested = null)
    {
        _settingsService = settingsService;
        _onSettingsChanged = onSettingsChanged;
        _onExitRequested = onExitRequested;

        InitializeComponent();

        // Apply Mica backdrop for Windows 11 look
        TrySetMicaBackdrop();

        // Configure window appearance
        ConfigureWindow();

        // Position at cursor location (handles multi-monitor)
        PositionAtCursor();

        // Load current settings
        LoadSettings();
        _isInitializing = false;

        // Handle keyboard shortcuts
        Content.KeyDown += OnKeyDown;

        // Close when window loses focus (flyout behavior)
        Activated += OnActivated;
    }

    private void TrySetMicaBackdrop()
    {
        if (MicaController.IsSupported())
        {
            SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };
        }
        else if (DesktopAcrylicController.IsSupported())
        {
            SystemBackdrop = new DesktopAcrylicBackdrop();
        }
    }

    private void ConfigureWindow()
    {
        // Compact size for flyout appearance
        AppWindow.Resize(new SizeInt32(340, 420));
        Title = "TeamsHider";

        // Configure title bar
        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            var titleBar = AppWindow.TitleBar;
            titleBar.ExtendsContentIntoTitleBar = false;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        }

        // Set as tool window (no taskbar button)
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
        }
    }

    private void PositionAtCursor()
    {
        // Get cursor position
        if (!GetCursorPos(out POINT cursorPos))
        {
            // Fallback to primary display bottom-right
            PositionFallback();
            return;
        }

        // Get the monitor at cursor position
        IntPtr hMonitor = MonitorFromPoint(cursorPos, MONITOR_DEFAULTTONEAREST);

        var monitorInfo = new MONITORINFO();
        monitorInfo.cbSize = Marshal.SizeOf<MONITORINFO>();

        if (!GetMonitorInfo(hMonitor, ref monitorInfo))
        {
            PositionFallback();
            return;
        }

        // Work area excludes taskbar
        var workArea = monitorInfo.rcWork;
        var windowSize = AppWindow.Size;

        // Determine taskbar position by comparing monitor rect to work area
        int taskbarHeight = monitorInfo.rcMonitor.Bottom - workArea.Bottom;
        int taskbarTop = workArea.Top - monitorInfo.rcMonitor.Top;
        int taskbarLeft = workArea.Left - monitorInfo.rcMonitor.Left;
        int taskbarRight = monitorInfo.rcMonitor.Right - workArea.Right;

        int x, y;
        int padding = 12;

        // Position based on likely taskbar location (usually bottom or right)
        if (taskbarHeight > 0)
        {
            // Taskbar at bottom - position above it, near cursor X
            x = Math.Clamp(cursorPos.X - windowSize.Width / 2, workArea.Left + padding, workArea.Right - windowSize.Width - padding);
            y = workArea.Bottom - windowSize.Height - padding;
        }
        else if (taskbarRight > 0)
        {
            // Taskbar at right - position left of it
            x = workArea.Right - windowSize.Width - padding;
            y = Math.Clamp(cursorPos.Y - windowSize.Height / 2, workArea.Top + padding, workArea.Bottom - windowSize.Height - padding);
        }
        else if (taskbarTop > 0)
        {
            // Taskbar at top - position below it
            x = Math.Clamp(cursorPos.X - windowSize.Width / 2, workArea.Left + padding, workArea.Right - windowSize.Width - padding);
            y = workArea.Top + padding;
        }
        else if (taskbarLeft > 0)
        {
            // Taskbar at left - position right of it
            x = workArea.Left + padding;
            y = Math.Clamp(cursorPos.Y - windowSize.Height / 2, workArea.Top + padding, workArea.Bottom - windowSize.Height - padding);
        }
        else
        {
            // No taskbar detected, position at bottom-right
            x = workArea.Right - windowSize.Width - padding;
            y = workArea.Bottom - windowSize.Height - padding;
        }

        AppWindow.Move(new PointInt32(x, y));
    }

    private void PositionFallback()
    {
        // Fallback: use primary display bottom-right
        var hwnd = WindowNative.GetWindowHandle(this);
        var displayArea = DisplayArea.GetFromWindowId(
            Win32Interop.GetWindowIdFromWindow(hwnd),
            DisplayAreaFallback.Primary);

        var workArea = displayArea.WorkArea;
        var windowSize = AppWindow.Size;
        int padding = 12;

        int x = workArea.X + workArea.Width - windowSize.Width - padding;
        int y = workArea.Y + workArea.Height - windowSize.Height - padding;

        AppWindow.Move(new PointInt32(x, y));
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        // Close when window loses focus (flyout behavior)
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            Close();
        }
    }

    private void LoadSettings()
    {
        HideTopBarToggle.IsOn = _settingsService.CurrentSettings.HideTopBar;
        HideBottomOverlayToggle.IsOn = _settingsService.CurrentSettings.HideBottomOverlay;
        LaunchAtStartupToggle.IsOn = _settingsService.CurrentSettings.LaunchAtStartup;
    }

    /// <summary>
    /// Updates the status display with current monitoring state.
    /// </summary>
    public void UpdateStatus(MonitorStatus status)
    {
        StatusText.Text = status.StatusMessage;

        // Set indicator color based on state
        if (status.OverlaysHidden > 0)
        {
            StatusIndicator.Fill = GreenBrush; // Actively hiding
        }
        else if (status.TeamsDetected)
        {
            StatusIndicator.Fill = OrangeBrush; // Monitoring (Teams detected)
        }
        else
        {
            StatusIndicator.Fill = GrayBrush; // Idle (Teams not detected)
        }
    }

    private void OnHideTopBarToggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;

        _settingsService.CurrentSettings.HideTopBar = HideTopBarToggle.IsOn;
        _settingsService.SaveSettings();
        _onSettingsChanged?.Invoke();
    }

    private void OnHideBottomOverlayToggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;

        _settingsService.CurrentSettings.HideBottomOverlay = HideBottomOverlayToggle.IsOn;
        _settingsService.SaveSettings();
        _onSettingsChanged?.Invoke();
    }

    private void OnLaunchAtStartupToggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;

        bool enabled = LaunchAtStartupToggle.IsOn;
        _settingsService.CurrentSettings.LaunchAtStartup = enabled;
        _settingsService.SaveSettings();
        StartupManager.SetStartup(enabled);
        _onSettingsChanged?.Invoke();
    }

    private void OnExitClicked(object sender, RoutedEventArgs e)
    {
        _onExitRequested?.Invoke();
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Escape to close window
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            Close();
        }
    }
}
