# AH-6J and Apache rotor baseline refresh

> **Apache update:** [V2 test.5 gun-balance refresh](../apache-test5/README.md) retains this rotor baseline and corrects/reduces the cannon layers. AH-6J test.4 is unchanged.

**[Download both refreshed profiles](AH6-Apache-Rotor-Refresh-v2.0.0-test.4.zip?raw=true)**

**[Download the complete six-aircraft package](Helicopter-Haptic-Engine-Rotor-Refresh.zip?raw=true)** — includes these two test.4 profiles and the unchanged Kiowa, MH-6J, Huey and UH-60 test.3 profiles.

Import the replacement profile and select it manually. Disable the earlier version. Keep your existing V2 engine plugin and sound-card routing. The files have new IDs and are labelled **test.4 Rotor Baseline** in SimHub.

## What the recordings show

The AH-6J recording contains 22,221 packets over 546.269 simulation seconds. Apache contains 12,318 packets over 390.377 seconds. Neither file contains invalid JSON or duplicate packet sequences in this analysis.

The previous profiles have a mathematically nonzero baseline, but effect gains reduce its strength substantially. The table shows median formula output multiplied by effect gain, before downstream master/device processing. These values are a profile-side comparison, not measured amplifier output or seat acceleration.

| Stationary rotor layer | AH-6 before | AH-6 refreshed | Apache before | Apache refreshed |
|---|---:|---:|---:|---:|
| 1/rev | 3.83 | 12.92 | 5.27 | 12.60 |
| 2/rev | 2.71 | 8.42 | 3.44 | 8.16 |
| Blade pass | 6.47 | 16.27 | 7.38 | 15.84 |

The AH-6's `AH6N1Proxy` reports 58.98–72.91% across the recording; the old governed-speed curve therefore never reaches its maximum. The left engine RPM field is approximately 98.97–103.55%, confirming the two exports have different scales/semantics. Neither is a direct measurement of rotor Nr in this capture. Apache also uses a rotor proxy. Raw RPM, frequency and capability fields are present; the observations do not support a missing rotor telemetry diagnosis.

Collective/load terms then add appreciable output during control changes and powered flight. That explains why those changes are easier to feel than the quiet baseline. The saved profile global gains (50 AH-6, 45 Apache) have `UseProfileGain=false`; those inactive profile gains were not applied as another multiplier in this calculation. Live master gain, equalization, device routing and transducer response are not in these recordings.

## Fix

- Strengthened the independent 1/rev, 2/rev and blade-pass running baselines to 42, 34 and 44 before effect gain.
- Set their effect gains to 30, 24 and 36. Group and global gain settings remain unchanged.
- Baseline ramps smoothly from zero at 15% of the existing RPM proxy to full at 60%, making it suitable for the observed running range without using collective as the baseline trigger. This is a tactile design curve, not an assertion that actual rotor speed is 60%.
- Retained collective/load/maneuver swell on top of the baseline and retained each original formula amplitude cap. Higher effect gains do increase the post-gain ceiling, explicitly listed in `Rotor-Changes.json`.
- Retained all original frequency formulas. Recorded AH-6 carriers span approximately 4.62–5.71 Hz (1/rev), 9.24–11.42 Hz (2/rev), and 23.10–28.56 Hz (blade pass). Apache blade pass spans 16.07–19.27 Hz. These remain proxy-derived frequencies. Correcting rotor-frequency provenance requires actual module Nr telemetry, not guessing a replacement from the engine field.
- Retained every non-rotor effect byte-for-byte at the JSON-object level, including landing, weapons and lifecycle. No Hook, engine DLL or installed settings changed.

In steady airborne samples, median blade-pass level rises from 9.33 to 19.52 for AH-6 and 12.44 to 22.11 for Apache. All three layers retain further load response within their caps.

## Verification and limits

All 34,539 packets were replayed through the old and new JavaScript rotor formulas at packet cadence. V2 rotor-load pass-through and maneuver/control conditioning were reconstructed from the inspected engine source for the inputs these formulas use. The captures contain raw telemetry, not the plugin's complete live haptic state or audio output; this is not a replay of SimHub's entire audio pipeline. SimHub's actual formula evaluation cadence can differ.

Stationary samples require rotor running, ground speed below 0.3 m/s, ground contact and absolute collective movement rate below 0.03. Steady airborne samples require rotor running, airborne state, vertical speed below 1 m/s, G within 0.12 of 1, and collective movement rate below 0.03. Startup settling samples are retained in these distributions.

Regression checks verify exactly three changed effects per aircraft, unchanged frequencies and non-rotor effects, retained load swell, output caps, RPM ramp, and zero output after shutdown settling or immediately on disconnect/missing engine. All 76 effects from the two refreshed profiles executed in installed SimHub Jint 4.2.2.0 with simulated properties. These results establish stronger computed baseline output; tactile adequacy still requires your seat test.

Source recordings: `DcsExtraTelemetry-20260906-162215.jsonl` (AH-6J) and `DcsExtraTelemetry-20260906-163154.jsonl` (Apache). Recordings themselves are not included in GitHub downloads.
