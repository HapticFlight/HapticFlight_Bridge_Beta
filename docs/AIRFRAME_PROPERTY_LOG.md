# Airframe property log

This is the release-source log for the downloadable property catalog. It records
whether each property group is meaningful for a specific airframe, rather than
claiming that a zero-valued property is supported.

Legend: **Native** = module/DCS source; **Derived** = calculated from valid
telemetry; **Proxy** = useful approximation; **Unavailable** = intentionally
not fabricated. Every numeric use still requires `DCSExtra.Connected` and the
listed `DCSExtra.Has.*` gate.

## Current coverage

| Property group | Capability gate | UH-60L/DAP | AH-6J/MH-6J | AH-64D | UH-1H | OH-58D |
|---|---|---|---|---|---|---|
| Identity and connection | `Connected` | Native | Native | Native | Native | Native |
| Acceleration and angular velocity | `Has.Acceleration`, `Has.AngularVelocity` | Native | Native | Native | Native | Native |
| Airspeed, AoA, Mach, vertical velocity | `Has.AirData` | Native | Native | Native | Native | Native |
| Engine RPM | `Has.EngineRpm` | Native | Native | Native | Native | Native; rotor proxy source |
| Rotor RPM, Hz, blade-pass Hz | `Has.RotorDynamics` | Derived; 4 blades | Proxy; 5 blades | Proxy; 4 blades | Derived; 2 blades | Proxy; 4 blades; provisional |
| Collective position and rate | `Has.ControlPositions` | Native | Native | Native | Native | Native argument 3; provisional |
| Rotor/powertrain intensities | `Has.PowertrainEffects` | Derived | Derived | Derived | Derived | Derived from proxy rotor source |
| Torque gauge | `Has.TorqueGauge` | Unavailable | Unavailable | Unavailable | Native animation | Unavailable |
| Aerodynamic and BVI cues | `Has.AerodynamicEffects`, `Has.BladeSlap` | Derived | Derived; proxy rotor source | Derived | Derived | Derived; proxy rotor source |
| Generic ground state and touchdown | `Has.GroundMotion`, `Has.GearStruts` | Derived | Derived from skids | Derived from wheels | Derived from skids | Derived from four skids; provisional |
| Detailed skid compression/contact/pulse | `Has.Skids` | Unavailable | Native draw arguments | Unavailable | Unavailable | Native draw arguments 1/9/6/4; provisional |
| Wheel rotations | `Has.WheelRotation` | Native | Unavailable | Unavailable | Unavailable | Unavailable |
| Gear-borne vibration, rocking, suspension | `Has.DetailedGroundEffects` | Derived | Derived | Derived when wheel data exists | Derived | Derived when body-speed data exists |
| Skid scrub | `Has.DetailedGroundEffects` | Unavailable | Derived | Unavailable | Derived | Derived |
| Wheel shimmy and tailwheel caster | `Has.DetailedGroundEffects` | Derived | Unavailable | Derived if wheel data permits | Unavailable | Unavailable |
| Brakes | `Has.Brakes` | Native pedal/parking sources | DCS generic source if available | DCS generic source if available | DCS generic source if available | DCS generic source if available |
| Weapons and classified intensities | `Has.WeaponEvents`, `Has.WeaponEffects` | Derived with mappings | Derived; loadout dependent | Derived with M230/Hydra/Hellfire mappings | Derived with module mappings | Generic DCS payload support if available |
| Crew-gun sides | `Has.CrewGuns` | Native mapping | Loadout dependent | Unavailable | Module mapping where available | Unavailable |
| Chaff and flare release | `Has.Countermeasures` | Native DCS counts | Native DCS counts | Native DCS counts | Native DCS counts | Native DCS counts |
| Cockpit weapon/countermeasure state | `Has.CockpitStates` | Native | Unavailable | Native | Native | Unavailable |
| Door positions and motion | `Has.Doors` | Native | Unavailable | Unavailable | Unavailable | Unavailable |

## AH-6J/MH-6J validation log

| Recording | Result | Evidence |
|---|---|---|
| `DcsExtraTelemetry-20260731-024518.jsonl` | Pass | `AH6` / `AH-6J`; `AH6N1Proxy`; all four skid contacts; collective, rotor, air-data, ground, powertrain, and weapon capability gates were available. |
| Rotor frequency | Pass with proxy caveat | Recorded 1-rev range was 4.82–5.76 Hz; recorded five-rev blade-pass range was 24.11–28.82 Hz. Use dynamic `BladePassHz`, not a fixed nominal frequency. |
| Single-skid low hover | Pass for profile cue | The first brief contact was front-left only. Skid contact and pulse were exported; `TouchdownEnvelope` remained zero because it was not a full touchdown. |
| Blade slap | Not yet validated | No non-zero `BladeSlapIntensity` event occurred in this recording. |

## Next additions

1. Expand each row into individual exported property names for the release XLSX/PDF.
2. Add AH-64D and UH-1H flight-log validation entries.
3. Add OH-58D flight-log validation for the engine-RPM rotor proxy,
   collective argument 3, and four-point skid adapter. Keep it labelled as an
   engineering baseline until that recording exists.
4. Add OH-6A only after its live telemetry mappings are verified.
