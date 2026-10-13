# DCS Extra Telemetry 0.1.0-test.128

This complete test package contains Haptic Engine 1.7.1 and the separate
six-aircraft `v2.0.0-test.19` SimHub profiles. Test.19 does not overwrite
earlier profile versions.

## Included haptic changes

- Enables confirmed DCS chaff and flare release pulses by default.
- Removes their incorrect optional/augmented labels.
- Retains test.18 confirmed liftoff unloading and test.17 main-wheel contact
  calibration.
- Keeps only hardware-dependent or clearly derived/unconfirmed effects
  optional after a full profile audit.

## Install and rollback

Extract the normal ZIP, run the graphical launcher, and import one matching
test.19 profile. Disable overlapping older profiles. The launcher's retained
rollback baseline is `0.1.0-test.121`; test.18 and earlier profiles remain in
`Profiles\History`.

This normal ZIP contains no self-extracting wrapper. The launcher and monitor
are unsigned test binaries; Windows may still require an allow or trust
decision until trusted Authenticode signing is configured.
