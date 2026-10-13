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
| AH-6J | `DCS-AH-6J-Haptic-Engine-v2.0.0-test.19-Realism.siprofile` |
| MH-6J | `DCS-MH-6J-Haptic-Engine-v2.0.0-test.19-Realism.siprofile` |
| OH-58D | `DCS-OH-58D-Haptic-Engine-v2.0.0-test.19-Realism.siprofile` |
| UH-1H | `DCS-UH-1H-Haptic-Engine-v2.0.0-test.19-Realism.siprofile` |
| CH-47F | `DCS-CH-47F-Haptic-Engine-v2.0.0-test.2.siprofile` |

The UH-60L `v1.9.0` profile is an alternative tune, not an additional layer.
Use it instead of the UH-60L test.19 profile only when you intentionally want
to compare the two.

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
