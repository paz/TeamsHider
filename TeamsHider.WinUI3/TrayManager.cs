using H.NotifyIcon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;
using TeamsHider.Services;

namespace TeamsHider;

/// <summary>
/// Manages the system tray icon and context menu.
/// Provides quick access to settings and quit functionality.
/// </summary>
public class TrayManager : IDisposable
{
    private readonly TaskbarIcon _trayIcon;
    private readonly SettingsService _settingsService;
    private readonly Action _showSettingsAction;
    private readonly Action _quitAction;
    private MenuFlyoutItem? _topBarItem;
    private MenuFlyoutItem? _bottomOverlayItem;

    public TrayManager(SettingsService settingsService, Action showSettingsAction, Action quitAction)
    {
        _settingsService = settingsService;
        _showSettingsAction = showSettingsAction;
        _quitAction = quitAction;

        _trayIcon = new TaskbarIcon();
        InitializeTrayIcon();
    }

    private void InitializeTrayIcon()
    {
        // Set icon from file
        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "invisible.ico");
        if (File.Exists(iconPath))
        {
            _trayIcon.Icon = new System.Drawing.Icon(iconPath);
        }

        _trayIcon.ToolTipText = "TeamsHider - Running";

        // Create context menu
        MenuFlyout menu = new();

        // Quick toggle: Hide Top Bar
        _topBarItem = new MenuFlyoutItem
        {
            Text = _settingsService.CurrentSettings.HideTopBar
                ? "Hide Top Bar (On)"
                : "Hide Top Bar (Off)"
        };
        _topBarItem.Click += OnToggleTopBar;
        menu.Items.Add(_topBarItem);

        // Quick toggle: Hide Bottom Overlay
        _bottomOverlayItem = new MenuFlyoutItem
        {
            Text = _settingsService.CurrentSettings.HideBottomOverlay
                ? "Hide Bottom Overlay (On)"
                : "Hide Bottom Overlay (Off)"
        };
        _bottomOverlayItem.Click += OnToggleBottomOverlay;
        menu.Items.Add(_bottomOverlayItem);

        menu.Items.Add(new MenuFlyoutSeparator());

        // Settings
        MenuFlyoutItem settingsItem = new() { Text = "Settings..." };
        settingsItem.Click += (s, e) => _showSettingsAction();
        menu.Items.Add(settingsItem);

        // About
        MenuFlyoutItem aboutItem = new() { Text = "About" };
        aboutItem.Click += OnAboutClicked;
        menu.Items.Add(aboutItem);

        menu.Items.Add(new MenuFlyoutSeparator());

        // Quit
        MenuFlyoutItem quitItem = new() { Text = "Quit" };
        quitItem.Click += (s, e) => _quitAction();
        menu.Items.Add(quitItem);

        _trayIcon.ContextFlyout = menu;
    }

    private void OnToggleTopBar(object sender, RoutedEventArgs e)
    {
        _settingsService.CurrentSettings.HideTopBar = !_settingsService.CurrentSettings.HideTopBar;
        _settingsService.SaveSettings();
        UpdateMenuItems();
    }

    private void OnToggleBottomOverlay(object sender, RoutedEventArgs e)
    {
        _settingsService.CurrentSettings.HideBottomOverlay = !_settingsService.CurrentSettings.HideBottomOverlay;
        _settingsService.SaveSettings();
        UpdateMenuItems();
    }

    /// <summary>
    /// Updates menu item text to reflect current settings.
    /// Called after settings change.
    /// </summary>
    public void UpdateMenuItems()
    {
        if (_topBarItem != null)
        {
            _topBarItem.Text = _settingsService.CurrentSettings.HideTopBar
                ? "Hide Top Bar (On)"
                : "Hide Top Bar (Off)";
        }

        if (_bottomOverlayItem != null)
        {
            _bottomOverlayItem.Text = _settingsService.CurrentSettings.HideBottomOverlay
                ? "Hide Bottom Overlay (On)"
                : "Hide Bottom Overlay (Off)";
        }
    }

    private void OnAboutClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            ProcessStartInfo psi = new()
            {
                FileName = "https://github.com/mroter93/TeamsHider",
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch
        {
            // Silently fail if browser cannot be opened
        }
    }

    public void Dispose()
    {
        _trayIcon.Dispose();
    }
}
