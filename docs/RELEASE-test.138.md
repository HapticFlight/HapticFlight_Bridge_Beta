# HapticFlight Bridge 0.1.0-test.138 — one clearly versioned installer

Download `HapticFlight-Bridge-Setup-0.1.0-test.138.exe` and run it directly.
Setup installs only HapticFlight Bridge and offers to launch it. Inside the app,
install or uninstall the DCS Hook and SimHub plugin independently. After those
components are installed, the app may be closed while DCS and SimHub exchange
telemetry.

## Which version is this?

- **App and installer release:** `0.1.0-test.138`. This is the one version to
  use when checking for updates or reporting a problem.
- **Windows file properties:** Setup, the app, plugin, and bundled monitor report
  product version `0.1.0-test.138` and numeric file version `0.1.0.138`.
  The stable assembly compatibility version `0.1.0.0` is not a release number.
- **Built-in haptic processing engine:** `1.7.1`. This is a component version,
  not a separate installer.
- **SimHub aircraft profiles:** most included realism profiles are
  `2.0.0-test.19`; the CH-47F profile is `2.0.0-test.2`; an optional UH-60L
  alternative is `1.9.0`. These are profile revisions, not app versions.

This update corrects stale Windows file properties that incorrectly showed
`test.132` in the prior installer. It does not claim new flight-effect tuning.

This beta is unsigned. Windows App Control may block it on protected PCs; do
not disable security protections to force it to run. Live DCS/SimHub hardware
output and protected-PC installation remain unverified.
