[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [string]$SimHubPath,

    [switch]$ProductionOnly
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot

function Find-SimHubPath {
    param([string]$RequestedPath)

    $candidates = [System.Collections.Generic.List[string]]::new()

    if ($RequestedPath) {
        $candidates.Add($RequestedPath)
    }

    if ($env:SIMHUB_INSTALL_PATH) {
        $candidates.Add($env:SIMHUB_INSTALL_PATH)
    }

    if (${env:ProgramFiles(x86)}) {
        $candidates.Add((Join-Path ${env:ProgramFiles(x86)} 'SimHub'))
    }

    if ($env:ProgramFiles) {
        $candidates.Add((Join-Path $env:ProgramFiles 'SimHub'))
    }

    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path -LiteralPath (Join-Path $candidate 'SimHub.Plugins.dll'))) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    throw 'SimHub was not found. Pass -SimHubPath with the folder containing SimHub.Plugins.dll.'
}

$resolvedSimHubPath = Find-SimHubPath -RequestedPath $SimHubPath
$previousSimHubInstallPath = $env:SIMHUB_INSTALL_PATH
$env:SIMHUB_INSTALL_PATH = $resolvedSimHubPath.TrimEnd('\') + '\'

$visualStudioMsBuild = @(
    (Join-Path ${env:ProgramFiles} 'Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe'),
    (Join-Path ${env:ProgramFiles} 'Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe'),
    (Join-Path ${env:ProgramFiles} 'Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe')
) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1

Push-Location $repositoryRoot
try {
    $targets = if ($ProductionOnly) {
        @(
            '.\src\DcsExtraTelemetry.Plugin\DcsExtraTelemetry.Plugin.csproj',
            '.\tools\DcsExtraTelemetry.Monitor\DcsExtraTelemetry.Monitor.csproj',
            '.\tools\DcsExtraTelemetry.Launcher\DcsExtraTelemetry.Launcher.csproj'
        )
    }
    else {
        @('.\DcsExtraTelemetry.sln')
    }

    foreach ($target in $targets) {
        if ($visualStudioMsBuild) {
            & $visualStudioMsBuild $target '/t:Rebuild' "/p:Configuration=$Configuration" '/v:minimal'
        }
        else {
            & dotnet msbuild $target '/t:Rebuild' "/p:Configuration=$Configuration" '/v:minimal'
        }
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed for $target with exit code $LASTEXITCODE."
        }
    }
}
finally {
    Pop-Location
    $env:SIMHUB_INSTALL_PATH = $previousSimHubInstallPath
}

Write-Host "Build passed ($Configuration)."
Write-Host "Plugin: $repositoryRoot\src\DcsExtraTelemetry.Plugin\bin\$Configuration\DcsExtraTelemetry.Plugin.dll"
