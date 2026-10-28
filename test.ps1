$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$output = Join-Path $PSScriptRoot 'build'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$sources = @((Join-Path $PSScriptRoot 'src/RoomPolicy.cs'))
$sources += Join-Path $PSScriptRoot 'src/ActivationSession.cs'
$sources += Join-Path $PSScriptRoot 'src/LifecycleActivationTarget.cs'
$sources += Join-Path $PSScriptRoot 'src/RuntimeContracts.cs'
$sources += Join-Path $PSScriptRoot 'src/ArenaActivation.cs'
$sources += Join-Path $PSScriptRoot 'src/TriangleClipper.cs'
$sources += Join-Path $PSScriptRoot 'src/CleanupSequence.cs'
$sources += Join-Path $PSScriptRoot 'src/TunnelCoordinates.cs'
$sources += Join-Path $PSScriptRoot 'src/LightmapProjection.cs'
$sources += Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'tests') -Filter '*.cs' | ForEach-Object FullName
$exe = Join-Path $output 'MagmarenaPrivate.Tests.exe'
& $compiler /nologo /target:exe /warnaserror+ "/out:$exe" @sources
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed' }
& $exe
if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
