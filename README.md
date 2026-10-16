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

The currently published beta is a ZIP. Download it from [Releases](../../releases),
extract it to a writable folder, and run `Tools\DcsExtraTelemetry.Launcher.exe`.
Choose your DCS Saved Games profile and SimHub folder, install both integration
components, then restart DCS and SimHub. Import the profile for the aircraft
you fly. A one-file Setup installer is in development and is not yet a public
release asset.

HapticFlight Bridge can be closed while flying. DCS sends telemetry directly to
the plugin loaded by SimHub; the application is only needed for setup, recording,
updates, and diagnostics.

The detailed guide is in [Installation](docs/INSTALLATION.md). Read
[SimHub profiles](docs/SIMHUB-PROFILES.md) before enabling any bass-shaker
profile.

## Included aircraft profiles

The package's `Profiles` folder contains the current beta profiles for:

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

The application checks this repository's public releases. Existing ZIP installs
continue to use verified ZIP updates; future Setup installs will use verified
Setup updates once a signed installer is published. Beta testers do not need a
GitHub account to receive updates.

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
