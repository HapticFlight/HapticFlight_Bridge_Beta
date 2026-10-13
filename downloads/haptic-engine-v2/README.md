# V2 haptic-engine aircraft profiles

> **AH-6J / Apache update:** [test.4 rotor-baseline refresh](../rotor-refresh-v2/README.md) strengthens the steady rotor layers using recorded-flight evidence. The other four test.3 profiles remain unchanged.

**[Download all six V2 profiles](Helicopter-Haptic-Engine-v2.0.0-test.3.zip?raw=true)**

This package supersedes the older v0.4 repaired profiles and the clean UH-60 v1.9.0 copy. It uses the same aircraft-aware Haptic Engine design as the working UH-60 `v2.0.0-test.1` profile. The test.3 suffix identifies this profile revision, not a replacement plugin installer.

| Aircraft | Enabled effects | Landing signal |
|---|---:|---|
| AH-64D Apache | 38 | `Haptic.AH64.HeavyLanding.Envelope` |
| OH-58D Kiowa | 33 | `Haptic.Touchdown.Envelope` |
| AH-6J | 38 | `Haptic.AH6.LightLanding.Envelope` |
| MH-6J | 31 | `Haptic.AH6.LightLanding.Envelope` |
| UH-1H Huey | 31 | `Haptic.UH1.WeightOnSkidsLanding.Envelope` |
| UH-60L / DAP | 41 | `Haptic.Touchdown.Envelope` |

## Import

1. Extract the ZIP and import your aircraft's `.siprofile` into SimHub ShakeIt Bass Shakers.
2. Select it manually. Disable the previous aircraft profile so its effects do not overlap.
3. Keep the Haptic Engine plugin you already use with your working UH-60 V2 profile, and keep your existing output routing.

These files require `DcsExtraTelemetryPlugin.DCSExtra.Haptic.*` properties. The inspected engine reports `Haptic.EngineVersion = 1.0.0`; that is the engine's own version, distinct from the V2 profile version. An older plugin without this property produces no output intentionally. Do not install an older bridge binary from the main-branch source to use this package.

## Changes and retained tuning

The common signal migration follows the existing V2 generator. Rotor load/spool/droop, transmission/governor, maneuver, aerodynamic, ground, braking, weapon and countermeasure cues now consume the haptic-engine outputs. Four-skid contact pulses use the engine's skid channels. Landing uses the aircraft-specific envelope above. Skid sliding uses `Haptic.Ground.SkidScrub.Intensity`, not a profile-only ground-speed substitute. Raw RPM/frequency, collective, airspeed and capability data remain where the V2 design uses them.

The UH-60 effect objects match the user's working V2 test.1 profile, except for a common missing-engine/disconnected guard. All original effect and group gains are retained. The other five retain their aircraft-specific frequencies, gains and module corrections while migrating their derived effect inputs. All 212 included effects are enabled. MH-6 retains its transport loadout rather than adding AH-6 fixed weapons.

This package does not enable telemetry the module/Hook cannot supply. In particular, non-UH60 starter/APU cues may remain silent, rotor sources can be proxies, and the Kiowa model remains provisional. No new claim of flight-model or tactile calibration is made.

## Verification and provenance

- All six profiles checked against 849 property registrations in the haptic-engine source contract.
- 212 effect formulas executed in installed SimHub Jint 4.2.2.0 with simulated properties.
- Node checks cover missing-engine/disconnection silence, numeric and string RPM values, rotor output, proportional touchdown response, skid airborne suppression and exact UH-60 V2 effect preservation.
- The engine regression project compiled from the inspected source. Windows Application Control prevented executing that test binary; no engine regression runtime pass is claimed.
- No live DCS session or hardware-output test was performed.

Engine source baseline: `8ac66074467cc9f646956f6b322c25d306c6a4bf` from the aircraft-haptic-engine worktree. Exact source hashes and per-effect input names are in [V2-Effect-Audit.json](V2-Effect-Audit.json); a filterable list is in [V2-Effect-Inputs.csv](V2-Effect-Inputs.csv?raw=true). The older telemetry workbook is a legacy-interface reference and does not inventory these V2 effect inputs.

Rebuild using `node scripts/Build-V2AircraftProfiles.js <haptic-engine-checkout>`. Run `node tests/Test-V2AircraftProfiles.js` and `pwsh -NoProfile -File tests/Test-V2ProfilesJint.ps1`.
