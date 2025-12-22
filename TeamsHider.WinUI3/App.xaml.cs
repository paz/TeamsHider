using Microsoft.UI.Xaml;
using TeamsHider.Services;

namespace TeamsHider;

/// <summary>
/// Application entry point.
/// Manages single instance, services, and lifecycle.
/// Run with --debug flag to enable console logging.
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
        // Initialize debug mode from command line args
        DebugLog.Initialize(Environment.GetCommandLineArgs());
        DebugLog.Log("App", "Constructor called");

        InitializeComponent();
        DebugLog.Log("App", "InitializeComponent completed");
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        DebugLog.Log("App", "OnLaunched called");

        // Single instance enforcement using GUID-based mutex
        _mutex = new Mutex(false, MutexId);
        if (!_mutex.WaitOne(0, false))
        {
            DebugLog.Log("App", "Another instance is running - exiting");
            Exit();
            return;
        }
        DebugLog.Log("App", "Mutex acquired - we are the only instance");

        try
        {
            // Initialize services
            DebugLog.Log("App", "Initializing SettingsService...");
            _settingsService = new SettingsService();
            _settingsService.LoadSettings();
            DebugLog.Log("App", $"Settings loaded: HideTopBar={_settingsService.CurrentSettings.HideTopBar}, HideBottomOverlay={_settingsService.CurrentSettings.HideBottomOverlay}");

            // Sync startup setting with registry state
            bool registryStartup = StartupManager.IsStartupEnabled();
            DebugLog.Log("App", $"Registry startup enabled: {registryStartup}");
            if (_settingsService.CurrentSettings.LaunchAtStartup != registryStartup)
            {
                _settingsService.CurrentSettings.LaunchAtStartup = registryStartup;
                _settingsService.SaveSettings();
                DebugLog.Log("App", "Synced startup setting with registry");
            }

            DebugLog.Log("App", "Initializing WindowMonitorService...");
            _monitorService = new WindowMonitorService(_settingsService);
            _monitorService.Start();
            DebugLog.Log("App", "WindowMonitorService started");

            // Initialize tray icon
            DebugLog.Log("App", "Initializing TrayManager...");
            _trayManager = new TrayManager(_settingsService, ShowSettingsWindow, QuitApplication);
            DebugLog.Log("App", "TrayManager initialized - app is running");
        }
        catch (Exception ex)
        {
            DebugLog.LogError("Failed during OnLaunched", ex);
            throw;
        }
    }

    private void ShowSettingsWindow()
    {
        DebugLog.Log("App", "ShowSettingsWindow called");
        try
        {
            if (_settingsWindow == null || _settingsWindow.AppWindow == null)
            {
                DebugLog.Log("App", "Creating new SettingsWindow");
                _settingsWindow = new SettingsWindow(_settingsService!, OnSettingsChanged);
                _settingsWindow.Closed += (s, e) =>
                {
                    DebugLog.Log("App", "SettingsWindow closed");
                    _settingsWindow = null;
                };
            }

            DebugLog.Log("App", "Activating SettingsWindow");
            _settingsWindow.Activate();
        }
        catch (Exception ex)
        {
            DebugLog.LogError("Failed to show settings window", ex);
        }
    }

    private void OnSettingsChanged()
    {
        DebugLog.Log("App", "OnSettingsChanged called");
        _trayManager?.UpdateMenuItems();
    }

    private void QuitApplication()
    {
        DebugLog.Log("App", "QuitApplication called");
        _monitorService?.Dispose();
        _trayManager?.Dispose();
        _settingsWindow?.Close();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        DebugLog.Log("App", "Exiting...");
        Exit();
    }
}
