# Aircraft telemetry compatibility guide

This guide explains which `DCSExtra.*` SimHub properties are meaningful for each currently supported helicopter. It is a companion to [PROPERTIES.md](PROPERTIES.md), which defines every property.

## Reading the table

| Label | Meaning |
|---|---|
| **Native** | Read from a module/DCS source intended to represent that state. |
| **Derived** | Calculated from valid DCS telemetry. Use its matching `Has.*` capability gate. |
| **Proxy** | A useful approximation, but not the physical quantity named by a real cockpit gauge. |
| **Unavailable** | Not exported because no trustworthy source is known. It must not be simulated with a constant. |

`UH-60` means both `UH-60L` and `UH-60L_DAP`. `AH/MH-6` means `AH-6J` and `MH-6J`.

## Normalized identity and convenience outputs

`AircraftType` always preserves the raw DCS module name. `AircraftFamily` adds the stable families `UH60`, `AH6`, `AH64`, `UH1`, `OH58`, and `OTHER`; `GroundType` reports `WHEELS`, `SKIDS`, or `UNKNOWN`. `LoadoutSummary` lists normalized weapon classes present in the current payload without changing the detailed station properties.

The OH-58D engineering baseline uses a four-blade 395 RPM rotor, engine RPM
as an explicitly labelled rotor proxy, cockpit collective argument 3, and the
module FM's four skid suspension arguments. It remains provisional until a
live OH-58D recording validates those exported values end to end.

## OH-58D property scope (provisional)

The existing comparison tables predate the OH-58D adapter. Until a flight log
promotes this baseline to validated status, use this row-level scope rather
than assuming an unlisted property is available.

| Property group | OH-58D status |
|---|---|
| Identity, acceleration, angular velocity, air data, engine RPM, shake amplitude | Native DCS values when their `Has.*` gate is true |
| `MainRotorRpm*`, `MainRotorHz`, `BladePassHz`, `RotorSource`, `RotorRunning` | **Proxy** from engine RPM; 395 nominal RPM, 4 blades, `OH58EngineRpmProxy` |
| `CollectivePosition`, `CollectiveMovementRate`, powertrain cues | Cockpit argument 3 and derived cues; provisional |
| `SkidFrontLeft/Right*`, `SkidRearLeft/Right*`, `LeftSkidCompression`, `RightSkidCompression` | Four-point skid source: FL 1, FR 9, RL 6, RR 4; `OH58SkidDrawArguments` |
| Generic contact, touchdown, liftoff, ground resonance, rocking, skid scrub | Derived from the four-point skid source when the matching gate is true |
| Wheel rotation, wheel shimmy, tailwheel caster, dedicated parking brake, doors, cockpit weapon panel | Unavailable unless a future verified source is added |
| Weapon and countermeasure events | Generic DCS payload/countermeasure support only when their respective `Has.*` gates are true |

The five optional effect envelopes (`TouchdownEnvelope`, `CombatDamageEnvelope`, `DoorMotionEnvelope`, `BrakeEnvelope`, and `WeaponRecoilEnvelope`) are available whenever their underlying source is available. A zero can mean either no current event or no source, so profiles must still use the corresponding `Has.*` capability gate. All existing raw properties remain unchanged.

## Universal DCS flight data

These properties are sourced from DCS Export APIs and are available whenever DCS supplies the underlying value, regardless of aircraft. Always gate them with the listed capability.

| Properties | Gate | UH-60 | AH/MH-6 | AH-64D | UH-1H |
|---|---|---|---|---|---|
| `AccelXG`, `AccelYG`, `AccelZG` | `Has.Acceleration` | Native | Native | Native | Native |
| `AngularXRadS`, `AngularYRadS`, `AngularZRadS` | `Has.AngularVelocity` | Native | Native | Native | Native |
| `IndicatedAirSpeedMps`, `TrueAirSpeedMps`, `VerticalVelocityMps`, `AngleOfAttackRad`, `Mach` | `Has.AirData` | Native | Native | Native | Native |
| `EngineRpmLeftPct`, `EngineRpmRightPct` | `Has.EngineRpm` | Native DCS channel; do not label as torque | Native DCS channel | Native DCS channel | Native DCS channel |
| `ShakeAmplitude` | `Has.ShakeAmplitude` | Native DCS channel | Native DCS channel | Native DCS channel | Native DCS channel |
| `GearPosition`, `FlapsPosition`, `SpeedbrakePosition` | matching `Has.*` gate | DCS-dependent | DCS-dependent | DCS-dependent | DCS-dependent |

## Rotor, drivetrain, and flight-state cues

| Properties | Gate | UH-60 | AH/MH-6 | AH-64D | UH-1H |
|---|---|---|---|---|---|
| `MainRotorRpm`, `MainRotorRpmPct`, `MainRotorHz`, `BladePassHz`, `RotorSource` | `Has.RotorDynamics` | Derived from main-rotor phase; 4 blades | **Proxy** from N1; 5 blades | **Proxy** from engine RPM; 4 blades | Derived from phase, cockpit-gauge fallback; 2 blades |
| `CollectivePosition`, `CollectiveMovementRate` | `Has.ControlPositions` | Native cockpit argument | Native cockpit argument | Native cockpit argument | Native cockpit argument |
| `RotorLoadIntensity`, `TransmissionLoadIntensity`, `RotorDroopIntensity`, `RotorSpoolIntensity` | `Has.PowertrainEffects` | Derived | Derived where collective/rotor data exist | Derived where collective/rotor data exist | Derived where collective/rotor data exist |
| `EngineGovernorResponseIntensity` | no separate gate; use `Has.EngineRpm` | Derived from engine-RPM movement | Derived when RPM is available | Derived when RPM is available | Derived when RPM is available |
| `TorqueGaugeNormalized` | `Has.TorqueGauge` | Unavailable | Unavailable | Unavailable | Native cockpit torque-gauge animation |
| `LowRotorRpmWarning` | `Has.RotorWarning` | Unavailable | Unavailable | Unavailable | Native warning argument |
| `ApuRunning`, `Engine1StarterActive`, `Engine2StarterActive` | `Has.EngineLifecycle` | Native UH-60 cockpit arguments | Unavailable | Unavailable | Unavailable |
| `RotorRunning` | `Has.RotorDynamics` | Derived | Derived from N1 proxy | Derived | Derived |
| Tail-rotor RPM/authority | — | Unavailable: model does not export a reliable source | Unavailable | Unavailable | Unavailable |
| Rotor-brake state | — | Unavailable: no confirmed animation/state source | Unavailable | Unavailable | Unavailable |
| Hydraulic/governor fault state | — | Unavailable: no confirmed exportable source | Unavailable | Unavailable | Unavailable |

`BladePassHz` is capped against isolated phase-differentiation spikes. The AH/MH-6 source is deliberately labelled a proxy; do not treat it as measured main-rotor RPM.

## Aerodynamic and impact cues

| Properties | Gate | UH-60 | AH/MH-6 | AH-64D | UH-1H |
|---|---|---|---|---|---|
| `TurbulenceIntensity`, `AirframeBuffetIntensity` | `Has.AerodynamicEffects` | Derived | Derived | Derived | Derived |
| `ControlResponseIntensity` | `Has.AerodynamicEffects` | Derived | Derived when controls available | Derived from aircraft response | Derived |
| `ManeuverLoadIntensity`, `HardManeuverPulse` | `Has.AerodynamicEffects` | Derived | Derived | Derived | Derived |
| `VrsRiskIntensity`, `SettlingWithPowerCue` | `Has.AerodynamicEffects` | Derived, time-qualified VRS-risk / compatibility alias | Same, subject to N1 rotor proxy | Same | Same |
| `TranslationalLiftTransition` | `Has.AerodynamicEffects` | Derived from airspeed, acceleration, and control/airspeed trend | Same, subject to proxy rotor source | Same when air data is available | Same |
| `BladeSlapIntensity` | `Has.BladeSlap` | Derived BVI cue from rotor speed, forward speed, descent rate, and load | Same, subject to N1 rotor proxy | Same when rotor phase and air data are available | Same |
| `AirframeImpactPulse` | `Has.AerodynamicEffects` | Derived raw-disturbance edge; suitable for combat damage/hard shock | Derived | Derived | Derived |

`ControlResponseIntensity` is intentionally a separate layer. It should not be used as turbulence, and it does not replace `AirframeImpactPulse` during combat damage.

## Ground, landing, and taxi data

| Properties | Gate | UH-60 | AH/MH-6 | AH-64D | UH-1H |
|---|---|---|---|---|---|
| `GroundSpeedMps`, `OnGround`, `TouchdownPulse`, `TouchdownVerticalSpeedMps`, `TouchdownStrength`, `LiftoffPulse` | `Has.GroundMotion` / `Has.GearStruts` | Derived from wheel contact and DCS velocity | Derived from modeled skid contacts | Derived from weight-on-wheels | Derived from weight-on-skids |
| `TailStrutCompression`, `LeftMainStrutCompression`, `RightMainStrutCompression` | `Has.GearStruts` | Native/adapter baseline-corrected | Skid-derived compatibility values | Wheel-contact compatible values | Weight-on-skids compatible values |
| `TailWheelContact`, `LeftWheelContact`, `RightWheelContact` and pulse properties | `Has.GearStruts` | Derived with UH-60 hysteresis | Compatibility contact outputs; prefer skid properties | Derived | Compatibility contact outputs |
| `TailWheelRotation`, `LeftWheelRotation`, `RightWheelRotation` | `Has.WheelRotation` | Native UH-60 draw arguments | Unavailable | Unavailable | Unavailable |
| `SkidFrontLeft/RightCompression`, `SkidRearLeft/RightCompression`, contact and pulse properties | `Has.Skids` | Unavailable | Native mod skid-sag arguments | Unavailable | Unavailable; module has one weight-on-skids source only |
| `GroundRollIntensity`, `GroundResonanceIntensity`, `SuspensionMovementIntensity`, `GroundPitchRockIntensity`, `GroundRollRockIntensity`, `WeightOnGroundIntensity` | `Has.DetailedGroundEffects` | Derived; strongest validated set | Derived; validate normal, side-slope, and firm landings | Derived when wheel data are available | Derived from available skid/weight data |
| `WheelShimmyIntensity`, `TailWheelCasterIntensity` | `Has.DetailedGroundEffects` | Derived | Unavailable | Derived if wheel data allow | Unavailable |
| `SkidScrubIntensity` | `Has.DetailedGroundEffects` | Unavailable | Derived | Unavailable | Derived from available skid/weight data |
| `WheelBrakesPosition`, `BrakeSource`, `ParkingBrakeOn`, `BrakingIntensity`, `BrakeDecelerationIntensity` | `Has.Brakes` | Pedal brake argument 501 and parking-brake handle argument 81; progressive low-speed motion plus measured deceleration | Generic source only if DCS provides it | Generic source only if DCS provides it | Generic source only if DCS provides it |
| `AltitudeAglM` | no capability guarantee | Currently unavailable in the tested UH-60 capture | DCS-dependent | DCS-dependent | DCS-dependent |

`GroundResonanceIntensity` is a quiet continuous layer while loaded on the ground. It is not a touchdown, impact, or braking effect.

## Weapons and countermeasures

| Properties | Gate | UH-60 | AH/MH-6 | AH-64D | UH-1H |
|---|---|---|---|---|---|
| `WeaponFirePulse`, `WeaponEventCounter`, `LastFiredWeaponName`, `LastFiredWeaponStation`, `WeaponClass`, `WeaponBurstActive`, `WeaponBurstDurationMs`, `WeaponRateHz`, `WeaponImpulseStrength` | `Has.WeaponEvents` / `Has.WeaponEffects` | Derived from payload ammunition transitions and UH-60 mapping | Derived from payload transitions | Derived from payload transitions | Derived from payload transitions |
| `LightGunIntensity` | `Has.WeaponEffects` | M134 door guns | Module/loadout dependent | Module/loadout dependent | M134/M60 station mapping |
| `HeavyGunIntensity` | `Has.WeaponEffects` | DAP M230/30 mm mapping | Module/loadout dependent | M230 mapping | Usually unavailable |
| `RocketLaunchIntensity`, `MissileLaunchIntensity` | `Has.WeaponEffects` | Derived by weapon class | Derived by weapon class | Hydra/Hellfire classification | Derived by weapon class |
| `LeftCrewGun*`, `RightCrewGun*` | `Has.CrewGuns` | Native adapter mapping for door guns | Module-dependent | Unavailable | Module station mapping where available |
| `StationGun*` | `Has.StationGuns` | Native DAP station mapping | Module-dependent | Native station-gun behavior | Native station-gun behavior |
| `ChaffRemaining`, `FlareRemaining`, release pulse/count | `Has.Countermeasures` | Native DCS counts | Native DCS counts | Native DCS counts | Native DCS counts |

Payload totals (`WeaponPayloadCount`, `CannonShells`) are inventory information. Do not use them as shaker amplitude. Use class, burst, rate, and intensity properties instead.

## Cockpit, doors, and weapon-state properties

| Properties | Gate | UH-60 | AH/MH-6 | AH-64D | UH-1H |
|---|---|---|---|---|---|
| `MasterArmOn`, `WeaponSelectorPosition`, `WeaponSelectorName`, `RocketPairMode`, `RocketRippleMode`, `CountermeasureArmed`, `CountermeasureMode` | `Has.CockpitStates` | Native documented cockpit arguments | Unavailable | Native cockpit arguments; selector is momentary action | Native cockpit arguments |
| `PilotDoorPosition`, `CopilotDoorPosition`, `LeftCargoDoorPosition`, `RightCargoDoorPosition`, `LeftGunnerDoorPosition`, `RightGunnerDoorPosition`, `DoorMovementIntensity` | `Has.Doors` | Native UH-60 draw arguments 38 and 400-404 | Unavailable | Unavailable | Unavailable |

## Recommended profile gating

Every effect should begin with `DCSExtra.Connected` and the most specific capability gate available. Examples:

```javascript
// UH-60 brake texture
if (!$prop('DCSExtra.Connected') || !$prop('DCSExtra.Has.Brakes')) return 0;
return $prop('DCSExtra.BrakingIntensity');

// Only use continuous ground resonance when detailed ground telemetry exists.
if (!$prop('DCSExtra.Connected') || !$prop('DCSExtra.Has.DetailedGroundEffects')) return 0;
return $prop('DCSExtra.GroundResonanceIntensity');
```

Do not create an effect for a property marked unavailable. A zero or fabricated fallback can look valid in SimHub while making the haptic behavior less realistic.
