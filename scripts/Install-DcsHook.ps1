[CmdletBinding(SupportsShouldProcess)]
param(
    [string[]]$DcsProfilePath
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$sourceHook = Join-Path $repositoryRoot 'dcs-hook\DCSExtraTelemetry.lua'
$hookFileName = 'DCSExtraTelemetry.lua'

function Get-SavedGamesRoot {
    $registryPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders'
    $savedGamesId = '{4C5C32FF-BB9D-43B0-BF7B-1DDE8B8C7D1E}'

    try {
        $value = (Get-ItemProperty -LiteralPath $registryPath -Name $savedGamesId -ErrorAction Stop).$savedGamesId
        if ($value) {
            return [Environment]::ExpandEnvironmentVariables($value)
        }
    }
    catch {
        # Use the standard fallback below.
    }

    return (Join-Path $env:USERPROFILE 'Saved Games')
}

function Find-DcsProfiles {
    if ($DcsProfilePath) {
        return $DcsProfilePath | ForEach-Object {
            if (-not (Test-Path -LiteralPath $_ -PathType Container)) {
                throw "DCS profile folder does not exist: $_"
            }
            (Resolve-Path -LiteralPath $_).Path
        }
    }

    $savedGamesRoot = Get-SavedGamesRoot
    if (-not (Test-Path -LiteralPath $savedGamesRoot -PathType Container)) {
        return @()
    }

    return @(Get-ChildItem -LiteralPath $savedGamesRoot -Directory | Where-Object {
        $_.Name -eq 'DCS' -or $_.Name -like 'DCS.*'
    } | ForEach-Object { $_.FullName })
}

function Get-UnrelatedHashes {
    param([string]$ScriptsPath, [string]$OwnedHookPath)

    $hashes = @{}
    if (-not (Test-Path -LiteralPath $ScriptsPath -PathType Container)) {
        return $hashes
    }

    Get-ChildItem -LiteralPath $ScriptsPath -File -Recurse | Where-Object {
        $_.FullName -ne $OwnedHookPath
    } | ForEach-Object {
        $hashes[$_.FullName] = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
    }

    return $hashes
}

if (-not (Test-Path -LiteralPath $sourceHook -PathType Leaf)) {
    throw "Hook source file is missing: $sourceHook"
}

$profiles = @(Find-DcsProfiles | Sort-Object -Unique)
if ($profiles.Count -eq 0) {
    throw 'No DCS Saved Games profiles were found. Pass -DcsProfilePath with one or more profile folders.'
}

foreach ($profile in $profiles) {
    $scriptsPath = Join-Path $profile 'Scripts'
    $hooksPath = Join-Path $scriptsPath 'Hooks'
    $destination = Join-Path $hooksPath $hookFileName
    $before = Get-UnrelatedHashes -ScriptsPath $scriptsPath -OwnedHookPath $destination

    if ($PSCmdlet.ShouldProcess($destination, 'Install isolated DCS GameGUI Hook')) {
        New-Item -ItemType Directory -Path $hooksPath -Force | Out-Null
        Copy-Item -LiteralPath $sourceHook -Destination $destination -Force
    }

    $after = Get-UnrelatedHashes -ScriptsPath $scriptsPath -OwnedHookPath $destination
    foreach ($path in $before.Keys) {
        if (-not $after.ContainsKey($path) -or $after[$path] -ne $before[$path]) {
            throw "Safety check failed: an unrelated file changed: $path"
        }
    }

    Write-Host "Installed isolated Hook: $destination"
}

Write-Host 'Export.lua was not read, written, or used.'
