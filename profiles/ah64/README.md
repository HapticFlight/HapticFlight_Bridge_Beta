# AH-64D ShakeIt profile

## Current V2 profile — test.5

**[Download Apache Haptic Engine V2 test.5](DCS-AH-64D-Apache-Haptic-Engine-v2.0.0-test.5.siprofile?raw=true)**

This is the September 7 gun-balance refresh. It retains the test.4 rotor baseline,
corrects recognition of the recorded `Cannon` weapon name, and reduces the two
M230 layers. All 38 effects remain enabled. It requires the V2 Haptic Engine.
Import and select **DCS AH-64D Apache - Haptic Engine v2.0.0-test.5 Gun Balance**,
disable the previous Apache profile, and retain your existing output routing.

[Recording assessment and complete package](../../downloads/apache-test5/README.md)

## Historical v0.1 baseline

The profile below is retained for historical reference; use the V2 profile above
with the V2 engine.

`DCS World - DCS AH-64D - DCSExtra Baseline v0.1.siprofile` is the first
AH-64D-specific profile. It inherits the UH-60 v1.8.8 cue separation and
smoothing architecture while using the Apache's 289 RPM, four-blade rotor
frequencies and its M230, Hydra, missile, wheeled-ground, and braking cues.

The profile depends on the AH-64 telemetry adapter that publishes
`AH64EngineRpmProxy` and cockpit collective arguments 474/475. It is an
engineering baseline, not a final seat calibration. Validate parked-hot,
straight-and-level, climb/dive, maneuver, touchdown/taxi/braking, M230, Hydra,
Hellfire, and countermeasure cases before increasing the global gain.

Regenerate it with:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build-Ah64Profile.ps1
```

Run its structural checks with:

```powershell
node .\tests\Test-Ah64ProfileRegression.js
```
