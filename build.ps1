param([string]$GamePath = 'C:/Program Files (x86)/Steam/steamapps/common/Gorilla Tag')
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$managed = Join-Path $GamePath 'Gorilla Tag_Data/Managed'
$core = Join-Path $GamePath 'BepInEx/core'
$output = Join-Path $PSScriptRoot 'build'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$references = @('mscorlib.dll','System.dll','System.Core.dll','netstandard.dll','UnityEngine.dll','UnityEngine.CoreModule.dll','UnityEngine.PhysicsModule.dll','Assembly-CSharp.dll','Fusion.Runtime.dll','Fusion.Common.dll','PhotonUnityNetworking.dll','PhotonRealtime.dll','Photon3Unity3D.dll') | ForEach-Object { Join-Path $managed $_ }
$references += @('BepInEx.dll','0Harmony.dll') | ForEach-Object { Join-Path $core $_ }
$references += Join-Path $managed 'Fusion.Log.dll'
$arguments = @('/nologo','/target:library','/optimize+','/warnaserror+','/nostdlib+',('/out:"' + (Join-Path $output 'MagmarenaPrivate.dll') + '"'))
foreach ($reference in $references) {
    if (!(Test-Path -LiteralPath $reference)) { throw "Missing build reference: $reference" }
    $arguments += '/reference:"' + $reference + '"'
}
$arguments += Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object { '"' + $_.FullName + '"' }
$responseFile = Join-Path $output 'compiler.rsp'
$arguments | Set-Content -LiteralPath $responseFile -Encoding UTF8
& $compiler /noconfig ('@' + $responseFile)
if ($LASTEXITCODE -ne 0) { throw 'Plugin compilation failed' }
Write-Output (Join-Path $output 'MagmarenaPrivate.dll')
