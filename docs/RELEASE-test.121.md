# DCS Extra Telemetry 0.1.0-test.121 — Engine 1.1.0 ground effects

Download `DcsExtraTelemetry-0.1.0-test.121.zip` for the complete package: DCS
Hook, SimHub plugin, installation scripts, documentation, profiles, and the
standard launcher and monitor applications under `Tools`. The former root-level
self-extracting EXE is not included.

Ground bumps now have a dedicated compression-based event while the aircraft
remains supported. Touchdown preserves pre-impact sink evidence through delayed
contact confirmation and can rearm for a real bounce. Shaped contact and settling
cues, time-based roughness/rocking, quieter wheel rolling, and forward/lateral
skid sliding make ground events more distinct.

The complete package includes Haptic Engine 1.1.0 and six matching test.6 profiles. All
non-ground profile groups retain their latest published tuning, including the
AH-6J rotor refresh and Apache test.5 gun balance. The V2 engine source is now
included on main, alongside the profiles that depend on it.

Extract the ZIP and run `Tools\\DcsExtraTelemetry.Launcher.exe`. It installs the
project-owned DCS Hook and SimHub plugin while leaving `Export.lua` untouched.
Import one matching test.6 profile and disable overlapping profiles. Existing
device routing and prior rollback profiles are preserved.

Validation includes Release builds, engine/transport regressions, 30/60/120 Hz
bump consistency, six-profile property and preservation checks, and 210 formulas
executed in installed SimHub Jint. Ground calibration remains provisional and
requires live aircraft recordings and shaker evaluation. UH-1H binary skid
telemetry does not enable invented suspension bumps; Apache bumps require actual
travel rather than its binary weight-on-wheels fallback.

Microsoft Defender definition `1.459.273.0` scanned the replacement ZIP and its
extracted contents with no threats found on September 18, 2026. ZIP SHA-256:
`69F8EBBD20A6626F9ABAD958095C6EFE7A3FFD5921F08A660FF0D661550DA97B`.

Full details: [ground-effect implementation and validation](https://github.com/coledesigns/dcs-simhub-telemetry-export/blob/main/docs/GROUND_EFFECTS.md).
