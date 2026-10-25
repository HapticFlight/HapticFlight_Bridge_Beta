# Flight analyzer

The Flights page turns one recorded `.jsonl` telemetry file into a repeatable
profile-tuning review. It does not modify DCS, `Export.lua`, SimHub, an
installed profile, or shaker hardware.

## What the analyzer shows

- **Timeline:** modelled effect episodes, their intensity, duration, and
  estimated flight phase.
- **Flight phases:** a compact comparison of modelled effect peaks by detected
  flight phase.
- **Combined load:** simultaneous modelled effect activity by category.
- **Telemetry:** compact, bucketed source telemetry such as indicated airspeed,
  vertical speed, rotor RPM, brakes, touchdown envelope, braking, and rotor
  droop. The source is shown for every selected trace.
- **Debrief:** time-sorted effect events, review flags, and a comparison against
  a second recent recording.

Selecting an event or clicking the telemetry chart moves the shared replay
cursor. Exported reports include debrief findings, all effect events, and a
list of telemetry-signal sources.

## Using it to tune a profile

1. Record a controlled flight scenario before changing a profile.
2. Open **Debrief** and flag the event that felt wrong, such as a touchdown or
   brake application.
3. Open **Telemetry** and align the relevant source signal with the effect
   episode. Check timing, peak, duration, and overlap before changing a
   threshold or gain.
4. Make one isolated profile change, repeat the scenario, then use the Debrief
   comparison selector to review the difference.
5. Validate the result in SimHub and on the physical haptic device.

## Interpretation limits

Effect timelines and combined load are replayed from the recording and the
application's profile logic. Frequencies come from profile metadata. Flight
phases are estimates. None of those values measure final SimHub mixing,
amplifier clipping, or shaker output. The telemetry trace labels call out
aircraft-dependent direct, derived, or proxy rotor data so that proxy values
are not treated as physical rotor measurements.
