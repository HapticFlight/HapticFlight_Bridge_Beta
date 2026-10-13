# DCS Extra Telemetry 0.1.0-test.127

This complete test package contains Haptic Engine 1.7.1 and the separate
six-aircraft `v2.0.0-test.18` SimHub profiles. Test.18 does not overwrite
earlier profile versions.

## Included haptic changes

- Enables the confirmed `Liftoff unloading` release cue by default.
- Removes its incorrect optional/augmented label.
- Keeps its restrained 18 Hz release tuning rather than treating liftoff as a
  touchdown impact.
- Preserves test.17 direct main-wheel contact calibration and all existing
  ground texture, braking, wheel-skid, rotor, and optional VRS-risk behavior.

## Install and rollback

Extract the normal ZIP, run the graphical launcher, and import one matching
test.18 profile. Disable overlapping older profiles. The launcher's retained
rollback baseline is `0.1.0-test.121`; test.17 and earlier profiles remain in
`Profiles\History`.

This normal ZIP contains no self-extracting wrapper. The launcher and monitor
are unsigned test binaries; Windows may still require an allow or trust
decision until trusted Authenticode signing is configured.
