[CmdletBinding()]
param(
    [string[]]$DcsProfilePath,
    [string]$SimHubPath,
    [string]$OutputPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'baseline.json')
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')

if (Get-Process -Name 'DCS', 'SimHubWPF' -ErrorAction SilentlyContinue) {
    throw 'Close DCS and SimHub before capturing the baseline.'
}

$profiles = @(Find-DcsProfiles -RequestedPaths $DcsProfilePath)
if ($profiles.Count -eq 0) {
    throw 'No DCS profiles were found. Pass -DcsProfilePath with the Saved Games DCS profile folder.'
}

$resolvedSimHubPath = Find-SimHubPath -RequestedPath $SimHubPath
$dcsScopes = foreach ($profile in $profiles) {
    $scriptsPath = Join-Path $profile 'Scripts'
    $ownedHook = Join-Path $scriptsPath 'Hooks\DCSExtraTelemetry.lua'
    [pscustomobject]@{
        Root = $scriptsPath
        Files = @(Get-HashRecords -Root $scriptsPath -ExcludedPaths @($ownedHook) -Recurse)
    }
}

$ownedSimHubFiles = @(
    (Join-Path $resolvedSimHubPath 'DcsExtraTelemetry.Plugin.dll'),
    (Join-Path $resolvedSimHubPath 'DcsExtraTelemetry.Plugin.pdb')
)

$baseline = [pscustomobject]@{
    FormatVersion = 1
    CreatedUtc = [DateTime]::UtcNow.ToString('o')
    DcsScopes = @($dcsScopes)
    SimHubScope = [pscustomobject]@{
        Root = $resolvedSimHubPath
        Files = @(Get-HashRecords -Root $resolvedSimHubPath -ExcludedPaths $ownedSimHubFiles)
    }
}

$resolvedOutput = [IO.Path]::GetFullPath($OutputPath)
$baseline | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath $resolvedOutput -Encoding UTF8
Write-Host "Baseline captured: $resolvedOutput"
$dcsFileCount = @($dcsScopes | ForEach-Object { $_.Files }).Count
Write-Host "DCS profiles: $($profiles.Count); unrelated files hashed: $($dcsFileCount + $baseline.SimHubScope.Files.Count)"
