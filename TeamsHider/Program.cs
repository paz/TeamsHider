using System.Reflection;

namespace TeamsHider
{
    static class Program
    {
        private static readonly string AppId = System.Diagnostics.Process.GetCurrentProcess().ProcessName;

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main(string[] args)
        {
            using var mutex = new Mutex(false, AppId);
            if (!mutex.WaitOne(0, false))
            {
                return;
            }

            // Create tray icon and start background monitor
            using var tray = new TrayApplicationContext();

            // Start background task to monitor Teams windows
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        var toHide = new List<(string title, WindowHelper.DisplayAffinity affinity, IntPtr hwnd)>();
                        WindowHelper.EnumWindows(delegate (IntPtr wnd, IntPtr param)
                        {
                            try
                            {
                                var containsClass = WindowHelper.GetClassName(wnd).Contains("TeamsWebView");
                                if (!containsClass) return true;
                                var wdwText = WindowHelper.GetWindowText(wnd);
                                var containsTitle = wdwText.Contains("| Microsoft Teams");
                                if (!containsTitle) return true;
                                WindowHelper.GetWindowDisplayAffinity(wnd, out var affinity);
                                toHide.Add((wdwText, affinity, wnd));
                            }
                            catch
                            {
                                // ignored - window may have closed
                            }

                            return true;

                        }, IntPtr.Zero);

                        toHide = toHide.SelectMany(x =>
                                    (x.title.Split("|").FirstOrDefault() ?? string.Empty)
                                        .Split(", ", StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim())
                                        .ToList(),
                                (x, y) => (y, x.affinity, x.hwnd))
                            .ToList();
                        const string bottomOverlayText = "Meeting compact view";

                        foreach (var list in toHide.GroupBy(x => x.title))
                        {
                            try
                            {
                                var firstItem = list.FirstOrDefault();
                                if (firstItem.title is bottomOverlayText && Config.HideBottomOverlay)
                                {
                                    var smallestItem = list.FirstOrDefault(x =>
                                        x.affinity is WindowHelper.DisplayAffinity.Monitor
                                            or WindowHelper.DisplayAffinity.ExcludeFromCapture);
                                    WindowHelper.ShowWindow((int)smallestItem.hwnd, WindowHelper.SW_HIDE);
                                    continue;
                                }

                                if (Config.HideTopBar && list.Count() > 1 &&
                                    firstItem.affinity is WindowHelper.DisplayAffinity.Monitor
                                        or WindowHelper.DisplayAffinity.ExcludeFromCapture)
                                {
                                    WindowHelper.ShowWindow((int)firstItem.hwnd, WindowHelper.SW_HIDE);
                                }
                            }
                            catch
                            {
                                // ignored - window state may have changed
                            }
                        }
                    }
                    catch
                    {
                        // ignored - continue monitoring
                    }

                    await Task.Delay(2000);
                }
            });

            // Run the application with the tray context
            Application.Run(tray);
        }
    }
}

