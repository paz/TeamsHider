using System.Runtime.InteropServices;
using System.Text;

namespace TeamsHider.Helpers;

/// <summary>
/// Win32 API interop for window enumeration and manipulation.
/// Used to detect and hide Microsoft Teams overlay windows.
/// </summary>
public static class WindowHelper
{
    /// <summary>
    /// Display affinity determines whether a window is excluded from screen capture.
    /// Teams overlays use Monitor or ExcludeFromCapture affinity.
    /// </summary>
    public enum DisplayAffinity : uint
    {
        None = 0x00,
        Monitor = 0x01,
        ExcludeFromCapture = 0x11
    }

    public const int SW_HIDE = 0;
    public const int SW_SHOW = 5;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowDisplayAffinity(IntPtr hwnd, out DisplayAffinity affinity);

    [DllImport("User32")]
    public static extern int ShowWindow(int hwnd, int nCmdShow);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder strText, int maxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    /// <summary>
    /// Callback delegate for EnumWindows.
    /// Return true to continue enumeration, false to stop.
    /// </summary>
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    /// <summary>
    /// Gets the title text of a window.
    /// </summary>
    public static string GetWindowText(IntPtr hWnd)
    {
        int size = GetWindowTextLength(hWnd);
        if (size > 0)
        {
            StringBuilder builder = new StringBuilder(size + 1);
            GetWindowText(hWnd, builder, builder.Capacity);
            return builder.ToString();
        }
        return string.Empty;
    }

    /// <summary>
    /// Gets the class name of a window.
    /// Teams windows use "TeamsWebView" class.
    /// </summary>
    public static string GetClassName(IntPtr hWnd)
    {
        StringBuilder builder = new StringBuilder(256);
        GetClassName(hWnd, builder, builder.Capacity);
        return builder.ToString();
    }
}
