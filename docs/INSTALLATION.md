# Installation guide

## Requirements

- Windows 10 or Windows 11
- DCS World with a writable Saved Games profile
- SimHub with ShakeIt Bass Shakers configured for your haptic hardware
- .NET Framework 4.8 Desktop Runtime

## Install HapticFlight Bridge

Download the single Setup EXE from **Releases**. It does not need to be
extracted. The older ZIP package remains a legacy beta format.

1. Download `HapticFlight-Bridge-Setup-<version>.exe` from this repository's
   **Releases** page and compare its SHA-256 with the release checksum.
2. Close DCS and SimHub, then run Setup.
3. Choose the DCS Saved Games profile you actually launch, such as
   `C:\Users\<you>\Saved Games\DCS` or `DCS.openbeta`.
4. Confirm the SimHub folder and complete Setup. Windows may ask for
   administrator approval to install the plugin in SimHub.
5. Launch HapticFlight Bridge from Setup or the Start menu.
6. Restart SimHub and DCS. Enter a mission and confirm the application shows
   connected telemetry.

You can close HapticFlight Bridge while flying. The installed DCS hook sends
directly to the plugin running inside SimHub. Keep both DCS and SimHub running
for live telemetry and tactile output. The application must stay open only when
using its flight recorder.

The installer uses an isolated GameGUI Hook. It never modifies `Export.lua`.

## Update or remove

Use **Check for updates** in the installed application. Setup installations
receive verified Setup downloads. Existing ZIP users must download Setup
manually to move to the installer format.
Public beta updates require no GitHub sign-in. To remove
the bridge, close DCS and SimHub and choose **Uninstall HapticFlight Bridge**
on the main dashboard (or use Windows **Installed apps**) if Setup was used.
For a ZIP installation, use the launcher's component removal controls. The
uninstaller removes the recorded integration files and preserves recordings.

## If Windows blocks Setup

This beta Setup is unsigned. Windows App Control may block it on protected PCs.
Do not disable security protections to force it to run. Confirm the download
source and checksum; a signed release is needed for broader compatibility.

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
