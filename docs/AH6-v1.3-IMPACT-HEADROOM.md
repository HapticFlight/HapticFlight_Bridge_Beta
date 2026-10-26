# AH-6J v1.3 impact headroom

Import `DCS-AH-6J-Haptic-Engine-v2.0.0-AH6-v1.3-Impact-Headroom.siprofile` for the next AH-6J test.

For the concise history and testing format used by every aircraft profile, see
[Profile change log](PROFILE-CHANGELOG.md).

This is a targeted successor to v1.2. It retains the phase-derived five-blade frequency, ETL smoothing, liftoff, and soft/one-skid landing detail. When the confirmed AH-6 landing envelope rises above 35%, it progressively reserves event headroom for the calibrated body impact. At a very hard landing, it ducks ground bump, individual-skid detail, and settling rather than asking SimHub to sum those bursts with the body hit.

Test the same landing sequence that clipped in v1.2: a firm full-skid touchdown, a bounced recontact, a soft touchdown, and a one-skid set-down. The hard hit should remain distinct without clipping; the soft and partial-skid cues should remain present. This profile changes no blade-frequency formula.
