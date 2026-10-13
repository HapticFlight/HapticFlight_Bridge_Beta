# DCS Extra Telemetry 0.1.0-test.126

This complete test package contains Haptic Engine 1.7.1 and the separate
six-aircraft `v2.0.0-test.17` SimHub profiles. Test.17 does not overwrite
earlier profile versions.

## Included haptic changes

- Corrects the calibration imbalance found in a UH-60L DAP recording where a
  soft direct main-wheel contact could be weaker than a preceding tail-wheel
  cue.
- Gives direct left and right main-wheel contact more headroom and limits
  tail-only contact dominance.
- Keeps the main-wheel contact cue distinct from the touchdown-body effect.
- Preserves test.16 ground texture, ground bump, braking, wheel-skid, rotor,
  and optional VRS-risk behavior.

## Install and rollback

Extract the normal ZIP, run the graphical launcher, and import one matching
test.17 profile. Disable overlapping older profiles. The launcher's retained
rollback baseline is `0.1.0-test.121`; test.16 and earlier profiles remain in
`Profiles\History`.

This normal ZIP contains no self-extracting wrapper. The launcher and monitor
are unsigned test binaries; Windows may still require an allow or trust
decision until trusted Authenticode signing is configured.
