# TeamsHider - Claude Development Guide

## Project Overview

**TeamsHider** is a lightweight Windows utility that hides Microsoft Teams overlay windows during calls and screen sharing.

**Type**: System tray utility  
**Framework**: .NET 8 (Windows-specific)  
**UI**: Windows Forms (legacy) → Target: WinUI 3  
**Deployment**: Self-contained executable  
**License**: MIT

## Architecture

### Core Components

1. **Window Detection & Hiding** (`WindowHelper.cs`)
   - Win32 API interop for window enumeration
   - Uses `GetWindowDisplayAffinity` to identify Teams overlay windows
   - Filters by class name `TeamsWebView` and title patterns
   - Hides windows using `ShowWindow(SW_HIDE)`

2. **Background Monitor** (`Program.cs`)
   - 2-second polling loop for Teams windows
   - Single-instance enforcement via Mutex
   - Groups windows by title to handle multi-participant calls
   - Identifies overlays by `DisplayAffinity` property

3. **System Tray** (`TrayApplicationContext.cs`)
   - Persistent tray icon
   - Context menu: About, Quit
   - No main window (runs entirely in background)

4. **Configuration** (`Config.cs`, `settings.conf`)
   - JSON-based settings file
   - Two toggles: `HideTopBar`, `HideBottomOverlay`
   - Currently external file, no runtime editing

### Window Identification Logic

```
Teams windows with class "TeamsWebView" + title "| Microsoft Teams"
├─ DisplayAffinity = Monitor/ExcludeFromCapture → Overlay window
├─ Title contains "Meeting compact view" → Bottom overlay
└─ Multiple windows with same participant names → Top bar (when sharing)
```

## Development Guidelines

### Code Quality Standards

**CRITICAL - Maintain These Properties:**
- **Auditability**: Code must be easily reviewable for security/privacy
- **Minimal footprint**: Single executable, no installer, <10MB
- **Low resource usage**: Polling every 2 seconds, minimal CPU/memory
- **No telemetry**: Zero network calls, zero data collection
- **Reliability**: Handle Teams updates gracefully

### Design Principles

1. **Single Responsibility**: Each file has one clear purpose
2. **Defensive Coding**: Try-catch around window enumeration (Teams windows can close mid-enumeration)
3. **No Assumptions**: Teams window structure can change - rely on documented APIs
4. **Backwards Compatibility**: Settings migration for existing users

### Testing Strategy

Since this is a UI automation tool:
- **Manual testing primary**: Run with Teams in different states
- **Test scenarios**:
  - 1:1 call, minimize call window → Bottom overlay should hide
  - Multi-participant call with screen share → Top bar should hide
  - Rapid window creation/destruction → No crashes
  - Config changes → Should apply on next polling cycle
- **Edge cases**:
  - Teams not running
  - Multiple Teams instances
  - Teams window class name changes (past issue #3)

## Common Development Tasks

### Adding New Overlay Detection

```csharp
// In Program.cs polling loop
var containsClass = WindowHelper.GetClassName(wnd).Contains("NewTeamsClass");
var containsTitle = wdwText.Contains("New Overlay Pattern");
if (containsClass && containsTitle) {
    WindowHelper.ShowWindow((int)wnd, WindowHelper.SW_HIDE);
}
```

### Adding Configuration Options

1. Add property to `Config.cs`:
   ```csharp
   public static bool HideNewFeature => (bool)JsonConfig.Config.Global.HideNewFeature;
   ```

2. Add to `settings.conf`:
   ```json
   {
       HideTopBar: true,
       HideBottomOverlay: true,
       HideNewFeature: false
   }
   ```

3. Apply in polling loop:
   ```csharp
   if (Config.HideNewFeature && /* condition */) {
       WindowHelper.ShowWindow((int)hwnd, WindowHelper.SW_HIDE);
   }
   ```

### Debugging Window Detection

Add diagnostic logging to polling loop:
```csharp
Console.WriteLine($"Found window: Class={WindowHelper.GetClassName(wnd)}, Title={wdwText}, Affinity={affinity}");
```

Run from command line to see console output.

## Dependencies

### NuGet Packages
- **JsonConfig** (1.0.0): Simple JSON configuration reader
  - Lightweight, no schema validation
  - Direct property access via dynamic objects

### Win32 APIs Used
- `EnumWindows`: Enumerate all top-level windows
- `GetWindowText`: Get window title
- `GetClassName`: Get window class name
- `ShowWindow`: Show/hide windows
- `GetWindowDisplayAffinity`: Check if window excluded from screen capture
- `IsIconic`: Check if window is minimized (unused in current code)

## Known Issues & Quirks

### Issue #3: Teams Internal Changes
- **Problem**: Teams changed internal window structure between versions
- **Solution**: Rely on `GetWindowDisplayAffinity` rather than title matching
- **Lesson**: Use most stable APIs available, avoid string matching when possible

### Window Title Parsing
```csharp
// Teams titles: "Person1, Person2, Person3 | Microsoft Teams"
// Must handle: ", " as separator, trim whitespace, handle empty segments
toHide = toHide.SelectMany(x =>
    x.title.Split("|").FirstOrDefault()
        .Split(", ", StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim())
```

### Bottom Overlay Detection
```csharp
const string bottomOverlayText = "Meeting compact view";
// This string is hardcoded - brittle if Teams changes it
// Consider: Multiple known patterns, or affinity-only detection
```

## Modernization Plan

### Current State: Windows Forms
- Legacy UI framework (2001)
- Limited styling options
- System tray only, no settings window
- External JSON configuration

### Target State: WinUI 3
- Modern XAML-based UI
- Fluent Design System
- Built-in settings window
- In-memory configuration with persistence

### Migration Strategy

**Phase 1: Settings Window**
- Add WinUI 3 settings window (opened from tray menu)
- Keep polling logic identical
- Load settings.conf on startup (backwards compat)
- Save changes back to JSON + in-memory

**Phase 2: Modern Tray Integration**
- Use WinUI 3 tray APIs (cleaner than WinForms NotifyIcon)
- Modernize context menu styling
- Add quick toggles in tray menu

**Phase 3: Polish**
- About window with version info, GitHub link
- Auto-update checking (optional)
- Settings import/export

### Backwards Compatibility Requirements

1. **Settings Migration**:
   ```csharp
   // On first run with new version
   if (File.Exists("settings.conf")) {
       var legacy = JsonConfig.Config.Global;
       modernSettings.HideTopBar = (bool)legacy.HideTopBar;
       modernSettings.HideBottomOverlay = (bool)legacy.HideBottomOverlay;
       modernSettings.Save();
   }
   ```

2. **Same Installation Path**:
   - Keep `TeamsHider.exe` name
   - Support running from same directory
   - No registry changes required

3. **Exit Code Compatibility**:
   - Single instance mutex still blocks duplicates
   - Silent startup (no splash screen)

## Build & Release

### Local Development
```bash
dotnet build --configuration Debug
dotnet run --project TeamsHider
```

### Release Build (GitHub Actions)
```bash
dotnet publish --configuration Release --self-contained true -r win-x64 -o ./publish
```

**Key flags**:
- `--self-contained`: Bundle .NET runtime (no dependency install)
- `-r win-x64`: Windows 64-bit only
- Outputs ~60MB (includes entire .NET 8 runtime)

### Version Tagging
```bash
git tag v5
git push origin v5
# GitHub Actions auto-builds and creates release
```

## Performance Characteristics

### Resource Usage
- **CPU**: <1% (2-second sleep, minimal window enumeration)
- **Memory**: ~15-20MB (self-contained runtime)
- **Disk I/O**: Only on startup (read settings.conf)
- **Network**: None

### Polling Frequency Trade-off
```csharp
await Task.Delay(2000); // Current: 2 seconds
```
- **Faster**: More responsive, higher CPU usage
- **Slower**: Lower CPU, overlays visible longer
- **2 seconds**: Good balance for typical use

## Security Considerations

### Why This is Safe to Run
1. **No network access**: Zero HTTP/HTTPS calls
2. **No file writes**: Only reads settings.conf
3. **No registry access**: Portable application
4. **Limited Win32 API**: Only window enumeration/hiding
5. **Open source**: Full code audit possible

### Potential Concerns
- **Window hiding**: Could theoretically hide any window
  - Mitigated: Only targets windows matching specific patterns
  - Mitigated: No privilege escalation needed
- **Single instance mutex**: Could be abused for persistence detection
  - Mitigated: Uses process name, easily bypassed

## Troubleshooting

### "Overlays Still Showing"
- Check `settings.conf` exists and is valid JSON
- Verify Teams window class is still `TeamsWebView`
- Check if Teams updated (may need code changes)

### "TeamsHider Won't Start"
- Already running? Check Task Manager
- .NET 8 Desktop Runtime installed? (if not self-contained)
- Icon file `invisible.ico` missing?

### "High CPU Usage"
- Check for rapid window creation/destruction
- Consider increasing polling delay
- Profile with Performance Monitor

## Contributing Guidelines

### Pull Request Checklist
- [ ] Code is easily auditable (clear logic, comments for "why")
- [ ] No new dependencies without justification
- [ ] Tested with Teams stable and insider builds
- [ ] No breaking changes to settings.conf format
- [ ] Updated README.md if user-facing changes
- [ ] No telemetry or network calls added

### Code Style
- Follow existing C# conventions
- Use explicit types over `var` for clarity
- Try-catch only where necessary (window enumeration)
- Comments explain *why*, not *what*

## Future Enhancements

### High Priority
- [ ] Settings UI (WinUI 3)
- [ ] Per-monitor DPI awareness
- [ ] Keyboard shortcuts for quick toggle

### Medium Priority  
- [ ] Multiple profile support (different settings per Teams account)
- [ ] Activity log (which overlays were hidden when)
- [ ] Teams detection without polling (event-based)

### Low Priority
- [ ] Support for Teams for Linux (different architecture)
- [ ] Whitelist specific meeting types
- [ ] Integration with Windows Focus Assist

## References

### Microsoft Documentation
- [WinUI 3 Overview](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/)
- [ShowWindow API](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-showwindow)
- [GetWindowDisplayAffinity API](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowdisplayaffinity)

### Related Projects
- [Teams Auto Muter](https://github.com/lewisvrobinson/teams-auto-muter) - Similar concept, audio focus
- [PowerToys](https://github.com/microsoft/PowerToys) - Reference for system tray utilities

### Teams Window Structure
- Class: `TeamsWebView` (Chromium Embedded Framework)
- Title pattern: `[Participants] | Microsoft Teams`
- Display affinity: `ExcludeFromCapture` (0x11) or `Monitor` (0x01) for overlays
- Compact view title: `Meeting compact view`
