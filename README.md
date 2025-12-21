# TeamsHider

A very simple tool to hide the overlays shown by teams during calls.

![overlays](https://github.com/mroter93/TeamsHider/assets/156033398/31047bbd-0da0-4779-affd-c1aa927f4524)

## Download

The latest version can be found here: https://github.com/mroter93/TeamsHider/releases (Release.zip)

## Config

### v5 (WinUI 3)
Right-click the tray icon and select "Settings..." to open the built-in settings window. Changes are saved automatically.

You can also toggle settings directly from the tray menu using the quick toggle items.

### v4 (Legacy)
Open settings.conf to configure TeamsHider

![image](https://github.com/mroter93/TeamsHider/assets/156033398/ba7a2b8b-6468-4862-b14d-bffc02eb10e4)

**HideTopBar**
Set to true to hide the bar which appears as soon as you share your screen.

**HideBottomOverlay**
Hide the bottom right overlay which displays as soon as you minimize an active call window.

## Tray

A tray icon is shown while running the tool. Right-click to access the menu:
- Quick toggles for Hide Top Bar and Hide Bottom Overlay
- Settings window
- About (opens GitHub)
- Quit

## Autostart

### v5 (WinUI 3)
Enable "Launch at Windows startup" in the Settings window.

### v4 (Legacy)
Hit Windows + R and enter shell:startup.
Place a shortcut to the TeamsHider.exe in the previously opened folder.

## Dependencies

**v5 (WinUI 3)**: Self-contained, no additional runtime needed.

**v4 (Legacy)**: Requires .NET 8 Desktop Runtime. Download it here:
https://dotnet.microsoft.com/en-us/download/dotnet/8.0 (.NET Desktop Runtime 8.0.1)

## Video Showcase

Head over to YouTube for a short video:  
https://youtu.be/xbHCp9kb0vc

## Credits

Thanks to flaticon for the icon (https://www.flaticon.com/free-icons/hidden)

## Changelog

**v5.0.0**
- Modernized UI framework from Windows Forms to WinUI 3
- Added built-in Settings window with Fluent Design
- Added quick toggle menu items in tray context menu
- Added "Launch at Windows startup" setting
- Automatic migration of legacy settings.conf to new format
- Self-contained executable (no runtime install needed)
- Preserved all existing window detection and hiding logic

**v.4**
Adjusted to internal changes in Teams to fix https://github.com/mroter93/TeamsHider/issues/3.
Also from now on the release will be self-contained (https://github.com/mroter93/TeamsHider/issues/1)

**v.3**  
Use GetWindowDisplayAffinity to make checking the window title and GetWindows obsolete aswell hiding the right window 100% reliable

**v.2**  
In conference with multiple people it is possible that the overlay name doesnt match the call window title exactly. Implemented a way to also work around this.

**v.1**  
Initial release

