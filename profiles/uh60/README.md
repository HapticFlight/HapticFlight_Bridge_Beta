# UH-60 ShakeIt profiles

This folder contains versioned ShakeIt profiles for the UH-60L and UH-60L DAP.
Each file is retained as a rollback point; do not overwrite an earlier version.

## Current working profile

`DCS World - DCS UH60L - DCSExtra Refreshed v1.9.0.siprofile` is the latest
generated profile in this repository. It raises each sustained rotor harmonic
above its own seat-calibrated minimum for parked-hot and straight-and-level
flight, with the strongest constant presence at the 4-rev blade-pass
frequency. Maneuver swell and asymmetric attack/release smoothing are retained.
Signed-G swell is airborne-only so landing/contact acceleration spikes do not
contaminate the rotor layers. Progressive braking, measured deceleration, and
support rocking are retained.

v1.9.0 renames the former ground-resonance effect to `Gear-borne rotor
vibration`, because it represents normal vibration transmitted through loaded
landing gear rather than the dangerous mechanical instability called ground
resonance. It adds a restrained governed-rotor floor during confirmed contact,
scales that reinforcement with supported weight, and moves it to 13 Hz so the
17.2 Hz 4/rev blade-pass rhythm remains distinct. Taxi and suspension motion
can still strengthen the layer.

The v1.9.0 formulas are covered by structural regression tests and are replayed
against recorded telemetry before release. A live seat test is still required
to confirm tactile calibration.

## Build and test

Generate the current profile from a source `.siprofile`:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build-Uh60RefreshedProfile.ps1
```

Run the structural regression test:

```powershell
node .\tests\Test-Uh60ProfileRegression.js
```

The generated formulas use the fully qualified SimHub plugin namespace:
`DcsExtraTelemetryPlugin.DCSExtra.*`.

## Versioning

- Use a new versioned filename and profile ID for every profile revision.
- Preserve prior versions for rollback.
- Treat offline validation as structural only; verify output in a current DCS flight
  before describing an effect as calibrated.
