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
/// Styled as Windows 11 flyout with acrylic, rounded corners, no titlebar.
/// Reuses window instance for fast show/hide.
/// </summary>
public sealed partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly Action? _onSettingsChanged;
    private readonly Action? _onExitRequested;
    private bool _isInitializing = true;
    private bool _isClosing = false;

    // Colors for status indicator
    private static readonly SolidColorBrush GreenBrush = new(Colors.LimeGreen);
    private static readonly SolidColorBrush OrangeBrush = new(Colors.Orange);
    private static readonly SolidColorBrush GrayBrush = new(Colors.Gray);

    // Win32 interop
    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    private static extern int GetDpiForWindow(IntPtr hwnd);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;

    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

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

        // Configure window for flyout appearance
        ConfigureAsFlyout();

        // Load current settings
        LoadSettings();
        _isInitializing = false;

        // Handle keyboard shortcuts
        Content.KeyDown += OnKeyDown;

        // Hide on deactivation (flyout behavior)
        Activated += OnActivated;
    }

    private void ConfigureAsFlyout()
    {
        var hwnd = WindowNative.GetWindowHandle(this);

        // Apply acrylic/mica backdrop for transparency
        // DesktopAcrylicBackdrop provides the translucent blur effect
        if (DesktopAcrylicController.IsSupported())
        {
            SystemBackdrop = new DesktopAcrylicBackdrop();
        }
        else if (MicaController.IsSupported())
        {
            SystemBackdrop = new MicaBackdrop();
        }

        // Get DPI for proper sizing
        int dpi = GetDpiForWindow(hwnd);
        double scale = dpi / 96.0;

        // Size window to fit content
        int width = (int)(300 * scale);
        int height = (int)(380 * scale);
        AppWindow.Resize(new SizeInt32(width, height));

        // Configure title bar - extend content into it and collapse
        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            var titleBar = AppWindow.TitleBar;
            titleBar.ExtendsContentIntoTitleBar = true;
            titleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;

            // Make title bar buttons transparent so they don't show
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        }

        // Set rounded corners (Windows 11 style)
        int cornerPreference = DWMWCP_ROUND;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference, sizeof(int));

        // Configure presenter for borderless popup-like window
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.SetBorderAndTitleBar(false, false);
        }

        // Set window as topmost so it appears above other windows
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
    }

    /// <summary>
    /// Shows the flyout at the current cursor position.
    /// </summary>
    public void ShowAtCursor()
    {
        _isClosing = false;
        PositionAtCursor();
        LoadSettings(); // Refresh settings

        // Ensure window is topmost when shown
        var hwnd = WindowNative.GetWindowHandle(this);
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

        Activate();
    }

    private void PositionAtCursor()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        int dpi = GetDpiForWindow(hwnd);
        double scale = dpi / 96.0;

        if (!GetCursorPos(out POINT cursorPos))
        {
            PositionFallback();
            return;
        }

        IntPtr hMonitor = MonitorFromPoint(cursorPos, MONITOR_DEFAULTTONEAREST);
        var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };

        if (!GetMonitorInfo(hMonitor, ref monitorInfo))
        {
            PositionFallback();
            return;
        }

        var workArea = monitorInfo.rcWork;
        var windowSize = AppWindow.Size;
        int padding = (int)(12 * scale);

        // Detect taskbar position
        int taskbarBottom = monitorInfo.rcMonitor.Bottom - workArea.Bottom;
        int taskbarTop = workArea.Top - monitorInfo.rcMonitor.Top;
        int taskbarRight = monitorInfo.rcMonitor.Right - workArea.Right;
        int taskbarLeft = workArea.Left - monitorInfo.rcMonitor.Left;

        int x, y;

        if (taskbarBottom > 0)
        {
            // Taskbar at bottom
            x = Math.Clamp(cursorPos.X - windowSize.Width / 2, workArea.Left + padding, workArea.Right - windowSize.Width - padding);
            y = workArea.Bottom - windowSize.Height - padding;
        }
        else if (taskbarRight > 0)
        {
            x = workArea.Right - windowSize.Width - padding;
            y = Math.Clamp(cursorPos.Y - windowSize.Height / 2, workArea.Top + padding, workArea.Bottom - windowSize.Height - padding);
        }
        else if (taskbarTop > 0)
        {
            x = Math.Clamp(cursorPos.X - windowSize.Width / 2, workArea.Left + padding, workArea.Right - windowSize.Width - padding);
            y = workArea.Top + padding;
        }
        else if (taskbarLeft > 0)
        {
            x = workArea.Left + padding;
            y = Math.Clamp(cursorPos.Y - windowSize.Height / 2, workArea.Top + padding, workArea.Bottom - windowSize.Height - padding);
        }
        else
        {
            x = workArea.Right - windowSize.Width - padding;
            y = workArea.Bottom - windowSize.Height - padding;
        }

        AppWindow.Move(new PointInt32(x, y));
    }

    private void PositionFallback()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var displayArea = DisplayArea.GetFromWindowId(
            Win32Interop.GetWindowIdFromWindow(hwnd),
            DisplayAreaFallback.Primary);

        var workArea = displayArea.WorkArea;
        var windowSize = AppWindow.Size;
        int padding = 12;

        AppWindow.Move(new PointInt32(
            workArea.X + workArea.Width - windowSize.Width - padding,
            workArea.Y + workArea.Height - windowSize.Height - padding));
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated && !_isClosing)
        {
            // Hide instead of close for faster re-show
            AppWindow.Hide();
        }
    }

    private void LoadSettings()
    {
        _isInitializing = true;
        HideTopBarToggle.IsOn = _settingsService.CurrentSettings.HideTopBar;
        HideBottomOverlayToggle.IsOn = _settingsService.CurrentSettings.HideBottomOverlay;
        LaunchAtStartupToggle.IsOn = _settingsService.CurrentSettings.LaunchAtStartup;
        _isInitializing = false;
    }

    public void UpdateStatus(MonitorStatus status)
    {
        StatusText.Text = status.StatusMessage;
        StatusIndicator.Fill = status.OverlaysHidden > 0 ? GreenBrush
            : status.TeamsDetected ? OrangeBrush : GrayBrush;
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
        _isClosing = true;
        _onExitRequested?.Invoke();
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            AppWindow.Hide();
        }
    }
}
