# DCS Extra Telemetry 0.1.0-test.125

This complete test package contains Haptic Engine 1.7.0 and the separate
six-aircraft `v2.0.0-test.16` SimHub profiles. Test.16 does not overwrite
earlier profile versions.

## Included haptic changes

- Adds a stateful, optional derived VRS-risk texture. It uses flight state and
  observed motion; it is not presented as a direct DCS VRS export.
- Repairs tail-first and one/two-main-wheel rolling-contact feedback.
- Separates brake pedal application, measured braking/deceleration load, and
  derived lateral wheel skid/scrub for wheel-equipped aircraft.
- Leaves ground-surface texture behavior unchanged.
- Makes 1/rev and 2/rev rotor layers optional low-frequency extensions. The
  blade-pass layer remains the default steady rotor cue.

## Install and rollback

Extract the normal ZIP, run the graphical launcher, and import one matching
 test.16 profile. Disable overlapping older profiles. The launcher's retained
 rollback baseline is `0.1.0-test.121`; test.15 profiles remain available under
 `Profiles\History\test.15`.

This normal ZIP deliberately contains no self-extracting wrapper. The launcher
and monitor are unsigned test binaries; Windows may still require an allow or
trust decision until trusted Authenticode signing is configured.
