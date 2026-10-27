# HapticFlight Bridge 0.1.0-test.154

## Installation

1. Run `HapticFlight-Bridge-Setup-0.1.0-test.154.exe`.
2. Open the app and choose **Update components** if your DCS and SimHub paths are already configured.
3. In SimHub, import `DCS-AH-6J-Haptic-Engine-v2.0.0-AH6-v1.5-Rotor-Baseline.siprofile` and disable any older AH-6J profile.

## Change log

- **AH-6J v1.5:** 1/rev and 2/rev rotor layers are now conservative default cues, so rotor texture remains present through normal manoeuvres.
- **Landing headroom:** Retains the touchdown limiter, ground-texture ducking, and liftoff suppression that protect against stacked-impact clipping.
- **Ground telemetry:** Records the available AGL export source so ground-effect evidence can be verified in future recordings.
- **Telemetry recovery:** Briefly returns haptics to neutral after a telemetry reset or timing gap to prevent a stale-value burst.

Earlier AH-6J profiles are archived in `Profiles\Archive\AH-6J` for rollback.

## Beta notice

This beta is unsigned. Windows App Control may block it on protected PCs; do
not disable security protections to force it to run.
