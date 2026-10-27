# SimHub profile guide

## Choose one profile

The `Profiles` folder ships separate profiles because rotor frequency, ground
contact, and engine behavior differ across aircraft. Import **one matching
profile**, and do not leave another aircraft profile enabled on the same
Shaker output.

| DCS aircraft | Recommended beta profile |
| --- | --- |
| UH-60L / UH-60L DAP | `DCS-UH60L-Haptic-Engine-v2.0.0-test.19-Realism.siprofile` |
| AH-64D | `DCS-AH-64D-Haptic-Engine-v2.0.0-test.19-Realism.siprofile` |
| AH-6J | `DCS-AH-6J-Haptic-Engine-v2.0.0-AH6-v1.6-Rotor-Gain-Reduction.siprofile` |
| MH-6J | `DCS-MH-6J-Haptic-Engine-v2.0.0-test.19-Realism.siprofile` |
| OH-58D | `DCS-OH-58D-Haptic-Engine-v2.0.0-test.19-Realism.siprofile` |
| UH-1H | `DCS-UH-1H-Haptic-Engine-v2.0.0-test.19-Realism.siprofile` |
| CH-47F | `DCS-CH-47F-Haptic-Engine-v2.0.0-test.2.siprofile` |

The UH-60L `v1.9.0` profile is an alternative tune, not an additional layer.
Use it instead of the UH-60L test.19 profile only when you intentionally want
to compare the two.

For a concise summary of what changed in each profile and how to test it, see
[Profile change log](PROFILE-CHANGELOG.md).

## Import a profile

1. Open SimHub and navigate to **ShakeIt Bass Shakers**.
2. Use its profile import command.
3. Browse to the HapticFlight Bridge `Profiles` folder and select the file for
   your aircraft.
4. Review the imported output devices and channels. Profile import cannot know
   your physical wiring.
5. Save the SimHub profile, then test while stationary with conservative gains.

## Gain and safety

Start low. Configure the physical amplifier and the ShakeIt master/output gains
before raising individual effect levels. Stop if the transducer bottoms out,
clips, overheats, or produces a harsh mechanical sound. Recording analysis can
help identify overlapping effects, but it is not a measurement of seat output.

## Tune a profile with recorded evidence

HapticFlight Bridge supplies telemetry and a read-only recording analysis; it
does not render, test, or edit ShakeIt effects. Tune in small, reversible
steps:

1. Save a clearly named copy of the working SimHub profile.
2. Fly a repeatable baseline scenario and record it in HapticFlight Bridge.
3. Select the suspect event in **Flights**, then use **Copy A/B worksheet**.
4. In SimHub, alter one parameter in the matching effect and test it with
   SimHub's own preview/test control or the same DCS scenario.
5. Repeat the scenario, record it, and compare the runs in **Debrief**.
6. Keep the edit only when both the recorded evidence and the physical result
   improve; otherwise restore the named baseline.

Avoid changing a global gain, effect gain, frequency, and envelope together.
If a cue remains harsh or muddy, first check the analyzer for overlapping
modelled events, then check SimHub output routing and physical amplifier headroom.

## Automatic selection by DCS airframe

There is no supported SimHub setting that automatically switches a ShakeIt Bass
Shakers profile by the DCS aircraft currently loaded. SimHub’s profile selection
is game-oriented; DCS World is a single game for its modules.

Use this safe workflow instead:

1. Before launching a mission, load the profile for the aircraft you plan to
   fly.
2. Confirm no other aircraft profile is active on the same outputs.
3. Start DCS and verify `DCSExtra.Connected` in SimHub once in the cockpit.
4. If you change to a different airframe, select that aircraft’s profile in
   SimHub before the next flight.

HapticFlight Bridge detects the airframe for telemetry and recording analysis,
but this beta does not automatically edit SimHub profile state. A future
airframe-switch feature must be implemented through a supported SimHub API or
explicit user-approved automation; it is not enabled by a hidden setting.
