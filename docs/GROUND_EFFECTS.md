# Ground effects update: engine 1.1.0 / profiles test.6

This is a development test release, not a live-flight or hardware calibration.
The engine source is now included on main alongside the V2 profiles it serves.
The raw Hook protocol and legacy properties remain compatible. Export.lua is
not used or modified.

## What changes

- Touchdown retains the V2 aircraft contact policies and attack/hold/release
  envelope. Pre-contact sink history is captured at first contact, so delayed
  confirmation cannot erase it. Real airborne bounces can rearm after the
  aircraft's bounce interval without waiting for the previous impact cooldown.
- Valid compression rate can reinforce a confirmed touchdown by up to 20% of
  remaining headroom. Animation travel is not physical force or calibrated G.
- Ground.Bump detects positive compression transients during established ground
  support, without requiring another contact transition. Contact changes block
  bump classification for 250 ms. Extension is rebound, not another impact.
  Weapon-active samples do not trigger bumps. A noise-adaptive threshold and
  180 ms retrigger floor restrain repeated events.
- Ground.Settling adds a quiet finite tail after touchdown or bump. Actual
  travel may reinforce it; weight-on-skids-only aircraft use a labelled proxy.
- Wheel/skid contact envelopes preserve relative loading rather than returning
  the same fixed pulse for every contact. Per-corner signals remain available
  for user routing; supplied profiles retain centered mono output and do not
  overwrite device/channel settings.
- Ground.Roll is a quiet speed-based wheel texture, separate from suspension
  travel. Ground.Suspension and pitch/roll rocking use elapsed-time-normalized
  rates with time-based smoothing. Roughness is speed gated, and impact output
  reduces competing texture. No surface material is guessed.
- Skid scrub uses both longitudinal and lateral body-axis motion. Missing body
  motion fails closed. Wheel rolling is not enabled for skid aircraft.

## Coverage and confidence

| Aircraft/source | Landing and settling | Bumps/roughness/contact loading |
| --- | --- | --- |
| UH-60L/DAP | Main-wheel confirmation; single-main persistence; tail detail | When valid individual strut travel exists |
| AH-64D | Main-wheel confirmation; tail detail | Only with actual strut travel; binary WoW fallback cannot supply bumps |
| AH-6J/MH-6J | Bilateral/multiple-skid confirmation; separate partial contacts | Four-point skid travel |
| OH-58D | Same four-point policy | Provisional source and tuning |
| UH-1H | Weight-on-skids landing; quiet settling proxy | No invented independent skid travel; bump/roughness layers omitted |
| Unknown | Existing capability-gated generic landing | No invented wheel/skid interpretation |

New compression/contact outputs are Provisional. Rolling and skid sliding are
Proxy cues. Ground.Resonance retains its legacy property name for compatibility;
the profile labels it gear-borne rotor vibration, not true ground resonance.
Sink-rate severity thresholds remain conservative existing model defaults;
aircraft-specific gains and contact policies differ. Further threshold tuning
requires module recordings and the user's physical shaker evaluation.

## New properties

Under `DcsExtraTelemetryPlugin.DCSExtra`:

- `Haptic.Ground.Bump.{Active,Intensity,Envelope,Phase,EventId,Confidence,Source}`
- `Haptic.Ground.Settling.{Active,Intensity,Envelope,Phase,EventId,Confidence,Source}`
- `Has.HapticGroundBump`, `Has.HapticGroundSettling`

The engine reports `Haptic.EngineVersion = 1.1.0`. New ground formulas require
at least 1.1.0; installing profiles alone on an older engine leaves their ground
group silent. Capability absence returns unsupported/zero. Engine resets clear
event and filter history across aircraft, session, capability/source changes,
stale reconnects and timing discontinuities. Event IDs are local to that history.

## Install and rollback

1. Keep the prior plugin/package and profiles as rollback copies.
2. Download and extract `DcsExtraTelemetry-0.1.0-test.121.zip` from test.121.
   Run `Tools\\DcsExtraTelemetry.Launcher.exe`; it installs the project-owned
   DCS Hook and SimHub plugin without changing `Export.lua`. The former
   unsigned root-level self-extracting EXE is not included.
3. Import the test.6 profile for your aircraft from the bundle's Profiles folder
   or downloads/ground-effects-v2. Select it manually and disable overlapping
   older profiles. Existing output routing is preserved.
4. Verify engine version 1.1.0 and connection before evaluating ground effects.
5. To roll back, reinstall the prior plugin and select its matching prior profile.

The six profiles preserve every non-ground group from the latest published
baseline: AH-6J test.4, Apache test.5, and the other four test.3 profiles. Thus
the published rotor and Apache gun changes are retained. Distinct profile IDs
avoid overwriting prior imports. Package generation does not install anything.

## Verification and live checklist

Automated tests cover ground spawn, gentle/firm landing, partial contacts,
bounces, delayed confirmation, a bump with continuous contact, extension,
airborne suppression, missing/non-finite travel, forward skid sliding, and
30/60/120 Hz replay consistency. Profile tests execute formulas, check the
registered property contract, and compare every non-ground group to its source.

Live acceptance still requires gentle/firm landings, one-side set-downs,
tailwheel settle, bounced landings, smooth and rough taxi at matched speeds,
braking, stationary rotor operation, and skid sliding. Compare event timings
and summed tactile output; avoid interpreting synthetic replay as measured
physical realism. Frequencies/gains are starting points for hardware tuning.
