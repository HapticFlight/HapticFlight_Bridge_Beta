[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$EngineVersion,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+-test\.\d+$')]
    [string]$ApplicationVersion
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$packageName = "DcsExtraTelemetry-$ApplicationVersion-Engine-$EngineVersion"
$distRoot = Join-Path $repositoryRoot 'dist'
$packageRoot = Join-Path $distRoot $packageName
$zipPath = Join-Path $distRoot ($packageName + '.zip')

if ((Test-Path -LiteralPath $packageRoot) -or (Test-Path -LiteralPath $zipPath)) {
    throw "Package output already exists: $packageName"
}

& (Join-Path $PSScriptRoot 'Build.ps1') -Configuration Release -ProductionOnly

foreach ($directory in @('SimHub', 'Profiles', 'Docs')) {
    New-Item -ItemType Directory -Path (Join-Path $packageRoot $directory) -Force | Out-Null
}

$pluginPath = Join-Path $repositoryRoot 'src\DcsExtraTelemetry.Plugin\bin\Release\DcsExtraTelemetry.Plugin.dll'
$pdbPath = Join-Path $repositoryRoot 'src\DcsExtraTelemetry.Plugin\bin\Release\DcsExtraTelemetry.Plugin.pdb'
Copy-Item -LiteralPath $pluginPath -Destination (Join-Path $packageRoot 'SimHub\DcsExtraTelemetry.Plugin.dll')
Copy-Item -LiteralPath $pdbPath -Destination (Join-Path $packageRoot 'SimHub\DcsExtraTelemetry.Plugin.pdb')

$profiles = @(Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'downloads\realism-test12') -Filter '*.siprofile')
if ($profiles.Count -ne 6) { throw 'Expected six Engine 1.3.0 realism test.12 profiles.' }
foreach ($profile in $profiles) {
    Copy-Item -LiteralPath $profile.FullName -Destination (Join-Path $packageRoot 'Profiles')
}

Copy-Item -LiteralPath (Join-Path $repositoryRoot 'docs\REALISM-IMPLEMENTATION-test.122.md') -Destination (Join-Path $packageRoot 'Docs\GROUND_EFFECTS.md')
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'docs\RELEASE-test.124.md') -Destination (Join-Path $packageRoot 'README.md')

[ordered]@{
    engineVersion = $EngineVersion
    applicationVersion = $ApplicationVersion
    pluginFileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($pluginPath).FileVersion
    packageKind = 'plugin-and-profiles-only'
    executableFiles = 0
    instructions = 'Stop SimHub, back up the existing project DLL/PDB, copy SimHub files, restart SimHub, import one matching profile.'
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $packageRoot 'package-info.json') -Encoding UTF8

$manifestPath = Join-Path $packageRoot 'manifest.sha256'
$manifest = Get-ChildItem -LiteralPath $packageRoot -File -Recurse | Sort-Object FullName | ForEach-Object {
    $relative = $_.FullName.Substring($packageRoot.Length + 1)
    "$(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256 | Select-Object -ExpandProperty Hash)  $relative"
}
$manifest | Set-Content -LiteralPath $manifestPath -Encoding ASCII
Compress-Archive -LiteralPath $packageRoot -DestinationPath $zipPath -CompressionLevel Optimal

Write-Host "Plugin update ZIP: $zipPath"
Write-Host "ZIP SHA-256: $((Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash)"
