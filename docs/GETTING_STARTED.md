# Build and Test Walkthrough

This walkthrough covers local development builds and direct verification. Current
test packages also include aircraft-aware, effect-ready telemetry for rotor,
ground-contact, touchdown, weapon, countermeasure, and cockpit cues. Capability
gates remain authoritative: a property is only available when the active aircraft
and DCS interface can supply it.

The commands launch project scripts with a one-process execution-policy bypass because Windows commonly blocks unsigned local scripts. This does not change the computer's permanent PowerShell policy.

## What the two pieces do

1. `DCSExtraTelemetry.lua` runs as an isolated DCS GameGUI Hook and sends small JSON packets to `127.0.0.1:29381`, with a recorder mirror on `127.0.0.1:29382` by default.
2. `DcsExtraTelemetry.Plugin.dll` listens on the primary port and publishes properties to SimHub. The launcher recorder listens on the mirror, so both can run simultaneously.

There is no line to add to `Export.lua`. The installer never opens or changes it.

Choose the SimHub port under **Flights > Telemetry connection** in the launcher. The launcher checks it for a local collision and automatically assigns a different available recorder port. Expand **Advanced recorder port** only when a specific recorder port is required. The validated pair is saved to `%LOCALAPPDATA%\DcsExtraTelemetry\ports.ini`. Restart DCS and SimHub after saving so the Hook, plugin, monitor, and recorder all switch together. Manual files may specify either key independently; a missing key retains its default. Invalid lines or a port collision are reported in the DCS log and safely fall back where necessary.

## Step 1: build the solution

Open PowerShell in the repository folder and run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build.ps1
```

The script locates SimHub, rebuilds the solution for .NET Framework 4.8, and prints the plugin DLL path. To build against a nonstandard SimHub location:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build.ps1 -SimHubPath 'D:\Apps\SimHub'
```

## Step 2: run the transport smoke test

```powershell
.\tests\DcsExtraTelemetry.TransportSmokeTest\bin\Debug\DcsExtraTelemetry.TransportSmokeTest.exe
```

Expected result:

```text
PASS: UDP transport, validation, sequence tracking, and stale detection.
```

This test does not need SimHub or DCS to be running. It proves that the receiver accepts and parses every generic packet section, rejects invalid protocol data, counts dropped packets, resets sequence tracking for a new DCS session, and disconnects stale telemetry.

## Step 3: install the SimHub plugin

Close SimHub. Open PowerShell as Administrator if SimHub is under `Program Files`, then run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Install-SimHubPlugin.ps1
```

The script copies only these project-owned files into the detected SimHub folder:

- `DcsExtraTelemetry.Plugin.dll`
- `DcsExtraTelemetry.Plugin.pdb`

Start SimHub. If it asks whether to enable a new third-party plugin, enable **DCS Extra Telemetry**, then restart SimHub if requested.

## Step 4: test SimHub without DCS

Keep SimHub open and run this in a second PowerShell window:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Send-TestTelemetry.ps1 -Seconds 15
```

In SimHub's available-properties picker, search for `DCSExtra` and press Enter to apply the filter. A useful verification subset is:

| Property | Expected test value |
|---|---|
| `DCSExtra.Connected` | `true` while the sender runs |
| `DCSExtra.AircraftType` | `TEST_AIRCRAFT` |
| `DCSExtra.PacketRateHz` | approximately `50` to `60` from the PowerShell test sender |
| `DCSExtra.DataAgeMs` | a small changing number |
| `DCSExtra.AccelXG` | changing value near `-0.1` to `0.1` |
| `DCSExtra.AccelYG` | changing value near `0.95` to `1.05` |
| `DCSExtra.AccelZG` | changing value near `-0.1` to `0.1` |
| `DCSExtra.InvalidPackets` | `0` |
| `DCSExtra.DroppedPackets` | normally `0` |
| `DCSExtra.EngineRpmLeftPct` | `82` |
| `DCSExtra.GearPosition` | `1` |
| `DCSExtra.Has.EngineRpm` | `true` |

About one second after the sender stops, `DCSExtra.Connected` should become `false`.

See [PROPERTIES.md](PROPERTIES.md) for the complete property and capability list.

## Step 5: install the isolated DCS Hook

Close DCS, then run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Install-DcsHook.ps1
```

The script uses the Windows Saved Games known-folder location and installs into every detected `DCS` or `DCS.*` profile. For a custom profile location:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Install-DcsHook.ps1 -DcsProfilePath 'D:\My DCS Profile'
```

It writes only:

```text
<DCS profile>\Scripts\Hooks\DCSExtraTelemetry.lua
```

Before and after installation, it hashes every unrelated file under `Scripts` and fails if any unrelated file content changes.

## Step 6: test with DCS

1. Start SimHub.
2. Start DCS and enter an aircraft in a mission.
3. Search SimHub properties for `DCSExtra`.
4. Confirm `Connected` is true and `AircraftType` matches the DCS module name.
5. Move or fly the aircraft and watch the three acceleration values.

If packets do not arrive, check `Saved Games\DCS*\Logs\dcs.log` for `DCSExtraTelemetry` and SimHub's log for `DCS Extra Telemetry`.

## Next steps

For normal test-package installation, use the graphical launcher included in the
GitHub Release instead of these development commands. For the full property
contract, supported-aircraft status, and ShakeIt guidance, see
[PROPERTIES.md](PROPERTIES.md),
[AIRCRAFT_COMPATIBILITY.md](AIRCRAFT_COMPATIBILITY.md), and
[REALISM_EFFECTS.md](REALISM_EFFECTS.md).
