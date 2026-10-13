# SimHub Property Reference

Haptic Engine 1.7.1 exposes capability-gated signal families. It adds
priority-mix guidance and a model-time delta used by test.12 profiles for
update-rate-independent smoothing. See [the V2 engine contract](HAPTIC_ENGINE.md)
and [the test.17 realism implementation](REALISM-IMPLEMENTATION-test.17.md).

`DCSExtra.Haptic.Timing.DeltaSeconds.Intensity` reports the bounded DCS model-time
step used by the latest profile formulas. `DCSExtra.Haptic.Mix.ContinuousGain.Intensity`
is smooth gain guidance for continuous layers during physical impacts and weapon
events; `Mix.PriorityDemand.Intensity` reports the event demand that drives it.
These values guide profile mixing and do not measure downstream audio clipping.

All properties use the stable `DCSExtra` namespace. SimHub groups them under `DcsExtraTelemetryPlugin` in **Available properties**.

## Airframe labels

Every table below now identifies the aircraft for which a property is meaningful.
`All supported` means the property is common telemetry and still requires its
matching `DCSExtra.Has.*` gate. `Adapter-dependent` means the property exists
for every aircraft but is meaningful only when the listed capability is true.
The currently supported adapters are **UH-60L/UH-60L DAP**, **AH-6J/MH-6J**,
**AH-64D**, **UH-1H**, and the provisional **OH-58D** baseline. OH-6A is not
listed because it has not yet been validated.

For a full cross-airframe matrix, including whether a signal is native,
derived, a proxy, or unavailable, see [Aircraft compatibility](AIRCRAFT_COMPATIBILITY.md).

## Connection and identity

| Property | Type | Meaning | Applies to |
|---|---|---|---|
| `DCSExtra.Connected` | Boolean | A valid packet arrived less than one second ago | All supported |
| `DCSExtra.DataAgeMs` | Number | Milliseconds since the latest valid packet; `-1` before the first packet | All supported |
| `DCSExtra.PacketRateHz` | Number | Measured valid-packet receive rate | All supported |
| `DCSExtra.InvalidPackets` | Number | Packets rejected by schema or numeric validation | All supported |
| `DCSExtra.DroppedPackets` | Number | Sequence gaps within a DCS session | All supported |
| `DCSExtra.AircraftType` | String | Raw module name reported by `LoGetSelfData()` | All supported |
| `DCSExtra.AircraftFamily` | String | Stable normalized family: `UH60`, `AH6`, `AH64`, `UH1`, `OH58`, or `OTHER` | All supported |
| `DCSExtra.GroundType` | String | Normalized landing-gear family: `WHEELS`, `SKIDS`, or `UNKNOWN` | All supported |

## Raw telemetry

| Property | Unit/source | Applies to |
|---|---|---|
| `DCSExtra.AccelXG`, `AccelYG`, `AccelZG` | G, `LoGetAccelerationUnits()` | All supported |
| `DCSExtra.AngularXRadS`, `AngularYRadS`, `AngularZRadS` | rad/s, `LoGetAngularVelocity()` | All supported |
| `DCSExtra.IndicatedAirSpeedMps`, `TrueAirSpeedMps`, `VerticalVelocityMps`, `AngleOfAttackRad`, `Mach` | DCS air-data exports | All supported |
| `DCSExtra.ShakeAmplitude` | DCS normalized/aircraft-dependent shake value | All supported |
| `DCSExtra.EngineRpmLeftPct`, `EngineRpmRightPct` | percent, `LoGetEngineInfo().RPM.left/right`; these are not torque | All supported |
| `DCSExtra.GearPosition`, `FlapsPosition`, `SpeedbrakePosition` | `LoGetMechInfo()` normalized positions | Adapter-dependent; use matching `Has.*` gate |

## Ground contact and suspension

| Property | Meaning | Applies to |
|---|---|---|
| `DCSExtra.GroundSpeedMps`, `AltitudeAglM` | Horizontal world velocity and height above surface | All supported when DCS exports them |
| `DCSExtra.WheelBrakesPosition`, `BrakeSource` | Brake-pedal source; UH-60 uses documented draw argument 501, others need a generic DCS brake source | UH-60 native; other adapters only when `Has.Brakes` is true |
| `DCSExtra.ParkingBrakeOn` | `true` when the UH-60 parking-brake handle is set | UH-60L / UH-60L DAP only |
| `DCSExtra.TailStrutCompression` | Tail/nose support value; for skid aircraft this is a compatibility average, not a physical wheel | All supported; see `StrutSource` |
| `DCSExtra.LeftMainStrutCompression`, `RightMainStrutCompression` | Main-wheel support, or left/right skid average | UH-60 and AH-64D wheel adapters; AH/MH-6, UH-1H, and OH-58D compatibility skid values |
| `DCSExtra.StrutCompressionRate` | Signed support-compression change: positive loading, negative unloading | All supported where `Has.GearStruts` is true |
| `DCSExtra.TailWheelRotation`, `LeftWheelRotation`, `RightWheelRotation` | Tail and main-wheel animation values | UH-60L / UH-60L DAP only |
| `DCSExtra.StrutSource` | Exact adapter: `LoGetMechInfo`, `UH60DrawArguments`, `AH64WeightOnWheels`, `AH6SkidDrawArguments`, `OH58SkidDrawArguments`, or `UH1WeightOnSkids` | All supported; inspect this value to identify the active airframe source |
| `DCSExtra.OnGround` | `1` when any supported contact point is compressed | All supported |
| `DCSExtra.TailWheelContact`, `LeftWheelContact`, `RightWheelContact` | Generic tail/left/right support contacts; on skid aircraft they are compatibility outputs | All supported; prefer four-point skid properties on AH/MH-6 and OH-58D |
| `DCSExtra.*WheelContactPulse` | 200 ms contact pulse; UH-60 tail also detects support returning after brake-induced tail unload | All supported with `Has.GearStruts`; wheel-specific meaning is UH-60/AH-64D |
| `DCSExtra.TouchdownPulse`, `TouchdownVerticalSpeedMps`, `TouchdownStrength`, `LiftoffPulse` | Landing/liftoff events and last-event strength; `TouchdownStrength` is latched and must be pulse-gated | All supported with contact data |
| `DCSExtra.SuspensionMovementIntensity`, `GroundRollIntensity`, `GroundResonanceIntensity`, `GroundPitchRockIntensity`, `GroundRollRockIntensity`, `WeightOnGroundIntensity` | Derived loaded-airframe and taxi cues | All supported when `Has.DetailedGroundEffects` is true |
| `DCSExtra.BrakingIntensity`, `BrakeDecelerationIntensity`, `WheelSkidIntensity` | Combined braking, measured deceleration/load, and derived lateral wheel-skid risk | Any adapter with `Has.Brakes`; UH-60 is the validated dedicated source; skid risk is not a wheel-lock flag |
| `DCSExtra.LongitudinalGroundSpeedMps`, `LateralGroundSlipMps` | Helicopter-FM body-axis ground speeds | Adapter-dependent; required for detailed ground effects |
| `DCSExtra.SkidScrubIntensity` | Lateral skid-scrub cue while in contact | AH/MH-6, UH-1H, and OH-58D |
| `DCSExtra.WheelShimmyIntensity`, `TailWheelCasterIntensity` | Rolling wheel and tailwheel cues | UH-60; AH-64D only if wheel/body data permit |

The UH-60L adapter uses the mod's documented draw arguments 1, 6, and 4 for tail, left-main, and right-main strut compression, and 101, 103, and 102 for wheel rotation, only when DCS's standard three-strut result is unavailable. The raw compression properties remain exactly as DCS reports them. Derived contact and weight-on-ground properties compensate for the UH-60 tail strut's approximately `0.33` airborne animation baseline and use hysteresis so the baseline cannot keep taxi effects active in flight.

`DCSExtra.Haptic.Contact.Left/Right/Tail.Envelope` are normalized provisional
contact envelopes, not wheel-force measurements. In Engine 1.7.1, a direct
main-wheel edge retains more headroom than a tail-only edge so a soft rolling
main-wheel arrival remains perceptible. Use them for separate short contact
cues and keep `TouchdownEnvelope` for the landing-body impact.

### UH-60 doors

| Property | Meaning | Applies to |
|---|---|---|
| `DCSExtra.PilotDoorPosition`, `CopilotDoorPosition` | Cockpit-door open position, `0..1` | UH-60L / UH-60L DAP only |
| `DCSExtra.LeftCargoDoorPosition`, `RightCargoDoorPosition` | Cargo-door open position, `0..1` | UH-60L / UH-60L DAP only |
| `DCSExtra.LeftGunnerDoorPosition`, `RightGunnerDoorPosition` | Gunner-hatch open position, `0..1` | UH-60L / UH-60L DAP only |
| `DCSExtra.DoorMovementIntensity` | Effect-ready combined door-motion value, `0..1` | UH-60L / UH-60L DAP only |

For the UH-60L mod these read documented networked draw arguments: 38, 400, 401, 402, 403, and 404. Door state can be used to gate door-gunner ambience or a short door-motion cue; it does not itself represent gun fire.

### AH-6J / MH-6J and OH-58D skids

The Little Bird mod implements skid sag internally with four suspension contacts. The plugin intentionally presents them as skids:

| Property group | AH-6J / MH-6J draw argument | OH-58D draw argument |
|---|---|---|
| `SkidFrontLeftCompression` / `SkidFrontLeftContact` / `SkidFrontLeftContactPulse` | 202 | 1 |
| `SkidFrontRightCompression` / `SkidFrontRightContact` / `SkidFrontRightContactPulse` | 204 | 9 |
| `SkidRearLeftCompression` / `SkidRearLeftContact` / `SkidRearLeftContactPulse` | 201 | 6 |
| `SkidRearRightCompression` / `SkidRearRightContact` / `SkidRearRightContactPulse` | 203 | 4 |
| `LeftSkidCompression` | average of front-left and rear-left | average of front-left and rear-left |
| `RightSkidCompression` | average of front-right and rear-right | average of front-right and rear-right |

These raw values make left/right skid touchdown, fore/aft rocking, uneven-ground sag, and set-down effects possible without pretending either helicopter has visible wheels. The OH-58D mapping is an engineering baseline pending live flight-log validation.

## Countermeasures and aircraft cockpit state

| Property | Meaning | Applies to |
|---|---|---|
| `DCSExtra.ChaffRemaining`, `FlareRemaining`, `ChaffReleasePulse`, `FlareReleasePulse`, `ChaffReleasedCount`, `FlareReleasedCount` | Remaining standard DCS countermeasures and 200 ms release events | All supported when `Has.Countermeasures` is true |
| `DCSExtra.MasterArmOn` | Read-only master-arm state | UH-60L/DAP, AH-64D, UH-1H |
| `DCSExtra.WeaponSelectorPosition`, `WeaponSelectorName` | UH-60 persistent selector; AH-64D momentary weapon-action switch; UH-1H gun-side selector | UH-60L/DAP, AH-64D, UH-1H |
| `DCSExtra.RocketPairMode`, `RocketRippleMode` | UH-60 rocket panel modes | UH-60L / UH-60L DAP only |
| `DCSExtra.CountermeasureArmed`, `CountermeasureMode` | UH-60 countermeasure panel state | UH-60L / UH-60L DAP only |

Countermeasure release detection uses `LoGetSnares()` and therefore does not depend on a particular button binding. Chaff and flare release pulses are direct confirmed events and are enabled by default in test.19 profiles. Cockpit-state reads are read-only, protected calls; if DCS does not expose the cockpit device in the Hook environment, `Has.CockpitStates` is false and no value is invented.

## Weapons and effect triggers

| Property | Meaning | Applies to |
|---|---|---|
| `DCSExtra.SelectedWeaponStation`, `SelectedWeaponName`, `WeaponPayloadCount`, `CannonShells` | Current DCS payload/station inventory | All supported when DCS reports payload data |
| `DCSExtra.WeaponFirePulse`, `GunFirePulse`, `StationGunFirePulse`, `PayloadReleasePulse` | 150 ms generic weapon events | All supported when `Has.WeaponEvents` is true |
| `DCSExtra.CrewGunFirePulse`, `CrewGunRoundsFired`, `LeftCrewGun*`, `RightCrewGun*` | Mapped crew/door-gun events, side-specific only when the adapter recognizes stations | UH-60L/DAP; UH-1H where its station mapping is available |
| `DCSExtra.StationGunRoundsFired`, `StationGunAmmoRemaining` | Shell-type station-gun details | All supported when `Has.StationGuns` is true |
| `DCSExtra.GunRoundsFired`, `PayloadReleasedCount`, `LastFiredWeaponStation`, `LastFiredWeaponName`, `WeaponEventCounter` | Generic ammunition-transition diagnostics | All supported when `Has.WeaponEvents` is true |

For a general weapon shaker effect, gate on `DCSExtra.Connected && DCSExtra.Has.WeaponEvents` and use `DCSExtra.WeaponFirePulse`. Use `GunFirePulse` and `PayloadReleasePulse` when guns and released payloads need different frequencies or strengths. For the UH-60L door guns, gate on `DCSExtra.Has.CrewGuns` and use `CrewGunFirePulse`, or the left/right pulses for separate shaker zones.

## Rotor, controls, and powertrain

| Property | Meaning | Applies to |
|---|---|---|
| `DCSExtra.MainRotorRpmPct`, `MainRotorRpm`, `MainRotorHz` | Effect-ready main-rotor speed; adapter source is disclosed separately | All supported; AH/MH-6, AH-64D, and OH-58D are labelled proxies |
| `DCSExtra.BladePassHz` | Rotor speed × blade count: UH-60/AH-64D/OH-58D 4, AH/MH-6 5, UH-1H 2 | All supported |
| `DCSExtra.RotorSource` | Exact source: `UH60RotorPhase`, `AH6N1Proxy`, `AH64EngineRpmProxy`, `UH1RotorPhase`/`UH1RotorGaugeProxy`, or `OH58EngineRpmProxy` | All supported |
| `DCSExtra.CollectivePosition`, `CollectiveMovementRate` | Normalized collective and its movement rate | All supported when `Has.ControlPositions` is true |
| `DCSExtra.RotorLoadIntensity`, `TransmissionLoadIntensity`, `RotorDroopIntensity`, `RotorSpoolIntensity`, `EngineGovernorResponseIntensity` | Derived rotor/powertrain cues | All supported when the relevant rotor/RPM capability is true |
| `DCSExtra.ControlResponseIntensity` | Brisk collective/airframe response, separate from turbulence | All supported when aerodynamic/control telemetry is available |
| `DCSExtra.EngineTorqueLeftPct`, `EngineTorqueRightPct` | Reserved for trustworthy calibrated torque exports; currently capability-gated off | None currently; do not use |
| `DCSExtra.TorqueGaugeNormalized` | Normalized cockpit torque-gauge animation, not calibrated percent torque | UH-1H only |
| `DCSExtra.RotorRunning` | Rotor speed is above the adapter's running threshold | All supported |
| `DCSExtra.ApuRunning`, `Engine1StarterActive`, `Engine2StarterActive` | Guarded cockpit lifecycle states | UH-60L / UH-60L DAP only |
| `DCSExtra.LowRotorRpmWarning` | Dedicated low-rotor warning argument | UH-60L / UH-60L DAP and UH-1H |

## Aerodynamic effect cues

| Property | Meaning | Applies to |
|---|---|---|
| `DCSExtra.TurbulenceIntensity`, `AirframeBuffetIntensity`, `ManeuverLoadIntensity`, `ManeuverResponseIntensity`, `LateralManeuverIntensity`, `HardManeuverPulse`, `AirframeImpactPulse` | Derived airborne motion and disturbance cues | All supported when `Has.AerodynamicEffects` is true |
| `DCSExtra.VrsRiskIntensity`, `SettlingWithPowerCue`, `TranslationalLiftTransition` | Derived VRS-risk, compatibility alias, and translational-lift cues | VRS-risk requires air-data, rotor/control inputs, and sustained qualifying state; it is not a direct VRS measurement |
| `DCSExtra.BladeSlapIntensity` | Conservative BVI-style cue; not a direct vortex measurement | All supported when `Has.BladeSlap` is true; proxy caveat applies to AH/MH-6 and OH-58D |

## Detailed ground interaction

| Property | Meaning | Applies to |
|---|---|---|
| `DCSExtra.GroundPitchRockIntensity`, `GroundRollRockIntensity`, `LongitudinalGroundSpeedMps`, `LateralGroundSlipMps` | Support-balance and body-axis ground-motion telemetry | All supported where `Has.DetailedGroundEffects` is true |
| `DCSExtra.BrakeDecelerationIntensity`, `WheelSkidIntensity` | Smoothed brake/deceleration cue and derived lateral wheel-skid risk | Wheel-skid requires braking, wheel contact, rolling speed, and measured lateral motion; it is not a surface-material effect |
| `DCSExtra.SkidScrubIntensity` | Lateral skid-scrub cue | AH/MH-6, UH-1H, and OH-58D |
| `DCSExtra.WheelShimmyIntensity`, `TailWheelCasterIntensity` | Rolling-wheel and tailwheel cues | UH-60; AH-64D only when wheel/body data permit |
| `DCSExtra.LiftoffPulse`, `WeightOnGroundIntensity` | Contact unload and normalized supported weight | All supported with contact data |

## Classified weapon effects

| Property | Meaning | Applies to |
|---|---|---|
| `DCSExtra.WeaponClass`, `WeaponBurstActive`, `WeaponBurstDurationMs`, `WeaponRateHz`, `DetectedWeaponRateHz`, `WeaponImpulseStrength`, `LoadoutSummary` | Classified weapon event diagnostics | All supported when `Has.WeaponEffects` is true |
| `DCSExtra.NominalWeaponRateHz` | Name-mapped cyclic/aggregate rate for known weapons; zero if no trustworthy mapping exists | Adapter/loadout dependent; presently mapped for known UH-60, AH-64D, and UH-1H weapons |
| `DCSExtra.LightGunIntensity` | Sustained door/minigun cue | UH-60L/DAP and UH-1H mappings; other adapters are loadout-dependent |
| `DCSExtra.HeavyGunIntensity` | Stronger cannon/station-gun cue | UH-60L DAP and AH-64D mappings; other adapters are loadout-dependent |
| `DCSExtra.RocketLaunchIntensity`, `MissileLaunchIntensity` | Classified launch cues | All supported when a recognized payload event is reported |

Station-gun detection is installation-neutral: it classifies shell-type entries already reported by `LoGetPayloadInfo().Stations` and watches their ammunition counts. Crew-gun side labels still require an aircraft mapping. The built-in UH-60L/UH-60L_DAP adapter maps payload station 3 to the left gun and station 5 to the right gun, matching UH-60L version 1.5. The DAP adapter also resolves its shared gun type `4,6,6,0` by station: station 2 is `Twin M230 30 mm Cannons`, while stations 3 and 5 are `M134 Door Gun`; this preserves the correct heavy-gun versus light-gun effects. The earlier unnamed type `4,6,10,46` remains a generic `M134 Minigun` fallback for the UH-60 adapter. The UH-1H adapter recognizes M134/M60-style names and the module's left/right gun stations. No aircraft module file or `Export.lua` is modified.

## Optional effect-ready envelopes

These additive `0..1` convenience properties combine already-exported signals for simple profiles. They do not replace, suppress, rename, or change any raw or detailed derived property. Advanced profiles should continue using the individual source properties when they need aircraft-specific tuning.

| Property | Composition | Applies to |
|---|---|---|
| `DCSExtra.TouchdownEnvelope` | `TouchdownPulse * TouchdownStrength` | All supported with contact data |
| `DCSExtra.CombatDamageEnvelope` | `AirframeImpactPulse` | All supported with `Has.AerodynamicEffects` |
| `DCSExtra.DoorMotionEnvelope` | `DoorMovementIntensity` | UH-60L / UH-60L DAP only |
| `DCSExtra.BrakeEnvelope` | `BrakingIntensity` | Any adapter with `Has.Brakes` |
| `DCSExtra.WeaponRecoilEnvelope` | Maximum of `LightGunIntensity` and `HeavyGunIntensity` | All supported when a mapped weapon effect exists |

## Capability gates

| Property | True when |
|---|---|
| `DCSExtra.Has.Acceleration` | all three acceleration components are available and finite |
| `DCSExtra.Has.AngularVelocity` | all three angular components are available and finite |
| `DCSExtra.Has.AirData` | IAS, TAS, vertical velocity, AoA, and Mach are all available and finite |
| `DCSExtra.Has.EngineRpm` | both RPM values are available and finite |
| `DCSExtra.Has.Gear` | gear position is available and finite |
| `DCSExtra.Has.Flaps` | flap position is available and finite |
| `DCSExtra.Has.Speedbrake` | speedbrake position is available and finite |
| `DCSExtra.Has.ShakeAmplitude` | shake amplitude is available and finite |
| `DCSExtra.Has.WeaponPayload` | DCS reports weapon-station counts and/or cannon shells |
| `DCSExtra.Has.WeaponEvents` | the standard payload source is available, so safe ammunition-decrease event detection is active |
| `DCSExtra.Has.StationGuns` | at least one shell-type payload station is currently reported |
| `DCSExtra.Has.CrewGuns` | the active aircraft adapter recognizes at least one installed crew-gun station |
| `DCSExtra.Has.GearStruts` | three generic/UH-60 compatible compression signals are available |
| `DCSExtra.Has.GroundMotion` | ground speed and derived ground-motion effects are available; AGL is optional |
| `DCSExtra.Has.WheelRotation` | UH-60's three wheel animation values are available |
| `DCSExtra.Has.Skids` | all four AH-6J/MH-6J skid-sag contacts are available |
| `DCSExtra.Has.Countermeasures` | DCS reports both chaff and flare counts |
| `DCSExtra.Has.CockpitStates` | the active UH-60, AH-64D, or UH-1H cockpit-state adapter is readable |
| `DCSExtra.Has.Brakes` | a dedicated brake-position source is available |
| `DCSExtra.Has.Doors` | all six UH-60 door/hatch positions are available |
| `DCSExtra.Has.RotorDynamics` | a supported UH-60, AH/MH-6, AH-64D, UH-1H, or OH-58D adapter is producing rotor speed |
| `DCSExtra.Has.ControlPositions` | the aircraft adapter can read collective position |
| `DCSExtra.Has.EngineTorque` | both real engine torque channels are available; currently false for these community modules |
| `DCSExtra.Has.TorqueGauge` | the UH-1H normalized cockpit torque gauge is readable |
| `DCSExtra.Has.RotorWarning` | a dedicated low-rotor warning argument is readable (currently UH-1H) |
| `DCSExtra.Has.PowertrainEffects` | rotor and collective inputs support derived powertrain cues |
| `DCSExtra.Has.BladeSlap` | rotor-speed and air-data inputs support the derived blade-slap cue |
| `DCSExtra.Has.AerodynamicEffects` | acceleration and angular velocity support derived aerodynamic cues |
| `DCSExtra.Has.DetailedGroundEffects` | support compression plus helicopter body-speed components are available |
| `DCSExtra.Has.WeaponEffects` | payload data supports classified weapon effect outputs |
| `DCSExtra.Has.EngineLifecycle` | guarded UH-60 APU, starter, and low-rotor cockpit states are available |

An unsupported numeric property reads as zero because SimHub formulas require a numeric value. Always gate a raw ShakeIt formula with both `DCSExtra.Connected` and its matching `Has.*` property. This prevents unsupported data from being mistaken for a real zero and prevents a stale last value from driving a shaker after DCS disconnects. Multiplayer servers can restrict ownship exports; capability flags will turn false instead of switching to `Export.lua` or another integration method.

## Official-module adapter sources

The AH-64D and UH-1H argument mappings were cross-checked against the maintained [DCS-BIOS AH-64D module definition](https://github.com/DCS-Skunkworks/dcs-bios/blob/main/Scripts/DCS-BIOS/lib/modules/aircraft_modules/AH-64D.lua) and [DCS-BIOS UH-1H module definition](https://github.com/DCS-Skunkworks/dcs-bios/blob/main/Scripts/DCS-BIOS/lib/modules/aircraft_modules/UH-1H.lua). Rotor geometry and nominal values follow the [official DCS AH-64D flight manual](https://www.digitalcombatsimulator.com/upload/iblock/241/a0jh82sp6hjozpfkj537skz629a9sxpz/DCS%20AH-64D%20Flight%20Manual%20EN.pdf) and [official DCS UH-1H product documentation](https://www.digitalcombatsimulator.com/en/products/huey/?SHOWALL_1=1). These references establish the adapter definitions; live output still requires flight testing because DCS can change export behavior between builds.
