# Portable DCS Computer Test

This release is a normal ZIP. Import one V2 test.19 profile from `Profiles`;
select the aircraft profile manually and disable overlapping older profiles.
Earlier test.7 through test.18 revisions are retained under
`Profiles\History`. See `Docs/GROUND_EFFECTS.md` for changes, source limits,
validation, and rollback to working test.121.

This bundle does not require Codex, Git, Visual Studio, the .NET SDK, or an internet connection. It contains a graphical Windows setup launcher, compiled .NET Framework 4.8 monitor and SimHub plugin, plus one isolated DCS GameGUI Hook.

The workflow deliberately separates DCS testing from SimHub installation.

The launcher's **Telemetry flight recorder** captures validated packets to `Documents\DCS Extra Telemetry\Recordings`. SimHub may remain open while you start recording, fly the test sequence in DCS, and stop recording. The resulting JSON Lines file can be used to tune effects without repeatedly guessing from screenshots.

## Safety properties

- No script reads, writes, chains, repairs, or depends on `Export.lua`.
- The only DCS file installed is `Scripts\Hooks\DCSExtraTelemetry.lua`.
- The only SimHub files installed are `DcsExtraTelemetry.Plugin.dll` and its optional PDB.
- Installers hash all unrelated files in their scope before and after the operation.
- A baseline captures every unrelated DCS Scripts file and every unrelated file in the SimHub root.
- Installers refuse to overwrite a different project-owned file unless `-ReplaceExisting` is explicitly supplied.
- Removal scripts refuse to delete a file that differs from this package unless `-Force` is explicitly supplied.

## Before transferring the ZIP

On the build computer, record the ZIP SHA-256 value shown by the packaging command. After copying the ZIP to the DCS computer, run:

```powershell
Get-FileHash '.\DcsExtraTelemetry-0.1.0-test.130.zip' -Algorithm SHA256
```

Compare the value before extracting. After extraction, verify every file:

```powershell
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File .\Scripts\Verify-Package.ps1
```

## Recommended graphical workflow

The graphical launcher performs verification, baseline protection, installation, and removal natively. It does not execute the packaged PowerShell scripts and does not change Windows execution policy. The scripts remain available only for optional manual auditing and troubleshooting.

Double-click:

```text
Tools\DcsExtraTelemetry.Launcher.exe
```

Windows will request administrator approval because installing a SimHub plugin under `Program Files` requires it. The launcher detects `DCS` and `DCS.openbeta` Saved Games profiles and SimHub and displays the detected paths before changing anything. Choose exactly one DCS profile and click **Confirm DCS profile**; installation remains disabled until that explicit confirmation is complete.

For a normal installation, close DCS and SimHub and click **Install** once. The launcher automatically:

1. Verifies every packaged file.
2. Captures a safety baseline of unrelated DCS and SimHub files.
3. Installs the isolated DCS Hook.
4. Installs the SimHub plugin.
5. Verifies that unrelated files remain byte-for-byte unchanged.

If another local application already uses the telemetry port, open **Flights > Telemetry connection**, choose **Find available**, and then **Save**. The user selects only the SimHub port; the launcher automatically assigns a different available recorder port. Expand **Advanced recorder port** only when a specific recorder port is required. Restart DCS and SimHub after saving so the Hook, plugin, monitor, and recorder all use the same pair.

## One-click updates

The launcher checks GitHub for newer **published releases**. A source-code push alone does not create an update. The release must use a newer `v0.1.0-test.N` tag and include the matching `DcsExtraTelemetry-0.1.0-test.N.zip` asset. Public repositories update directly over HTTPS. While the repository remains private, GitHub CLI and a one-time **Sign in** are required; the launcher never reads, displays, or stores the GitHub token.

When an update is available:

1. Close DCS and SimHub.
2. Click **Download and update**.
3. Confirm the update.

The launcher downloads the exact release ZIP, verifies its SHA-256 digest against GitHub's release metadata, extracts it with path-traversal and size protections, verifies every file against `manifest.sha256`, captures a fresh baseline, replaces only the project-owned Hook and plugin files, and verifies unrelated files again. Successfully downloaded updater packages are retained under `%LOCALAPPDATA%\DcsExtraTelemetry\Packages`; an older launcher automatically opens the newest verified launcher package on the next run.

Click **Uninstall** once to remove both project-owned components and verify the baseline. If DCS or SimHub uses a nonstandard location, choose that folder once before clicking Install. The **Advanced tools and troubleshooting** section retains the individual operations and standalone monitor for diagnosis.

Every operation is shown in the activity log. A replacement confirmation appears only if a different version of a project-owned file already exists; it is backed up before replacement. Removal remains limited to project-owned filenames. The PowerShell instructions below provide the same operations for advanced troubleshooting or manual auditing.

## Stage 0: capture the untouched baseline

Close DCS and SimHub. From the extracted bundle folder, run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File .\Scripts\Capture-Baseline.ps1
```

For nonstandard paths:

```powershell
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File .\Scripts\Capture-Baseline.ps1 -DcsProfilePath 'D:\Saved Games\DCS.openbeta' -SimHubPath 'D:\Apps\SimHub'
```

Keep the generated `baseline.json`. It contains paths and hashes, not file contents.

## Stage 1: test DCS without changing SimHub

1. SimHub may remain open. The launcher and standalone recording workflow use the dedicated recorder mirror on UDP port 29382.
2. Install the uniquely named Hook:

```powershell
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File .\Scripts\Install-DcsHook.ps1
```

3. Start the monitor:

```powershell
.\Tools\DcsExtraTelemetry.Monitor.exe --port 29382 --seconds 180
```

4. Start DCS, enter a mission, and watch the monitor. A successful test displays `LIVE`, the aircraft name, packet rate, acceleration, RPM, and gear position, then ends with `PASS`.
5. Close DCS and run the baseline verification while the Hook remains installed:

```powershell
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File .\Scripts\Verify-Baseline.ps1
```

The project-owned Hook is excluded from comparison. Every unrelated file must remain byte-for-byte identical.

If Stage 1 fails, remove only the Hook and stop; do not install the SimHub plugin:

```powershell
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File .\Scripts\Remove-DcsHook.ps1
```

## Stage 2: test inside SimHub

Only continue after Stage 1 passes.

1. Close DCS, SimHub, and the standalone monitor.
2. Open PowerShell as Administrator if SimHub is under `Program Files`.
3. Install the uniquely named plugin files:

```powershell
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File .\Scripts\Install-SimHubPlugin.ps1
```

4. Start SimHub. Enable **DCS Extra Telemetry** if SimHub asks about a new third-party plugin.
5. Start DCS and enter a mission.
6. In SimHub, open **Available properties**, enter `DCSExtra`, and press Enter. Confirm:

   - `DCSExtra.Connected` is `true`;
   - `DCSExtra.AircraftType` matches the module;
   - packet rate is updating;
   - relevant `DCSExtra.Has.*` flags are true;
   - raw values move plausibly.

7. Close both programs and run `Verify-Baseline.ps1` again.

## Complete rollback

Close DCS and SimHub. Remove only this project's two integration points:

```powershell
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File .\Scripts\Remove-SimHubPlugin.ps1
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File .\Scripts\Remove-DcsHook.ps1
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File .\Scripts\Verify-Baseline.ps1
```

The final command must report `PASS`. The removal scripts do not remove directories, configuration, profiles, `Export.lua`, or third-party files.

## What to bring back for diagnosis

If testing fails, copy back only these non-sensitive diagnostics if you are comfortable sharing them:

- the monitor's console text;
- lines containing `DCSExtraTelemetry` from `Saved Games\DCS*\Logs\dcs.log`;
- lines containing `DCS Extra Telemetry` from `SimHub\Logs\SimHub.txt`;
- the failing baseline verifier's Changed/Missing/Added list.

Do not copy the entire Saved Games profile, mission files, credentials, or unrelated logs.
