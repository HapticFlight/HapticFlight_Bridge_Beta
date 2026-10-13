param(
    [string]$SourceProfile = (Join-Path $PSScriptRoot '..\profiles\uh60\DCS World - DCS UH60L - DCSExtra Refreshed v1.8.8.siprofile'),
    [string]$OutputProfile = (Join-Path $PSScriptRoot '..\profiles\ah64\DCS World - DCS AH-64D - DCSExtra Baseline v0.1.siprofile')
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

function Convert-RotorEffect([object]$Effect, [string]$Name, [double]$Gain, [double]$Frequency, [string]$OldState, [string]$NewState) {
    $Effect.Description = $Name
    $Effect.Gain = $Gain
    $Effect.Output.Frequency = $Frequency
    $Effect.FrontLeftFormula.Expression = $Effect.FrontLeftFormula.Expression.Replace($OldState, $NewState)
    $Effect.FrontLeftFormula.PreExpression = $Effect.FrontLeftFormula.PreExpression.Replace($OldState, $NewState)
    $connected = "`$prop('DcsExtraTelemetryPlugin.DCSExtra.Connected') &&"
    $familyGate = "`$prop('DcsExtraTelemetryPlugin.DCSExtra.Connected') && `$prop('DcsExtraTelemetryPlugin.DCSExtra.AircraftFamily') === 'AH64' &&"
    $Effect.FrontLeftFormula.Expression = $Effect.FrontLeftFormula.Expression.Replace($connected, $familyGate)
}

if (-not (Test-Path -LiteralPath $SourceProfile)) { throw "Source profile not found: $SourceProfile" }
$profile = Get-Content -Raw -LiteralPath $SourceProfile | ConvertFrom-Json
$rotor = Get-Group $profile 'Rotor and Powertrain'

$oneRev = Get-Effect $rotor 'Rotor 1-rev airframe pulse'
Convert-RotorEffect $oneRev 'AH-64 rotor 1-rev airframe pulse' 18 4.82 'rotorOneRevLevel' 'ah64RotorOneRevLevel'

$twoRev = Get-Effect $rotor 'Rotor 2-rev structural harmonic'
Convert-RotorEffect $twoRev 'AH-64 rotor 2-rev structural harmonic' 16 9.63 'rotorTwoRevLevel' 'ah64RotorTwoRevLevel'

$bladePass = Get-Effect $rotor 'Main rotor blade-pass 4-rev'
Convert-RotorEffect $bladePass 'AH-64 main rotor blade-pass 4-rev' 28 19.27 'rotorFourRevLevel' 'ah64RotorFourRevLevel'

$bladeSlap = Get-Effect $rotor 'Blade slap (descent BVI cue)'
$bladeSlap.Description = 'AH-64 blade slap (descent BVI cue)'
$bladeSlap.Gain = 18
$bladeSlap.Output.Frequency = 19.27

$weapons = Get-Group $profile 'Weapons and Countermeasures'
$irrelevant = @('M134 and light-gun sustained fire', 'M134 low body harmonic', 'GAU-19 recoil harmonic')
$weapons.EffectsContainers = @($weapons.EffectsContainers | Where-Object { $irrelevant -notcontains $_.Description })

$m230 = Get-Effect $weapons 'Weapon-specific heavy gun rhythm'
$m230.Description = 'AH-64 M230 chain-gun rhythm'
$m230.Gain = 24
$m230.Output.Frequency = 10.42
$m230.ForceFrequencies = $true
$m230.FrontLeftFrequencyFormula.Expression = @'
if (!$prop('DcsExtraTelemetryPlugin.DCSExtra.Connected') || !$prop('DcsExtraTelemetryPlugin.DCSExtra.Has.WeaponEffects')) return 10.42;
var rate = Math.max(0, $prop('DcsExtraTelemetryPlugin.DCSExtra.NominalWeaponRateHz'));
return Math.max(5, Math.min(35, rate > 0 ? rate : 10.42));
'@.Trim()

$m230Body = Get-Effect $weapons 'M230 individual-shot body thump'
$m230Body.Description = 'AH-64 M230 low-body thump'
$m230Body.Gain = 10
$m230Body.Output.Frequency = 5.21
$m230Body.ForceFrequencies = $true
$m230Body.FrontLeftFrequencyFormula.Expression = @'
if (!$prop('DcsExtraTelemetryPlugin.DCSExtra.Connected') || !$prop('DcsExtraTelemetryPlugin.DCSExtra.Has.WeaponEffects')) return 5.21;
var rate = Math.max(0, $prop('DcsExtraTelemetryPlugin.DCSExtra.NominalWeaponRateHz'));
return Math.max(3, Math.min(18, (rate > 0 ? rate : 10.42) * 0.5));
'@.Trim()

(Get-Effect $weapons 'Rocket launch kick').Description = 'AH-64 Hydra rocket launch kick'
(Get-Effect $weapons 'Rocket launch body reaction').Description = 'AH-64 Hydra rocket body reaction'
(Get-Effect $weapons 'Weapon-specific missile body').Description = 'AH-64 missile launch body'
(Get-Effect $weapons 'Missile motor ignition').Description = 'AH-64 missile motor ignition'

$profile.Name = 'DCS AH-64D - DCSExtra Baseline v0.1'
$profile.Description = 'AH-64D tactile baseline derived from the UH-60 v1.8.8 cue architecture. It uses dynamic 1/rev, 2/rev, and four-blade-pass harmonics with governed Nr presence, collective/load/lateral/G swell, asymmetric smoothing, wheeled-ground detail, progressive braking, and AH-64-specific M230, Hydra, missile, and countermeasure layers. Initial calibration is telemetry-validated and requires a structured seat test before promotion.'
$profile.ProfileId = [guid]::NewGuid().ToString()
$profile.LastLoaded = '0001-01-01T00:00:00'
$profile.GlobalGain = 45.0

$resolvedOutput = [System.IO.Path]::GetFullPath($OutputProfile)
[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($resolvedOutput)) | Out-Null
$json = $profile | ConvertTo-Json -Depth 100
[System.IO.File]::WriteAllText($resolvedOutput, $json, [System.Text.UTF8Encoding]::new($false))
Write-Output $resolvedOutput
