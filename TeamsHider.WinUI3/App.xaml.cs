using Microsoft.UI.Xaml;
using TeamsHider.Services;

namespace TeamsHider;

/// <summary>
/// Application entry point.
/// Manages single instance, services, and lifecycle.
/// </summary>
public partial class App : Application
{
    // Use a GUID-based mutex name to avoid collisions with other applications
    private const string MutexId = "Global\\TeamsHider-{7B3A4F2E-1C9D-4E5F-8A6B-0D2C3E4F5A6B}";
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
        // Single instance enforcement using GUID-based mutex
        _mutex = new Mutex(false, MutexId);
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
