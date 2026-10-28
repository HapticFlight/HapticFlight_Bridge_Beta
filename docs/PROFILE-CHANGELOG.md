# Profile change log

This is the user-facing history for importable SimHub profiles. Add one entry
whenever a profile file changes. Keep it brief and use this format exactly:

```md
## <Aircraft> — <Profile version>

- **Change:** What was adjusted, in plain language.
- **Improves:** The intended in-flight result.
- **Test:** The short scenario that confirms it.
```

Use a new profile version and preserve the prior file in that aircraft's
archive. Do not put speculative claims, raw implementation details, or app
release notes in these entries.

## AH-6J — v1.7 Rotor Response (test candidate)

- **Change:** Separates steady rotor presence from manoeuvre-driven swell and bounds all three rotor targets.
- **Improves:** Leaves room for banks and control inputs to change the rotor feel without a constantly saturated baseline.
- **Test:** Compare hover, climb, bank and descent at fixed device and master gains; verify landing and one-skid detail remain distinct.

## AH-6J / MH-6J — v1.6 Rotor Gain Reduction

- **Change:** Reduced the 1/rev, 2/rev, and 5/rev continuous rotor gains to 9%, 6%, and 24%.
- **Improves:** Prevents the three rotor layers from overwhelming high-output tactile hardware while retaining manoeuvre-linked rotor texture.
- **Test:** Hover, climb, bank, and descend at your normal master gain; confirm a continuous but non-dominant rotor baseline.

## AH-6J / MH-6J — v1.5 Rotor Baseline

- **Change:** Made the restrained 1/rev and 2/rev rotor layers standard defaults; retained the v1.4 impact limiter and ground-texture ducking.
- **Improves:** Adds low-frequency rotor movement through manoeuvres while retaining headroom for firm touchdowns.
- **Test:** Fly hover, climb, bank, descent, and both soft and firm landings; use only one AH-6J profile at a time.

## AH-6J / MH-6J — v1.3 Impact Headroom

- **Change:** Firm touchdowns now give the body impact priority over concurrent ground-bump, skid-detail, and settling bursts.
- **Improves:** Keeps a hard landing distinct while reducing summed bass-shaker demand and clipping risk; soft and one-skid detail remain.
- **Test:** Compare a firm full-skid landing and bounce with a soft touchdown and one-skid set-down.

## AH-6J / MH-6J — v1.2 Flight Cues

- **Change:** Added phase-derived rotor tracking, smoothed ETL, and stronger landing, liftoff, and grounded-rotor cues.
- **Improves:** Makes rotor texture and transition cues more continuous while making meaningful skid events easier to feel.
- **Test:** Fly startup, a 16–26 kt ETL pass, liftoff, a soft touchdown, and a firm full-skid landing.
