using System.Runtime.InteropServices;

namespace TeamsHider;

/// <summary>
/// Debug logging helper. Enabled with --debug command line flag.
/// </summary>
public static class Debug
{
    public static bool IsEnabled { get; private set; }

    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int dwProcessId);

    public static void Initialize(string[] args)
    {
        IsEnabled = args.Contains("--debug", StringComparer.OrdinalIgnoreCase);

        if (IsEnabled)
        {
            // Try to attach to parent console (if launched from cmd/powershell)
            // Otherwise allocate a new console window
            if (!AttachConsole(-1))
            {
                AllocConsole();
            }

            Console.WriteLine();
            Console.WriteLine("=== TeamsHider Debug Mode ===");
            Console.WriteLine($"Started at: {DateTime.Now}");
            Console.WriteLine($"Args: {string.Join(" ", args)}");
            Console.WriteLine();
        }
    }

    public static void Log(string message)
    {
        if (IsEnabled)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
        }
    }

    public static void Log(string category, string message)
    {
        if (IsEnabled)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [{category}] {message}");
        }
    }

    public static void LogError(string message, Exception? ex = null)
    {
        if (IsEnabled)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [ERROR] {message}");
            if (ex != null)
            {
                Console.WriteLine($"  Exception: {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine($"  Stack: {ex.StackTrace}");
            }
        }
    }
}
