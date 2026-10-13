# Realism test.16: rotor defaults and wheel braking stages

Test.16 requires Haptic Engine 1.7.0 and is a separate importable profile set.
It does not overwrite test.15 or earlier profiles.

- 1/rev and 2/rev rotor layers are now disabled by default and labelled
  optional low-frequency extensions. Blade-pass remains the default steady
  rotor cue for shakers with limited low-frequency output.
- Wheel-equipped profiles split braking into pedal application, measured
  deceleration/airframe load, and derived lateral wheel skid/scrub.
- The wheel-skid cue requires braking, wheel-ground contact, rolling speed, and
  lateral motion. It is not a locked-wheel flag and does not change any ground
  material or surface-texture behavior.

Validate wheel-skid behavior with a controlled rolling-brake test before
increasing its gain. Keep it distinct from straight-line braking and existing
skid-aircraft slide effects.
