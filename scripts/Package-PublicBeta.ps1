[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+(?:-test\.\d+)?$')]
    [string]$Version = '0.1.0-test.140',

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$')]
    [string]$Repository,

    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$packageName = "DcsExtraTelemetry-$Version"

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot 'dist\public-beta'
}

$outputRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
$packageRoot = Join-Path $outputRoot $packageName
$zipPath = Join-Path $outputRoot ($packageName + '.zip')
$zipHashPath = $zipPath + '.sha256'

foreach ($path in @($packageRoot, $zipPath, $zipHashPath)) {
    if (Test-Path -LiteralPath $path) {
        throw "Public-beta output already exists: $path. Choose a new output directory or version."
    }
}

& (Join-Path $PSScriptRoot 'Build.ps1') -Configuration Release -ProductionOnly

$fileVersion = if ($Version -match '^([0-9]+\.[0-9]+\.[0-9]+)-test\.([0-9]+)$') {
    "$($Matches[1]).$($Matches[2])"
} else { "$Version.0" }
foreach ($binary in @(
    'src\DcsExtraTelemetry.Plugin\bin\Release\DcsExtraTelemetry.Plugin.dll',
    'tools\DcsExtraTelemetry.Monitor\bin\Release\DcsExtraTelemetry.Monitor.exe',
    'tools\DcsExtraTelemetry.Launcher\bin\Release\DcsExtraTelemetry.Launcher.exe'
)) {
    $details = (Get-Item -LiteralPath (Join-Path $repositoryRoot $binary)).VersionInfo
    if ($details.ProductVersion -cne $Version -or $details.FileVersion -cne $fileVersion) {
        throw "Release blocked: $binary reports product $($details.ProductVersion), file $($details.FileVersion); expected $Version and $fileVersion."
    }
}

foreach ($directory in @('DCS', 'SimHub', 'Tools', 'Scripts', 'Docs', 'Profiles')) {
    New-Item -ItemType Directory -Path (Join-Path $packageRoot $directory) -Force | Out-Null
}

function Copy-PackageFile {
    param([string]$Source, [string]$Destination)
    if (-not (Test-Path -LiteralPath $Source -PathType Leaf)) {
        throw "Required package file is missing: $Source"
    }
    Copy-Item -LiteralPath $Source -Destination $Destination
}

$hookPath = Join-Path $repositoryRoot 'dcs-hook\DCSExtraTelemetry.lua'
$pluginPath = Join-Path $repositoryRoot 'src\DcsExtraTelemetry.Plugin\bin\Release\DcsExtraTelemetry.Plugin.dll'
$monitorPath = Join-Path $repositoryRoot 'tools\DcsExtraTelemetry.Monitor\bin\Release\DcsExtraTelemetry.Monitor.exe'
$launcherPath = Join-Path $repositoryRoot 'tools\DcsExtraTelemetry.Launcher\bin\Release\DcsExtraTelemetry.Launcher.exe'

Copy-PackageFile $hookPath (Join-Path $packageRoot 'DCS\DCSExtraTelemetry.lua')
Copy-PackageFile $pluginPath (Join-Path $packageRoot 'SimHub\DcsExtraTelemetry.Plugin.dll')
Copy-PackageFile $monitorPath (Join-Path $packageRoot 'Tools\DcsExtraTelemetry.Monitor.exe')
Copy-PackageFile $launcherPath (Join-Path $packageRoot 'Tools\DcsExtraTelemetry.Launcher.exe')
Copy-PackageFile (Join-Path $repositoryRoot 'packaging\PUBLIC-BETA-README.md') (Join-Path $packageRoot 'README-FIRST.md')
Copy-PackageFile (Join-Path $repositoryRoot 'packaging\PUBLIC-BETA-PROFILES.md') (Join-Path $packageRoot 'Profiles\README.md')

Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'packaging\Scripts') -Filter '*.ps1' -File |
    ForEach-Object { Copy-PackageFile $_.FullName (Join-Path $packageRoot ('Scripts\' + $_.Name)) }

foreach ($document in @(
    'docs\GETTING_STARTED.md',
    'docs\INSTALLATION.md',
    'docs\SIMHUB-PROFILES.md',
    'docs\UPDATES.md',
    'docs\BETA-TESTING.md',
    'docs\PROPERTIES.md',
    'docs\REALISM_EFFECTS.md',
    'docs\REALISM-IMPLEMENTATION-test.19.md',
    ('docs\RELEASE-test.' + $Version.Split('.')[-1] + '.md'),
    'profiles\ch47f\README.md'
)) {
    $destinationName = if ($document -like '*ch47f*') { 'CH47F-PROFILE.md' } else { Split-Path -Leaf $document }
    Copy-PackageFile (Join-Path $repositoryRoot $document) (Join-Path $packageRoot ('Docs\' + $destinationName))
}

$profilePaths = @(
    'downloads\realism-test19\DCS-UH60L-Haptic-Engine-v2.0.0-test.19-Realism.siprofile',
    'downloads\realism-test19\DCS-AH-64D-Apache-Haptic-Engine-v2.0.0-test.19-Realism.siprofile',
    'downloads\realism-test19\DCS-AH-6J-Haptic-Engine-v2.0.0-test.19-Realism.siprofile',
    'downloads\realism-test19\DCS-MH-6J-Haptic-Engine-v2.0.0-test.19-Realism.siprofile',
    'downloads\realism-test19\DCS-OH-58D-Kiowa-Haptic-Engine-v2.0.0-test.19-Realism.siprofile',
    'downloads\realism-test19\DCS-UH-1H-Huey-Haptic-Engine-v2.0.0-test.19-Realism.siprofile',
    'profiles\ch47f\DCS-CH-47F-Haptic-Engine-v2.0.0-test.2.siprofile',
    'profiles\uh60\DCS World - DCS UH60L - DCSExtra Refreshed v1.9.0.siprofile'
)

foreach ($relativePath in $profilePaths) {
    $source = Join-Path $repositoryRoot $relativePath
    Copy-PackageFile $source (Join-Path $packageRoot ('Profiles\' + (Split-Path -Leaf $source)))
}

$hookHash = (Get-FileHash -LiteralPath $hookPath -Algorithm SHA256).Hash
$commit = (git -C $repositoryRoot rev-parse HEAD).Trim()
[ordered]@{
    version = $Version
    packageFlavor = 'public-beta'
    sourceCommit = $commit
    repository = $Repository
    hookSha256 = $hookHash
    hapticEngineVersion = '1.7.1'
    groundProfileVersion = '2.0.0-test.19'
    ch47ProfileVersion = '2.0.0-test.2'
    ch47RotorSource = 'CH47EngineRpmProxy (provisional)'
    lastKnownWorkingVersion = $null
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
"$zipHash  $(Split-Path -Leaf $zipPath)" | Set-Content -LiteralPath $zipHashPath -Encoding ASCII

Write-Host "Public-beta bundle: $zipPath"
Write-Host "ZIP SHA-256: $zipHash"
Write-Host "Source commit: $commit"
