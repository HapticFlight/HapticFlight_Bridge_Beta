[CmdletBinding(SupportsShouldProcess)]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [string]$SimHubPath,

    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot

function Find-SimHubPath {
    param([string]$RequestedPath)

    $candidates = @(
        $RequestedPath,
        $env:SIMHUB_INSTALL_PATH,
        $(if (${env:ProgramFiles(x86)}) { Join-Path ${env:ProgramFiles(x86)} 'SimHub' }),
        $(if ($env:ProgramFiles) { Join-Path $env:ProgramFiles 'SimHub' })
    ) | Where-Object { $_ }

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath (Join-Path $candidate 'SimHubWPF.exe')) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    throw 'SimHub was not found. Pass -SimHubPath with the folder containing SimHubWPF.exe.'
}

$resolvedSimHubPath = Find-SimHubPath -RequestedPath $SimHubPath

if (Get-Process -Name 'SimHubWPF' -ErrorAction SilentlyContinue) {
    throw 'Close SimHub before installing the plugin.'
}

if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot 'Build.ps1') -Configuration $Configuration -SimHubPath $resolvedSimHubPath
}

$buildDirectory = Join-Path $repositoryRoot "src\DcsExtraTelemetry.Plugin\bin\$Configuration"
$files = @('DcsExtraTelemetry.Plugin.dll', 'DcsExtraTelemetry.Plugin.pdb')
$installedCount = 0

foreach ($file in $files) {
    $source = Join-Path $buildDirectory $file
    $destination = Join-Path $resolvedSimHubPath $file

    if (-not (Test-Path -LiteralPath $source)) {
        throw "Build output is missing: $source"
    }

    if ($PSCmdlet.ShouldProcess($destination, "Install $file")) {
        Copy-Item -LiteralPath $source -Destination $destination -Force
        $installedCount++
    }
}

if ($installedCount -eq $files.Count) {
    Write-Host "Installed DCS Extra Telemetry into: $resolvedSimHubPath"
}
else {
    Write-Host "Installation preview complete for: $resolvedSimHubPath"
}
Write-Host 'Only this project''s DLL and PDB were written.'
