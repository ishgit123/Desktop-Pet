param(
    [switch]$NoDesktopShortcut,
    [switch]$NoStartMenuShortcut,
    [switch]$NoLaunch
)

$ErrorActionPreference = 'Stop'

$appName = 'Pet Dude'
$processName = 'PetDude'
$repoRoot = $PSScriptRoot
$projectPath = Join-Path $repoRoot 'PetDude\PetDude.csproj'
$publishPath = Join-Path $repoRoot 'artifacts\publish'
$installRoot = Join-Path $env:LOCALAPPDATA 'PetDude'
$installPath = Join-Path $installRoot 'app'
$exePath = Join-Path $installPath 'PetDude.exe'
$localDotnetDir = Join-Path $repoRoot '.dotnet'
$localDotnet = Join-Path $localDotnetDir 'dotnet.exe'

function Get-Dotnet {
    $globalDotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($globalDotnet) {
        $sdks = & $globalDotnet.Source --list-sdks
        if ($LASTEXITCODE -eq 0 -and ($sdks -match '^8\.')) {
            return $globalDotnet.Source
        }
    }

    if (Test-Path $localDotnet) {
        $sdks = & $localDotnet --list-sdks
        if ($LASTEXITCODE -eq 0 -and ($sdks -match '^8\.')) {
            return $localDotnet
        }
    }

    Write-Host 'Installing local .NET 8 SDK for build...'
    New-Item -ItemType Directory -Force -Path $localDotnetDir | Out-Null
    $installer = Join-Path $env:TEMP 'petdude-dotnet-install.ps1'
    Invoke-WebRequest 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer
    & powershell -NoProfile -ExecutionPolicy Bypass -File $installer -Channel 8.0 -InstallDir $localDotnetDir -NoPath

    if (!(Test-Path $localDotnet)) {
        throw 'Could not install the .NET 8 SDK. Install .NET 8 SDK manually, then run this script again.'
    }

    return $localDotnet
}

function New-Shortcut($path, $target, $workingDirectory, $description) {
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($path)
    $shortcut.TargetPath = $target
    $shortcut.WorkingDirectory = $workingDirectory
    $shortcut.Description = $description
    $shortcut.Save()
}

if (!(Test-Path $projectPath)) {
    throw "Could not find project file at $projectPath"
}

$dotnet = Get-Dotnet

Write-Host 'Publishing Pet Dude...'
Remove-Item -LiteralPath $publishPath -Recurse -Force -ErrorAction SilentlyContinue
& $dotnet publish $projectPath -c Release -r win-x64 --self-contained true -o $publishPath
if ($LASTEXITCODE -ne 0) {
    throw 'Publish failed.'
}

Write-Host "Installing to $installPath..."
Stop-Process -Name $processName -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $installRoot | Out-Null
Remove-Item -LiteralPath $installPath -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $installPath | Out-Null
Copy-Item -Path (Join-Path $publishPath '*') -Destination $installPath -Recurse -Force

if (!$NoDesktopShortcut) {
    $desktopShortcut = Join-Path ([Environment]::GetFolderPath('Desktop')) "$appName.lnk"
    New-Shortcut $desktopShortcut $exePath $installPath 'Launch Pet Dude'
}

if (!$NoStartMenuShortcut) {
    $startMenuDir = Join-Path ([Environment]::GetFolderPath('StartMenu')) 'Programs\Pet Dude'
    New-Item -ItemType Directory -Force -Path $startMenuDir | Out-Null
    New-Shortcut (Join-Path $startMenuDir "$appName.lnk") $exePath $installPath 'Launch Pet Dude'
}

if (!$NoLaunch) {
    Start-Process -FilePath $exePath
}

Write-Host ''
Write-Host 'Pet Dude installed successfully.'
Write-Host "App files: $installPath"
Write-Host 'Settings are stored under %AppData%\PetDude.'
