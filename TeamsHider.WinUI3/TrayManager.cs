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

        // Get TaskbarIcon from application resources (defined in App.xaml)
        _trayIcon = (TaskbarIcon)Application.Current.Resources["TrayIcon"];
        InitializeTrayIcon();
    }

    private void InitializeTrayIcon()
    {
        // Try to load icon from file, then embedded resource
        System.Drawing.Icon? icon = null;

        // Try Assets folder first
        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "invisible.ico");
        if (File.Exists(iconPath))
        {
            icon = new System.Drawing.Icon(iconPath);
        }
        else
        {
            // Try same directory as exe
            string altPath = Path.Combine(AppContext.BaseDirectory, "invisible.ico");
            if (File.Exists(altPath))
            {
                icon = new System.Drawing.Icon(altPath);
            }
            else
            {
                // Try embedded resource (for single-file publish)
                var assembly = typeof(TrayManager).Assembly;
                using var stream = assembly.GetManifestResourceStream("TeamsHider.invisible.ico");
                if (stream != null)
                {
                    icon = new System.Drawing.Icon(stream);
                }
            }
        }

        if (icon != null)
        {
            _trayIcon.Icon = icon;
        }

        // Build the context menu
        var menu = _trayIcon.ContextFlyout as MenuFlyout;
        if (menu == null)
        {
            menu = new MenuFlyout();
            _trayIcon.ContextFlyout = menu;
        }
        menu.Items.Clear();

        // Quick toggle: Hide Top Bar
        _topBarItem = new MenuFlyoutItem
        {
            Text = _settingsService.CurrentSettings.HideTopBar
                ? "✓ Hide Top Bar"
                : "  Hide Top Bar"
        };
        _topBarItem.Click += OnToggleTopBar;
        menu.Items.Add(_topBarItem);

        // Quick toggle: Hide Bottom Overlay
        _bottomOverlayItem = new MenuFlyoutItem
        {
            Text = _settingsService.CurrentSettings.HideBottomOverlay
                ? "✓ Hide Bottom Overlay"
                : "  Hide Bottom Overlay"
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

        // Force the tray icon to show
        _trayIcon.ForceCreate();
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
                ? "✓ Hide Top Bar"
                : "  Hide Top Bar";
        }

        if (_bottomOverlayItem != null)
        {
            _bottomOverlayItem.Text = _settingsService.CurrentSettings.HideBottomOverlay
                ? "✓ Hide Bottom Overlay"
                : "  Hide Bottom Overlay";
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
