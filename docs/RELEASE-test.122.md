# DCS Extra Telemetry 0.1.0-test.122

The normal ZIP contains the full launcher, monitor, DCS Hook, SimHub plugin,
Haptic Engine 1.3.0, six recommended V2 test.12 aircraft profiles, and preserved
test.7 through test.11 profile stages. No self-extracting executable is included.

Engine 1.3 adds elapsed-time profile smoothing, accurate capability gating,
recorded detailed wheel-roll input with a speed-only fallback, reset-order fixes,
and smooth continuous-layer attenuation during physical impacts and weapon
events. Optional augmented cues and the unconfirmed airframe-shock heuristic are
disabled and clearly labelled.

UH-60 flight, rotor, landing, and ground behavior was replayed against the
supplied 2026-09-19 test.121 recording. Weapons, countermeasures, door movement,
full startup/shutdown, and brake-tail recovery were not present in that capture
and remain pending live validation. Other aircraft profiles carry the shared
engine and formula improvements but still require fresh module recordings and
seat tests before aircraft-specific calibration claims.

Use `DcsExtraTelemetry-0.1.0-test.121.zip` as the last-known-working rollback.
See `REALISM-IMPLEMENTATION-test.122.md` for the complete effect table,
validation evidence, installation steps, and limitations.
