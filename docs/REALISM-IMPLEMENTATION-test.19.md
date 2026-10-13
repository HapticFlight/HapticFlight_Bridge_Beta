# Realism test.19: confirmed countermeasure releases

Test.19 requires Haptic Engine 1.7.1 and is a separate six-aircraft SimHub
profile set. It never overwrites test.18 or an earlier profile revision.

## Correction

`Flare dispenser pulse` and `Chaff dispenser pulse` were incorrectly swept
into the original test.7 broad optional-effects rule and stayed disabled in
later profile revisions. They are not speculative effects: the Hook reads
`LoGetSnares()` and creates a release pulse only when the reported flare or
chaff count decreases. The engine exposes these as direct countermeasure event
signals.

## Profile behavior

All six test.19 profiles enable the two direct countermeasure-release effects
by default and restore their plain names. Existing gain and frequency settings
are unchanged: flare is gain 11 at 45 Hz, and chaff is gain 9 at 35 Hz. Both
remain capability-gated and are silent when DCS does not expose snares.

Test.18 confirmed liftoff unloading, Engine 1.7.1 main-wheel contact
calibration, touchdown-body, surface texture, braking, and wheel-skid behavior
are unchanged.

## Disabled effects after the audit

The only disabled effects in test.19 are deliberate:

- 1/rev and 2/rev rotor extensions are hardware-dependent low-frequency
  additions; blade-pass remains enabled.
- collective movement, maneuver, and hard-maneuver overlays are derived
  augmentation and can duplicate enabled baseline motion layers.
- VRS-risk is a clearly labelled derived state model, never a DCS VRS export.
- airframe shock is motion-derived and cannot identify a projectile or source.

No disabled current-profile effect uses a direct confirmed liftoff, chaff, or
flare event signal. The profile regression test enforces that rule.
