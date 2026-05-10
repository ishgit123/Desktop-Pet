# Desktop Pets

Pixel-art desktop pet habitat for Windows.

## Install

1. Download or clone this repository.
2. Open PowerShell in the repository folder.
3. Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\Install-PetDude.ps1
```

The installer publishes a self-contained Windows build, copies it to:

```text
%LocalAppData%\PetDude\app
```

It also creates Desktop and Start Menu shortcuts, then launches the app.

If the .NET 8 SDK is not installed, the installer downloads a local SDK copy into `.dotnet` for building.

## Uninstall

```powershell
powershell -ExecutionPolicy Bypass -File .\Uninstall-PetDude.ps1
```

Keep settings while removing the app:

```powershell
powershell -ExecutionPolicy Bypass -File .\Uninstall-PetDude.ps1 -KeepSettings
```

## Build Manually

```powershell
dotnet build .\PetDude\PetDude.csproj
```

## Run From Source

```powershell
dotnet run --project .\PetDude\PetDude.csproj
```
