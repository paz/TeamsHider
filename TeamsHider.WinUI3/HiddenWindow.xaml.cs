using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinRT.Interop;

namespace TeamsHider;

/// <summary>
/// Hidden window that keeps the WinUI 3 app alive.
/// Required because WinUI 3 exits when all windows close.
/// </summary>
public sealed partial class HiddenWindow : Window
{
    public HiddenWindow()
    {
        InitializeComponent();

        // Make window invisible
        AppWindow.Resize(new SizeInt32(0, 0));
        AppWindow.Move(new PointInt32(-10000, -10000));

        // Remove from taskbar
        var hwnd = WindowNative.GetWindowHandle(this);
        HideFromTaskbar(hwnd);
    }

    private static void HideFromTaskbar(IntPtr hwnd)
    {
        // Set window style to tool window (doesn't appear in taskbar)
        const int GWL_EXSTYLE = -20;
        const int WS_EX_TOOLWINDOW = 0x00000080;

        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
