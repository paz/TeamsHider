using Microsoft.Win32;
using System.Reflection;

namespace TeamsHider.Services;

/// <summary>
/// Manages Windows startup registry entry for TeamsHider.
/// Uses HKCU\Software\Microsoft\Windows\CurrentVersion\Run.
/// </summary>
public static class StartupManager
{
    private const string AppName = "TeamsHider";
    private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>
    /// Sets or removes the startup registry entry.
    /// </summary>
    public static void SetStartup(bool enabled)
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
            if (key == null) return;

            if (enabled)
            {
                string? exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(AppName, $"\"{exePath}\"");
                }
            }
            else
            {
                key.DeleteValue(AppName, false);
            }
        }
        catch
        {
            // Silently fail - startup setting is optional
        }
    }

    /// <summary>
    /// Checks if TeamsHider is set to launch at Windows startup.
    /// </summary>
    public static bool IsStartupEnabled()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
            return key?.GetValue(AppName) != null;
        }
        catch
        {
            return false;
        }
    }
}
