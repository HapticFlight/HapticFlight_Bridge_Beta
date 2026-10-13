# Installation guide

## Requirements

- Windows 10 or Windows 11
- DCS World with a writable Saved Games profile
- SimHub with ShakeIt Bass Shakers configured for your haptic hardware
- .NET Framework 4.8 Desktop Runtime

## Install HapticFlight Bridge

1. Download the latest release ZIP from this repository’s **Releases** page.
2. Optional but recommended: verify the download with the included
   `Scripts\Verify-Package.ps1` script.
3. Extract the ZIP to a writable folder such as `C:\HapticFlightBridge`. Do
   not extract it to `Program Files` and do not run it from inside the ZIP.
4. Close DCS and SimHub.
5. Run `Tools\DcsExtraTelemetry.Launcher.exe`.
6. Choose the DCS Saved Games profile you actually launch, such as
   `C:\Users\<you>\Saved Games\DCS` or `DCS.openbeta`.
7. Confirm the SimHub folder, then install the DCS telemetry hook and SimHub
   plugin from **Home**.
8. Restart SimHub and DCS. Enter a mission and confirm the launcher shows a
   connected telemetry state.

The installer uses an isolated GameGUI Hook. It never modifies `Export.lua`.

## Update or remove

Use **Check for updates** in the launcher. Public beta updates require no
GitHub sign-in. To remove the bridge, close DCS and SimHub, then use the
launcher’s uninstall action. It removes only files owned by this project and
reports unrelated file changes instead of overwriting them.

## If Windows blocks the launcher

The beta package is not code-signed. Confirm you downloaded it from this
repository’s Releases page, validate the SHA-256 file, then use Windows’
**More info** option only if you trust the release source.

## Troubleshooting

- **No telemetry:** confirm the selected Saved Games profile is the one DCS is
  using, then restart DCS after installation.
- **Plugin not detected:** close SimHub before installation, confirm its folder,
  install again, and restart SimHub.
- **No tactile output:** the bridge only exposes properties. Import and enable
  the matching ShakeIt profile, check its output assignment, and verify the
  amplifier is on.
- **Unexpected output:** remove or disable overlapping ShakeIt profiles before
  testing the included profile.
