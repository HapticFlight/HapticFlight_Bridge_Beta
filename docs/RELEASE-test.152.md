# HapticFlight Bridge 0.1.0-test.152

## Installation

1. Run `HapticFlight-Bridge-Setup-0.1.0-test.152.exe`.
2. In the installed `Profiles` folder, import
   `DCS-AH-6J-Haptic-Engine-v2.0.0-AH6-v1.3-Impact-Headroom.siprofile` for the AH-6J.
3. Disable any earlier AH-6J profile in SimHub before testing.

## Change log

### AH-6J v1.3 — impact headroom

- **Change:** Hard-landings now give the body-impact cue priority over simultaneous ground bump, skid, and settling cues.
- **Improves:** Firm full-skid landings remain distinct with less summed output and lower clipping risk; soft and one-skid detail remains available.
- **Test:** Compare a firm full-skid landing or bounce with a soft landing and a one-skid contact.

The installer moves earlier top-level AH-6J profiles into `Profiles\Archive\AH-6J` as rollback copies.

## Beta notice

This beta is unsigned. Windows App Control may block it on protected PCs; do
not disable security protections to force it to run.
