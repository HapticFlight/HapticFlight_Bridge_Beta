# AH-6J / MH-6J v1.6 rotor gain reduction

Import `DCS-AH-6J-Haptic-Engine-v2.0.0-AH6-v1.6-Rotor-Gain-Reduction.siprofile`.

v1.6 reduces the continuous rotor layers as a group: 1/rev is 9%, 2/rev is
6%, and 5/rev blade-pass is 24%. It retains the phase-derived rotor tracking,
touchdown limiter, and ground-texture ducking from prior revisions.

Use one AH-6J profile at a time. Start at your normal SimHub master gain and
confirm a hover, climb, bank, and descent before changing individual gains.
