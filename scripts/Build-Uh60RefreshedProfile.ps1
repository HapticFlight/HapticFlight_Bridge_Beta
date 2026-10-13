param(
    [string]$SourceProfile = (Join-Path $PSScriptRoot '..\profiles\uh60\DCS World - DCS UH60L - DCSExtra Refreshed v1.8.9.siprofile'),
    [string]$OutputProfile = (Join-Path $PSScriptRoot '..\profiles\uh60\DCS World - DCS UH60L - DCSExtra Refreshed v1.9.0.siprofile')
)

$ErrorActionPreference = 'Stop'

function New-Formula([string]$Expression = '', [string]$PreExpression = '') {
    if ([string]::IsNullOrWhiteSpace($Expression) -and [string]::IsNullOrWhiteSpace($PreExpression)) {
        return [ordered]@{ Expression = '' }
    }

    # AttachDelegate properties are exposed to SimHub formulas beneath the
    # owning plugin class name. The Available Properties browser visually
    # groups them this way, but the earlier profile used only the short name.
    $Expression = $Expression.Replace('$prop(''DCSExtra.', '$prop(''DcsExtraTelemetryPlugin.DCSExtra.')
    $PreExpression = $PreExpression.Replace('$prop(''DCSExtra.', '$prop(''DcsExtraTelemetryPlugin.DCSExtra.')

    return [ordered]@{
        JSExt = 0
        Interpreter = 1
        Expression = $Expression.Trim()
        PreExpression = $PreExpression.Trim()
    }
}

function New-Effect {
    param(
        [string]$Name,
        [double]$Gain,
        [string]$Amplitude,
        [double]$Frequency,
        [string]$FrequencyFormula = '',
        [bool]$WhiteNoise = $false,
        [double]$WhiteNoiseAmount = 10,
        [string]$PreExpression = ''
    )

    $dynamicFrequency = -not [string]::IsNullOrWhiteSpace($FrequencyFormula)
    return [ordered]@{
        ContainerType = 'CustomEffectContainer'
        IsEnabled = $true
        Gain = $Gain
        Description = $Name
        FrontLeftFormula = New-Formula $Amplitude $PreExpression
        FrontRightFormula = New-Formula
        RearLeftFormula = New-Formula
        RearRightFormula = New-Formula
        ForceFrequencies = $dynamicFrequency
        FrontLeftFrequencyFormula = New-Formula $FrequencyFormula
        FrontRightFrequencyFormula = New-Formula
        RearLeftFrequencyFormula = New-Formula
        RearRightFrequencyFormula = New-Formula
        AlwaysExecute = $false
        ContainerId = [guid]::NewGuid().ToString()
        AggregationMode = 'Mono'
        Filter = [ordered]@{
            GammaValue = 1.0
            InputGain = 100.0
            MinimumForce = 0
            Threshold = 0
            FilterType = 'GammaFilter'
        }
        Output = [ordered]@{
            UseHighFrequency = $true
            HighFrequency = 80
            WhiteNoise = $WhiteNoiseAmount
            UseWhiteNoise = $WhiteNoise
            FrequencyBasedOnPreFilter = $false
            UsePrehemptiveMode = $false
            Frequency = $Frequency
            OutputType = 'ToneOutput'
        }
    }
}

function New-Group([string]$Name, [object[]]$Effects) {
    return [ordered]@{
        ContainerType = 'GroupContainer'
        IsEnabled = $true
        Gain = 100.0
        Description = $Name
        EffectsContainers = $Effects
        ContainerId = [guid]::NewGuid().ToString()
        Filter = $null
        Output = $null
    }
}

$profile = Get-Content -Raw -LiteralPath $SourceProfile | ConvertFrom-Json

$lifecycle = @(
    New-Effect -Name 'APU machinery' -Gain 16 -Frequency 55 -WhiteNoise $true -WhiteNoiseAmount 12 -PreExpression @'
root["apuMachineryLevel"] = 0;
'@ -Amplitude @'
var target = 0;
if ($prop('DCSExtra.Connected') && $prop('DCSExtra.Has.EngineLifecycle') && $prop('DCSExtra.ApuRunning')) target = 28;
var coefficient = target > root["apuMachineryLevel"] ? 0.08 : 0.04;
root["apuMachineryLevel"] = root["apuMachineryLevel"] +
    (target - root["apuMachineryLevel"]) * coefficient;
return Math.max(0, Math.min(28, root["apuMachineryLevel"]));
'@
    New-Effect -Name 'Engine starters' -Gain 24 -Frequency 48 -WhiteNoise $true -WhiteNoiseAmount 8 -PreExpression @'
root["engineStarterLevel"] = 0;
'@ -Amplitude @'
var active = $prop('DCSExtra.Connected') &&
    $prop('DCSExtra.Has.EngineLifecycle') &&
    ($prop('DCSExtra.Engine1StarterActive') || $prop('DCSExtra.Engine2StarterActive'));
var target = active ? 38 : 0;
var coefficient = target > root["engineStarterLevel"] ? 0.12 : 0.045;
root["engineStarterLevel"] = root["engineStarterLevel"] +
    (target - root["engineStarterLevel"]) * coefficient;
return Math.max(0, Math.min(38, root["engineStarterLevel"]));
'@
    New-Effect -Name 'Rotor spool transition' -Gain 26 -Frequency 18 -FrequencyFormula @'
if (!$prop('DCSExtra.Connected') || !$prop('DCSExtra.Has.RotorDynamics')) return 18;
return Math.max(8, Math.min(34, $prop('DCSExtra.BladePassHz')));
'@ -PreExpression @'
root["rotorSpoolLevel"] = 0;
'@ -Amplitude @'
if (!$prop('DCSExtra.Connected') || !$prop('DCSExtra.Has.PowertrainEffects')) {
    root["rotorSpoolLevel"] = root["rotorSpoolLevel"] * 0.90;
    if (root["rotorSpoolLevel"] < 0.05) root["rotorSpoolLevel"] = 0;
    return root["rotorSpoolLevel"];
}

var raw = Math.max(0, Math.min(1, $prop('DCSExtra.RotorSpoolIntensity')));
var cue = Math.max(0, Math.min(1, (raw - 0.10) / 0.35));
var target = 48 * Math.pow(cue, 0.85);
var coefficient = target > root["rotorSpoolLevel"] ? 0.10 : 0.035;
root["rotorSpoolLevel"] = root["rotorSpoolLevel"] +
    (target - root["rotorSpoolLevel"]) * coefficient;
if (target === 0 && root["rotorSpoolLevel"] < 0.05) root["rotorSpoolLevel"] = 0;
return Math.max(0, Math.min(48, root["rotorSpoolLevel"]));
'@
)

$rotor = @(
    New-Effect -Name 'Rotor 1-rev airframe pulse' -Gain 20 -Frequency 4.3 -FrequencyFormula @'
if (!$prop('DCSExtra.Connected') || !$prop('DCSExtra.Has.RotorDynamics')) return 1;
return Math.max(1, Math.min(80, $prop('DCSExtra.MainRotorHz')));
'@ -PreExpression @'
root["rotorOneRevLevel"] = 0;
'@ -Amplitude @'
var target = 0;
if ($prop('DCSExtra.Connected') && $prop('DCSExtra.Has.RotorDynamics') && $prop('DCSExtra.RotorRunning')) {
var load = Math.max(0, Math.min(1, $prop('DCSExtra.RotorLoadIntensity')));
var lateral = Math.max(0, Math.min(1, $prop('DCSExtra.LateralManeuverIntensity')));
var nr = Math.max(0, Math.min(1, $prop('DCSExtra.MainRotorRpmPct') / 100));
var governed = Math.max(0, Math.min(1, (nr - 0.15) / 0.80));
governed = governed * governed * (3 - 2 * governed);
var power = 0;
if ($prop('DCSExtra.Has.EngineTorque')) {
    power = Math.max($prop('DCSExtra.EngineTorqueLeftPct'), $prop('DCSExtra.EngineTorqueRightPct')) / 100;
} else if ($prop('DCSExtra.Has.TorqueGauge')) {
    power = $prop('DCSExtra.TorqueGaugeNormalized');
} else if ($prop('DCSExtra.Has.ControlPositions')) {
    power = $prop('DCSExtra.CollectivePosition');
}
power = Math.max(0, Math.min(1, (power - 0.12) / 0.68));
var airborne = !$prop('DCSExtra.OnGround');
var g = $prop('DCSExtra.Has.Acceleration') && airborne ? $prop('DCSExtra.AccelYG') : 1;
var positiveG = Math.max(0, Math.min(1, (g - 1.03) / 0.67));
var smoothG = positiveG * positiveG * (3 - 2 * positiveG);
positiveG = 0.40 * smoothG + 0.60 * positiveG * positiveG;
var negativeG = Math.max(0, Math.min(1, (0.97 - g) / 0.55));
if ($prop('DCSExtra.WeaponBurstActive')) { lateral *= 0.35; }
var control = Math.max(0, Math.min(1, $prop('DCSExtra.ControlResponseIntensity')));
var vv = Math.abs($prop('DCSExtra.VerticalVelocityMps'));
var vertical = Math.max(0, Math.min(1, (vv - 1.5) / 8.5));
target = Math.min(60, 9 + 21 * governed + 12 * power + 8 * load + 20 * positiveG + 4 * negativeG + 13 * lateral + 8 * control + 4 * vertical);
}
var coefficient = target > root["rotorOneRevLevel"] ? 0.35 : 0.14;
root["rotorOneRevLevel"] += (target - root["rotorOneRevLevel"]) * coefficient;
if (target === 0 && root["rotorOneRevLevel"] < 0.05) root["rotorOneRevLevel"] = 0;
return Math.max(0, Math.min(60, root["rotorOneRevLevel"]));
'@
    New-Effect -Name 'Rotor 2-rev structural harmonic' -Gain 16 -Frequency 8.6 -FrequencyFormula @'
if (!$prop('DCSExtra.Connected') || !$prop('DCSExtra.Has.RotorDynamics')) return 1;
return Math.max(1, Math.min(80, $prop('DCSExtra.MainRotorHz') * 2));
'@ -PreExpression @'
root["rotorTwoRevLevel"] = 0;
'@ -Amplitude @'
var target = 0;
if ($prop('DCSExtra.Connected') && $prop('DCSExtra.Has.RotorDynamics') && $prop('DCSExtra.RotorRunning')) {
var load = Math.max(0, Math.min(1, $prop('DCSExtra.RotorLoadIntensity')));
var lateral = Math.max(0, Math.min(1, $prop('DCSExtra.LateralManeuverIntensity')));
var nr = Math.max(0, Math.min(1, $prop('DCSExtra.MainRotorRpmPct') / 100));
var governed = Math.max(0, Math.min(1, (nr - 0.15) / 0.80));
governed = governed * governed * (3 - 2 * governed);
var power = 0;
if ($prop('DCSExtra.Has.EngineTorque')) {
    power = Math.max($prop('DCSExtra.EngineTorqueLeftPct'), $prop('DCSExtra.EngineTorqueRightPct')) / 100;
} else if ($prop('DCSExtra.Has.TorqueGauge')) {
    power = $prop('DCSExtra.TorqueGaugeNormalized');
} else if ($prop('DCSExtra.Has.ControlPositions')) {
    power = $prop('DCSExtra.CollectivePosition');
}
power = Math.max(0, Math.min(1, (power - 0.12) / 0.68));
var airborne = !$prop('DCSExtra.OnGround');
var g = $prop('DCSExtra.Has.Acceleration') && airborne ? $prop('DCSExtra.AccelYG') : 1;
var positiveG = Math.max(0, Math.min(1, (g - 1.03) / 0.67));
var smoothG = positiveG * positiveG * (3 - 2 * positiveG);
positiveG = 0.40 * smoothG + 0.60 * positiveG * positiveG;
var negativeG = Math.max(0, Math.min(1, (0.97 - g) / 0.55));
if ($prop('DCSExtra.WeaponBurstActive')) { lateral *= 0.35; }
var control = Math.max(0, Math.min(1, $prop('DCSExtra.ControlResponseIntensity')));
var vv = Math.abs($prop('DCSExtra.VerticalVelocityMps'));
var vertical = Math.max(0, Math.min(1, (vv - 1.5) / 8.5));
target = Math.min(52, 6 + 16 * governed + 10 * power + 7 * load + 16 * positiveG + 3 * negativeG + 14 * lateral + 6 * control + 3 * vertical);
}
var coefficient = target > root["rotorTwoRevLevel"] ? 0.35 : 0.14;
root["rotorTwoRevLevel"] += (target - root["rotorTwoRevLevel"]) * coefficient;
if (target === 0 && root["rotorTwoRevLevel"] < 0.05) root["rotorTwoRevLevel"] = 0;
return Math.max(0, Math.min(52, root["rotorTwoRevLevel"]));
'@
    New-Effect -Name 'Main rotor blade-pass 4-rev' -Gain 30 -Frequency 17.2 -FrequencyFormula @'
if (!$prop('DCSExtra.Connected') || !$prop('DCSExtra.Has.RotorDynamics')) return 1;
return Math.max(1, Math.min(80, $prop('DCSExtra.BladePassHz')));
'@ -PreExpression @'
root["rotorFourRevLevel"] = 0;
'@ -Amplitude @'
var target = 0;
if ($prop('DCSExtra.Connected') && $prop('DCSExtra.Has.RotorDynamics') && $prop('DCSExtra.RotorRunning')) {
var load = Math.max(0, Math.min(1, $prop('DCSExtra.RotorLoadIntensity')));
var lateral = Math.max(0, Math.min(1, $prop('DCSExtra.LateralManeuverIntensity')));
var nr = Math.max(0, Math.min(1, $prop('DCSExtra.MainRotorRpmPct') / 100));
var governed = Math.max(0, Math.min(1, (nr - 0.15) / 0.80));
governed = governed * governed * (3 - 2 * governed);
var power = 0;
if ($prop('DCSExtra.Has.EngineTorque')) {
    power = Math.max($prop('DCSExtra.EngineTorqueLeftPct'), $prop('DCSExtra.EngineTorqueRightPct')) / 100;
} else if ($prop('DCSExtra.Has.TorqueGauge')) {
    power = $prop('DCSExtra.TorqueGaugeNormalized');
} else if ($prop('DCSExtra.Has.ControlPositions')) {
    power = $prop('DCSExtra.CollectivePosition');
}
power = Math.max(0, Math.min(1, (power - 0.12) / 0.68));
var airborne = !$prop('DCSExtra.OnGround');
var g = $prop('DCSExtra.Has.Acceleration') && airborne ? $prop('DCSExtra.AccelYG') : 1;
var positiveG = Math.max(0, Math.min(1, (g - 1.03) / 0.67));
var smoothG = positiveG * positiveG * (3 - 2 * positiveG);
positiveG = 0.40 * smoothG + 0.60 * positiveG * positiveG;
var negativeG = Math.max(0, Math.min(1, (0.97 - g) / 0.55));
if ($prop('DCSExtra.WeaponBurstActive')) { lateral *= 0.35; }
var control = Math.max(0, Math.min(1, $prop('DCSExtra.ControlResponseIntensity')));
var vv = Math.abs($prop('DCSExtra.VerticalVelocityMps'));
var vertical = Math.max(0, Math.min(1, (vv - 1.5) / 8.5));
target = Math.min(62, 7 + 20 * governed + 16 * power + 12 * load + 10 * positiveG + 2 * negativeG + 5 * lateral + 4 * control + 2 * vertical);
}
var coefficient = target > root["rotorFourRevLevel"] ? 0.35 : 0.14;
root["rotorFourRevLevel"] += (target - root["rotorFourRevLevel"]) * coefficient;
if (target === 0 && root["rotorFourRevLevel"] < 0.05) root["rotorFourRevLevel"] = 0;
return Math.max(0, Math.min(62, root["rotorFourRevLevel"]));
'@
    New-Effect -Name 'Blade slap (descent BVI cue)' -Gain 20 -Frequency 17.2 -FrequencyFormula @'
if (!$prop('DCSExtra.Connected') || !$prop('DCSExtra.Has.BladeSlap')) return 1;
return Math.max(1, Math.min(80, $prop('DCSExtra.BladePassHz')));
'@ -Amplitude @'
if (!$prop('DCSExtra.Connected') || !$prop('DCSExtra.Has.BladeSlap') || $prop('DCSExtra.OnGround')) return 0;
var cue = Math.max(0, Math.min(1, $prop('DCSExtra.BladeSlapIntensity')));
return Math.min(50, 50 * Math.pow(cue, 0.92));
'@
    New-Effect -Name 'Transmission load' -Gain 16 -Frequency 22 -Amplitude @'
if (!$prop('DCSExtra.Connected') || !$prop('DCSExtra.Has.PowertrainEffects')) return 0;
// This is a secondary drive-train layer. Rotor harmonics already carry the
// primary load response, so keep the transmission texture subordinate.
return Math.min(28, 28 * $prop('DCSExtra.TransmissionLoadIntensity'));
'@
    New-Effect -Name 'Engine governor load response' -Gain 14 -Frequency 22 -WhiteNoise $false -WhiteNoiseAmount 0 -PreExpression @'
root["governorResponseLevel"] = 0;
'@ -Amplitude @'
var target = 0;
if ($prop('DCSExtra.Connected') && $prop('DCSExtra.Has.PowertrainEffects')) {
    var raw = Math.max(0, Math.min(1, $prop('DCSExtra.EngineGovernorResponseIntensity')));
    if (raw > 0.045) target = 45 * (raw - 0.045) / 0.955;
}
var coefficient = target > root["governorResponseLevel"] ? 0.12 : 0.04;
root["governorResponseLevel"] = root["governorResponseLevel"] +
    (target - root["governorResponseLevel"]) * coefficient;
if (target === 0 && root["governorResponseLevel"] < 0.05) root["governorResponseLevel"] = 0;
return Math.max(0, Math.min(45, root["governorResponseLevel"]));
'@
    New-Effect -Name 'Collective movement thump' -Gain 14 -Frequency 22 -PreExpression @'
root["collectiveThumpLevel"] = 0;
'@ -Amplitude @'
var target = 0;
if ($prop('DCSExtra.Connected') && $prop('DCSExtra.Has.ControlPositions')) {
    var rate = Math.max(0, $prop('DCSExtra.CollectiveMovementRate'));
    var cue = Math.max(0, Math.min(1, (rate - 0.02) / 0.55));
    target = 52 * Math.pow(cue, 0.75);
}
var coefficient = target > root["collectiveThumpLevel"] ? 0.22 : 0.07;
root["collectiveThumpLevel"] = root["collectiveThumpLevel"] +
    (target - root["collectiveThumpLevel"]) * coefficient;
if (target === 0 && root["collectiveThumpLevel"] < 0.05) root["collectiveThumpLevel"] = 0;
return Math.max(0, Math.min(52, root["collectiveThumpLevel"]));
'@
    New-Effect -Name 'Low rotor droop roughness' -Gain 20 -Frequency 15 -WhiteNoise $false -WhiteNoiseAmount 0 -PreExpression @'
root["rotorDroopLevel"] = 0;
'@ -Amplitude @'
var target = 0;
if ($prop('DCSExtra.Connected') && $prop('DCSExtra.Has.PowertrainEffects')) {
    var raw = Math.max(0, Math.min(1, $prop('DCSExtra.RotorDroopIntensity')));
    if (raw >= 0.04) target = 60 * raw;
}
var coefficient = target > root["rotorDroopLevel"] ? 0.15 : 0.05;
root["rotorDroopLevel"] = root["rotorDroopLevel"] +
    (target - root["rotorDroopLevel"]) * coefficient;
if (target === 0 && root["rotorDroopLevel"] < 0.05) root["rotorDroopLevel"] = 0;
return Math.max(0, Math.min(60, root["rotorDroopLevel"]));
'@
)

$aerodynamics = @(
    New-Effect -Name 'Maneuver load and vertical energy' -Gain 26 -Frequency 18 -WhiteNoise $false -WhiteNoiseAmount 0 -PreExpression @'
root["maneuverBody"] = 0;
'@ -Amplitude @'
if (!$prop('DCSExtra.Connected') || !$prop('DCSExtra.Has.AerodynamicEffects') || !$prop('DCSExtra.Has.AirData') || $prop('DCSExtra.OnGround')) return 0;
var maneuver = Math.max(0, Math.min(1, $prop('DCSExtra.ManeuverResponseIntensity')));
// Preserve the broader raw maneuver-load channel at a restrained weight.
// ManeuverResponseIntensity remains the primary shaped response signal.
var maneuverLoad = Math.max(0, Math.min(1, $prop('DCSExtra.ManeuverLoadIntensity')));
var lateral = Math.max(0, Math.min(1, $prop('DCSExtra.LateralManeuverIntensity')));
var control = Math.max(0, Math.min(1, $prop('DCSExtra.ControlResponseIntensity')));
if ($prop('DCSExtra.WeaponBurstActive')) {
    maneuver *= 0.78;
    lateral *= 0.35;
    control *= 0.78;
}
var governor = Math.max(0, Math.min(1, $prop('DCSExtra.EngineGovernorResponseIntensity')));
var vv = Math.abs($prop('DCSExtra.VerticalVelocityMps'));
var vertical = Math.max(0, Math.min(1, (vv - 1.5) / 8.5));
var combined = 1 -
    (1 - maneuver) *
    (1 - maneuverLoad * 0.45) *
    (1 - lateral * 0.68) *
    (1 - control * 0.85) *
    (1 - governor * 0.45) *
    (1 - vertical * 0.40);
combined = Math.max(0, Math.min(1, combined));
var target = 64 * Math.pow(combined, 0.90);
var coefficient = target > root["maneuverBody"] ? 0.12 : 0.04;
root["maneuverBody"] = root["maneuverBody"] + (target - root["maneuverBody"]) * coefficient;
if (target === 0 && root["maneuverBody"] < 0.05) root["maneuverBody"] = 0;
return Math.max(0, Math.min(64, root["maneuverBody"]));
'@
    New-Effect -Name 'Translational lift transition' -Gain 11 -Frequency 13 -WhiteNoise $false -WhiteNoiseAmount 0 -Amplitude @'
if (!$prop('DCSExtra.Connected') || !$prop('DCSExtra.Has.AerodynamicEffects')) return 0;
return Math.min(42, 42 * $prop('DCSExtra.TranslationalLiftTransition'));
'@
    New-Effect -Name 'Turbulence body thump' -Gain 16 -Frequency 16 -WhiteNoise $false -WhiteNoiseAmount 0 -PreExpression @'
root["turbulenceBody"] = 0;
'@ -Amplitude @'
if (!$prop('DCSExtra.Connected') || !$prop('DCSExtra.Has.AerodynamicEffects') || $prop('DCSExtra.OnGround')) {
    root["turbulenceBody"] = root["turbulenceBody"] * 0.92;
    if (root["turbulenceBody"] < 0.05) root["turbulenceBody"] = 0;
    return root["turbulenceBody"];
}

var raw = Math.max(0, Math.min(1, $prop('DCSExtra.TurbulenceIntensity')));
var cue = Math.max(0, Math.min(1, (raw - 0.12) / 0.45));
var target = 42 * Math.pow(cue, 0.90);
if ($prop('DCSExtra.WeaponBurstActive')) target *= 0.20;
var coefficient = target > root["turbulenceBody"] ? 0.09 : 0.035;
root["turbulenceBody"] = root["turbulenceBody"] +
    (target - root["turbulenceBody"]) * coefficient;
if (target === 0 && root["turbulenceBody"] < 0.05) root["turbulenceBody"] = 0;
return Math.max(0, Math.min(42, root["turbulenceBody"]));
'@
    New-Effect -Name 'Airframe buffet texture' -Gain 5 -Frequency 22 -WhiteNoise $false -WhiteNoiseAmount 0 -PreExpression @'
root["airframeBuffetTexture"] = 0;
'@ -Amplitude @'
if (!$prop('DCSExtra.Connected') || !$prop('DCSExtra.Has.AerodynamicEffects') || $prop('DCSExtra.OnGround')) {
    root["airframeBuffetTexture"] = root["airframeBuffetTexture"] * 0.90;
    if (root["airframeBuffetTexture"] < 0.05) root["airframeBuffetTexture"] = 0;
    return root["airframeBuffetTexture"];
}

var raw = Math.max(0, Math.min(1, $prop('DCSExtra.AirframeBuffetIntensity')));
// Flight captures show useful UH-60 buffet below the former 0.45 onset.
var cue = Math.max(0, Math.min(1, (raw - 0.19) / 0.38));
var target = 18 * Math.pow(cue, 1.20);
if ($prop('DCSExtra.WeaponBurstActive')) target = 0;
var coefficient = target > root["airframeBuffetTexture"] ? 0.15 : 0.035;
root["airframeBuffetTexture"] = root["airframeBuffetTexture"] +
    (target - root["airframeBuffetTexture"]) * coefficient;
if (target === 0 && root["airframeBuffetTexture"] < 0.05) root["airframeBuffetTexture"] = 0;
return Math.max(0, Math.min(18, root["airframeBuffetTexture"]));
'@
    New-Effect -Name 'High-speed airframe texture' -Gain 10 -Frequency 30 -WhiteNoise $false -WhiteNoiseAmount 0 -PreExpression @'
root["cruiseBuffetLevel"] = 0;
'@ -Amplitude @'
var target = 0;
if ($prop('DCSExtra.Connected') && $prop('DCSExtra.Has.AirData') && !$prop('DCSExtra.OnGround')) {
var ias = $prop('DCSExtra.IndicatedAirSpeedMps');
// Begin near maximum cruise and build toward the module's declared maximum
// speed. This remains a restrained airframe texture, not a VNE warning.
var onset = 75;
var full = 95;
var cue = (ias - onset) / (full - onset);
cue = Math.max(0, Math.min(1, cue));
target = 28 * cue * cue;
}
var coefficient = target > root["cruiseBuffetLevel"] ? 0.09 : 0.035;
root["cruiseBuffetLevel"] = root["cruiseBuffetLevel"] +
    (target - root["cruiseBuffetLevel"]) * coefficient;
if (target === 0 && root["cruiseBuffetLevel"] < 0.05) root["cruiseBuffetLevel"] = 0;
return Math.max(0, Math.min(28, root["cruiseBuffetLevel"]));
'@
    New-Effect -Name 'Powered low-speed descent cue' -Gain 14 -Frequency 12 -WhiteNoise $false -WhiteNoiseAmount 0 -Amplitude @'
if (!$prop('DCSExtra.Connected') || !$prop('DCSExtra.Has.AerodynamicEffects')) return 0;
var cue = Math.max(0, Math.min(1, $prop('DCSExtra.SettlingWithPowerCue')));
return Math.min(34, 34 * cue);
'@
    New-Effect -Name 'Hard maneuver pulse' -Gain 18 -Frequency 18 -Amplitude @'
if (!$prop('DCSExtra.Connected') || !$prop('DCSExtra.Has.AerodynamicEffects')) return 0;
return $prop('DCSExtra.HardManeuverPulse') ? 55 : 0;
'@
    New-Effect -Name 'Airframe impact pulse' -Gain 30 -Frequency 18 -Amplitude @'
if (!$prop('DCSExtra.Connected') || !$prop('DCSExtra.Has.AerodynamicEffects')) return 0;
if ($prop('DCSExtra.WeaponBurstActive')) return 0;
return $prop('DCSExtra.AirframeImpactPulse') ? 82 : 0;
'@
)

$ground = @(
    New-Effect -Name 'Touchdown body impact' -Gain 34 -Frequency 22 -Amplitude @'
if (!$prop('DCSExtra.Connected') || !$prop('DCSExtra.Has.GearStruts')) return 0;
return Math.min(85, 85 * $prop('DCSExtra.TouchdownEnvelope'));
'@
    New-Effect -Name 'Main wheel contact detail' -Gain 17 -Frequency 30 -Amplitude @'
if (!$prop('DCSExtra.Connected') || !$prop('DCSExtra.Has.GearStruts')) return 0;
var pulse = $prop('DCSExtra.LeftWheelContactPulse') || $prop('DCSExtra.RightWheelContactPulse');
return pulse ? 48 : 0;
'@
    New-Effect -Name 'Tailwheel settle tick' -Gain 18 -Frequency 22 -Amplitude @'
if (!$prop('DCSExtra.Connected') || !$prop('DCSExtra.Has.GearStruts')) return 0;
return $prop('DCSExtra.TailWheelContactPulse') ? 55 : 0;
'@
    New-Effect -Name 'Liftoff unloading' -Gain 12 -Frequency 14 -Amplitude @'
if (!$prop('DCSExtra.Connected') || !$prop('DCSExtra.Has.GearStruts')) return 0;
return $prop('DCSExtra.LiftoffPulse') ? 34 : 0;
'@
    New-Effect -Name 'Ground roll base' -Gain 18 -Frequency 24 -FrequencyFormula @'
if (!$prop('DCSExtra.Connected') || !$prop('DCSExtra.Has.GearStruts')) return 20;
return Math.max(12, Math.min(38, 14 + $prop('DCSExtra.GroundSpeedMps') * 1.4));
'@ -Amplitude @'
if (!$prop('DCSExtra.Connected') || !$prop('DCSExtra.Has.GearStruts') || !$prop('DCSExtra.OnGround')) return 0;
return Math.min(45, 45 * $prop('DCSExtra.GroundRollIntensity'));
'@
    New-Effect -Name 'Suspension surface roughness' -Gain 18 -Frequency 23 -WhiteNoise $true -WhiteNoiseAmount 10 -PreExpression @'
root["suspensionRoughnessLevel"] = 0;
'@ -Amplitude @'
var target = 0;
if ($prop('DCSExtra.Connected') && $prop('DCSExtra.Has.GearStruts') && $prop('DCSExtra.OnGround')) {
    var raw = Math.max(0, Math.min(1, $prop('DCSExtra.SuspensionMovementIntensity')));
    var cue = Math.max(0, Math.min(1, (raw - 0.005) / 0.20));
    target = 50 * Math.pow(cue, 0.75);
}
var coefficient = target > root["suspensionRoughnessLevel"] ? 0.18 : 0.06;
root["suspensionRoughnessLevel"] = root["suspensionRoughnessLevel"] +
    (target - root["suspensionRoughnessLevel"]) * coefficient;
if (target === 0 && root["suspensionRoughnessLevel"] < 0.05) root["suspensionRoughnessLevel"] = 0;
return Math.max(0, Math.min(50, root["suspensionRoughnessLevel"]));
'@
    New-Effect -Name 'Wheel shimmy' -Gain 10 -Frequency 38 -WhiteNoise $true -WhiteNoiseAmount 8 -PreExpression @'
root["wheelShimmyLevel"] = 0;
'@ -Amplitude @'
var target = 0;
if ($prop('DCSExtra.Connected') && $prop('DCSExtra.Has.DetailedGroundEffects') && $prop('DCSExtra.OnGround')) {
    target = 36 * Math.max(0, Math.min(1, $prop('DCSExtra.WheelShimmyIntensity')));
}
var coefficient = target > root["wheelShimmyLevel"] ? 0.18 : 0.06;
root["wheelShimmyLevel"] = root["wheelShimmyLevel"] +
    (target - root["wheelShimmyLevel"]) * coefficient;
if (target === 0 && root["wheelShimmyLevel"] < 0.05) root["wheelShimmyLevel"] = 0;
return Math.max(0, Math.min(36, root["wheelShimmyLevel"]));
'@
    New-Effect -Name 'Tailwheel caster' -Gain 9 -Frequency 17 -Amplitude @'
if (!$prop('DCSExtra.Connected') || !$prop('DCSExtra.Has.DetailedGroundEffects') || !$prop('DCSExtra.OnGround')) return 0;
return Math.min(32, 32 * $prop('DCSExtra.TailWheelCasterIntensity'));
'@
    New-Effect -Name 'Gear-borne rotor vibration' -Gain 8 -Frequency 13 -PreExpression @'
root["gearBorneRotorLevel"] = 0;
'@ -Amplitude @'
var target = 0;
if ($prop('DCSExtra.Connected') && $prop('DCSExtra.Has.DetailedGroundEffects') && $prop('DCSExtra.OnGround')) {
    var raw = Math.max(0, Math.min(1, $prop('DCSExtra.GroundResonanceIntensity')));
    var cue = Math.max(0, Math.min(1, (raw - 0.08) / 0.28));
    var groundMotion = 26 * Math.pow(cue, 0.90);

    // The recorded UH-60 struts report a low normalized supported weight even
    // when settled on all three wheels. Preserve a restrained rotor-transmitted
    // floor on any confirmed contact, then strengthen it progressively as the
    // gear carries more weight. Keep this separate from the 17.2 Hz 4/rev pulse.
    var rotorTransfer = 0;
    if ($prop('DCSExtra.Has.RotorDynamics') && $prop('DCSExtra.RotorRunning')) {
        var nr = Math.max(0, Math.min(1, $prop('DCSExtra.MainRotorRpmPct') / 100));
        var governed = Math.max(0, Math.min(1, (nr - 0.70) / 0.25));
        governed = governed * governed * (3 - 2 * governed);
        var supportedWeight = Math.max(0, Math.min(1,
            $prop('DCSExtra.WeightOnGroundIntensity') / 0.20));
        rotorTransfer = governed * (6 + 10 * Math.sqrt(supportedWeight));
    }
    target = Math.min(30, Math.max(groundMotion, rotorTransfer));
}
var coefficient = target > root["gearBorneRotorLevel"] ? 0.10 : 0.04;
root["gearBorneRotorLevel"] += (target - root["gearBorneRotorLevel"]) * coefficient;
if (target === 0 && root["gearBorneRotorLevel"] < 0.05) root["gearBorneRotorLevel"] = 0;
return Math.max(0, Math.min(30, root["gearBorneRotorLevel"]));
'@
    New-Effect -Name 'Ground support rocking' -Gain 10 -Frequency 16 -PreExpression @'
root["groundRockingLevel"] = 0;
'@ -Amplitude @'
var target = 0;
if ($prop('DCSExtra.Connected') && $prop('DCSExtra.Has.DetailedGroundEffects') && $prop('DCSExtra.OnGround')) {
    var pitch = Math.max(0, Math.min(1, $prop('DCSExtra.GroundPitchRockIntensity')));
    var roll = Math.max(0, Math.min(1, $prop('DCSExtra.GroundRollRockIntensity')));
    target = 36 * Math.pow(Math.max(pitch, roll), 0.80);
}
var coefficient = target > root["groundRockingLevel"] ? 0.22 : 0.08;
root["groundRockingLevel"] += (target - root["groundRockingLevel"]) * coefficient;
if (target === 0 && root["groundRockingLevel"] < 0.05) root["groundRockingLevel"] = 0;
return Math.max(0, Math.min(36, root["groundRockingLevel"]));
'@
    New-Effect -Name 'Wheel braking load' -Gain 24 -Frequency 22 -PreExpression @'
root["wheelBrakingLevel"] = 0;
'@ -Amplitude @'
var target = 0;
if ($prop('DCSExtra.Connected') && $prop('DCSExtra.Has.Brakes') && $prop('DCSExtra.OnGround')) {
    var braking = Math.max(0, Math.min(1, $prop('DCSExtra.BrakingIntensity')));
    var deceleration = Math.max(0, Math.min(1, $prop('DCSExtra.BrakeDecelerationIntensity')));
    target = 70 * Math.pow(Math.max(braking, deceleration * 0.75), 0.72);
}
var coefficient = target > root["wheelBrakingLevel"] ? 0.25 : 0.10;
root["wheelBrakingLevel"] += (target - root["wheelBrakingLevel"]) * coefficient;
if (target === 0 && root["wheelBrakingLevel"] < 0.05) root["wheelBrakingLevel"] = 0;
return Math.max(0, Math.min(70, root["wheelBrakingLevel"]));
'@
)

$weapons = @(
    New-Effect -Name 'M134 and light-gun sustained fire' -Gain 27 -Frequency 50 -FrequencyFormula @'
if (!$prop('DCSExtra.Connected') || !$prop('DCSExtra.Has.WeaponEffects')) return 50;
var name = String($prop('DCSExtra.LastFiredWeaponName') || '').toLowerCase();
if (name.indexOf('m134') >= 0 || name.indexOf('minigun') >= 0) return 50;
return 43;
'@ -WhiteNoise $true -WhiteNoiseAmount 6 -Amplitude @'
if (!$prop('DCSExtra.Connected') || !$prop('DCSExtra.Has.WeaponEffects')) return 0;
return Math.min(75, 75 * $prop('DCSExtra.LightGunIntensity'));
'@
    New-Effect -Name 'M134 low body harmonic' -Gain 12 -Frequency 25 -Amplitude @'
if (!$prop('DCSExtra.Connected') || !$prop('DCSExtra.Has.WeaponEffects')) return 0;
var name = String($prop('DCSExtra.LastFiredWeaponName') || '').toLowerCase();
if (name.indexOf('m134') < 0 && name.indexOf('minigun') < 0) return 0;
return Math.min(38, 38 * $prop('DCSExtra.LightGunIntensity'));
'@
    New-Effect -Name 'Weapon-specific heavy gun rhythm' -Gain 24 -Frequency 21 -FrequencyFormula @'
if (!$prop('DCSExtra.Connected') || !$prop('DCSExtra.Has.WeaponEffects')) return 21;
var name = String($prop('DCSExtra.LastFiredWeaponName') || '').toLowerCase();
if (name.indexOf('m230') >= 0) return 20.8;
if (name.indexOf('gau-19') >= 0 || name.indexOf('gau19') >= 0) return 21.7;
return 26;
'@ -PreExpression @'
root["heavyGunRhythmLevel"] = 0;
'@ -Amplitude @'
var target = 0;
if ($prop('DCSExtra.Connected') && $prop('DCSExtra.Has.WeaponEffects')) {
    target = Math.min(70, 70 * $prop('DCSExtra.HeavyGunIntensity'));
}
var coefficient = target > root["heavyGunRhythmLevel"] ? 0.28 : 0.12;
root["heavyGunRhythmLevel"] = root["heavyGunRhythmLevel"] +
    (target - root["heavyGunRhythmLevel"]) * coefficient;
if (target === 0 && root["heavyGunRhythmLevel"] < 0.05) root["heavyGunRhythmLevel"] = 0;
return Math.max(0, Math.min(70, root["heavyGunRhythmLevel"]));
'@
    New-Effect -Name 'M230 individual-shot body thump' -Gain 10 -Frequency 10.4 -PreExpression @'
root["m230BodyThumpLevel"] = 0;
'@ -Amplitude @'
var target = 0;
if (!$prop('DCSExtra.Connected') || !$prop('DCSExtra.Has.WeaponEffects')) {
    root["m230BodyThumpLevel"] *= 0.82;
    if (root["m230BodyThumpLevel"] < 0.05) root["m230BodyThumpLevel"] = 0;
    return root["m230BodyThumpLevel"];
}
var name = String($prop('DCSExtra.LastFiredWeaponName') || '').toLowerCase();
if (name.indexOf('m230') >= 0) target = Math.min(48, 48 * $prop('DCSExtra.HeavyGunIntensity'));
var coefficient = target > root["m230BodyThumpLevel"] ? 0.30 : 0.13;
root["m230BodyThumpLevel"] = root["m230BodyThumpLevel"] +
    (target - root["m230BodyThumpLevel"]) * coefficient;
if (target === 0 && root["m230BodyThumpLevel"] < 0.05) root["m230BodyThumpLevel"] = 0;
return Math.max(0, Math.min(48, root["m230BodyThumpLevel"]));
'@
    New-Effect -Name 'GAU-19 recoil harmonic' -Gain 14 -Frequency 43.4 -Amplitude @'
if (!$prop('DCSExtra.Connected') || !$prop('DCSExtra.Has.WeaponEffects')) return 0;
var name = String($prop('DCSExtra.LastFiredWeaponName') || '').toLowerCase();
if (name.indexOf('gau-19') < 0 && name.indexOf('gau19') < 0) return 0;
return Math.min(42, 42 * $prop('DCSExtra.HeavyGunIntensity'));
'@
    New-Effect -Name 'Rocket launch kick' -Gain 32 -Frequency 32 -Amplitude @'
if (!$prop('DCSExtra.Connected') || !$prop('DCSExtra.Has.WeaponEffects')) return 0;
return Math.min(82, 82 * $prop('DCSExtra.RocketLaunchIntensity'));
'@
    New-Effect -Name 'Rocket launch body reaction' -Gain 20 -Frequency 18 -Amplitude @'
if (!$prop('DCSExtra.Connected') || !$prop('DCSExtra.Has.WeaponEffects')) return 0;
return Math.min(55, 55 * $prop('DCSExtra.RocketLaunchIntensity'));
'@
    New-Effect -Name 'Weapon-specific missile body' -Gain 34 -Frequency 20 -FrequencyFormula @'
if (!$prop('DCSExtra.Connected') || !$prop('DCSExtra.Has.WeaponEffects')) return 20;
var name = String($prop('DCSExtra.LastFiredWeaponName') || '').toLowerCase();
if (name.indexOf('atas') >= 0 || name.indexOf('stinger') >= 0 || name.indexOf('fim-92') >= 0 || name.indexOf('fim_92') >= 0) return 30;
return 20;
'@ -Amplitude @'
if (!$prop('DCSExtra.Connected') || !$prop('DCSExtra.Has.WeaponEffects')) return 0;
var name = String($prop('DCSExtra.LastFiredWeaponName') || '').toLowerCase();
var atas = name.indexOf('atas') >= 0 || name.indexOf('stinger') >= 0 || name.indexOf('fim-92') >= 0 || name.indexOf('fim_92') >= 0;
var scale = atas ? 55 : 85;
return Math.min(scale, scale * $prop('DCSExtra.MissileLaunchIntensity'));
'@
    New-Effect -Name 'Missile motor ignition' -Gain 18 -Frequency 28 -FrequencyFormula @'
if (!$prop('DCSExtra.Connected') || !$prop('DCSExtra.Has.WeaponEffects')) return 28;
var name = String($prop('DCSExtra.LastFiredWeaponName') || '').toLowerCase();
if (name.indexOf('atas') >= 0 || name.indexOf('stinger') >= 0 || name.indexOf('fim-92') >= 0 || name.indexOf('fim_92') >= 0) return 32;
return 28;
'@ -Amplitude @'
if (!$prop('DCSExtra.Connected') || !$prop('DCSExtra.Has.WeaponEffects')) return 0;
var name = String($prop('DCSExtra.LastFiredWeaponName') || '').toLowerCase();
var atas = name.indexOf('atas') >= 0 || name.indexOf('stinger') >= 0 || name.indexOf('fim-92') >= 0 || name.indexOf('fim_92') >= 0;
var scale = atas ? 60 : 48;
return Math.min(scale, scale * $prop('DCSExtra.MissileLaunchIntensity'));
'@
    New-Effect -Name 'Flare dispenser pulse' -Gain 11 -Frequency 45 -Amplitude @'
if (!$prop('DCSExtra.Connected') || !$prop('DCSExtra.Has.Countermeasures')) return 0;
return $prop('DCSExtra.FlareReleasePulse') ? 38 : 0;
'@
    New-Effect -Name 'Chaff dispenser pulse' -Gain 9 -Frequency 35 -Amplitude @'
if (!$prop('DCSExtra.Connected') || !$prop('DCSExtra.Has.Countermeasures')) return 0;
return $prop('DCSExtra.ChaffReleasePulse') ? 30 : 0;
'@
)

$profile.EffectsContainers = @(
    New-Group 'Lifecycle' $lifecycle
    New-Group 'Rotor and Powertrain' $rotor
    New-Group 'Aerodynamics' $aerodynamics
    New-Group 'Ground Interaction' $ground
    New-Group 'Weapons and Countermeasures' $weapons
)

$profile.Name = 'DCS UH60L - DCSExtra Refreshed v1.9.0'
$profile.Description = 'UH-60L/DAP tactile profile with seat-calibrated rotor baseline presence, torque-first power swell, signed-G and lateral/control response, and asymmetric smoothing. v1.9.0 renames the former ground-resonance cue to gear-borne rotor vibration and adds restrained rotor-RPM and supported-weight reinforcement during confirmed ground contact.'
$profile.ProfileId = [guid]::NewGuid().ToString()
$profile.LastLoaded = '0001-01-01T00:00:00'
$profile.GlobalGain = 50.0

$resolvedOutput = [System.IO.Path]::GetFullPath($OutputProfile)
[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($resolvedOutput)) | Out-Null
$json = $profile | ConvertTo-Json -Depth 100
[System.IO.File]::WriteAllText($resolvedOutput, $json, [System.Text.UTF8Encoding]::new($false))
Write-Output $resolvedOutput
