[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+(?:-test\.\d+)?$')]
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$packageName = "DcsExtraTelemetry-$Version"
$distRoot = Join-Path $repositoryRoot 'dist'
$packageRoot = Join-Path $distRoot $packageName
$zipPath = Join-Path $distRoot ($packageName + '.zip')

if ((Test-Path -LiteralPath $packageRoot) -or (Test-Path -LiteralPath $zipPath)) {
    throw "Package output already exists. Choose a new -Version or remove the exact old package deliberately: $packageRoot"
}

& (Join-Path $PSScriptRoot 'Build.ps1') -Configuration Release -ProductionOnly

foreach ($directory in @('DCS', 'SimHub', 'Tools', 'Scripts', 'Docs', 'Profiles')) {
    New-Item -ItemType Directory -Path (Join-Path $packageRoot $directory) -Force | Out-Null
}

$hookSourcePath = Join-Path $repositoryRoot 'dcs-hook\DCSExtraTelemetry.lua'
$hookSha256 = (Get-FileHash -LiteralPath $hookSourcePath -Algorithm SHA256).Hash
Copy-Item -LiteralPath $hookSourcePath -Destination (Join-Path $packageRoot 'DCS\DCSExtraTelemetry.lua')
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'src\DcsExtraTelemetry.Plugin\bin\Release\DcsExtraTelemetry.Plugin.dll') -Destination (Join-Path $packageRoot 'SimHub\DcsExtraTelemetry.Plugin.dll')
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'src\DcsExtraTelemetry.Plugin\bin\Release\DcsExtraTelemetry.Plugin.pdb') -Destination (Join-Path $packageRoot 'SimHub\DcsExtraTelemetry.Plugin.pdb')
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'tools\DcsExtraTelemetry.Monitor\bin\Release\DcsExtraTelemetry.Monitor.exe') -Destination (Join-Path $packageRoot 'Tools\DcsExtraTelemetry.Monitor.exe')
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'tools\DcsExtraTelemetry.Monitor\bin\Release\DcsExtraTelemetry.Monitor.pdb') -Destination (Join-Path $packageRoot 'Tools\DcsExtraTelemetry.Monitor.pdb')
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'tools\DcsExtraTelemetry.Launcher\bin\Release\DcsExtraTelemetry.Launcher.exe') -Destination (Join-Path $packageRoot 'Tools\DcsExtraTelemetry.Launcher.exe')
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'tools\DcsExtraTelemetry.Launcher\bin\Release\DcsExtraTelemetry.Launcher.pdb') -Destination (Join-Path $packageRoot 'Tools\DcsExtraTelemetry.Launcher.pdb')
Copy-Item -Path (Join-Path $repositoryRoot 'packaging\Scripts\*.ps1') -Destination (Join-Path $packageRoot 'Scripts')
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'packaging\README-FIRST.md') -Destination (Join-Path $packageRoot 'README-FIRST.md')
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'docs\PROPERTIES.md') -Destination (Join-Path $packageRoot 'Docs\PROPERTIES.md')
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'docs\REALISM_EFFECTS.md') -Destination (Join-Path $packageRoot 'Docs\REALISM_EFFECTS.md')
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'docs\REALISM-IMPLEMENTATION-test.15.md') -Destination (Join-Path $packageRoot 'Docs\REALISM-IMPLEMENTATION-test.15.md')
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'docs\REALISM-IMPLEMENTATION-test.16.md') -Destination (Join-Path $packageRoot 'Docs\REALISM-IMPLEMENTATION-test.16.md')
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'docs\REALISM-IMPLEMENTATION-test.17.md') -Destination (Join-Path $packageRoot 'Docs\REALISM-IMPLEMENTATION-test.17.md')
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'docs\REALISM-IMPLEMENTATION-test.18.md') -Destination (Join-Path $packageRoot 'Docs\REALISM-IMPLEMENTATION-test.18.md')
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'docs\REALISM-IMPLEMENTATION-test.19.md') -Destination (Join-Path $packageRoot 'Docs\REALISM-IMPLEMENTATION-test.19.md')
Copy-Item -LiteralPath (Join-Path $repositoryRoot ("docs\RELEASE-test.$($Version.Split('.')[-1]).md")) -Destination (Join-Path $packageRoot ("Docs\RELEASE-test.$($Version.Split('.')[-1]).md"))
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'profiles\README.md') -Destination (Join-Path $packageRoot 'Profiles\README.md')
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'profiles\uh60\DCS World - DCS UH60L - DCSExtra Refreshed v1.9.0.siprofile') -Destination (Join-Path $packageRoot 'Profiles\DCS World - DCS UH60L - DCSExtra Refreshed v1.9.0.siprofile')

# Bundle the latest V2 profiles at the root and every intermediate realism
# revision in labelled history folders. No generated profile is overwritten.
$latestProfileRoot = Join-Path $repositoryRoot 'downloads\realism-test19'
$groundProfiles = @(Get-ChildItem -LiteralPath $latestProfileRoot -Filter '*.siprofile')
if ($groundProfiles.Count -ne 6) { throw 'Build the six realism test.19 profiles before packaging.' }
foreach ($profile in $groundProfiles) {
    Copy-Item -LiteralPath $profile.FullName -Destination (Join-Path $packageRoot 'Profiles')
}
if ($Version -eq '0.1.0-test.131') {
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'profiles\ch47f\DCS-CH-47F-Haptic-Engine-v2.0.0-test.2.siprofile') -Destination (Join-Path $packageRoot 'Profiles')
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'profiles\ch47f\DCS-CH-47F-Haptic-Engine-v2.0.0-test.1.siprofile') -Destination (Join-Path $packageRoot 'Profiles')
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'profiles\ch47f\README.md') -Destination (Join-Path $packageRoot 'Docs\CH47F-PROFILE.md')
}
foreach ($stage in 7..18) {
    $historySource = Join-Path $repositoryRoot ("downloads\realism-test" + $stage)
    $historyDestination = Join-Path $packageRoot ("Profiles\History\test." + $stage)
    $historyProfiles = @(Get-ChildItem -LiteralPath $historySource -Filter '*.siprofile')
    if ($historyProfiles.Count -ne 6) { throw "Expected six profiles for realism test.$stage." }
    New-Item -ItemType Directory -Path $historyDestination -Force | Out-Null
    foreach ($profile in $historyProfiles) { Copy-Item -LiteralPath $profile.FullName -Destination $historyDestination }
}
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'docs\REALISM-IMPLEMENTATION-test.19.md') -Destination (Join-Path $packageRoot 'Docs\GROUND_EFFECTS.md')

[ordered]@{
    version = $Version
    repository = 'coledesigns/dcs-simhub-telemetry-export'
    hookSha256 = $hookSha256
    hapticEngineVersion = '1.7.1'
    groundProfileVersion = '2.0.0-test.19'
    ch47ProfileVersion = if ($Version -eq '0.1.0-test.131') { '2.0.0-test.2' } else { $null }
    ch47RotorSource = if ($Version -eq '0.1.0-test.131') { 'CH47EngineRpmProxy (provisional)' } else { $null }
    lastKnownWorkingVersion = '0.1.0-test.121'
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $packageRoot 'package-info.json') -Encoding UTF8

$manifestPath = Join-Path $packageRoot 'manifest.sha256'
$manifestLines = Get-ChildItem -LiteralPath $packageRoot -File -Recurse | Where-Object {
    $_.FullName -ne $manifestPath
} | Sort-Object FullName | ForEach-Object {
    $relativePath = $_.FullName.Substring($packageRoot.Length + 1)
    $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
    "$hash  $relativePath"
}
$manifestLines | Set-Content -LiteralPath $manifestPath -Encoding ASCII

Compress-Archive -LiteralPath $packageRoot -DestinationPath $zipPath -CompressionLevel Optimal
$zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash

Write-Host "Portable test bundle: $zipPath"
Write-Host "ZIP SHA-256: $zipHash"
Write-Host "Hook SHA-256: $hookSha256"
