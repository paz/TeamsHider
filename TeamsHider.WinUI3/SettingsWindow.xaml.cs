using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using TeamsHider.Services;
using Windows.Graphics;

namespace TeamsHider;

/// <summary>
/// Settings window for TeamsHider.
/// Opens from system tray, hides when closed.
/// </summary>
public sealed partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly Action? _onSettingsChanged;
    private bool _isInitializing = true;

    public SettingsWindow(SettingsService settingsService, Action? onSettingsChanged = null)
    {
        _settingsService = settingsService;
        _onSettingsChanged = onSettingsChanged;

        InitializeComponent();

        // Set window size and properties
        AppWindow.Resize(new SizeInt32(400, 360));
        Title = "TeamsHider Settings";

        // Load current settings
        LoadSettings();
        _isInitializing = false;

        // Handle keyboard shortcuts
        Content.KeyDown += OnKeyDown;
    }

    private void LoadSettings()
    {
        HideTopBarToggle.IsOn = _settingsService.CurrentSettings.HideTopBar;
        HideBottomOverlayToggle.IsOn = _settingsService.CurrentSettings.HideBottomOverlay;
        LaunchAtStartupToggle.IsOn = _settingsService.CurrentSettings.LaunchAtStartup;
    }

    private void OnHideTopBarToggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;

        _settingsService.CurrentSettings.HideTopBar = HideTopBarToggle.IsOn;
        _settingsService.SaveSettings();
        _onSettingsChanged?.Invoke();
    }

    private void OnHideBottomOverlayToggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;

        _settingsService.CurrentSettings.HideBottomOverlay = HideBottomOverlayToggle.IsOn;
        _settingsService.SaveSettings();
        _onSettingsChanged?.Invoke();
    }

    private void OnLaunchAtStartupToggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;

        bool enabled = LaunchAtStartupToggle.IsOn;
        _settingsService.CurrentSettings.LaunchAtStartup = enabled;
        _settingsService.SaveSettings();
        StartupManager.SetStartup(enabled);
        _onSettingsChanged?.Invoke();
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Escape to close window
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            Close();
        }
    }
}
