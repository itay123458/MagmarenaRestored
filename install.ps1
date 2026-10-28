param([string]$GamePath = 'C:/Program Files (x86)/Steam/steamapps/common/Gorilla Tag')
$ErrorActionPreference = 'Stop'
if (Get-Process -Name 'Gorilla Tag' -ErrorAction SilentlyContinue) { throw 'Close Gorilla Tag before installing this mod.' }
$source = Join-Path $PSScriptRoot 'build/MagmarenaPrivate.dll'
if (!(Test-Path -LiteralPath $source)) { throw 'Build the plugin first with ./build.ps1' }
if (!(Test-Path -LiteralPath (Join-Path $GamePath 'BepInEx/core/BepInEx.dll'))) { throw 'An existing BepInEx installation is required.' }
$assembly = Join-Path $GamePath 'Gorilla Tag_Data/Managed/Assembly-CSharp.dll'
$supportedHash = 'E0C7FCB26FD064897550FCEDC21DDC43E3DF7B92204E4AEF42110BE6DC88A1C1'
if ((Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash -ne $supportedHash) { throw 'This build does not support the installed game version.' }
$folder = Join-Path $GamePath 'BepInEx/plugins/MagmarenaPrivate'
$destination = Join-Path $folder 'MagmarenaPrivate.dll'
if (Test-Path -LiteralPath $destination) {
    $backupRoot = Join-Path $env:LOCALAPPDATA 'MagmarenaPrivate/backups'
    New-Item -ItemType Directory -Force -Path $backupRoot | Out-Null
    $backupFile = Join-Path $backupRoot ((Get-Date -Format 'yyyyMMdd-HHmmss-fffffff') + '-MagmarenaPrivate.dll')
    Copy-Item -LiteralPath $destination -Destination $backupFile
    Write-Output "Previous mod backed up to $backupFile"
}
New-Item -ItemType Directory -Force -Path $folder | Out-Null
Copy-Item -LiteralPath $source -Destination $destination -Force
if ((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $destination).Hash) { throw 'Installed file hash does not match the build.' }
Write-Output "Installed: $destination"
