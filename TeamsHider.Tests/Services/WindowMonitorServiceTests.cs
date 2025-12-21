namespace TeamsHider.Tests.Services;

/// <summary>
/// Tests for WindowMonitorService logic.
/// Focuses on testable components like title parsing.
/// </summary>
public class WindowMonitorServiceTests
{
    [Theory]
    [InlineData("John Doe | Microsoft Teams", new[] { "John Doe" })]
    [InlineData("John Doe, Jane Smith | Microsoft Teams", new[] { "John Doe", "Jane Smith" })]
    [InlineData("Alice, Bob, Charlie | Microsoft Teams", new[] { "Alice", "Bob", "Charlie" })]
    [InlineData("Meeting compact view | Microsoft Teams", new[] { "Meeting compact view" })]
    public void ParseTeamsTitle_ExtractsParticipantNames(string title, string[] expectedNames)
    {
        // Act
        List<string> names = TitleParser.ParseParticipants(title);

        // Assert
        Assert.Equal(expectedNames.Length, names.Count);
        for (int i = 0; i < expectedNames.Length; i++)
        {
            Assert.Equal(expectedNames[i], names[i]);
        }
    }

    [Fact]
    public void ParseTeamsTitle_HandlesExtraSpaces()
    {
        // Arrange
        string title = "  John Doe  ,  Jane Smith  | Microsoft Teams";

        // Act
        List<string> names = TitleParser.ParseParticipants(title);

        // Assert
        Assert.Equal(2, names.Count);
        Assert.Equal("John Doe", names[0]);
        Assert.Equal("Jane Smith", names[1]);
    }

    [Fact]
    public void ParseTeamsTitle_EmptyTitle_ReturnsEmpty()
    {
        // Act
        List<string> names = TitleParser.ParseParticipants("");

        // Assert
        Assert.Empty(names);
    }

    [Fact]
    public void ParseTeamsTitle_NoPipe_ReturnsFullTitle()
    {
        // Arrange
        string title = "Some Window Title";

        // Act
        List<string> names = TitleParser.ParseParticipants(title);

        // Assert
        Assert.Single(names);
        Assert.Equal("Some Window Title", names[0]);
    }

    [Theory]
    [InlineData("TeamsWebView", true)]
    [InlineData("Chrome_WidgetWin_1", false)]
    [InlineData("ApplicationFrameWindow", false)]
    [InlineData("", false)]
    public void IsTeamsWindowClass_IdentifiesCorrectly(string className, bool expected)
    {
        // Act
        bool result = WindowClassifier.IsTeamsWindowClass(className);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("John Doe | Microsoft Teams", true)]
    [InlineData("Some Chat | Microsoft Teams", true)]
    [InlineData("Microsoft Teams", false)]
    [InlineData("| Microsoft Teams", true)]
    [InlineData("Random Window", false)]
    public void IsTeamsWindowTitle_IdentifiesCorrectly(string title, bool expected)
    {
        // Act
        bool result = WindowClassifier.IsTeamsWindowTitle(title);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsBottomOverlay_IdentifiesCompactView()
    {
        // Arrange
        string title = "Meeting compact view";

        // Act
        bool result = WindowClassifier.IsBottomOverlay(title);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("John Doe", false)]
    [InlineData("Meeting", false)]
    [InlineData("compact view", false)]
    public void IsBottomOverlay_RejectsOtherTitles(string title, bool expected)
    {
        // Act
        bool result = WindowClassifier.IsBottomOverlay(title);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void WindowGrouping_MultipleWindowsSameName_ShouldHideTopBar()
    {
        // Arrange - Simulate multiple windows with same participant name
        var windows = new List<(string name, int affinity)>
        {
            ("John Doe", 1), // Monitor affinity
            ("John Doe", 0), // No affinity
        };

        // Act
        bool shouldHide = HidingLogic.ShouldHideTopBar(windows, hideTopBarEnabled: true);

        // Assert
        Assert.True(shouldHide);
    }

    [Fact]
    public void WindowGrouping_SingleWindow_ShouldNotHideTopBar()
    {
        // Arrange - Single window
        var windows = new List<(string name, int affinity)>
        {
            ("John Doe", 1),
        };

        // Act
        bool shouldHide = HidingLogic.ShouldHideTopBar(windows, hideTopBarEnabled: true);

        // Assert
        Assert.False(shouldHide);
    }

    [Fact]
    public void WindowGrouping_DisabledSetting_ShouldNotHide()
    {
        // Arrange
        var windows = new List<(string name, int affinity)>
        {
            ("John Doe", 1),
            ("John Doe", 0),
        };

        // Act
        bool shouldHide = HidingLogic.ShouldHideTopBar(windows, hideTopBarEnabled: false);

        // Assert
        Assert.False(shouldHide);
    }
}

/// <summary>
/// Helper class for parsing Teams window titles.
/// </summary>
public static class TitleParser
{
    public static List<string> ParseParticipants(string title)
    {
        if (string.IsNullOrEmpty(title))
            return new List<string>();

        string? beforePipe = title.Split('|').FirstOrDefault();
        if (string.IsNullOrEmpty(beforePipe))
            return new List<string> { title };

        return beforePipe
            .Split(", ", StringSplitOptions.RemoveEmptyEntries)
            .Select(name => name.Trim())
            .Where(name => !string.IsNullOrEmpty(name))
            .ToList();
    }
}

/// <summary>
/// Helper class for classifying windows.
/// </summary>
public static class WindowClassifier
{
    private const string TeamsWindowClass = "TeamsWebView";
    private const string TeamsWindowTitle = "| Microsoft Teams";
    private const string BottomOverlayText = "Meeting compact view";

    public static bool IsTeamsWindowClass(string className)
    {
        return !string.IsNullOrEmpty(className) && className.Contains(TeamsWindowClass);
    }

    public static bool IsTeamsWindowTitle(string title)
    {
        return !string.IsNullOrEmpty(title) && title.Contains(TeamsWindowTitle);
    }

    public static bool IsBottomOverlay(string title)
    {
        return title == BottomOverlayText;
    }
}

/// <summary>
/// Helper class for hiding logic decisions.
/// </summary>
public static class HidingLogic
{
    public static bool ShouldHideTopBar(List<(string name, int affinity)> windows, bool hideTopBarEnabled)
    {
        if (!hideTopBarEnabled)
            return false;

        if (windows.Count <= 1)
            return false;

        // Check if first window has overlay affinity (Monitor=1 or ExcludeFromCapture=0x11)
        var first = windows.FirstOrDefault();
        return first.affinity == 1 || first.affinity == 0x11;
    }

    public static bool ShouldHideBottomOverlay(string title, bool hideBottomOverlayEnabled)
    {
        return hideBottomOverlayEnabled && title == "Meeting compact view";
    }
}
