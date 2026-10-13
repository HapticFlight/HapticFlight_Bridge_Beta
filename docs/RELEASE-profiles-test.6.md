# Ground-effects profiles test.6

This release contains six importable `v2.0.0-test.6` SimHub profiles and the
ground-effect audit only. It contains no executable, DLL, installer, or bundled
application ZIP.

The profiles introduce ground-bump, shaped contact, settling, separate rolling
and roughness, bounce rearming, and forward/lateral skid-slide cues. They preserve
the latest published non-ground tuning for each aircraft, including the AH-6J
rotor refresh and Apache gun balance.

These profiles require a locally built Haptic Engine 1.1.0 plugin from the source
on `main`. The former unsigned application test.121 was withdrawn following a
Windows Defender detection. Do not restore or bypass that detection. Binary
distribution is paused until code signing and Defender review are complete.

OH-58D remains provisional. This is a development profile release, not a live
aircraft or hardware calibration.
