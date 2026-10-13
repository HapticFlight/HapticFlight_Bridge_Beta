param(
    [string]$SourceProfile = (Join-Path $PSScriptRoot '..\profiles\uh60\DCS World - DCS UH60L - DCSExtra Refreshed v1.8.8.siprofile'),
    [string]$OutputProfile = (Join-Path $PSScriptRoot '..\profiles\oh58\DCS World - DCS OH-58D - DCSExtra Baseline v0.1.siprofile')
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
    $familyGate = "`$prop('DcsExtraTelemetryPlugin.DCSExtra.Connected') && `$prop('DcsExtraTelemetryPlugin.DCSExtra.AircraftFamily') === 'OH58' &&"
    $Effect.FrontLeftFormula.Expression = $Effect.FrontLeftFormula.Expression.Replace($connected, $familyGate)
}

if (-not (Test-Path -LiteralPath $SourceProfile)) { throw "Source profile not found: $SourceProfile" }
$profile = Get-Content -Raw -LiteralPath $SourceProfile | ConvertFrom-Json

$lifecycle = Get-Group $profile 'Lifecycle'
$lifecycle.EffectsContainers = @($lifecycle.EffectsContainers | Where-Object Description -ne 'APU machinery')

$rotor = Get-Group $profile 'Rotor and Powertrain'
Convert-RotorEffect (Get-Effect $rotor 'Rotor 1-rev airframe pulse') 'OH-58D rotor 1-rev airframe pulse' 17 6.58 'rotorOneRevLevel' 'oh58RotorOneRevLevel'
Convert-RotorEffect (Get-Effect $rotor 'Rotor 2-rev structural harmonic') 'OH-58D rotor 2-rev structural harmonic' 15 13.17 'rotorTwoRevLevel' 'oh58RotorTwoRevLevel'
Convert-RotorEffect (Get-Effect $rotor 'Main rotor blade-pass 4-rev') 'OH-58D main rotor blade-pass 4-rev' 25 26.33 'rotorFourRevLevel' 'oh58RotorFourRevLevel'
$bladeSlap = Get-Effect $rotor 'Blade slap (descent BVI cue)'
$bladeSlap.Description = 'OH-58D blade slap (descent BVI cue)'
$bladeSlap.Gain = 17
$bladeSlap.Output.Frequency = 26.33

$ground = Get-Group $profile 'Ground Interaction'
(Get-Effect $ground 'Main wheel contact detail').Description = 'OH-58D skid contact detail'
(Get-Effect $ground 'Ground roll base').Description = 'OH-58D skid ground motion'
$removeGround = @('Tailwheel settle tick', 'Wheel shimmy', 'Tailwheel caster', 'Wheel braking load')
$ground.EffectsContainers = @($ground.EffectsContainers | Where-Object { $removeGround -notcontains $_.Description })

$weapons = Get-Group $profile 'Weapons and Countermeasures'
$removeWeapons = @('M134 and light-gun sustained fire', 'M134 low body harmonic', 'GAU-19 recoil harmonic')
$weapons.EffectsContainers = @($weapons.EffectsContainers | Where-Object { $removeWeapons -notcontains $_.Description })
$m3p = Get-Effect $weapons 'Weapon-specific heavy gun rhythm'
$m3p.Description = 'OH-58D M3P .50-caliber gun rhythm'
$m3p.Gain = 22
$m3p.Output.Frequency = 18.33
$m3p.ForceFrequencies = $true
$m3p.FrontLeftFrequencyFormula.Expression = @'
if (!$prop('DcsExtraTelemetryPlugin.DCSExtra.Connected') || !$prop('DcsExtraTelemetryPlugin.DCSExtra.Has.WeaponEffects')) return 18.33;
var rate = Math.max(0, $prop('DcsExtraTelemetryPlugin.DCSExtra.NominalWeaponRateHz'));
return Math.max(5, Math.min(35, rate > 0 ? rate : 18.33));
'@.Trim()
$m3pBody = Get-Effect $weapons 'M230 individual-shot body thump'
$m3pBody.Description = 'OH-58D M3P low-body thump'
$m3pBody.Gain = 9
$m3pBody.Output.Frequency = 9.17
$m3pBody.ForceFrequencies = $true
$m3pBody.FrontLeftFrequencyFormula.Expression = @'
if (!$prop('DcsExtraTelemetryPlugin.DCSExtra.Connected') || !$prop('DcsExtraTelemetryPlugin.DCSExtra.Has.WeaponEffects')) return 9.17;
var rate = Math.max(0, $prop('DcsExtraTelemetryPlugin.DCSExtra.NominalWeaponRateHz'));
return Math.max(3, Math.min(18, (rate > 0 ? rate : 18.33) * 0.5));
'@.Trim()
(Get-Effect $weapons 'Rocket launch kick').Description = 'OH-58D Hydra rocket launch kick'
(Get-Effect $weapons 'Rocket launch body reaction').Description = 'OH-58D Hydra rocket body reaction'
(Get-Effect $weapons 'Weapon-specific missile body').Description = 'OH-58D Hellfire or ATAS launch body'
(Get-Effect $weapons 'Missile motor ignition').Description = 'OH-58D missile motor ignition'

$profile.Name = 'DCS OH-58D - DCSExtra Baseline v0.1'
$profile.Description = 'OH-58D Kiowa Warrior tactile baseline derived from the UH-60 v1.8.8 cue architecture. It uses dynamic four-blade rotor harmonics, collective/load/lateral/G response, four-point skid interaction, and separated M3P, Hydra, Hellfire/ATAS, countermeasure, maneuver, and damage cues. Rotor speed currently uses a bounded engine-RPM proxy and requires live-log and seat validation.'
$profile.ProfileId = [guid]::NewGuid().ToString()
$profile.LastLoaded = '0001-01-01T00:00:00'
$profile.GlobalGain = 42.0

$resolvedOutput = [IO.Path]::GetFullPath($OutputProfile)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($resolvedOutput)) | Out-Null
[IO.File]::WriteAllText($resolvedOutput, ($profile | ConvertTo-Json -Depth 100), [Text.UTF8Encoding]::new($false))
Write-Output $resolvedOutput
