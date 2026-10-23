# HapticFlight Bridge 0.1.0-test.139

## Installation instructions

1. Download `HapticFlight-Bridge-Setup-0.1.0-test.139.exe` below. No ZIP
   extraction is required.
2. Run Setup to install HapticFlight Bridge, then launch it from the final
   Setup page or the Start menu.
3. In HapticFlight Bridge, select your DCS Saved Games profile and SimHub folder.
4. Close DCS and SimHub. Install the DCS Hook and SimHub plugin using their
   separate controls, or choose **Install everything**.
5. Restart SimHub and DCS, then import and enable the matching aircraft profile
   in SimHub ShakeIt Bass Shakers.

The application can be closed while flying; DCS sends telemetry to the plugin
running in SimHub. See the repository's
[installation guide](https://github.com/HapticFlight/HapticFlight_Bridge_Beta/blob/main/docs/INSTALLATION.md)
and
[SimHub profile guide](https://github.com/HapticFlight/HapticFlight_Bridge_Beta/blob/main/docs/SIMHUB-PROFILES.md)
for requirements, troubleshooting, and profile details.

## Change log

- Rebuilt Setup with a pinned, tested Inno Setup compiler after Microsoft
  Defender quarantined the prior test.138 installer on the release PC.
- Added Defender scans of the staged payload and final installer to the build
  and release gates. The build fails if either scan reports a threat.
- Updated the app, installer, plugin, and monitor product version to
  `0.1.0-test.139` (numeric Windows file version `0.1.0.139`).
- No flight-effect tuning changes are included.

## Beta notice

This beta is unsigned. Windows App Control may block it on protected PCs; do
not disable security protections to force it to run. A clean scan on the build
PC does not guarantee every antivirus or protected PC will accept the download.
