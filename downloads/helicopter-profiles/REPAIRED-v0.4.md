# Helicopter profiles v0.4 — output compatibility repair

Use the five **Repaired v0.4** imports instead of v0.3. All 171 included effects are enabled: Apache 38, Kiowa 33, AH-6J 38, MH-6J 31, Huey 31.

## Import

1. Import the file for your aircraft in ShakeIt Bass Shakers.
2. **Select it manually.** These replacements deliberately have no automatic aircraft assignment. The added exact AircraftType/AircraftFamily formula checks have been removed, so missing or differently represented identity metadata cannot silence the selected profile. Change the selected profile when changing aircraft.
3. Disable the previous profile. Retain your working UH-60 sound-card/channel routing; hardware output settings are not included in these imports.
4. With DCS connected and rotor running, check the 1-rev and blade-pass effect live values first, then ground and weapons.

## What changed

- Returned to the established UH-60-style inline JavaScript and explicit PreExpression state initialization, preserving the aircraft adaptations from v0.2. Removed v0.3's numeric-type-only helper and nested target wrapper. Numeric strings now behave like the baseline arithmetic instead of being discarded.
- Removed exact aircraft-identity gates. Connection and effect-specific telemetry gates remain. Every amplitude returns zero immediately on disconnection.
- Restored enabled rotor harmonics, blade slap, ETL, powered-descent, maneuver, ground and other included cues. These remain estimates where the bridge has no direct module event; this repair does not prove their physical calibration.
- Preserved the Huey legacy gauge-frequency and torque-display corrections. Combined Huey landing/contact and general liftoff use ground telemetry, not unavailable four-corner struts.
- Skid movement works for forward and lateral motion; no wheel/four-corner telemetry is required for the speed-based slide cue.
- Corrected the Kiowa M3P body name condition, restored AH-6 HMP400/missile layers, and protected weapon carriers from the checkout's clock-valued unknown nominal-rate fallback.
- MH-6 fixed gun/rocket/missile entries are excluded because the supplied transport pylon set lacks the AH-6 fixed armament. Its other included effects are enabled.

“Enabled” does not mean the module exports a usable signal. Lifecycle/caster and other capability-gated cues can remain zero until the bridge supplies their dependencies. They are not disabled in these files. The restored high-speed, BVI/VRS/ETL and structural response curves are estimated legacy cues, not newly validated aircraft limits or measured resonances.

## Evidence and limits

The actual in-flight failure has not been captured in this task. The available local SimHub logs stop on August 26 and predate the failed imports. Consequently, missing identity and numeric representation are tested compatibility failure modes, not a proven diagnosis of the user's live installation.

Validation passed for all five profiles: registered properties; enabled flags; syntax; rotor output with absent identity and numeric/string inputs; landing and restored blade-slap response; skid ground/air separation; and disconnection silence. All 171 effects also executed with the **installed SimHub Jint 4.2.2.0 DLL** in a test harness, with rotor and disconnect assertions. This exercises the real JavaScript engine with simulated properties, not SimHub's full property resolver, audio pipeline or live DCS telemetry.

No installed DCS/SimHub files were modified. Original profiles remain available. The v0.3 user guide is a historical reference: its disabled-state tables do not describe v0.4. This note and REPAIR-AUDIT-v0.4.json describe the repaired release.

Rebuild: `node scripts/Build-RepairedHelicopterProfiles.js`.

Checks: `node tests/Test-RepairedHelicopterProfiles.js` and `pwsh -NoProfile -File tests/Test-RepairedProfilesJint.ps1`.
