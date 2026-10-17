# HapticFlight Bridge — Public Beta

HapticFlight Bridge is a local DCS World telemetry bridge for SimHub. It gives
ShakeIt Bass Shakers and compatible tactile-feedback tools aircraft-aware
telemetry properties and ready-to-import helicopter profiles.

```text
DCS GameGUI Hook -> local UDP -> SimHub plugin -> ShakeIt / bass shaker
```

Everything runs locally. The bridge does not modify `Export.lua`, open an
internet listener, or send flight telemetry to a server.

## Start here

1. Download `HapticFlight-Bridge-Setup-<version>.exe` from [Releases](../../releases).
2. Close DCS and SimHub, then run the single Setup EXE; no ZIP extraction is needed.
3. Choose your DCS Saved Games profile and SimHub folder.
4. Finish Setup, then launch HapticFlight Bridge from its final page or the Start menu.
5. Restart DCS and SimHub and import the profile for the aircraft you fly.

The test.136 Setup beta is unsigned. Windows App Control may block it on
protected PCs; do not turn off security protections to force it to run.

HapticFlight Bridge can be closed while flying. DCS sends telemetry directly to
the plugin loaded by SimHub; the application is only needed for setup, recording,
updates, and diagnostics.

The detailed guide is in [Installation](docs/INSTALLATION.md). Read
[SimHub profiles](docs/SIMHUB-PROFILES.md) before enabling any bass-shaker
profile.

## Included aircraft profiles

The installed application's `Profiles` folder contains the current beta profiles for:

- UH-60L / UH-60L DAP
- AH-64D
- AH-6J and MH-6J
- OH-58D
- UH-1H
- CH-47F

Each profile is deliberately independent. Import and enable only the profile
that matches the aircraft you are flying; do not stack multiple aircraft
profiles at the same time.

## Updates

The installed application checks this repository's public releases for newer
Setup versions and verifies the published checksum before opening Setup. Existing
ZIP installations are legacy; to switch to Setup, download it manually from
Releases. Beta testers do not need a GitHub account to receive updates.

See [Updating](docs/UPDATES.md) for the release format and recovery steps.
Maintainers should follow [Publishing](docs/PUBLISHING.md) to create and verify
each beta release.

## Beta feedback

Please report problems through [Issues](../../issues) with the launcher
version, DCS module, DCS version, SimHub version, and a short description of
what occurred. Include a recording only if you are comfortable sharing it and
remove anything sensitive first. The feedback checklist is in
[Beta testing](docs/BETA-TESTING.md).

## Trademark notice

HapticFlight Bridge is an independent community project. DCS World and Digital
Combat Simulator are trademarks of Eagle Dynamics SA. SimHub is a trademark of
its respective owner. This project is not affiliated with, endorsed by, or
sponsored by either party.
