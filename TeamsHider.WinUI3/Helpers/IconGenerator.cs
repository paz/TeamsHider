using System.Drawing;
using System.Drawing.Drawing2D;

namespace TeamsHider.Helpers;

/// <summary>
/// Generates tray icons programmatically with theme awareness.
/// Uses a simple, bold design that works well at small sizes.
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
        return CreateIcon(iconColor, 16);
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
            return foreground.R > 128; // Light foreground = dark mode
        }
        catch
        {
            return true; // Default to dark theme
        }
    }

    /// <summary>
    /// Creates a simple, bold "hide" icon (circle with diagonal slash).
    /// Designed to be clear and recognizable at 16x16 pixels.
    /// </summary>
    private static Icon CreateIcon(Color color, int size)
    {
        using var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        float strokeWidth = 2f;
        using var pen = new Pen(color, strokeWidth);
        pen.StartCap = LineCap.Round;
        pen.EndCap = LineCap.Round;

        int padding = 2;
        int diameter = size - (padding * 2);

        // Draw circle outline
        graphics.DrawEllipse(pen, padding, padding, diameter, diameter);

        // Draw diagonal slash through the circle
        int offset = (int)(padding + strokeWidth / 2);
        graphics.DrawLine(pen,
            size - offset - 1, offset + 1,  // Top-right
            offset + 1, size - offset - 1); // Bottom-left

        IntPtr hIcon = bitmap.GetHicon();
        return Icon.FromHandle(hIcon);
    }
}
