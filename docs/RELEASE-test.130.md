# DCS Extra Telemetry 0.1.0-test.130

Fixes a launcher crash in test.129. Use this build instead of test.129.

## Fixed

- test.129 closed at startup with "'SuccessGlow' resource not found" on any
  computer where the DCS hook and SimHub plugin were both installed. The new
  dark launcher removed the old glowing status lights, but some status code
  still asked for the glow. Status lights are now flat everywhere and the
  launcher opens normally when setup is complete.
- The launcher's UI contract test now fails the build if code looks up a theme
  resource that does not exist, so this kind of crash is caught before release.

Everything else is the same as test.129: the dark glass launcher dashboard,
Haptic Engine 1.7.1 and the six-aircraft `v2.0.0-test.19` SimHub profiles.
See `RELEASE-test.129.md` for the interface changes.

## Install and rollback

Extract the normal ZIP, run the graphical launcher, and import one matching
test.19 profile. Disable overlapping older profiles. The launcher's retained
rollback baseline is `0.1.0-test.121`; test.18 and earlier profiles remain in
`Profiles\History`.

This normal ZIP contains no self-extracting wrapper. The launcher and monitor
are unsigned test binaries; Windows may still require an allow or trust
decision until trusted Authenticode signing is configured.
