param(
    [switch]$KeepSettings
)

$ErrorActionPreference = 'Stop'

$appName = 'Pet Dude'
$installRoot = Join-Path $env:LOCALAPPDATA 'PetDude'
$settingsRoot = Join-Path $env:APPDATA 'PetDude'
$desktopShortcut = Join-Path ([Environment]::GetFolderPath('Desktop')) "$appName.lnk"
$startMenuDir = Join-Path ([Environment]::GetFolderPath('StartMenu')) 'Programs\Pet Dude'

Stop-Process -Name 'PetDude' -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $desktopShortcut -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $startMenuDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $installRoot -Recurse -Force -ErrorAction SilentlyContinue

if (!$KeepSettings) {
    Remove-Item -LiteralPath $settingsRoot -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host 'Pet Dude uninstalled.'
if ($KeepSettings) {
    Write-Host "Settings were kept at $settingsRoot"
}
