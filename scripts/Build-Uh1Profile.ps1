param(
    [string]$SourceProfile = (Join-Path $PSScriptRoot '..\profiles\uh60\DCS World - DCS UH60L - DCSExtra Refreshed v1.8.8.siprofile'),
    [string]$OutputProfile = (Join-Path $PSScriptRoot '..\profiles\uh1\DCS World - DCS UH-1H - DCSExtra Baseline v0.1.siprofile')
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
    $familyGate = "`$prop('DcsExtraTelemetryPlugin.DCSExtra.Connected') && `$prop('DcsExtraTelemetryPlugin.DCSExtra.AircraftFamily') === 'UH1' &&"
    $Effect.FrontLeftFormula.Expression = $Effect.FrontLeftFormula.Expression.Replace($connected, $familyGate)
}

if (-not (Test-Path -LiteralPath $SourceProfile)) { throw "Source profile not found: $SourceProfile" }
$profile = Get-Content -Raw -LiteralPath $SourceProfile | ConvertFrom-Json

$lifecycle = Get-Group $profile 'Lifecycle'
$lifecycle.EffectsContainers = @($lifecycle.EffectsContainers | Where-Object Description -ne 'APU machinery')

$rotor = Get-Group $profile 'Rotor and Powertrain'
$oneRev = Get-Effect $rotor 'Rotor 1-rev airframe pulse'
Convert-RotorEffect $oneRev 'UH-1H rotor 1-rev airframe pulse' 22 5.40 'rotorOneRevLevel' 'uh1RotorOneRevLevel'
$bladePass = Get-Effect $rotor 'Main rotor blade-pass 4-rev'
Convert-RotorEffect $bladePass 'UH-1H two-blade rotor beat' 30 10.80 'rotorFourRevLevel' 'uh1RotorTwoBladeLevel'
$rotor.EffectsContainers = @($rotor.EffectsContainers | Where-Object Description -ne 'Rotor 2-rev structural harmonic')
$bladeSlap = Get-Effect $rotor 'Blade slap (descent BVI cue)'
$bladeSlap.Description = 'UH-1H blade slap (descent BVI cue)'
$bladeSlap.Gain = 20
$bladeSlap.Output.Frequency = 10.80

$ground = Get-Group $profile 'Ground Interaction'
(Get-Effect $ground 'Main wheel contact detail').Description = 'UH-1H skid contact detail'
(Get-Effect $ground 'Ground roll base').Description = 'UH-1H skid ground motion'
$removeGround = @('Tailwheel settle tick', 'Wheel shimmy', 'Tailwheel caster', 'Wheel braking load')
$ground.EffectsContainers = @($ground.EffectsContainers | Where-Object { $removeGround -notcontains $_.Description })

$weapons = Get-Group $profile 'Weapons and Countermeasures'
$removeWeapons = @('M230 individual-shot body thump', 'GAU-19 recoil harmonic', 'Weapon-specific missile body', 'Missile motor ignition')
$weapons.EffectsContainers = @($weapons.EffectsContainers | Where-Object { $removeWeapons -notcontains $_.Description })
(Get-Effect $weapons 'M134 and light-gun sustained fire').Description = 'UH-1H M134 and door-gun sustained fire'
(Get-Effect $weapons 'M134 low body harmonic').Description = 'UH-1H door-gun low body harmonic'
(Get-Effect $weapons 'Weapon-specific heavy gun rhythm').Description = 'UH-1H heavy gun or grenade-launcher rhythm'
(Get-Effect $weapons 'Rocket launch kick').Description = 'UH-1H rocket launch kick'
(Get-Effect $weapons 'Rocket launch body reaction').Description = 'UH-1H rocket body reaction'

$profile.Name = 'DCS UH-1H - DCSExtra Baseline v0.1'
$profile.Description = 'UH-1H tactile baseline derived from the UH-60 v1.8.8 cue architecture. It preserves a strong two-blade rotor identity without stacking two effects at the same 2/rev frequency, uses the module rotor gauge/phase, collective and torque-gauge telemetry, and separates skid, maneuver, door-gun, heavy-gun, rocket, and countermeasure cues. Requires a structured live seat calibration.'
$profile.ProfileId = [guid]::NewGuid().ToString()
$profile.LastLoaded = '0001-01-01T00:00:00'
$profile.GlobalGain = 42.0

$resolvedOutput = [IO.Path]::GetFullPath($OutputProfile)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($resolvedOutput)) | Out-Null
[IO.File]::WriteAllText($resolvedOutput, ($profile | ConvertTo-Json -Depth 100), [Text.UTF8Encoding]::new($false))
Write-Output $resolvedOutput
