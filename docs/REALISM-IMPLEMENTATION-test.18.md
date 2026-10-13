# Realism test.18: confirmed liftoff unloading

Test.18 requires Haptic Engine 1.7.1 and is a separate six-aircraft SimHub
profile set. It never overwrites test.17 or an earlier profile revision.

## Correction

`Liftoff unloading` was incorrectly classified as an optional augmented cue
when the test.7 profile baseline was built. That broad profile rule disabled
it and labelled it optional. Every later test profile inherited that state.

The underlying liftoff signal is not an augmented or speculative cue. The
engine detects a transition from supported to unsupported state, confirms that
the aircraft remains off the ground for 50 ms, and then emits a short 180 ms
release envelope. The Hook separately exports `LiftoffPulse` from the same
derived contact transition. This is a deterministic flight-state event, though
it is not a physical measurement of suspension force.

## Profile behavior

All six test.18 profiles enable `Liftoff unloading` by default and restore its
plain name. Its prior restrained tuning is unchanged: gain 9, 18 Hz, and a
maximum formula amplitude of 25. It remains a brief release cue, not a
touchdown-sized thump.

Engine 1.7.1 main-wheel contact calibration, test.17's separate touchdown
body, ground texture, bumps, braking, and wheel-skid behavior are unchanged.

## Validation

Profile validation checks that every test.18 profile has a unique ID, retains
the Engine 1.7.1 gate, uses `Haptic.Liftoff.Envelope`, and enables the cue.
The haptic engine test separately confirms the 50 ms liftoff confirmation and
that its release envelope clears after its hold/release period.
