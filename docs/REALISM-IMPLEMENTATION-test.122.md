# Realism implementation: application test.122, Engine 1.3.0, profiles test.12

This release improves physical event separation, ground motion, capability
truth, and update-rate consistency. The profiles remain independent of SimHub
output routing and do not assume a particular number of shakers. Buttkicker
Gamer Pro is the primary seat-test reference, not a hard-coded device target.

Import only the test.12 profile matching the aircraft and disable overlapping
profiles. Earlier test.7 through test.11 profiles are retained under
`Profiles\History`; they are comparison and rollback stages, not the recommended
final profile. Working application test.121 remains the full-package rollback.

## What changed

| Effect area | Earlier construction | test.122 / Engine 1.3 construction | Confidence and status |
| --- | --- | --- | --- |
| Touchdown | Contact policy plus captured sink-rate envelope | Preserved; session/pause reset ordering corrected so a new mission cannot inherit an old event | Derived and replay-verified; four UH-60 touchdowns observed |
| Bounce and settling | Finite landing/bump tail | Preserved as a separate event from touchdown and rolling texture | Provisional until seat-rated |
| Ground bumps | Positive strut-compression transient with contact and weapon suppression | Preserved with sample-rate-independent engine filters | Provisional; three UH-60 bump events observed |
| Ground roll | Weak speed-only engine proxy | Uses the Hook's speed-and-suspension-derived roll texture when available, with a speed-only fallback | Derived for UH-60; surface material is not claimed |
| Suspension and body rocking | Strut-rate, pitch, and roll movement | Preserved; time-based engine filtering and priority attenuation keep impacts distinct | Provisional because draw-argument travel is not force |
| Wheel shimmy and tail caster | Derived wheel motion and lateral motion | Preserved as subordinate layers | Derived; UH-60 recording coverage present |
| Rotor harmonics | Aircraft-specific RPM/load formulas with per-update smoothing | Dynamic 1/rev, structural, and blade-pass frequencies retained; all stateful smoothing now uses model-time delta | UH-60 derived and replayed; other modules await fresh recordings |
| Transmission and governor | Derived powertrain intensities | Preserved, capability-gated, and time-corrected | UH-60 recording coverage present |
| Blade slap | Derived BVI/descent cue | Preserved only when the module reports the blade-slap capability | Derived; UH-60 non-zero coverage present |
| Maneuver and vertical-energy cues | Derived body-motion cue | Disabled by default and labelled optional augmented cue | Optional; it can aid awareness but is not a direct vibration measurement |
| Turbulence and buffet | Derived aerodynamic disturbance | Preserved and separated from weapon recoil; continuous output yields to priority events | Derived; limited UH-60 coverage |
| Airframe shock | Heuristic acceleration-based pulse | Disabled by default and labelled optional because the source cannot reliably identify damage or impact cause | Unconfirmed; optional only |
| Weapons | Weapon-class, event, cadence, and named-weapon layers | Preserved; priority mix temporarily attenuates continuous layers during gun/rocket/missile events | Source-dependent; no weapon event occurred in this UH-60 recording |
| Countermeasures | Release pulses | Disabled by default and labelled optional augmented cues | Direct release event, but tactile realism depends on airframe and installation |
| Doors and mechanical endpoints | Module draw-argument movement/endpoints | Preserved with explicit capability gates | Direct/derived when the module exposes the values |
| Engine lifecycle | APU, starter, spool, and rotor state | Capability-gated; false family assumptions no longer mark absent warnings or lifecycle signals supported | UH-60 lifecycle capability present; this recording did not cover a full start/shutdown |
| Layer competition | Independent effects could stack at full strength | Engine emits smooth continuous-layer gain guidance during touchdown, bump, shock, and weapon demand | Engineering mix guidance; downstream amplifier clipping is not measured |

## Versioned profile stages

| Profile revision | Purpose |
| --- | --- |
| test.7 | Reproducible baseline with augmented cues explicitly optional and disabled |
| test.8 | Smooth priority attenuation for continuous layers; requires Engine 1.2 |
| test.9 | Capability-truth revision; unconfirmed airframe shock disabled |
| test.10 | Recorded detailed ground-roll source and UH-60 replay status; requires Engine 1.3 |
| test.11 | Model-time smoothing for 75 stateful formulas |
| test.12 | Completes model-time smoothing for six AH-6J/MH-6J rotor formulas; recommended revision |

Every revision has new profile and container IDs. Output-device routing is not
included, so importing a profile does not replace the user's channel layout.

## UH-60 recording evidence

The supplied test.121 recording contained 17,272 valid packets over 399.576
seconds at an average 43.226 Hz. It contained one session, no malformed packets,
no missing or duplicate sequence numbers, 6,616 ground samples, 10,656 airborne
samples, four liftoffs, and four touchdowns. Contact state matched the combined
wheel contacts on every sample.

Engine 1.3 replay detected four touchdown events, four liftoff events, three
ground-bump events, and seven settling events. Detailed ground-roll output changed
from a speed-proxy mean of 0.000297 and maximum of 0.010473 to a derived mean of
0.002500 and maximum of 0.069395. Touchdown, bump, settling, suspension, and
blade-slap output remained unchanged by that source correction.

The recording did not contain weapon fire, countermeasure release, door movement,
a complete cold-start/shutdown sequence, or a brake-induced tailwheel settle.
Those effects remain implemented but are not marked UH-60 recording-validated.

## Install and rollback

1. Keep the extracted test.121 package and its selected profile as the rollback.
2. Extract `DcsExtraTelemetry-0.1.0-test.122.zip`; do not use a self-extracting EXE.
3. Run `Tools\DcsExtraTelemetry.Launcher.exe` and verify the UI shows package
   `0.1.0-test.122`, Haptic Engine `1.3.0`, and rollback `0.1.0-test.121`.
4. Close DCS and SimHub, install the project-owned Hook and plugin, then restart
   both applications. `Export.lua` is not read or modified.
5. Import the matching test.12 profile from `Profiles`, select it manually, and
   disable overlapping profiles.
6. If behavior regresses, reinstall test.121 and reselect its previous profile.

Offline replay verifies signal behavior and formulas; it does not measure shaker,
seat, amplifier, mounting, or human perception. Final gains remain subject to
repeatable seat testing on the Buttkicker Gamer Pro and at least one different
shaker arrangement.
