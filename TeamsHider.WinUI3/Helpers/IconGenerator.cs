using System.Drawing;
using System.Drawing.Drawing2D;
using Microsoft.UI.Xaml;

namespace TeamsHider.Helpers;

/// <summary>
/// Generates tray icons programmatically with theme awareness.
/// Creates a simple "eye with slash" icon representing "hide".
/// </summary>
public static class IconGenerator
{
    /// <summary>
    /// Generates an icon suitable for the current system theme.
    /// </summary>
    public static Icon GenerateIcon(bool isDarkTheme)
    {
        // Icon color: white for dark theme, dark gray for light theme
        Color iconColor = isDarkTheme ? Color.White : Color.FromArgb(30, 30, 30);

        return CreateHideIcon(iconColor, 16);
    }

    /// <summary>
    /// Detects if the system is using dark theme.
    /// </summary>
    public static bool IsSystemDarkTheme()
    {
        try
        {
            var uiSettings = new Windows.UI.ViewManagement.UISettings();
            var foreground = uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Foreground);

            // If foreground is light, we're in dark mode
            return foreground.R > 128;
        }
        catch
        {
            return true; // Default to dark theme (most common for taskbar)
        }
    }

    /// <summary>
    /// Creates an "eye with slash" icon representing hiding.
    /// </summary>
    private static Icon CreateHideIcon(Color color, int size)
    {
        using var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        using var pen = new Pen(color, 1.5f);
        using var brush = new SolidBrush(color);

        int padding = 2;
        int eyeWidth = size - (padding * 2);
        int eyeHeight = (int)(eyeWidth * 0.5);
        int eyeTop = (size - eyeHeight) / 2;

        // Draw eye outline (almond shape)
        var eyePath = new GraphicsPath();
        eyePath.AddArc(padding, eyeTop, eyeWidth, eyeHeight * 2, 180, 180);
        eyePath.AddArc(padding, eyeTop - eyeHeight, eyeWidth, eyeHeight * 2, 0, 180);
        eyePath.CloseFigure();
        graphics.DrawPath(pen, eyePath);

        // Draw pupil (small circle in center)
        int pupilSize = 4;
        int pupilX = (size - pupilSize) / 2;
        int pupilY = (size - pupilSize) / 2;
        graphics.FillEllipse(brush, pupilX, pupilY, pupilSize, pupilSize);

        // Draw diagonal slash through the eye
        pen.Width = 2f;
        graphics.DrawLine(pen, size - padding - 1, padding + 1, padding + 1, size - padding - 1);

        // Convert bitmap to icon
        IntPtr hIcon = bitmap.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    /// <summary>
    /// Creates a simple circle icon with the specified color.
    /// Fallback if the eye icon doesn't render well.
    /// </summary>
    public static Icon CreateSimpleIcon(Color color, int size = 16)
    {
        using var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        using var brush = new SolidBrush(color);
        using var pen = new Pen(color, 1.5f);

        // Draw a filled circle with outline
        int padding = 2;
        graphics.FillEllipse(brush, padding, padding, size - padding * 2, size - padding * 2);

        IntPtr hIcon = bitmap.GetHicon();
        return Icon.FromHandle(hIcon);
    }
}
