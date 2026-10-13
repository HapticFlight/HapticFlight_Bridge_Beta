[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot

$profiles = @(
    @{
        Source = 'profiles\uh60\DCS World - DCS UH60L - DCSExtra Refreshed v1.9.0.siprofile'
        Output = 'profiles\uh60\DCS World - DCS UH60L - Haptic Engine v2.0.0-test.1.siprofile'
        OldName = 'DCS UH60L - DCSExtra Refreshed v1.9.0'
        NewName = 'DCS UH60L - Haptic Engine v2.0.0-test.1'
        ProfileId = '0e97382f-d177-4c31-bffd-e080e8bf8245'
        Description = 'UH-60L/DAP aircraft-aware haptic-engine test profile. Uses common physical signals plus UH-60 tailwheel, wheel, brake-tail, door-gun, and DAP outputs. Test-only until recorded replay and structured live seat validation pass.'
    },
    @{
        Source = 'profiles\uh60\DCS World - DCS UH60L - DCSExtra Refreshed v1.9.0.siprofile'
        Output = 'profiles\uh60\DCS World - DCS UH60L - Haptic Engine v2.0.0-test.2 Safe.siprofile'
        OldName = 'DCS UH60L - DCSExtra Refreshed v1.9.0'
        NewName = 'DCS UH60L - Haptic Engine v2.0.0-test.2 Safe'
        ProfileId = '5c60b492-6458-4b74-8b7c-857839202b19'
        Description = 'UH-60L/DAP aircraft-aware haptic-engine safety profile. This is the conservative starting profile for bass shakers: profile gain is enabled and every effect family has an explicit reduced group ceiling. Import it by itself; do not run it with any prior UH-60 profile.'
        SafeProfile = $true
    },
    @{
        Source = 'profiles\ah6\DCS World - AH-6J MH-6J - DCSExtra Baseline v0.1.siprofile'
        Output = 'profiles\ah6\DCS World - AH-6J MH-6J - Haptic Engine v1.0.0-test.1.siprofile'
        OldName = 'DCS AH-6J MH-6J - DCSExtra Baseline v0.1'
        NewName = 'DCS AH-6J MH-6J - Haptic Engine v1.0.0-test.1'
        ProfileId = 'f48741d2-fbc9-474e-a762-d8705c4ba666'
        Description = 'AH-6J/MH-6J aircraft-aware haptic-engine test profile with four-skid and light-airframe signals. Rotor telemetry remains an explicitly labelled proxy. Test-only until recorded replay and structured live seat validation pass.'
    },
    @{
        Source = 'profiles\ah64\DCS World - DCS AH-64D - DCSExtra Baseline v0.1.siprofile'
        Output = 'profiles\ah64\DCS World - DCS AH-64D - Haptic Engine v1.0.0-test.1.siprofile'
        OldName = 'DCS AH-64D - DCSExtra Baseline v0.1'
        NewName = 'DCS AH-64D - Haptic Engine v1.0.0-test.1'
        ProfileId = 'e254b84b-2ff6-4224-b47e-b70e0c669055'
        Description = 'AH-64D aircraft-aware haptic-engine test profile with wheel-sequence, heavy-landing, M230, Hydra, and Hellfire signals. Test-only until dedicated replay and structured live seat validation pass.'
    },
    @{
        Source = 'profiles\uh1\DCS World - DCS UH-1H - DCSExtra Baseline v0.1.siprofile'
        Output = 'profiles\uh1\DCS World - DCS UH-1H - Haptic Engine v1.0.0-test.1.siprofile'
        OldName = 'DCS UH-1H - DCSExtra Baseline v0.1'
        NewName = 'DCS UH-1H - Haptic Engine v1.0.0-test.1'
        ProfileId = '2cad4e8d-cd59-444a-b8e5-465a20d6ca37'
        Description = 'UH-1H aircraft-aware haptic-engine test profile with weight-on-skids, two-blade, torque-gauge, and verified weapon signals. Test-only until recorded replay and structured live seat validation pass.'
    },
    @{
        Source = 'profiles\oh58\DCS World - DCS OH-58D - DCSExtra Baseline v0.1.siprofile'
        Output = 'profiles\oh58\DCS World - DCS OH-58D - Haptic Engine v1.0.0-test.1.siprofile'
        OldName = 'DCS OH-58D - DCSExtra Baseline v0.1'
        NewName = 'DCS OH-58D - Haptic Engine v1.0.0-test.1'
        ProfileId = '005d8d13-f1b2-4d32-bfab-81093c681a2c'
        Description = 'OH-58D aircraft-aware haptic-engine provisional test profile with four-skid signals. Rotor and collective sources remain provisional. Not eligible for promotion until current replay and structured live seat validation pass.'
    }
)

$commonReplacements = [ordered]@{
    'TouchdownEnvelope' = 'Haptic.Touchdown.Envelope'
    'LeftWheelContactPulse' = 'Haptic.Contact.Left.Envelope'
    'RightWheelContactPulse' = 'Haptic.Contact.Right.Envelope'
    'LiftoffPulse' = 'Haptic.Liftoff.Envelope'
    'GroundRollIntensity' = 'Haptic.Ground.Roll.Intensity'
    'GroundResonanceIntensity' = 'Haptic.Ground.Resonance.Intensity'
    'SuspensionMovementIntensity' = 'Haptic.Ground.Suspension.Intensity'
    'GroundPitchRockIntensity' = 'Haptic.Ground.PitchRock.Intensity'
    'GroundRollRockIntensity' = 'Haptic.Ground.RollRock.Intensity'
    'WeightOnGroundIntensity' = 'Haptic.Ground.Weight.Intensity'
    'SkidScrubIntensity' = 'Haptic.Ground.SkidScrub.Intensity'
    'WheelShimmyIntensity' = 'Haptic.Ground.WheelShimmy.Intensity'
    'TailWheelCasterIntensity' = 'Haptic.Ground.TailwheelCaster.Intensity'
    'BrakingIntensity' = 'Haptic.Braking.Intensity'
    'BrakeDecelerationIntensity' = 'Haptic.Braking.Deceleration.Intensity'
    'RotorLoadIntensity' = 'Haptic.Rotor.Load.Intensity'
    'TransmissionLoadIntensity' = 'Haptic.Rotor.TransmissionLoad.Intensity'
    'RotorSpoolIntensity' = 'Haptic.Rotor.Spool.Intensity'
    'RotorDroopIntensity' = 'Haptic.Rotor.Droop.Intensity'
    'EngineGovernorResponseIntensity' = 'Haptic.Rotor.GovernorResponse.Intensity'
    'ManeuverResponseIntensity' = 'Haptic.Maneuver.Vertical.Intensity'
    'LateralManeuverIntensity' = 'Haptic.Maneuver.Lateral.Intensity'
    'ControlResponseIntensity' = 'Haptic.Maneuver.ControlResponse.Intensity'
    'TurbulenceIntensity' = 'Haptic.Aerodynamic.Turbulence.Intensity'
    'AirframeBuffetIntensity' = 'Haptic.Aerodynamic.Buffet.Intensity'
    'TranslationalLiftTransition' = 'Haptic.Aerodynamic.TranslationalLift.Intensity'
    'SettlingWithPowerCue' = 'Haptic.Aerodynamic.SettlingWithPower.Intensity'
    'BladeSlapIntensity' = 'Haptic.Aerodynamic.BladeSlap.Intensity'
    'AirframeImpactPulse' = 'Haptic.Airframe.Shock.Envelope'
    'DoorMovementIntensity' = 'Haptic.Mechanical.Doors.Intensity'
    'LightGunIntensity' = 'Haptic.Weapon.LightGun.Intensity'
    'HeavyGunIntensity' = 'Haptic.Weapon.HeavyGun.Intensity'
    'RocketLaunchIntensity' = 'Haptic.Weapon.Rocket.Envelope'
    'MissileLaunchIntensity' = 'Haptic.Weapon.Missile.Envelope'
    'FlareReleasePulse' = 'Haptic.Countermeasure.Flare.Envelope'
    'ChaffReleasePulse' = 'Haptic.Countermeasure.Chaff.Envelope'
}

foreach ($profile in $profiles) {
    $source = Join-Path $repositoryRoot $profile.Source
    $output = Join-Path $repositoryRoot $profile.Output
    $json = Get-Content -LiteralPath $source -Raw
    $json = $json.Replace($profile.OldName, $profile.NewName)
    $parsed = $json | ConvertFrom-Json
    $oldDescription = [string]$parsed.Description
    $oldId = [string]$parsed.ProfileId
    $json = $json.Replace($oldDescription, $profile.Description).Replace($oldId, $profile.ProfileId)
    foreach ($entry in $commonReplacements.GetEnumerator()) {
        $json = $json.Replace($entry.Key, $entry.Value)
    }
    if ($profile.NewName -like '*UH60L*') {
        $json = $json.Replace('TailWheelContactPulse', 'Haptic.UH60.TailwheelSettle.Envelope')
        $oldWheelDetail = 'var pulse = $prop(\u0027DcsExtraTelemetryPlugin.DCSExtra.Haptic.Contact.Left.Envelope\u0027) || $prop(\u0027DcsExtraTelemetryPlugin.DCSExtra.Haptic.Contact.Right.Envelope\u0027);\r\nreturn pulse ? 48 : 0;'
        $newWheelDetail = 'var left = $prop(\u0027DcsExtraTelemetryPlugin.DCSExtra.Haptic.Contact.Left.Envelope\u0027) > 0;\r\nvar right = $prop(\u0027DcsExtraTelemetryPlugin.DCSExtra.Haptic.Contact.Right.Envelope\u0027) > 0;\r\n// Centered contact detail: restrained for one main wheel, broader for both.\r\nif (left \u0026\u0026 right) return 42;\r\nreturn (left || right) ? 24 : 0;'
        if (!$json.Contains($oldWheelDetail)) { throw 'UH-60 main-wheel contact formula was not found.' }
        $json = $json.Replace($oldWheelDetail, $newWheelDetail)
    }
    elseif ($profile.NewName -like '*AH-6J*') {
        $json = $json.Replace('Haptic.Touchdown.Envelope', 'Haptic.AH6.LightLanding.Envelope')
    }
    elseif ($profile.NewName -like '*AH-64D*') {
        $json = $json.Replace('Haptic.Touchdown.Envelope', 'Haptic.AH64.HeavyLanding.Envelope')
    }
    elseif ($profile.NewName -like '*UH-1H*') {
        $json = $json.Replace('Haptic.Touchdown.Envelope', 'Haptic.UH1.WeightOnSkidsLanding.Envelope')
    }
    if ($profile.SafeProfile) {
        $safeProfile = $json | ConvertFrom-Json
        $safeProfile.GlobalGain = 30.0
        $safeProfile.UseProfileGain = $true
        $safeGroupGains = @{
            'Lifecycle' = 40.0
            'Rotor and Powertrain' = 35.0
            'Aerodynamics' = 30.0
            'Ground Interaction' = 40.0
            'Weapons and Countermeasures' = 45.0
        }
        foreach ($group in @($safeProfile.EffectsContainers)) {
            if ($safeGroupGains.ContainsKey($group.Description)) {
                $group.Gain = $safeGroupGains[$group.Description]
            }
        }
        $json = $safeProfile | ConvertTo-Json -Depth 100
    }
    [IO.File]::WriteAllText($output, $json, [Text.UTF8Encoding]::new($false))
    Write-Host "Generated $($profile.Output)"
}
