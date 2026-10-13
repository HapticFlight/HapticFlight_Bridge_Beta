# Realism-focused ShakeIt recipes

These are starting points for SimHub JavaScript/NCalc effects, not fixed tuning. Every effect must be gated by `DCSExtra.Connected` and its matching `DCSExtra.Has.*` property so an unsupported or stale signal cannot drive a shaker.

## Rotor texture

- Frequency: use `DCSExtra.BladePassHz` for the primary blade-pass layer.
- Amplitude: start with `0.10 + 0.20 * DCSExtra.RotorLoadIntensity` while `DCSExtra.RotorRunning` is true.
- Add a quieter subharmonic at `DCSExtra.MainRotorHz * 2` to provide body rather than buzz.
- Use `DCSExtra.RotorSpoolIntensity` as a temporary gain during startup and shutdown.

The nominal full-speed targets are about 17.2 Hz for the four-blade UH-60, 19.3 Hz for the four-blade AH-64D, 39.2 Hz for the five-blade AH/MH-6, and 10.8 Hz for the two-blade UH-1H. The exported frequency follows the module adapter instead of remaining fixed.

## Collective and drivetrain

- Low-frequency load: `DCSExtra.TransmissionLoadIntensity` at 22-30 Hz.
- Collective movement thump: `min(1, DCSExtra.CollectiveMovementRate / 2)` for a short low-frequency response.
- Rotor droop: apply a small deadband to `DCSExtra.RotorDroopIntensity` before using it at 14-20 Hz; the derived value may otherwise form a faint governed-rotor floor.
- Governor response: `DCSExtra.EngineGovernorResponseIntensity` is a short corrective-drive cue derived from real engine-RPM movement, not a torque claim. Keep it below the continuous rotor layer.
- UH-60 startup: use `ApuRunning`, starter states, `RotorSpoolIntensity`, and `RotorRunning` as separate stages.

`EngineTorqueLeftPct` and `EngineTorqueRightPct` intentionally remain disabled unless a real module source is available. Do not substitute engine RPM and label it torque.

For the UH-1H, `TorqueGaugeNormalized` is the actual cockpit torque-gauge animation (`0..1`), not a calibrated percent value. Gate it with `Has.TorqueGauge`. A useful Huey drivetrain layer is 18-28 Hz with gentle gain from this signal; retain headroom for two-blade rotor beats and weapons.

## Weapons

- Door/minigun: sustain `DCSExtra.LightGunIntensity` around 38-48 Hz and add a small 18-24 Hz body layer.
- 30 mm/heavy gun: use `DCSExtra.HeavyGunIntensity` for a stronger 20-32 Hz impulse.
- Rockets: use `DCSExtra.RocketLaunchIntensity` as a 100-180 ms impulse, with a short 25-40 Hz kick and lower after-body.
- Missiles: use `DCSExtra.MissileLaunchIntensity` as a 250-450 ms launch transient with a brief low-frequency kick followed by a softer rumble.
- Use `WeaponClass`, `WeaponBurstActive`, and `WeaponBurstDurationMs` to choose the envelope. Do not use total payload count as an effect amplitude.

## Aerodynamics

- Turbulence: modulate a noise/rumble layer with `DCSExtra.TurbulenceIntensity`.
- Control response: `DCSExtra.ControlResponseIntensity` identifies brisk collective/airframe response. Use it for a restrained transient, not as weather turbulence.
- Airframe buffet: use `DCSExtra.AirframeBuffetIntensity`, with the output capped below weapon and touchdown strength.
- Translational-lift transition: use `DCSExtra.TranslationalLiftTransition` as a subtle temporary texture, not a strong bump.
- Derived VRS-risk: `DCSExtra.VrsRiskIntensity` uses sustained low-forward-speed descent, collective/rotor demand, governed rotor state, optional AGL ground-effect attenuation, and observed linear/angular instability. It is an optional warning texture, not a DCS vortex-ring-state export. `DCSExtra.SettlingWithPowerCue` remains as a compatibility alias.
- Hard events: `HardManeuverPulse` and `AirframeImpactPulse` are heuristic one-shot triggers. They are disabled and labelled optional in the realism profiles because DCS does not identify the physical cause reliably. The exporter suppresses disturbance-derived impact, turbulence, and buffet during detected weapon bursts so gun recoil is not counted twice.
- Blade slap: use `DCSExtra.BladeSlapIntensity` at `DCSExtra.BladePassHz`. The cue is intentionally restricted to forward descents with the rotor near governed speed; it does not claim that DCS exposes blade/vortex geometry directly. Keep it separate from continuous rotor texture and gate it with `DCSExtra.Has.BladeSlap`.

NASA describes blade-vortex interaction as the impulsive source commonly called blade slap, occurring particularly in forward descents such as landing approaches. The UH-60 flight research included descent rates of 200-900 ft/min. These sources support the cue's flight-state gate, but not aircraft-specific amplitude calibration: [NASA IRAP overview](https://rotorcraft.arc.nasa.gov/Research/Programs/IRAP.html) and [NASA UH-60 flight tests](https://rotorcraft.arc.nasa.gov/Research/Programs/IRAP/IRAP-UH60.html).

## UH-60 ground handling

- Touchdown body: use `TouchdownEnvelope` (or explicitly multiply `TouchdownPulse * TouchdownStrength`) plus individual wheel contact pulses. `TouchdownStrength` is a latched last-event diagnostic and must not directly drive a continuous effect.
- Direct left/right main-wheel contact is a separate short cue from the touchdown body. Engine 1.7.1 gives direct main-wheel edges more profile headroom than tail-only edges; it does not report physical wheel force.
- Taxi: `GroundRollIntensity` with `SuspensionMovementIntensity` for surface roughness.
- Gear-borne vibration: the legacy `GroundResonanceIntensity` property is a quiet, continuous loaded-airframe layer while weight is on the gear; it is not true rotor/airframe ground resonance or an impact.
- Rocking: send `GroundRollRockIntensity` more strongly left/right and `GroundPitchRockIntensity` fore/aft if the shaker layout supports zones.
- Shimmy and caster: keep `WheelShimmyIntensity` and `TailWheelCasterIntensity` subtle; they should disappear when airborne.
- Liftoff: use `LiftoffPulse` as a small enabled unloading release, not a touchdown-style impact. It is a confirmed contact-state transition, not an optional augmented cue.

The exported raw UH-60 strut values retain the mod's animation offsets. Contact, touchdown, liftoff, weight-on-ground, taxi, shimmy, and caster signals use an aircraft-specific baseline and hysteresis, so effect profiles should gate on those derived properties rather than applying their own `strut > 0` test.

## AH/MH-6 skid handling

- Landing: combine `TouchdownStrength` with the four individual skid contact pulses.
- Uneven contact: route front/rear and left/right contact pulses to matching shaker zones where possible.
- Ground slide: drive a coarse low-frequency texture from `SkidScrubIntensity`.
- Wheel braking has separate stages: `Haptic.Braking.Application` is direct pedal position, `Haptic.Braking.Deceleration` is measured deceleration/load, and `Haptic.Ground.WheelSkid` is a restrained derived lateral skid risk. The last requires brake demand, rolling speed, and measured lateral ground motion; it does not claim a locked wheel or alter surface texture.
- Weight transfer: use `GroundPitchRockIntensity`, `GroundRollRockIntensity`, and `WeightOnGroundIntensity` for skid sag and rocking.

## Official AH-64D

- Use `BladePassHz` for the four-blade rotor layer and `MainRotorHz * 2` for a restrained airframe beat.
- Use `HeavyGunIntensity` for M230 amplitude, `RocketLaunchIntensity` for Hydra rockets, and `MissileLaunchIntensity` for Hellfire launches. Use `NominalWeaponRateHz` or a name-keyed table for cadence; `DetectedWeaponRateHz` is deliberately not audio-accurate.

## Maneuver layering

- Use `RotorLoadIntensity` for collective/power demand and `ManeuverResponseIntensity` for normal-G loading. The latter has a short attack and slower release to preserve onset while stabilizing sustained banks and pull-ups.
- Use `NormalGDeviation` when an effect must distinguish a pull from a pushover. `PositiveGIntensity` and `NegativeGIntensity` are normalized, source-side deadbanded forms of the same signed signal; `ManeuverResponseIntensity` remains deliberately unsigned for broad maneuver cues.
- `StrutCompressionRate` is signed: positive means the aggregate gear compression is increasing (loading/contact), and negative means it is decreasing (unloading/rebound). It includes a source-side rest deadband.
- The hook suppresses weapon-recoil motion from disturbance-derived turbulence, buffet, lateral maneuver, and impact cues. Turbulence, buffet, and rotor-droop outputs also apply small source-side deadbands before profiles see them.
- Blend `LateralManeuverIntensity` below the normal-G cue for rolls, sideslips, and pedal motion. It is suppressed during weapon bursts so recoil is not counted twice.
- Use the three wheel-contact pulses and `TouchdownStrength` for landing; use shimmy and suspension motion during taxi.
- `MasterArmOn` follows the pilot ARM/SAFE indicators. `WeaponSelectorName` reports the momentary cyclic weapon-action switch and deliberately returns `NONE` when it is released.

## Official UH-1H

- Emphasize the 10.8 Hz two-blade pass and a restrained harmonic rather than a high-frequency turbine buzz.
- Combine `TorqueGaugeNormalized`, `RotorDroopIntensity`, and `LowRotorRpmWarning` for load and low-RPM feedback.
- `OnGround`, touchdown, liftoff, and skid scrub use the module's weight-on-skids signal. Independent skid compression is not invented, so `Has.Skids` remains false.
- M134/M60 station guns classify as `LIGHT_GUN`; rockets use the standard rocket impulse.

## Safe JavaScript pattern

```javascript
if (!$prop('DCSExtra.Connected') || !$prop('DCSExtra.Has.PowertrainEffects')) {
  return 0;
}

return Math.min(1, $prop('DCSExtra.TransmissionLoadIntensity'));
```

Build one effect at a time, start below 20% output, and use the launcher's flight recorder to capture repeatable test flights before fine tuning.

After stopping a recording, open the launcher's advanced tools and choose **Analyze latest recording**. The report summarizes signal-family availability, a guided test checklist, and observed peak effect values without altering the recording.

The optional `TouchdownEnvelope`, `CombatDamageEnvelope`, `DoorMotionEnvelope`, `BrakeEnvelope`, and `WeaponRecoilEnvelope` properties provide safe starting points for simple effects. Existing raw and detailed derived properties remain available and should be preferred for aircraft-specific profiles.

## Recorded-telemetry replay

With DCS stopped, `scripts/Replay-DcsExtraTelemetry.ps1` can replay a JSONL recording to the local receiver so property registration and SimHub formulas can be checked without a mission. Use `-WhatIf` first to inspect the run, then replay to the same SimHub port configured for the Hook. It intentionally sends only to loopback.
