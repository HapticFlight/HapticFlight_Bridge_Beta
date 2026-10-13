# UH-60 profile KPI benchmarks

This benchmark compares every candidate against a preserved profile baseline
using the same telemetry recording. Its purpose is to prevent a profile from
appearing smoother merely because it became weaker, or stronger because it
restored unwanted noise.

## Decision framework

The framework has three primary KPI groups:

1. **Smoothness** measures ordinary and worst-case frame-to-frame amplitude
   movement, including the combined movement of the three rotor harmonics.
2. **Proportional response** compares strong dives and climbs with calm flight.
   Aggressive flight should remain distinctly stronger than cruise.
3. **Hot-ground and calm-flight rotor presence** measures every sustained
   harmonic separately as well as their combined output. Several individually
   imperceptible layers no longer pass merely because their values add up.

Strength retention, turbulence activity, formula reliability, property
coverage, and large transients are guardrails. The transient guardrail permits
a small explicit event budget above baseline so a deliberately restored
touchdown/contact pulse is not treated like continuous scratchiness. A
candidate with a smoother score still fails if it becomes materially weaker or
noisier elsewhere.

## Current baseline and targets

- Baseline: `DCSExtra Refreshed v1.8.2`
- Current candidate: `DCSExtra Refreshed v1.8.8`
- Standard recording: `DcsExtraTelemetry-20260729-211153.jsonl`
- Target definitions: `benchmarks/uh60/profile-kpi-goals.json`

The numerical targets are provisional engineering thresholds. They should be
recalibrated after several profiles have both replay results and structured
seat-test scores.

## Run the benchmark

```powershell
node .\scripts\Benchmark-Uh60Profiles.js `
  --log "C:\Users\Nicho\Downloads\DcsExtraTelemetry-20260729-211153.jsonl" `
  --baseline ".\profiles\uh60\DCS World - DCS UH60L - DCSExtra Refreshed v1.8.2.siprofile" `
  --candidate ".\profiles\uh60\DCS World - DCS UH60L - DCSExtra Refreshed v1.8.8.siprofile" `
  --output ".\analysis\profile-benchmarks\v1.8.8-vs-v1.8.2"
```

The command writes:

- `benchmark.md` for quick review;
- `benchmark.csv` for spreadsheet analysis;
- `benchmark.json` for automation;
- complete baseline and candidate replay results for auditability.

The command returns exit code `3` when any target has a gap. This makes the
benchmark suitable for a future release check without claiming that offline
testing replaces an in-seat evaluation.

## Seat-test companion

For each candidate, record 1-5 ratings for:

- hot-ground rotor presence;
- cruise smoothness;
- climb proportionality;
- bank/roll proportionality;
- dive quality;
- turbulence scratchiness;
- unwanted high-frequency hum;
- overall preference versus baseline.

Keep the shaker hardware, SimHub gain, profile global gain, mounting, and test
maneuvers unchanged. Once several rated flights exist, correlate those ratings
with the replay KPIs and tighten the targets around measurements that best
predict actual seat feel.
