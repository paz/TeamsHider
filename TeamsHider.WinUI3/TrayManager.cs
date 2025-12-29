using H.NotifyIcon;
using Microsoft.UI.Xaml;
using TeamsHider.Models;
using TeamsHider.Services;

namespace TeamsHider;

/// <summary>
/// Manages the system tray icon.
/// Shows flyout on left or right click.
/// </summary>
public class TrayManager : IDisposable
{
    private readonly TaskbarIcon _trayIcon = null!; // Initialized in constructor, throws if fails
    private readonly Action _showFlyoutAction;
    private MonitorStatus _lastStatus = MonitorStatus.Initial;

    public TrayManager(SettingsService settingsService, Action showFlyoutAction, Action quitAction)
    {
        DebugLog.Log("TrayManager", "Constructor called");
        _ = settingsService; // May be used later
        _ = quitAction; // Exit handled via flyout now
        _showFlyoutAction = showFlyoutAction;

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

        // Wire up click events - both left and right click show the flyout
        _trayIcon.LeftClickCommand = new RelayCommand(OnTrayClicked);
        _trayIcon.RightClickCommand = new RelayCommand(OnTrayClicked);

        DebugLog.Log("TrayManager", "Click handlers configured");

        // Force the tray icon to show
        DebugLog.Log("TrayManager", "Calling ForceCreate...");
        _trayIcon.ForceCreate();
        DebugLog.Log("TrayManager", "ForceCreate completed");
    }

    private void LoadIcon()
    {
        DebugLog.Log("TrayManager", "LoadIcon called");

        // Load icon from file (new icon works for both light and dark themes)
        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "invisible.ico");
        if (File.Exists(iconPath))
        {
            _trayIcon.Icon = new System.Drawing.Icon(iconPath);
            DebugLog.Log("TrayManager", "Icon loaded from Assets folder");
            return;
        }

        // Fallback: try exe directory
        string altPath = Path.Combine(AppContext.BaseDirectory, "invisible.ico");
        if (File.Exists(altPath))
        {
            _trayIcon.Icon = new System.Drawing.Icon(altPath);
            DebugLog.Log("TrayManager", "Icon loaded from exe directory");
            return;
        }

        // Fallback: embedded resource
        var assembly = typeof(TrayManager).Assembly;
        using var stream = assembly.GetManifestResourceStream("TeamsHider.invisible.ico");
        if (stream != null)
        {
            _trayIcon.Icon = new System.Drawing.Icon(stream);
            DebugLog.Log("TrayManager", "Icon loaded from embedded resource");
        }
    }

    private void OnTrayClicked()
    {
        DebugLog.Log("TrayManager", "Tray icon clicked - showing flyout");
        try
        {
            _showFlyoutAction();
        }
        catch (Exception ex)
        {
            DebugLog.LogError("OnTrayClicked failed", ex);
        }
    }

    /// <summary>
    /// Shows a welcome balloon tip on first launch to help users find the tray icon.
    /// </summary>
    public void ShowWelcomeBalloon()
    {
        DebugLog.Log("TrayManager", "ShowWelcomeBalloon called");
        try
        {
            _trayIcon.ShowNotification(
                title: "TeamsHider is running",
                message: "Click this icon to access settings.",
                icon: H.NotifyIcon.Core.NotificationIcon.Info,
                timeout: TimeSpan.FromSeconds(5));
            DebugLog.Log("TrayManager", "Welcome balloon shown");
        }
        catch (Exception ex)
        {
            DebugLog.LogError("Failed to show welcome balloon", ex);
        }
    }

    /// <summary>
    /// Updates the tooltip with current monitoring status.
    /// Called when monitor service status changes.
    /// </summary>
    public void UpdateStatus(MonitorStatus status)
    {
        _lastStatus = status;
        DebugLog.Log("TrayManager", $"UpdateStatus: {status.StatusMessage}");

        // Update tooltip with detailed status
        string tooltip = status.TeamsDetected
            ? $"TeamsHider - {status.StatusMessage}"
            : "TeamsHider - Teams not detected";

        _trayIcon.ToolTipText = tooltip;
    }

    /// <summary>
    /// Gets the current status for passing to the flyout window.
    /// </summary>
    public MonitorStatus CurrentStatus => _lastStatus;

    public void Dispose()
    {
        DebugLog.Log("TrayManager", "Dispose called");
        _trayIcon.Dispose();
    }
}
