using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using TeamsHider.Models;
using TeamsHider.Services;
using Windows.Graphics;
using WinRT.Interop;

namespace TeamsHider;

/// <summary>
/// Settings window for TeamsHider.
/// Flyout-style window positioned near the taskbar with Mica backdrop.
/// </summary>
public sealed partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly Action? _onSettingsChanged;
    private bool _isInitializing = true;

    // Colors for status indicator
    private static readonly SolidColorBrush GreenBrush = new(Colors.LimeGreen);
    private static readonly SolidColorBrush OrangeBrush = new(Colors.Orange);
    private static readonly SolidColorBrush GrayBrush = new(Colors.Gray);

    public SettingsWindow(SettingsService settingsService, Action? onSettingsChanged = null)
    {
        _settingsService = settingsService;
        _onSettingsChanged = onSettingsChanged;

        InitializeComponent();

        // Apply Mica backdrop for Windows 11 look
        TrySetMicaBackdrop();

        // Configure window appearance
        ConfigureWindow();

        // Position near taskbar
        PositionNearTaskbar();

        // Load current settings
        LoadSettings();
        _isInitializing = false;

        // Handle keyboard shortcuts
        Content.KeyDown += OnKeyDown;

        // Close on deactivation for flyout-like behavior (optional, can be too aggressive)
        // Activated += OnActivated;
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
        AppWindow.Resize(new SizeInt32(340, 380));
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

    private void PositionNearTaskbar()
    {
        // Get screen dimensions
        var hwnd = WindowNative.GetWindowHandle(this);
        var displayArea = DisplayArea.GetFromWindowId(
            Win32Interop.GetWindowIdFromWindow(hwnd),
            DisplayAreaFallback.Primary);

        var workArea = displayArea.WorkArea;
        var windowSize = AppWindow.Size;

        // Position in bottom-right corner with padding
        int padding = 12;
        int x = workArea.X + workArea.Width - windowSize.Width - padding;
        int y = workArea.Y + workArea.Height - windowSize.Height - padding;

        AppWindow.Move(new PointInt32(x, y));
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

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Escape to close window
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            Close();
        }
    }
}
