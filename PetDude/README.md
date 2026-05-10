# Pet Dude

A small Windows desktop pet built with C# and WPF.

## Easy install

From the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\Install-PetDude.ps1
```

This creates a self-contained Windows install under `%LocalAppData%\PetDude\app`, adds Desktop and Start Menu shortcuts, and launches the app.

To uninstall:

```powershell
powershell -ExecutionPolicy Bypass -File .\Uninstall-PetDude.ps1
```

## What is implemented

- Transparent, borderless habitat window
- Monitor targeting with relative saved position
- Right-click pet menu
- Tray icon menu for recovery and quick controls
- Lock/unlock habitat editing
- Light gray habitat outline while unlocked
- Resizable habitat with a bottom-right resize handle
- Save/reset position
- Switchable cat, dog, and robot characters
- Optional always-on-top
- Optional click-through
- Global cursor eye tracking
- Whole-pet wandering, bobbing, and tilting inside the habitat
- Blink, poke, nearby-alert, bored, and sleep states
- Idle detection through the Windows last-input API
- Fullscreen app hiding with mode selection
- Start with Windows toggle through the current-user Run registry key
- Settings stored at `%AppData%\PetDude\settings.json`

## Build

Install the .NET 8 SDK or Visual Studio 2022 with the .NET desktop development workload, then run:

```powershell
dotnet build .\PetDude.csproj
```

## Run

```powershell
dotnet run --project .\PetDude.csproj
```

Right-click the pet for settings. If click-through is enabled or the pet is hidden, use the **Pet Dude** tray icon to show it again or disable click-through.

## Publish a portable EXE

```powershell
dotnet publish .\PetDude.csproj -c Release -r win-x64 --self-contained false
```

The output will be under `bin\Release\net8.0-windows\win-x64\publish`.

For a self-contained build that does not require a preinstalled .NET runtime:

```powershell
dotnet publish .\PetDude.csproj -c Release -r win-x64 --self-contained true
```

## Notes

The MVP uses WPF vector shapes instead of PNG sprites. That keeps the first version small and easy to iterate on. Sprite skins can be added later by replacing the drawing in `MainWindow.xaml` or by introducing an asset-based pet control.
