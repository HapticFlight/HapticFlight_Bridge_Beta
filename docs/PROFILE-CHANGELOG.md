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

## AH-6J / MH-6J — v1.3 Impact Headroom

- **Change:** Firm touchdowns now give the body impact priority over concurrent ground-bump, skid-detail, and settling bursts.
- **Improves:** Keeps a hard landing distinct while reducing summed bass-shaker demand and clipping risk; soft and one-skid detail remain.
- **Test:** Compare a firm full-skid landing and bounce with a soft touchdown and one-skid set-down.

## AH-6J / MH-6J — v1.2 Flight Cues

- **Change:** Added phase-derived rotor tracking, smoothed ETL, and stronger landing, liftoff, and grounded-rotor cues.
- **Improves:** Makes rotor texture and transition cues more continuous while making meaningful skid events easier to feel.
- **Test:** Fly startup, a 16–26 kt ETL pass, liftoff, a soft touchdown, and a firm full-skid landing.
