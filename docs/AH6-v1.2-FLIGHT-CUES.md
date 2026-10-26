# AH-6J/MH-6J v1.2 flight-cue update

This update is based on `DcsExtraTelemetry-20261026-132137.jsonl.gz` with the
v1.1 calibrated profile active.

## What changed

- When rotor phase argument 37 is usable, it remains the preferred direct rotor
  source. If it is unavailable, the Hook now exposes `AH6EngineRpmProxy` as a
  bounded fallback for steady rotor presence and low-hover derived cues. It is
  not direct Nr.
- ETL retains the existing 8.5-13.5 m/s band and strength, with a 0.15 s rise
  and 0.40 s release envelope. It should enter and leave as a texture rather
  than a hard switch.
- Touchdown uses a recording-calibrated curve: the 34% raw event in the source
  recording, after AH-6 light-landing scaling, reaches about 30% effect output
  instead of about 12%.
- Liftoff unloading now reaches 11.2% at a full pulse rather than 2.25%.
- Grounded rotor/skid loading accepts the labelled RPM proxy, remains muted
  during an impact, and is separate from the low-hover ground-effect cue.

## Installation

1. Close DCS and SimHub.
2. Install the supplied DCS Hook and SimHub plugin through HapticFlight Bridge.
3. Import `DCS-AH-6J-Haptic-Engine-v2.0.0-AH6-v1.2-Flight-Cues.siprofile`.
4. Disable the prior AH-6J v1.1 profile so cues are not doubled.

## Limits

Ground effect remains a derived low-hover cue, not a DCS EFM-exported force.
The RPM fallback supports continuous presence and low-hover logic; it must not
be used to diagnose real rotor droop or phase behavior.
