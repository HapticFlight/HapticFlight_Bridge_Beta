# HapticFlight Bridge 0.1.0-test.149

## Installation instructions

1. Run `HapticFlight-Bridge-Setup-0.1.0-test.149.exe`.
2. Open HapticFlight Bridge and use the separate integration controls to update
   the DCS Hook and SimHub plugin.
3. In SimHub, import `DCS-AH-6J-Haptic-Engine-v2.0.0-AH6-v1.2-Flight-Cues.siprofile` and disable AH-6J v1.1.

## Change log

- AH-6J/MH-6J: restores an explicitly labelled engine-RPM fallback when rotor
  phase is unavailable. Direct phase remains preferred.
- Smooths ETL onset and release without increasing its baseline gain.
- Recalibrates AH-6 touchdown, liftoff unloading, and grounded rotor/skid load
  from the submitted recording.
- Ships the v1.2 AH-6 flight-cue profile and focused testing instructions.

## Beta notice

This beta is unsigned. Windows App Control may block it on protected PCs; do
not disable security protections to force it to run.
