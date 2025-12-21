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
        Debug.Log("TrayManager", "Constructor called");
        _settingsService = settingsService;
        _showSettingsAction = showSettingsAction;
        _quitAction = quitAction;

        try
        {
            // Get TaskbarIcon from application resources (defined in App.xaml)
            Debug.Log("TrayManager", "Getting TaskbarIcon from resources...");
            _trayIcon = (TaskbarIcon)Application.Current.Resources["TrayIcon"];
            Debug.Log("TrayManager", $"TaskbarIcon retrieved: {_trayIcon != null}");
            InitializeTrayIcon();
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed to initialize TrayManager", ex);
            throw;
        }
    }

    private void InitializeTrayIcon()
    {
        Debug.Log("TrayManager", "InitializeTrayIcon called");

        // Load icon
        LoadIcon();

        // Build the context menu with WinUI 3 controls
        Debug.Log("TrayManager", "Building context menu...");
        var menu = new MenuFlyout();

        // Toggle: Hide Top Bar (uses WinUI 3 ToggleMenuFlyoutItem with native checkbox)
        _topBarToggle = new ToggleMenuFlyoutItem
        {
            Text = "Hide Top Bar",
            IsChecked = _settingsService.CurrentSettings.HideTopBar
        };
        _topBarToggle.Click += OnToggleTopBar;
        menu.Items.Add(_topBarToggle);
        Debug.Log("TrayManager", $"Added Hide Top Bar toggle (checked={_topBarToggle.IsChecked})");

        // Toggle: Hide Bottom Overlay
        _bottomOverlayToggle = new ToggleMenuFlyoutItem
        {
            Text = "Hide Bottom Overlay",
            IsChecked = _settingsService.CurrentSettings.HideBottomOverlay
        };
        _bottomOverlayToggle.Click += OnToggleBottomOverlay;
        menu.Items.Add(_bottomOverlayToggle);
        Debug.Log("TrayManager", $"Added Hide Bottom Overlay toggle (checked={_bottomOverlayToggle.IsChecked})");

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

        Debug.Log("TrayManager", $"Menu built with {menu.Items.Count} items");

        _trayIcon.ContextFlyout = menu;
        Debug.Log("TrayManager", "ContextFlyout assigned to TaskbarIcon");

        // Force the tray icon to show
        Debug.Log("TrayManager", "Calling ForceCreate...");
        _trayIcon.ForceCreate();
        Debug.Log("TrayManager", "ForceCreate completed");
    }

    private void LoadIcon()
    {
        Debug.Log("TrayManager", "LoadIcon called");
        System.Drawing.Icon? icon = null;

        // Try Assets folder first
        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "invisible.ico");
        Debug.Log("TrayManager", $"Trying icon path: {iconPath}");
        if (File.Exists(iconPath))
        {
            icon = new System.Drawing.Icon(iconPath);
            Debug.Log("TrayManager", "Icon loaded from Assets folder");
        }
        else
        {
            // Try same directory as exe
            string altPath = Path.Combine(AppContext.BaseDirectory, "invisible.ico");
            Debug.Log("TrayManager", $"Trying alternate path: {altPath}");
            if (File.Exists(altPath))
            {
                icon = new System.Drawing.Icon(altPath);
                Debug.Log("TrayManager", "Icon loaded from exe directory");
            }
            else
            {
                // Try embedded resource (for single-file publish)
                Debug.Log("TrayManager", "Trying embedded resource...");
                var assembly = typeof(TrayManager).Assembly;
                using var stream = assembly.GetManifestResourceStream("TeamsHider.invisible.ico");
                if (stream != null)
                {
                    icon = new System.Drawing.Icon(stream);
                    Debug.Log("TrayManager", "Icon loaded from embedded resource");
                }
                else
                {
                    Debug.Log("TrayManager", "WARNING: No icon found!");
                }
            }
        }

        if (icon != null)
        {
            _trayIcon.Icon = icon;
            Debug.Log("TrayManager", "Icon assigned to TaskbarIcon");
        }
    }

    private void OnToggleTopBar(object sender, RoutedEventArgs e)
    {
        Debug.Log("TrayManager", "OnToggleTopBar clicked!");
        try
        {
            if (_topBarToggle == null)
            {
                Debug.Log("TrayManager", "ERROR: _topBarToggle is null");
                return;
            }
            Debug.Log("TrayManager", $"New IsChecked value: {_topBarToggle.IsChecked}");
            _settingsService.CurrentSettings.HideTopBar = _topBarToggle.IsChecked;
            _settingsService.SaveSettings();
            Debug.Log("TrayManager", "Settings saved");
        }
        catch (Exception ex)
        {
            Debug.LogError("OnToggleTopBar failed", ex);
        }
    }

    private void OnToggleBottomOverlay(object sender, RoutedEventArgs e)
    {
        Debug.Log("TrayManager", "OnToggleBottomOverlay clicked!");
        try
        {
            if (_bottomOverlayToggle == null)
            {
                Debug.Log("TrayManager", "ERROR: _bottomOverlayToggle is null");
                return;
            }
            Debug.Log("TrayManager", $"New IsChecked value: {_bottomOverlayToggle.IsChecked}");
            _settingsService.CurrentSettings.HideBottomOverlay = _bottomOverlayToggle.IsChecked;
            _settingsService.SaveSettings();
            Debug.Log("TrayManager", "Settings saved");
        }
        catch (Exception ex)
        {
            Debug.LogError("OnToggleBottomOverlay failed", ex);
        }
    }

    private void OnSettingsClicked(object sender, RoutedEventArgs e)
    {
        Debug.Log("TrayManager", "OnSettingsClicked!");
        try
        {
            _showSettingsAction();
        }
        catch (Exception ex)
        {
            Debug.LogError("OnSettingsClicked failed", ex);
        }
    }

    private void OnAboutClicked(object sender, RoutedEventArgs e)
    {
        Debug.Log("TrayManager", "OnAboutClicked!");
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/paz/TeamsHider",
                UseShellExecute = true
            });
            Debug.Log("TrayManager", "Browser opened");
        }
        catch (Exception ex)
        {
            Debug.LogError("OnAboutClicked failed", ex);
        }
    }

    private void OnExitClicked(object sender, RoutedEventArgs e)
    {
        Debug.Log("TrayManager", "OnExitClicked!");
        try
        {
            _quitAction();
        }
        catch (Exception ex)
        {
            Debug.LogError("OnExitClicked failed", ex);
        }
    }

    /// <summary>
    /// Updates toggle states to reflect current settings.
    /// Called after settings change from Settings window.
    /// </summary>
    public void UpdateMenuItems()
    {
        Debug.Log("TrayManager", "UpdateMenuItems called");
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
        Debug.Log("TrayManager", "Dispose called");
        _trayIcon.Dispose();
    }
}
