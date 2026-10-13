# Apache V2 test.5 — gun balance

**[Download refreshed Apache profile](DCS-AH-64D-Apache-Haptic-Engine-v2.0.0-test.5.siprofile?raw=true)**

**[Download profile and full assessment ZIP](Apache-Haptic-Engine-v2.0.0-test.5.zip?raw=true)**

Import and manually select **DCS AH-64D Apache - Haptic Engine v2.0.0-test.5 Gun Balance**. Disable the previous Apache profile. Keep your V2 plugin and existing output routing. The stronger test.4 rotor baseline remains intact; all 38 effects remain enabled.

## Findings

The September 7 recording contains 6,885 packets over 251.435 simulation seconds, all for AH-64D_BLK_II. All 178 observed leaf fields were inventoried. There are no duplicate/reversed sequence entries or reversed model-time steps; four adjacent timestamps are equal, and one simulation-time gap is 0.953 seconds. This is insufficient to determine the cause of that gap.

The cannon count falls from 300 to 247: 53 rounds. Payload store count stays at 48. Firing is classified HEAVY_GUN, but the weapon name is **Cannon**, not M230. There are 170 samples with positive heavy-gun intensity and that cannon name. During them, intensity reaches 1.0, which is a valid saturation of the normalized cue, not proof of audio clipping.

The old profile uses 25 Hz for the chain-gun layer because the name-based frequency lookup fails to recognize Cannon. Its gain-weighted output peaks at 15.67 during firing. The separate M230 low-body layer never activates because of the same naming mismatch, so double gun-layer stacking was not the cause in this recording. The strong main gun layer sits on top of sustained rotor output and sometimes a decaying maneuver layer. These findings support a profile-level explanation for the harsh firing sensation, but telemetry does not measure actual sound-card or amplifier clipping.

## Changes

| Layer | test.4 | test.5 |
|---|---|---|
| M230 primary gain | 24 | 12 |
| Primary formula cap | 70 | 50 |
| Primary carrier for recorded Cannon name | 25 Hz fallback | 625 / 60 = 10.4167 Hz |
| Primary maximum after effect gain | 16.8 | 6.0 |
| Low-body name matching | M230 only | Cannon or M230 |
| Low-body gain / formula cap | 10 / 48 | 4 / 18 |
| Low-body carrier | 12.5 Hz fallback | 5.2083 Hz, half-rate tactile layer |
| Low-body maximum after effect gain | 4.8 if recognized | 0.72 |

The rate is the existing project M230 nominal setting. It is not calculated from the broken nominal-rate telemetry or treated as an exact live shot-timestamp stream. The low-body layer is deliberately small; its half-rate carrier is a tactile design choice.

Only those two gun effect objects changed. Rotor, landing, maneuver, air data, ground, lifecycle and countermeasure formulas/gains are unchanged. No new master gain is applied, so the recently strengthened rotor baseline is not globally reduced.

## Broader assessment

- **Rotor:** continuous and valid as an engine-RPM proxy, 85.00–99.01%; retains test.4 presence. The three rotor layers peak at 18.00, 12.48 and 22.32 after effect gain. Actual rotor Nr remains unverified.
- **Flight motion:** normal G spans 0.892–1.562; during cannon firing its maximum is only 1.116. That does not suggest a severe recorded recoil-driven G spike. Maneuver effect maximum is approximately 12.35 after gain, with residual smoothing during some firing windows.
- **Ground/landing:** struts and contact transitions are present; suspension and wheel-contact detail can produce distinct transients. No blanket reduction was justified by the reported gun problem. The maximum estimated whole-profile overlap occurs outside a gun-positive sample. See the replay caveat below before interpreting event levels.
- **Braking, rockets, missiles and countermeasures:** corresponding commands/events stay zero in this capture. Their silence here is not evidence of a broken effect; this recording cannot validate their active behavior.
- **Unavailable channels:** engine lifecycle, engine torque, torque gauge, rotor warning, doors, crew guns and wheel rotation flags are false throughout. APU/start effects and tailwheel caster remain silent. Wheel-shimmy is a small derived cue, not direct measured wheel rotation.
- **NominalWeaponRateHz:** peaks at 1,788,811,658.4115, consistent with the previously identified clock-valued unknown-name fallback defect. Do not use it as a cyclic rate. Neither refreshed gun carrier depends on it. Sample-derived WeaponRateHz also fluctuates substantially and is not a reliable instantaneous carrier in this short recording.
- **angleOfAttackRad:** spans -179.99 to +180.00 despite its radians label. Treat the unit/interpretation as suspect. The Apache profile does not consume this field; no speculative conversion was added.
- **Ground/air motion:** true airspeed reaches 46.03 m/s; vertical velocity spans -14.87 to +10.16 m/s. High-speed texture did not activate under the supplied profile's conditions. That alone does not justify lowering its threshold.

## Replay and testing

Primary gun output during firing: median **12.21 → 4.36**, maximum **15.67 → 5.60** after effect gain. Newly recognized low-body maximum is **0.68**. The sum of absolute gain-weighted effect amplitudes during firing falls from a maximum of **76.11 to 66.81**. This sum is an overlap diagnostic, not the actual audio waveform, a decibel measure, or proof of a particular clipping threshold. Hardware gain, frequency response, channel mixing, phase and physical installation are not recorded.

The assessment evaluates all 38 profile formulas at recorded packet cadence using reconstructed common V2 conditioning. The recording contains raw telemetry rather than the live plugin's complete Haptic snapshot. In particular, the aircraft-specific V2 touchdown envelope is approximated from the recorded legacy envelope, and contact/mechanical conditioning is not a full engine/audio replay. Landing estimates are therefore provisional and were not used to justify landing changes. The gun signal pass-through and weapon name/frequency mismatch are directly supported by the captured fields and inspected formulas.

Tests verify unchanged non-gun effects, recognition of both Cannon and M230, fixed frequencies independent of erroneous telemetry, output ceilings, and disconnect/missing-engine silence. All 38 effects executed in installed SimHub Jint 4.2.2.0 with simulated properties. Physical clipping or tactile adequacy still requires a live seat check.

**[Full 178-field inventory and 38-effect before/after level tables](Full-Assessment.md)**

Source: `DcsExtraTelemetry-20260907-150430.jsonl`. The raw recording is not uploaded.
