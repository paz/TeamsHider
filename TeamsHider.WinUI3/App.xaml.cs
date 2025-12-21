using Microsoft.UI.Xaml;
using System.Diagnostics;
using TeamsHider.Services;

namespace TeamsHider;

/// <summary>
/// Application entry point.
/// Manages single instance, services, and lifecycle.
/// </summary>
public partial class App : Application
{
    private static readonly string AppId = Process.GetCurrentProcess().ProcessName;
    private static Mutex? _mutex;

    private SettingsService? _settingsService;
    private WindowMonitorService? _monitorService;
    private TrayManager? _trayManager;
    private SettingsWindow? _settingsWindow;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Single instance enforcement
        _mutex = new Mutex(false, AppId);
        if (!_mutex.WaitOne(0, false))
        {
            // Another instance is running - exit silently
            Exit();
            return;
        }

        // Initialize services
        _settingsService = new SettingsService();
        _settingsService.LoadSettings();

        // Sync startup setting with registry state
        bool registryStartup = StartupManager.IsStartupEnabled();
        if (_settingsService.CurrentSettings.LaunchAtStartup != registryStartup)
        {
            _settingsService.CurrentSettings.LaunchAtStartup = registryStartup;
            _settingsService.SaveSettings();
        }

        _monitorService = new WindowMonitorService(_settingsService);
        _monitorService.Start();

        // Initialize tray icon
        _trayManager = new TrayManager(_settingsService, ShowSettingsWindow, QuitApplication);
    }

    private void ShowSettingsWindow()
    {
        if (_settingsWindow == null || _settingsWindow.AppWindow == null)
        {
            _settingsWindow = new SettingsWindow(_settingsService!, OnSettingsChanged);
            _settingsWindow.Closed += (s, e) => _settingsWindow = null;
        }

        _settingsWindow.Activate();
    }

    private void OnSettingsChanged()
    {
        // Update tray menu to reflect new settings
        _trayManager?.UpdateMenuItems();
    }

    private void QuitApplication()
    {
        _monitorService?.Stop();
        _trayManager?.Dispose();
        _settingsWindow?.Close();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        Exit();
    }
}
