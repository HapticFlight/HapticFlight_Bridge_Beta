# Realism test.17: UH-60 main-wheel contact calibration

Test.17 requires Haptic Engine 1.7.1 and is a separate six-aircraft SimHub
profile set. It never overwrites test.16 or any earlier profile revision.

## Why this changed

The supplied UH-60L DAP recording from September 26 contains direct
left-, right-, and tail-wheel contact pulses, gear-strut travel, wheel
rotation, and ground-motion data for the whole flight. Several arrivals were
tail-first: the tail's compression-rate envelope reached about 0.80 while a
light following main-wheel direct edge was held to a 0.30 minimum. That made a
real main-wheel contact reach the profile with less tactile headroom than the
tail contact.

These normalized envelopes are a calibration signal, not a DCS wheel-load or
force export. The cues remain marked provisional for that reason.

## Engine 1.7.1

For a direct contact edge on a three-point wheeled aircraft:

- left and right main-wheel contact envelopes now have a 0.50 to 0.90 range;
- tail contact envelopes have a 0.32 to 0.57 range;
- compression-rate contact behavior remains available when a direct edge is
  absent.

This only changes the relative headroom of verified per-wheel contact edges.
It does not infer a surface type, weather, landing force, or a new touchdown.

## Profile behavior

`Wheel contact detail` is tuned separately from the touchdown-body effect:

- one direct main wheel uses `74 × main`;
- two direct main wheels use `82 × main`;
- tail-only contact uses `34 × tail`;
- a main contact is only lightly ducked during the simultaneous touchdown-body
  cue, while a tail-only contact retains the stronger duck.

The effect uses gain 22 at 30 Hz. This is intended to give a soft rolling
main-wheel arrival a distinct, short low-frequency contact cue without making
it a second touchdown impact.

Ground texture, ground bumps, suspension movement, touchdown body impact,
braking stages, and wheel-skid behavior are unchanged from test.16.

## Validation

The detector test asserts that a direct soft main-wheel contact is at least
0.50, stays stronger than an equivalent tail contact, and caps the tail at
0.57. Profile tests verify six unique test.17 profiles, the Engine 1.7.1
gate, and the contact formula. Recording replay validates the telemetry and
formula path; it cannot measure the physical output of a particular shaker.
