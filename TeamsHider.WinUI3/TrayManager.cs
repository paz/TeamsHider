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
    private ToggleMenuFlyoutItem? _topBarToggle;
    private ToggleMenuFlyoutItem? _bottomOverlayToggle;

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
        // Load icon
        LoadIcon();

        // Build the context menu with WinUI 3 controls
        var menu = new MenuFlyout();

        // Toggle: Hide Top Bar (uses WinUI 3 ToggleMenuFlyoutItem with native checkbox)
        _topBarToggle = new ToggleMenuFlyoutItem
        {
            Text = "Hide Top Bar",
            IsChecked = _settingsService.CurrentSettings.HideTopBar
        };
        _topBarToggle.Click += OnToggleTopBar;
        menu.Items.Add(_topBarToggle);

        // Toggle: Hide Bottom Overlay
        _bottomOverlayToggle = new ToggleMenuFlyoutItem
        {
            Text = "Hide Bottom Overlay",
            IsChecked = _settingsService.CurrentSettings.HideBottomOverlay
        };
        _bottomOverlayToggle.Click += OnToggleBottomOverlay;
        menu.Items.Add(_bottomOverlayToggle);

        menu.Items.Add(new MenuFlyoutSeparator());

        // Settings
        var settingsItem = new MenuFlyoutItem { Text = "Settings" };
        settingsItem.Click += OnSettingsClicked;
        menu.Items.Add(settingsItem);

        // About
        var aboutItem = new MenuFlyoutItem { Text = "About TeamsHider" };
        aboutItem.Click += OnAboutClicked;
        menu.Items.Add(aboutItem);

        menu.Items.Add(new MenuFlyoutSeparator());

        // Exit
        var exitItem = new MenuFlyoutItem { Text = "Exit" };
        exitItem.Click += OnExitClicked;
        menu.Items.Add(exitItem);

        _trayIcon.ContextFlyout = menu;

        // Force the tray icon to show
        _trayIcon.ForceCreate();
    }

    private void LoadIcon()
    {
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
    }

    private void OnToggleTopBar(object sender, RoutedEventArgs e)
    {
        if (_topBarToggle == null) return;
        _settingsService.CurrentSettings.HideTopBar = _topBarToggle.IsChecked;
        _settingsService.SaveSettings();
    }

    private void OnToggleBottomOverlay(object sender, RoutedEventArgs e)
    {
        if (_bottomOverlayToggle == null) return;
        _settingsService.CurrentSettings.HideBottomOverlay = _bottomOverlayToggle.IsChecked;
        _settingsService.SaveSettings();
    }

    private void OnSettingsClicked(object sender, RoutedEventArgs e)
    {
        _showSettingsAction();
    }

    private void OnAboutClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/paz/TeamsHider",
                UseShellExecute = true
            });
        }
        catch
        {
            // Silently fail if browser cannot be opened
        }
    }

    private void OnExitClicked(object sender, RoutedEventArgs e)
    {
        _quitAction();
    }

    /// <summary>
    /// Updates toggle states to reflect current settings.
    /// Called after settings change from Settings window.
    /// </summary>
    public void UpdateMenuItems()
    {
        if (_topBarToggle != null)
        {
            _topBarToggle.IsChecked = _settingsService.CurrentSettings.HideTopBar;
        }

        if (_bottomOverlayToggle != null)
        {
            _bottomOverlayToggle.IsChecked = _settingsService.CurrentSettings.HideBottomOverlay;
        }
    }

    public void Dispose()
    {
        _trayIcon.Dispose();
    }
}
