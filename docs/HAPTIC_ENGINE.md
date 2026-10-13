# Aircraft-aware haptic engine

Current engine: **1.3.0**, with 85 signal families. The original design below is
extended by the [test.122 realism implementation and validation](REALISM-IMPLEMENTATION-test.122.md).

The haptic engine is an additive SimHub-plugin layer that turns validated DCS
telemetry into immutable, capability-gated physical signals. The GameGUI Hook
continues to own DCS and module source adaptation; SimHub profiles continue to
own transducer frequency, gain, placement, and final tactile mixing.

## Runtime boundary

`GameGUI Hook -> validated protocol-v1 packet -> aircraft model -> haptic
detectors -> immutable snapshot -> DCSExtra.Haptic.* properties -> profile`

No detector reads sockets, SimHub state, files, or wall-clock globals. The UDP
receiver alone advances the engine. Property delegates perform bounded snapshot
reads and stale masking only. The Hook still uses `DCS.setUserCallbacks` and this
feature never reads or changes `Export.lua`.

Aircraft definitions are embedded from
`Haptics/Models/aircraft-haptic-models.json`, range-checked at startup, and
reported through `DCSExtra.Haptic.ModelStatus`. A missing or invalid definition
set activates only the conservative `Generic-v1` model; partially loaded model
sets are never used.

## Models

| Model | Contact interpretation | Status |
|---|---|---|
| `UH60-v1` | Three-point wheels with UH-60 source hysteresis | Derived, current reference model |
| `AH6-v1` | Four skids; transient single-skid contact is local detail | Derived; rotor remains a disclosed proxy |
| `AH64-v1` | Tail/main wheel support | Derived; rotor remains a disclosed proxy |
| `UH1-v1` | Weight-on-skids without invented point contacts | Derived |
| `OH58-v1-provisional` | Four skids | Provisional pending recorded and live validation |
| `Generic-v1` | Verified standard contact and air data only | Conservative fallback |

## Signal contract

Every registered signal exposes `Active`, `Intensity`, `Envelope`, `Phase`,
`EventId`, `Confidence`, and `Source`, plus a matching `DCSExtra.Has.Haptic*`
capability. Intensity and envelope are finite normalized values from zero to
one. Event output clears after 250 ms without fresh engine state; continuous
output fades from 250 ms to the receiver's 1000 ms stale threshold.

Touchdown is armed only after confirmed flight. Wheel models require main-wheel
support or persistent tail-first support; four-skid models require bilateral or
three-point support. Strength uses the peak downward speed in the 250 ms
pre-contact history. A smooth attack/hold/release envelope prevents a latched
diagnostic value from driving a continuous shaker effect.

## Validation status

New properties do not replace legacy properties or existing profiles. Synthetic
tests establish deterministic state behavior. The supplied UH-60 recording now
validates flight, rotor, touchdown, and ground paths offline; structured live
seat testing is still required, and every other module still needs a fresh
recording before aircraft-specific tuning can be promoted. Proxy and provisional
source labels remain visible after validation.

Legacy property names remain registered permanently for historical profiles.
They may become compatibility aliases only after maintained profiles migrate and
the complete replay/live validation gate passes.

## Implemented offline scope

- Engine lifecycle classification covers duplicate/out-of-order packets,
  sequence gaps, backward/frozen/excessive model time, stale resume, session,
  aircraft, model, capability/source, and receiver shutdown transitions.
- Touchdown and liftoff are deterministic stateful detectors. The remaining
  signal families reuse the current validated Hook calculations, then add
  aircraft/model availability, filtering, source labeling, stale masking, and
  recoil/touchdown/ground contamination suppression.
- Five newly named profiles exercise the common contract and module-specific
  extensions. They are test artifacts, have independent IDs, and do not replace
  any historical profile.
- The offline performance acceptance test measures the complete engine update;
  the test gate is `0.25 ms` per packet on the executing PC.

Recorded module replays and live seat validation have not been fabricated or
waived. Consequently the new profiles remain unapproved test artifacts, OH-58
remains provisional, and the permanent legacy registrations have not been
redirected to engine aliases.
