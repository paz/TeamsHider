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
    /// Generates an icon at the specified size with the given color.
    /// Used for window icons and other UI elements.
    /// </summary>
    public static Icon GenerateIcon(Color color, int size)
    {
        return CreateIcon(color, size);
    }

    /// <summary>
    /// Generates a multi-size icon suitable for application use.
    /// Uses a dark color that works on both light and dark backgrounds.
    /// </summary>
    public static Icon GenerateAppIcon()
    {
        // Use a medium gray that has good contrast on both light and dark backgrounds
        return CreateIcon(Color.FromArgb(100, 100, 100), 32);
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
    /// Saves the icon to a file. Used to regenerate the application icon.
    /// Call this method once to update Assets/invisible.ico when the design changes.
    /// </summary>
    public static void SaveIconToFile(string filePath)
    {
        // Create a multi-size icon with common sizes
        // Use a color that works on both light and dark backgrounds
        var color = Color.FromArgb(120, 120, 120);

        // Generate icons at standard sizes
        int[] sizes = [16, 24, 32, 48, 64, 256];
        var icons = new List<Icon>();

        foreach (int size in sizes)
        {
            icons.Add(CreateIcon(color, size));
        }

        // Save as ICO file (using the largest size, Windows will pick appropriate one)
        using var fs = new FileStream(filePath, FileMode.Create);
        icons[^1].Save(fs); // Save the largest icon

        foreach (var icon in icons)
        {
            icon.Dispose();
        }
    }

    /// <summary>
    /// Creates a simple, bold "hide" icon (circle with diagonal slash).
    /// Designed to be clear and recognizable at small sizes.
    /// </summary>
    private static Icon CreateIcon(Color color, int size)
    {
        using var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        // Scale stroke width based on icon size
        float strokeWidth = Math.Max(2f, size / 8f);
        using var pen = new Pen(color, strokeWidth);
        pen.StartCap = LineCap.Round;
        pen.EndCap = LineCap.Round;

        // Scale padding based on icon size
        int padding = Math.Max(2, size / 8);
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
