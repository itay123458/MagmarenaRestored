$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'test.ps1')
& (Join-Path $PSScriptRoot 'build.ps1')
$release = Join-Path $PSScriptRoot 'release'
$stage = Join-Path (Join-Path $PSScriptRoot 'build') ('package-' + [Guid]::NewGuid().ToString('N'))
$plugins = Join-Path $stage 'BepInEx/plugins/MagmarenaPrivate'
New-Item -ItemType Directory -Force -Path $release,$plugins | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'build/MagmarenaPrivate.dll') -Destination (Join-Path $plugins 'MagmarenaPrivate.dll')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination (Join-Path $stage 'README.md')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE') -Destination (Join-Path $stage 'LICENSE')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'build/MagmarenaPrivate.dll') -Destination (Join-Path $release 'MagmarenaPrivate.dll') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination (Join-Path $release 'README.md') -Force
$zip = Join-Path $release 'MagmarenaPrivate-0.1.0.zip'
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force
Write-Output $zip
Get-FileHash -LiteralPath (Join-Path $release 'MagmarenaPrivate.dll') -Algorithm SHA256
