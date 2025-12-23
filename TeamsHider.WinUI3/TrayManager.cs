using H.NotifyIcon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;
using TeamsHider.Helpers;
using TeamsHider.Models;
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
    private MenuFlyoutItem? _statusItem;

    public TrayManager(SettingsService settingsService, Action showSettingsAction, Action quitAction)
    {
        DebugLog.Log("TrayManager", "Constructor called");
        _settingsService = settingsService;
        _showSettingsAction = showSettingsAction;
        _quitAction = quitAction;

        try
        {
            // Get TaskbarIcon from application resources (defined in App.xaml)
            DebugLog.Log("TrayManager", "Getting TaskbarIcon from resources...");
            _trayIcon = (TaskbarIcon)Application.Current.Resources["TrayIcon"];
            DebugLog.Log("TrayManager", $"TaskbarIcon retrieved: {_trayIcon != null}");
            InitializeTrayIcon();
        }
        catch (Exception ex)
        {
            DebugLog.LogError("Failed to initialize TrayManager", ex);
            throw;
        }
    }

    private void InitializeTrayIcon()
    {
        DebugLog.Log("TrayManager", "InitializeTrayIcon called");

        // Load icon
        LoadIcon();

        // Build the context menu with WinUI 3 controls
        DebugLog.Log("TrayManager", "Building context menu...");
        var menu = new MenuFlyout();

        // Status line at top (non-clickable, shows current state)
        _statusItem = new MenuFlyoutItem
        {
            Text = "Status: Initializing...",
            IsEnabled = false // Greyed out, informational only
        };
        menu.Items.Add(_statusItem);
        menu.Items.Add(new MenuFlyoutSeparator());

        // Toggle: Hide Top Bar
        // H.NotifyIcon creates Win32 PopupMenus that call Commands, not Click events
        _topBarToggle = new ToggleMenuFlyoutItem
        {
            Text = "Hide Top Bar",
            IsChecked = _settingsService.CurrentSettings.HideTopBar,
            Command = new RelayCommand(OnToggleTopBar)
        };
        menu.Items.Add(_topBarToggle);
        DebugLog.Log("TrayManager", $"Added Hide Top Bar toggle (checked={_topBarToggle.IsChecked})");

        // Toggle: Hide Bottom Overlay
        _bottomOverlayToggle = new ToggleMenuFlyoutItem
        {
            Text = "Hide Bottom Overlay",
            IsChecked = _settingsService.CurrentSettings.HideBottomOverlay,
            Command = new RelayCommand(OnToggleBottomOverlay)
        };
        menu.Items.Add(_bottomOverlayToggle);
        DebugLog.Log("TrayManager", $"Added Hide Bottom Overlay toggle (checked={_bottomOverlayToggle.IsChecked})");

        menu.Items.Add(new MenuFlyoutSeparator());

        // Settings
        var settingsItem = new MenuFlyoutItem
        {
            Text = "Settings",
            Command = new RelayCommand(OnSettingsClicked)
        };
        menu.Items.Add(settingsItem);

        // About
        var aboutItem = new MenuFlyoutItem
        {
            Text = "About TeamsHider",
            Command = new RelayCommand(OnAboutClicked)
        };
        menu.Items.Add(aboutItem);

        menu.Items.Add(new MenuFlyoutSeparator());

        // Exit
        var exitItem = new MenuFlyoutItem
        {
            Text = "Exit",
            Command = new RelayCommand(OnExitClicked)
        };
        menu.Items.Add(exitItem);

        DebugLog.Log("TrayManager", $"Menu built with {menu.Items.Count} items");

        _trayIcon.ContextFlyout = menu;
        DebugLog.Log("TrayManager", "ContextFlyout assigned to TaskbarIcon");

        // Force the tray icon to show
        DebugLog.Log("TrayManager", "Calling ForceCreate...");
        _trayIcon.ForceCreate();
        DebugLog.Log("TrayManager", "ForceCreate completed");
    }

    private void LoadIcon()
    {
        DebugLog.Log("TrayManager", "LoadIcon called");

        // Generate a theme-aware icon
        try
        {
            bool isDarkTheme = IconGenerator.IsSystemDarkTheme();
            DebugLog.Log("TrayManager", $"System theme: {(isDarkTheme ? "Dark" : "Light")}");

            var icon = IconGenerator.GenerateIcon(isDarkTheme);
            _trayIcon.Icon = icon;
            DebugLog.Log("TrayManager", "Generated theme-aware icon assigned");
            return;
        }
        catch (Exception ex)
        {
            DebugLog.Log("TrayManager", $"Failed to generate icon: {ex.Message}, falling back to file");
        }

        // Fallback: Try to load from file
        System.Drawing.Icon? fileIcon = null;

        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "invisible.ico");
        if (File.Exists(iconPath))
        {
            fileIcon = new System.Drawing.Icon(iconPath);
            DebugLog.Log("TrayManager", "Icon loaded from Assets folder");
        }
        else
        {
            string altPath = Path.Combine(AppContext.BaseDirectory, "invisible.ico");
            if (File.Exists(altPath))
            {
                fileIcon = new System.Drawing.Icon(altPath);
                DebugLog.Log("TrayManager", "Icon loaded from exe directory");
            }
            else
            {
                var assembly = typeof(TrayManager).Assembly;
                using var stream = assembly.GetManifestResourceStream("TeamsHider.invisible.ico");
                if (stream != null)
                {
                    fileIcon = new System.Drawing.Icon(stream);
                    DebugLog.Log("TrayManager", "Icon loaded from embedded resource");
                }
            }
        }

        if (fileIcon != null)
        {
            _trayIcon.Icon = fileIcon;
            DebugLog.Log("TrayManager", "File icon assigned to TaskbarIcon");
        }
    }

    private void OnToggleTopBar()
    {
        DebugLog.Log("TrayManager", "OnToggleTopBar command executed!");
        try
        {
            if (_topBarToggle == null)
            {
                DebugLog.Log("TrayManager", "ERROR: _topBarToggle is null");
                return;
            }
            // Toggle the state (H.NotifyIcon Win32 menu doesn't auto-toggle)
            bool newValue = !_settingsService.CurrentSettings.HideTopBar;
            _topBarToggle.IsChecked = newValue;
            _settingsService.CurrentSettings.HideTopBar = newValue;
            _settingsService.SaveSettings();
            DebugLog.Log("TrayManager", $"HideTopBar toggled to: {newValue}");
        }
        catch (Exception ex)
        {
            DebugLog.LogError("OnToggleTopBar failed", ex);
        }
    }

    private void OnToggleBottomOverlay()
    {
        DebugLog.Log("TrayManager", "OnToggleBottomOverlay command executed!");
        try
        {
            if (_bottomOverlayToggle == null)
            {
                DebugLog.Log("TrayManager", "ERROR: _bottomOverlayToggle is null");
                return;
            }
            // Toggle the state (H.NotifyIcon Win32 menu doesn't auto-toggle)
            bool newValue = !_settingsService.CurrentSettings.HideBottomOverlay;
            _bottomOverlayToggle.IsChecked = newValue;
            _settingsService.CurrentSettings.HideBottomOverlay = newValue;
            _settingsService.SaveSettings();
            DebugLog.Log("TrayManager", $"HideBottomOverlay toggled to: {newValue}");
        }
        catch (Exception ex)
        {
            DebugLog.LogError("OnToggleBottomOverlay failed", ex);
        }
    }

    private void OnSettingsClicked()
    {
        DebugLog.Log("TrayManager", "OnSettingsClicked command executed!");
        try
        {
            _showSettingsAction();
        }
        catch (Exception ex)
        {
            DebugLog.LogError("OnSettingsClicked failed", ex);
        }
    }

    private void OnAboutClicked()
    {
        DebugLog.Log("TrayManager", "OnAboutClicked command executed!");
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/mroter93/TeamsHider",
                UseShellExecute = true
            });
            DebugLog.Log("TrayManager", "Browser opened");
        }
        catch (Exception ex)
        {
            DebugLog.LogError("OnAboutClicked failed", ex);
        }
    }

    private void OnExitClicked()
    {
        DebugLog.Log("TrayManager", "OnExitClicked command executed!");
        try
        {
            _quitAction();
        }
        catch (Exception ex)
        {
            DebugLog.LogError("OnExitClicked failed", ex);
        }
    }

    /// <summary>
    /// Updates toggle states to reflect current settings.
    /// Called after settings change from Settings window.
    /// </summary>
    public void UpdateMenuItems()
    {
        DebugLog.Log("TrayManager", "UpdateMenuItems called");
        if (_topBarToggle != null)
        {
            _topBarToggle.IsChecked = _settingsService.CurrentSettings.HideTopBar;
        }

        if (_bottomOverlayToggle != null)
        {
            _bottomOverlayToggle.IsChecked = _settingsService.CurrentSettings.HideBottomOverlay;
        }
    }

    /// <summary>
    /// Updates the status display in the tray menu and tooltip.
    /// Called when monitor service status changes.
    /// </summary>
    public void UpdateStatus(MonitorStatus status)
    {
        DebugLog.Log("TrayManager", $"UpdateStatus: {status.StatusMessage}");

        // Update menu status item
        if (_statusItem != null)
        {
            _statusItem.Text = $"Status: {status.StatusMessage}";
        }

        // Update tooltip with detailed status
        string tooltip = status.TeamsDetected
            ? $"TeamsHider - {status.StatusMessage}"
            : "TeamsHider - Teams not detected";

        _trayIcon.ToolTipText = tooltip;
    }

    public void Dispose()
    {
        DebugLog.Log("TrayManager", "Dispose called");
        _trayIcon.Dispose();
    }
}
