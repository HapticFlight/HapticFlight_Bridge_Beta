# HapticFlight Bridge public beta

This package is a complete, local Windows installation for DCS World and
SimHub beta testers. Extract the entire archive to a writable folder, then run:

```text
Tools\DcsExtraTelemetry.Launcher.exe
```

Do not run the launcher from inside the ZIP or from `Program Files`.

## Before installation

- Install SimHub and the .NET Framework 4.8 desktop runtime.
- Close DCS and SimHub before installing, repairing, or removing the bridge.
- Begin with amplifier and SimHub gains low.
- This beta is not code-signed. Windows may ask for confirmation before the
  launcher or plugin runs.

## Install the bridge

1. Start `Tools\DcsExtraTelemetry.Launcher.exe`.
2. On **Home**, choose the DCS Saved Games profile you use (for example,
   `DCS` or `DCS.openbeta`).
3. Confirm the SimHub folder the launcher detected, or choose it manually.
4. Choose **Install telemetry** and **Install SimHub plugin**.
5. Restart DCS and SimHub. Start a mission and confirm the launcher reports
   telemetry as connected.

The installer writes only this project’s isolated DCS Hook and SimHub plugin.
It does not modify `Export.lua`. Use the launcher’s uninstall action to remove
only the files it installed.

## Install a SimHub profile

1. In SimHub, open **ShakeIt Bass Shakers**.
2. Use the profile import command and browse to this package’s `Profiles`
   folder.
3. Import the profile matching the DCS aircraft you will fly.
4. Disable overlapping older profiles or other profiles that use the same
   shaker output.
5. Verify every configured output channel is correct and start at a low gain.

See `Profiles\README.md` and `Docs\SIMHUB-PROFILES.md` for the profile map,
safe gain guidance, and troubleshooting.

## Aircraft-based profile selection

SimHub can select profiles by **game**, but DCS World is one game regardless
of which airframe is loaded. The public SimHub profile format does not provide
a supported built-in switch that automatically changes a ShakeIt profile when
DCS reports a different aircraft.

For this beta, select the matching aircraft profile manually before flying.
The launcher detects the active airframe for telemetry and recording analysis,
but it does not silently edit SimHub’s active ShakeIt profile. This avoids
unexpected output changes and prevents two profiles from being active at once.

## Verify the download

The accompanying `.sha256` file gives the SHA-256 checksum of the ZIP. After
extracting, run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File .\Scripts\Verify-Package.ps1
```

It must report `PASS` before installation.

## Updates

The launcher checks the public GitHub Releases page configured in this package.
No GitHub sign-in is required. When an update is offered, download it in the
launcher, allow verification to complete, then restart the launcher.

For maintainers, every GitHub Release must include both:

- `DcsExtraTelemetry-<version>.zip`
- `DcsExtraTelemetry-<version>.zip.sha256`

The version must match the release tag, for example `v0.1.0-beta.1`.
