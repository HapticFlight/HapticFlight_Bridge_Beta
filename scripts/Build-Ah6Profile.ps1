param(
    [string]$SourceProfile = (Join-Path $PSScriptRoot '..\profiles\uh60\DCS World - DCS UH60L - DCSExtra Refreshed v1.8.6.siprofile'),
    [string]$OutputProfile = (Join-Path $PSScriptRoot '..\profiles\ah6\DCS World - AH-6J MH-6J - DCSExtra Baseline v0.1.siprofile')
)

$ErrorActionPreference = 'Stop'

function Get-Group([object]$Profile, [string]$Name) {
    $group = @($Profile.EffectsContainers | Where-Object Description -eq $Name)
    if ($group.Count -ne 1) { throw "Expected exactly one '$Name' group." }
    return $group[0]
}

function Get-Effect([object]$Group, [string]$Name) {
    $effect = @($Group.EffectsContainers | Where-Object Description -eq $Name)
    if ($effect.Count -ne 1) { throw "Expected exactly one '$Name' effect." }
    return $effect[0]
}

function Set-Effect([object]$Effect, [string]$Name, [double]$Gain, [double]$Frequency, [string]$Amplitude, [string]$FrequencyFormula = '', [string]$PreExpression = '') {
    $Effect.Description = $Name
    $Effect.Gain = $Gain
    $Effect.Output.Frequency = $Frequency
    $Effect.ForceFrequencies = -not [string]::IsNullOrWhiteSpace($FrequencyFormula)
    $Effect.FrontLeftFormula.Expression = $Amplitude.Trim()
    $Effect.FrontLeftFormula.PreExpression = $PreExpression.Trim()
    $Effect.FrontLeftFrequencyFormula.Expression = $FrequencyFormula.Trim()
}

if (-not (Test-Path -LiteralPath $SourceProfile)) { throw "Source profile not found: $SourceProfile" }
$profile = Get-Content -Raw -LiteralPath $SourceProfile | ConvertFrom-Json

$rotor = Get-Group $profile 'Rotor and Powertrain'

# AH/MH-6 is a five-blade aircraft.  BladePassHz is already calculated from
# the adapter's configured blade count; keeping this dynamic prevents a fixed
# UH-60 frequency from leaking into the Little Bird profile.
$oneRev = Get-Effect $rotor 'Rotor 1-rev airframe pulse'
Set-Effect $oneRev 'AH-6 rotor 1-rev airframe pulse' 18 7.8 @'
var target = 0;
if ($prop('DcsExtraTelemetryPlugin.DCSExtra.Connected') &&
    $prop('DcsExtraTelemetryPlugin.DCSExtra.AircraftFamily') === 'AH6' &&
    $prop('DcsExtraTelemetryPlugin.DCSExtra.Has.RotorDynamics') &&
    $prop('DcsExtraTelemetryPlugin.DCSExtra.RotorRunning')) {
    var nr = Math.max(0, Math.min(1, $prop('DcsExtraTelemetryPlugin.DCSExtra.MainRotorRpmPct') / 100));
    var governed = Math.max(0, Math.min(1, (nr - 0.15) / 0.80));
    governed = governed * governed * (3 - 2 * governed);
    var load = Math.max(0, Math.min(1, $prop('DcsExtraTelemetryPlugin.DCSExtra.RotorLoadIntensity')));
    var lateral = Math.max(0, Math.min(1, $prop('DcsExtraTelemetryPlugin.DCSExtra.LateralManeuverIntensity')));
    var control = Math.max(0, Math.min(1, $prop('DcsExtraTelemetryPlugin.DCSExtra.ControlResponseIntensity')));
    var power = $prop('DcsExtraTelemetryPlugin.DCSExtra.Has.ControlPositions') ?
        Math.max(0, Math.min(1, $prop('DcsExtraTelemetryPlugin.DCSExtra.CollectivePosition'))) : load;
    var g = (!$prop('DcsExtraTelemetryPlugin.DCSExtra.OnGround') && $prop('DcsExtraTelemetryPlugin.DCSExtra.Has.Acceleration')) ?
        $prop('DcsExtraTelemetryPlugin.DCSExtra.AccelYG') : 1;
    var positiveG = Math.max(0, Math.min(1, (g - 1.03) / 0.67));
    positiveG = 0.40 * positiveG * positiveG * (3 - 2 * positiveG) + 0.60 * positiveG * positiveG;
    target = Math.min(54, 8 + 19 * governed + 10 * power + 7 * load + 16 * positiveG + 15 * lateral + 10 * control);
}
var coefficient = target > root['ah6RotorOneRevLevel'] ? 0.35 : 0.14;
root['ah6RotorOneRevLevel'] += (target - root['ah6RotorOneRevLevel']) * coefficient;
if (target === 0 && root['ah6RotorOneRevLevel'] < 0.05) root['ah6RotorOneRevLevel'] = 0;
return Math.max(0, Math.min(54, root['ah6RotorOneRevLevel']));
'@ @'
if (!$prop('DcsExtraTelemetryPlugin.DCSExtra.Connected') || !$prop('DcsExtraTelemetryPlugin.DCSExtra.Has.RotorDynamics')) return 1;
return Math.max(1, Math.min(80, $prop('DcsExtraTelemetryPlugin.DCSExtra.MainRotorHz')));
'@ "root['ah6RotorOneRevLevel'] = 0;"

$twoRev = Get-Effect $rotor 'Rotor 2-rev structural harmonic'
Set-Effect $twoRev 'AH-6 rotor 2-rev structural harmonic' 15 15.6 ($oneRev.FrontLeftFormula.Expression -replace "ah6RotorOneRevLevel", "ah6RotorTwoRevLevel" -replace "Math.min\(54", "Math.min(48" -replace "19 \* governed", "14 * governed" -replace "15 \* lateral", "16 * lateral" -replace "10 \* control", "7 * control") @'
if (!$prop('DcsExtraTelemetryPlugin.DCSExtra.Connected') || !$prop('DcsExtraTelemetryPlugin.DCSExtra.Has.RotorDynamics')) return 1;
return Math.max(1, Math.min(80, $prop('DcsExtraTelemetryPlugin.DCSExtra.MainRotorHz') * 2));
'@ "root['ah6RotorTwoRevLevel'] = 0;"

$bladePass = Get-Effect $rotor 'Main rotor blade-pass 4-rev'
Set-Effect $bladePass 'AH-6 main rotor blade-pass 5-rev' 24 39.2 @'
var target = 0;
if ($prop('DcsExtraTelemetryPlugin.DCSExtra.Connected') &&
    $prop('DcsExtraTelemetryPlugin.DCSExtra.AircraftFamily') === 'AH6' &&
    $prop('DcsExtraTelemetryPlugin.DCSExtra.Has.RotorDynamics') &&
    $prop('DcsExtraTelemetryPlugin.DCSExtra.RotorRunning')) {
    var nr = Math.max(0, Math.min(1, $prop('DcsExtraTelemetryPlugin.DCSExtra.MainRotorRpmPct') / 100));
    var governed = Math.max(0, Math.min(1, (nr - 0.15) / 0.80));
    governed = governed * governed * (3 - 2 * governed);
    var load = Math.max(0, Math.min(1, $prop('DcsExtraTelemetryPlugin.DCSExtra.RotorLoadIntensity')));
    var power = $prop('DcsExtraTelemetryPlugin.DCSExtra.Has.ControlPositions') ?
        Math.max(0, Math.min(1, $prop('DcsExtraTelemetryPlugin.DCSExtra.CollectivePosition'))) : load;
    target = Math.min(56, 11 + 23 * governed + 11 * power + 8 * load);
}
var coefficient = target > root['ah6BladePassLevel'] ? 0.35 : 0.14;
root['ah6BladePassLevel'] += (target - root['ah6BladePassLevel']) * coefficient;
if (target === 0 && root['ah6BladePassLevel'] < 0.05) root['ah6BladePassLevel'] = 0;
return Math.max(0, Math.min(56, root['ah6BladePassLevel']));
'@ @'
if (!$prop('DcsExtraTelemetryPlugin.DCSExtra.Connected') || !$prop('DcsExtraTelemetryPlugin.DCSExtra.Has.RotorDynamics')) return 1;
return Math.max(1, Math.min(80, $prop('DcsExtraTelemetryPlugin.DCSExtra.BladePassHz')));
'@ "root['ah6BladePassLevel'] = 0;"

$slap = Get-Effect $rotor 'Blade slap (descent BVI cue)'
$slap.Description = 'AH-6 blade slap (descent BVI cue)'
$slap.Gain = 17

$ground = Get-Group $profile 'Ground Interaction'
Set-Effect (Get-Effect $ground 'Main wheel contact detail') 'Individual skid touchdown detail' 16 25 @'
if (!$prop('DcsExtraTelemetryPlugin.DCSExtra.Connected') || !$prop('DcsExtraTelemetryPlugin.DCSExtra.Has.Skids')) return 0;
var pulse = $prop('DcsExtraTelemetryPlugin.DCSExtra.SkidFrontLeftContactPulse') ||
    $prop('DcsExtraTelemetryPlugin.DCSExtra.SkidFrontRightContactPulse') ||
    $prop('DcsExtraTelemetryPlugin.DCSExtra.SkidRearLeftContactPulse') ||
    $prop('DcsExtraTelemetryPlugin.DCSExtra.SkidRearRightContactPulse');
return pulse ? 42 : 0;
'@
Set-Effect (Get-Effect $ground 'Tailwheel settle tick') 'Single-skid low-hover touch' 14 16 @'
if (!$prop('DcsExtraTelemetryPlugin.DCSExtra.Connected') || !$prop('DcsExtraTelemetryPlugin.DCSExtra.Has.Skids')) return 0;
var left = $prop('DcsExtraTelemetryPlugin.DCSExtra.SkidFrontLeftContact') ||
    $prop('DcsExtraTelemetryPlugin.DCSExtra.SkidRearLeftContact');
var right = $prop('DcsExtraTelemetryPlugin.DCSExtra.SkidFrontRightContact') ||
    $prop('DcsExtraTelemetryPlugin.DCSExtra.SkidRearRightContact');
var leftPulse = $prop('DcsExtraTelemetryPlugin.DCSExtra.SkidFrontLeftContactPulse') ||
    $prop('DcsExtraTelemetryPlugin.DCSExtra.SkidRearLeftContactPulse');
var rightPulse = $prop('DcsExtraTelemetryPlugin.DCSExtra.SkidFrontRightContactPulse') ||
    $prop('DcsExtraTelemetryPlugin.DCSExtra.SkidRearRightContactPulse');
// A Little Bird commonly settles one skid first in a low hover.  Keep the
// cue restrained while only one side carries weight; the main touchdown
// envelope remains responsible for a firm, full-airframe set-down.
if (left && !right) return leftPulse ? 42 : 18;
if (right && !left) return rightPulse ? 38 : 16;
return 0;
'@
Set-Effect (Get-Effect $ground 'Ground roll base') 'Skid slide and surface texture' 16 18 @'
if (!$prop('DcsExtraTelemetryPlugin.DCSExtra.Connected') || !$prop('DcsExtraTelemetryPlugin.DCSExtra.Has.Skids')) return 18;
return Math.max(12, Math.min(30, 12 + $prop('DcsExtraTelemetryPlugin.DCSExtra.GroundSpeedMps') * 1.1));
'@ @'
if (!$prop('DcsExtraTelemetryPlugin.DCSExtra.Connected') || !$prop('DcsExtraTelemetryPlugin.DCSExtra.Has.Skids') || !$prop('DcsExtraTelemetryPlugin.DCSExtra.OnGround')) return 0;
return Math.min(38, 38 * Math.max($prop('DcsExtraTelemetryPlugin.DCSExtra.SkidScrubIntensity'), $prop('DcsExtraTelemetryPlugin.DCSExtra.GroundRollIntensity') * 0.45));
'@
Set-Effect (Get-Effect $ground 'Wheel shimmy') 'Skid scrub roughness' 12 22 @'
var target = 0;
if ($prop('DcsExtraTelemetryPlugin.DCSExtra.Connected') && $prop('DcsExtraTelemetryPlugin.DCSExtra.Has.Skids') && $prop('DcsExtraTelemetryPlugin.DCSExtra.OnGround')) {
    target = 34 * Math.max(0, Math.min(1, $prop('DcsExtraTelemetryPlugin.DCSExtra.SkidScrubIntensity')));
}
root['ah6SkidScrubLevel'] += (target - root['ah6SkidScrubLevel']) * (target > root['ah6SkidScrubLevel'] ? 0.15 : 0.05);
return Math.max(0, Math.min(34, root['ah6SkidScrubLevel']));
'@ '' "root['ah6SkidScrubLevel'] = 0;"
Set-Effect (Get-Effect $ground 'Tailwheel caster') 'Skid pitch and roll rock' 10 14 @'
if (!$prop('DcsExtraTelemetryPlugin.DCSExtra.Connected') || !$prop('DcsExtraTelemetryPlugin.DCSExtra.Has.Skids') || !$prop('DcsExtraTelemetryPlugin.DCSExtra.OnGround')) return 0;
return Math.min(30, 20 * $prop('DcsExtraTelemetryPlugin.DCSExtra.GroundPitchRockIntensity') + 16 * $prop('DcsExtraTelemetryPlugin.DCSExtra.GroundRollRockIntensity'));
'@
Set-Effect (Get-Effect $ground 'Wheel braking load') 'Skid drag load' 13 18 @'
if (!$prop('DcsExtraTelemetryPlugin.DCSExtra.Connected') || !$prop('DcsExtraTelemetryPlugin.DCSExtra.Has.Skids') || !$prop('DcsExtraTelemetryPlugin.DCSExtra.OnGround')) return 0;
return Math.min(38, 38 * Math.max($prop('DcsExtraTelemetryPlugin.DCSExtra.SkidScrubIntensity'), $prop('DcsExtraTelemetryPlugin.DCSExtra.BrakingIntensity') * 0.45));
'@

$profile.Name = 'DCS AH-6J MH-6J - DCSExtra Baseline v0.1'
$profile.Description = 'AH-6J/MH-6J tactile baseline with dynamic five-blade rotor harmonics, sustained governed-rotor presence, collective/load/lateral maneuver response, and four-skid touchdown and scrub cues. The AH-6 rotor source is an N1/engine-RPM proxy, so validate against a live Little Bird flight before treating this as a final calibration.'
$profile.ProfileId = [guid]::NewGuid().ToString()
$profile.LastLoaded = '0001-01-01T00:00:00'
$profile.GlobalGain = 50.0

$resolvedOutput = [System.IO.Path]::GetFullPath($OutputProfile)
[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($resolvedOutput)) | Out-Null
$json = $profile | ConvertTo-Json -Depth 100
[System.IO.File]::WriteAllText($resolvedOutput, $json, [System.Text.UTF8Encoding]::new($false))
Write-Output $resolvedOutput
