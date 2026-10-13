[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$pluginPath = Join-Path $repositoryRoot 'src\DcsExtraTelemetry.Plugin\DcsExtraTelemetryPlugin.cs'
$snapshotPath = Join-Path $repositoryRoot 'src\DcsExtraTelemetry.Plugin\Haptics\HapticStateSnapshot.cs'
$manifestPath = Join-Path $repositoryRoot 'tests\fixtures\haptics\property-manifest.txt'
$source = Get-Content -LiteralPath $pluginPath -Raw
$snapshotSource = Get-Content -LiteralPath $snapshotPath -Raw

$names = @([regex]::Matches($source, 'RegisterHapticSignal\("([^"]+)"') | ForEach-Object { $_.Groups[1].Value })
if ($names.Count -ne 85) { throw "Expected exactly 85 haptic signal registrations; found $($names.Count)." }
$manifest = @(Get-Content -LiteralPath $manifestPath | Where-Object { $_ -and -not $_.StartsWith('#') })
if ($manifest.Count -ne $names.Count) { throw "Haptic property manifest count does not match registrations." }
for ($index = 0; $index -lt $names.Count; $index++) {
    if ($names[$index] -ne $manifest[$index]) {
        throw "Haptic property manifest differs at index $index`: expected '$($manifest[$index])', found '$($names[$index])'."
    }
}
$duplicates = @($names | Group-Object | Where-Object Count -gt 1)
if ($duplicates.Count -gt 0) { throw "Duplicate haptic registrations: $($duplicates.Name -join ', ')" }

foreach ($metadata in @('DCSExtra.Haptic.EngineVersion', 'DCSExtra.Haptic.ModelId',
    'DCSExtra.Haptic.ModelVersion', 'DCSExtra.Haptic.ModelStatus', 'DCSExtra.Haptic.LastResetReason')) {
    if (-not $source.Contains('"' + $metadata + '"')) { throw "Missing haptic metadata property: $metadata" }
}

foreach ($required in @('Touchdown', 'Ground.Roll', 'Rotor.Load', 'Maneuver.Vertical',
    'Aerodynamic.Buffet', 'Mechanical.Gear', 'Weapon.Recoil', 'UH60.TailwheelSettle',
    'AH6.SkidForeAftLoad', 'AH64.M230Recoil', 'UH1.TorqueLoad', 'OH58.SkidLateralLoad',
    'Timing.DeltaSeconds', 'Mix.ContinuousGain', 'Mix.PriorityDemand')) {
    if ($names -notcontains $required) { throw "Missing required haptic signal: $required" }
}

if (-not $snapshotSource.Contains('CurrentEngineVersion = "1.3.0"')) {
    throw 'Haptic engine version contract changed without updating this test.'
}

Write-Host "Haptic property contract passed with $($names.Count) signal families."
