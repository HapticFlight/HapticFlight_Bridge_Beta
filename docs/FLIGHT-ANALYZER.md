# Flight analyzer

The Flights page turns one recorded `.jsonl` telemetry file into a repeatable
profile-tuning review. It does not modify DCS, `Export.lua`, SimHub, an
installed profile, or shaker hardware.

## Recording storage

New recordings use the lossless `.jsonl.gz` format. They contain the same raw
60 Hz packet stream as the earlier `.jsonl` recordings, but gzip removes the
repeated JSON field names and unchanged text efficiently. Existing `.jsonl`
files remain supported. Stop a recording normally so the recorder can finish
and validate the compressed file before analysis.

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

## Profile tuning workflow

1. Choose one repeatable DCS scenario: for example, a specified takeoff, a
   controlled landing, or a fixed airspeed turn. Record a **baseline** without
   changing the profile.
2. In **Debrief**, select the event that felt wrong. In **Telemetry**, verify
   the source condition, timing, duration, and overlap. Treat direct,
   derived, and proxy sources differently.
3. Use **Copy A/B worksheet** for the selected effect. It records the baseline
   evidence and leaves space for one profile parameter, one change, and the
   physical result.
4. In SimHub, change **one** parameter in the matching ShakeIt effect: gain,
   threshold, attack/release, frequency, or routing. Do not use the analyzer
   as if it can apply or test that change.
5. Use SimHub's own preview/test control when available, then repeat the same
   DCS scenario on the actual hardware. Note strength, timing, harshness,
   localization, clipping, and unwanted overlap.
6. Record the retest and use **Debrief** comparison to check whether the
   source telemetry and modelled episode are comparable. Keep or revert the
   one change before starting another.

**Copy SimHub handoff** is an honest shortcut: it copies the selected effect's
modelled evidence and a reminder to use SimHub's own effect test. It never
starts a shaker effect, writes a profile, or changes hardware output.

### What makes a comparison useful

- Use the same aircraft, mission state, loadout, weather, gross weight, and
  maneuver where practical.
- Compare only recordings with similar telemetry: the same touchdown severity
  or rotor-RPM/load range matters more than matching elapsed time.
- Change one parameter per trial. A gain change and an envelope change in the
  same run do not identify the cause of an improvement.
- Keep the previous `.siprofile` as a named baseline. The analyzer does not
  edit or version SimHub profiles for you.

## Interpretation limits

Effect timelines and combined load are replayed **visually** from the recording
and the application's profile logic. The replay cursor does not publish data
to SimHub and does not move a shaker. Frequencies come from profile metadata;
flight phases are estimates. None of those values measure final SimHub mixing,
amplifier clipping, or shaker output. The telemetry trace labels call out
aircraft-dependent direct, derived, or proxy rotor data so that proxy values
are not treated as physical rotor measurements.
