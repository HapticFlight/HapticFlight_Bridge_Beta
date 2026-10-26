[CmdletBinding()]
param(
    [string]$OutputDirectory,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = Join-Path $repositoryRoot 'downloads\ah6-ship' }
$outputRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
$packageName = 'AH6-v1.1-Engine-1.8.0-Update'
$packageRoot = Join-Path $outputRoot $packageName
$zipPath = Join-Path $outputRoot ($packageName + '.zip')

if ((Test-Path -LiteralPath $packageRoot) -or (Test-Path -LiteralPath $zipPath)) {
    throw "Package output already exists: $packageRoot"
}

if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot 'Build.ps1') -Configuration Release -ProductionOnly
}
foreach ($directory in @('DCS', 'SimHub', 'Profiles', 'Docs')) {
    New-Item -ItemType Directory -Path (Join-Path $packageRoot $directory) -Force | Out-Null
}

$files = @{
    (Join-Path $repositoryRoot 'dcs-hook\DCSExtraTelemetry.lua') = (Join-Path $packageRoot 'DCS\DCSExtraTelemetry.lua')
    (Join-Path $repositoryRoot 'src\DcsExtraTelemetry.Plugin\bin\Release\DcsExtraTelemetry.Plugin.dll') = (Join-Path $packageRoot 'SimHub\DcsExtraTelemetry.Plugin.dll')
    (Join-Path $repositoryRoot 'src\DcsExtraTelemetry.Plugin\bin\Release\DcsExtraTelemetry.Plugin.pdb') = (Join-Path $packageRoot 'SimHub\DcsExtraTelemetry.Plugin.pdb')
    (Join-Path $repositoryRoot 'downloads\ah6-ship\DCS-AH-6J-Haptic-Engine-v2.0.0-AH6-v1.1-Calibrated.siprofile') = (Join-Path $packageRoot 'Profiles\DCS-AH-6J-Haptic-Engine-v2.0.0-AH6-v1.1-Calibrated.siprofile')
    (Join-Path $repositoryRoot 'docs\AH6-v1.1-SHIP-CALIBRATION.md') = (Join-Path $packageRoot 'Docs\AH6-v1.1-SHIP-CALIBRATION.md')
}
foreach ($entry in $files.GetEnumerator()) {
    if (-not (Test-Path -LiteralPath $entry.Key -PathType Leaf)) { throw "Required package file is missing: $($entry.Key)" }
    Copy-Item -LiteralPath $entry.Key -Destination $entry.Value
}

$manifestPath = Join-Path $packageRoot 'manifest.sha256'
$manifest = Get-ChildItem -LiteralPath $packageRoot -File -Recurse | Sort-Object FullName | ForEach-Object {
    $relative = $_.FullName.Substring($packageRoot.Length + 1)
    "$(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256 | Select-Object -ExpandProperty Hash)  $relative"
}
$manifest | Set-Content -LiteralPath $manifestPath -Encoding ASCII
Compress-Archive -LiteralPath $packageRoot -DestinationPath $zipPath -CompressionLevel Optimal
Write-Host "AH-6J component update: $zipPath"
Write-Host "SHA-256: $((Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash)"
