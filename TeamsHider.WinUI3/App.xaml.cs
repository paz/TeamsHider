using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using TeamsHider.Models;
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
    private HiddenWindow? _hiddenWindow;
    private DispatcherQueue? _dispatcherQueue;

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

            // Get dispatcher queue for UI thread marshalling
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            DebugLog.Log("App", "Initializing WindowMonitorService...");
            _monitorService = new WindowMonitorService(_settingsService);
            _monitorService.StatusChanged += OnMonitorStatusChanged;
            _monitorService.Start();
            DebugLog.Log("App", "WindowMonitorService started");

            // Create hidden window to keep WinUI 3 app alive
            DebugLog.Log("App", "Creating hidden window...");
            _hiddenWindow = new HiddenWindow();

            // Initialize tray icon
            DebugLog.Log("App", "Initializing TrayManager...");
            _trayManager = new TrayManager(_settingsService, ShowSettingsWindow, QuitApplication);
            DebugLog.Log("App", "TrayManager initialized - app is running");

            // Handle first launch: show welcome balloon to help users find the tray icon
            if (!_settingsService.CurrentSettings.FirstLaunchCompleted)
            {
                DebugLog.Log("App", "First launch detected - showing welcome balloon");
                _trayManager.ShowWelcomeBalloon();
                _settingsService.CurrentSettings.FirstLaunchCompleted = true;
                _settingsService.SaveSettings();
            }
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
            // Always create a new window since flyout closes on deactivation
            if (_settingsWindow != null)
            {
                // Close existing if somehow still around
                try { _settingsWindow.Close(); } catch { }
                _settingsWindow = null;
            }

            DebugLog.Log("App", "Creating new SettingsWindow flyout");
            _settingsWindow = new SettingsWindow(_settingsService!, OnSettingsChanged, QuitApplication);
            _settingsWindow.Closed += (s, e) =>
            {
                DebugLog.Log("App", "SettingsWindow closed");
                _settingsWindow = null;
            };

            // Update with current status
            if (_monitorService != null)
            {
                _settingsWindow.UpdateStatus(_monitorService.CurrentStatus);
            }
            else if (_trayManager != null)
            {
                _settingsWindow.UpdateStatus(_trayManager.CurrentStatus);
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
        // Settings are now handled directly via the flyout
    }

    private void OnMonitorStatusChanged(MonitorStatus status)
    {
        // Marshal to UI thread since this is called from background monitor
        _dispatcherQueue?.TryEnqueue(() =>
        {
            _trayManager?.UpdateStatus(status);
            _settingsWindow?.UpdateStatus(status);
        });
    }

    private void QuitApplication()
    {
        DebugLog.Log("App", "QuitApplication called");
        if (_monitorService != null)
        {
            _monitorService.StatusChanged -= OnMonitorStatusChanged;
            _monitorService.Dispose();
        }
        _trayManager?.Dispose();
        _settingsWindow?.Close();
        _hiddenWindow?.Close();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        DebugLog.Log("App", "Exiting...");
        Exit();
    }
}
