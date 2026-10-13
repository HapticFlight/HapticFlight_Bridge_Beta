[CmdletBinding()]
param(
    [string]$BaselinePath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'baseline.json')
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')

if (Get-Process -Name 'DCS', 'SimHubWPF' -ErrorAction SilentlyContinue) {
    throw 'Close DCS and SimHub before verifying the baseline.'
}

if (-not (Test-Path -LiteralPath $BaselinePath -PathType Leaf)) {
    throw "Baseline file was not found: $BaselinePath"
}

$baseline = Get-Content -LiteralPath $BaselinePath -Raw | ConvertFrom-Json
if ($baseline.FormatVersion -ne 1) {
    throw "Unsupported baseline format: $($baseline.FormatVersion)"
}

$allDifferences = [System.Collections.Generic.List[object]]::new()
foreach ($scope in $baseline.DcsScopes) {
    $ownedHook = Join-Path $scope.Root 'Hooks\DCSExtraTelemetry.lua'
    $current = @(Get-HashRecords -Root $scope.Root -ExcludedPaths @($ownedHook) -Recurse)
    foreach ($difference in @(Compare-HashRecords -Before @($scope.Files) -After $current)) {
        $allDifferences.Add($difference)
    }
}

$ownedSimHubFiles = @(
    (Join-Path $baseline.SimHubScope.Root 'DcsExtraTelemetry.Plugin.dll'),
    (Join-Path $baseline.SimHubScope.Root 'DcsExtraTelemetry.Plugin.pdb')
)
$currentSimHub = @(Get-HashRecords -Root $baseline.SimHubScope.Root -ExcludedPaths $ownedSimHubFiles)
foreach ($difference in @(Compare-HashRecords -Before @($baseline.SimHubScope.Files) -After $currentSimHub)) {
    $allDifferences.Add($difference)
}

if ($allDifferences.Count -gt 0) {
    $allDifferences | Format-Table -AutoSize
    throw "$($allDifferences.Count) unrelated file difference(s) detected."
}

Write-Host 'PASS: Every unrelated DCS Scripts file and SimHub root file matches the baseline byte-for-byte.'
