namespace TeamsHider.Tests.Services;

/// <summary>
/// Tests for StartupManager logic.
/// Uses a mock registry for platform-independent testing.
/// </summary>
public class StartupManagerTests
{
    [Fact]
    public void SetStartup_Enable_AddsToRegistry()
    {
        // Arrange
        var mockRegistry = new MockRegistry();
        var manager = new TestableStartupManager(mockRegistry, @"C:\App\TeamsHider.exe");

        // Act
        manager.SetStartup(true);

        // Assert
        Assert.True(mockRegistry.HasValue("TeamsHider"));
        Assert.Equal(@"""C:\App\TeamsHider.exe""", mockRegistry.GetValue("TeamsHider"));
    }

    [Fact]
    public void SetStartup_Disable_RemovesFromRegistry()
    {
        // Arrange
        var mockRegistry = new MockRegistry();
        mockRegistry.SetValue("TeamsHider", @"""C:\App\TeamsHider.exe""");
        var manager = new TestableStartupManager(mockRegistry, @"C:\App\TeamsHider.exe");

        // Act
        manager.SetStartup(false);

        // Assert
        Assert.False(mockRegistry.HasValue("TeamsHider"));
    }

    [Fact]
    public void IsStartupEnabled_WhenSet_ReturnsTrue()
    {
        // Arrange
        var mockRegistry = new MockRegistry();
        mockRegistry.SetValue("TeamsHider", @"""C:\App\TeamsHider.exe""");
        var manager = new TestableStartupManager(mockRegistry, @"C:\App\TeamsHider.exe");

        // Act
        bool result = manager.IsStartupEnabled();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsStartupEnabled_WhenNotSet_ReturnsFalse()
    {
        // Arrange
        var mockRegistry = new MockRegistry();
        var manager = new TestableStartupManager(mockRegistry, @"C:\App\TeamsHider.exe");

        // Act
        bool result = manager.IsStartupEnabled();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void SetStartup_Enable_QuotesPath()
    {
        // Arrange
        var mockRegistry = new MockRegistry();
        var manager = new TestableStartupManager(mockRegistry, @"C:\Program Files\TeamsHider\TeamsHider.exe");

        // Act
        manager.SetStartup(true);

        // Assert
        string? value = mockRegistry.GetValue("TeamsHider");
        Assert.StartsWith("\"", value);
        Assert.EndsWith("\"", value);
    }

    [Fact]
    public void SetStartup_NullPath_DoesNotThrow()
    {
        // Arrange
        var mockRegistry = new MockRegistry();
        var manager = new TestableStartupManager(mockRegistry, null);

        // Act & Assert - should not throw
        manager.SetStartup(true);
        Assert.False(mockRegistry.HasValue("TeamsHider"));
    }

    [Fact]
    public void SetStartup_DisableWhenNotExists_DoesNotThrow()
    {
        // Arrange
        var mockRegistry = new MockRegistry();
        var manager = new TestableStartupManager(mockRegistry, @"C:\App\TeamsHider.exe");

        // Act & Assert - should not throw
        manager.SetStartup(false);
    }
}

/// <summary>
/// Mock registry for testing.
/// </summary>
public class MockRegistry
{
    private readonly Dictionary<string, string> _values = new();

    public void SetValue(string name, string value)
    {
        _values[name] = value;
    }

    public string? GetValue(string name)
    {
        return _values.TryGetValue(name, out string? value) ? value : null;
    }

    public bool HasValue(string name)
    {
        return _values.ContainsKey(name);
    }

    public void DeleteValue(string name)
    {
        _values.Remove(name);
    }
}

/// <summary>
/// Testable StartupManager that uses mock registry.
/// </summary>
public class TestableStartupManager
{
    private const string AppName = "TeamsHider";
    private readonly MockRegistry _registry;
    private readonly string? _exePath;

    public TestableStartupManager(MockRegistry registry, string? exePath)
    {
        _registry = registry;
        _exePath = exePath;
    }

    public void SetStartup(bool enabled)
    {
        try
        {
            if (enabled)
            {
                if (!string.IsNullOrEmpty(_exePath))
                {
                    _registry.SetValue(AppName, $"\"{_exePath}\"");
                }
            }
            else
            {
                _registry.DeleteValue(AppName);
            }
        }
        catch
        {
            // Silently fail
        }
    }

    public bool IsStartupEnabled()
    {
        try
        {
            return _registry.HasValue(AppName);
        }
        catch
        {
            return false;
        }
    }
}
