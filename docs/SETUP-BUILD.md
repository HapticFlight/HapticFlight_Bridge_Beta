# Building the Windows Setup installer

The user-facing release asset is `HapticFlight-Bridge-Setup-<version>.exe` plus
its `.sha256` file. Without signing inputs, the build writes a `-preview.exe`
that the public updater will not offer, unless `-AllowUnsignedBeta` is explicitly
passed for a test prerelease. The Setup EXE contains the compiled application, DCS hook,
SimHub plugin, profiles, and support files. Users do not extract a source tree.

Build from a reviewed source checkout with Visual Studio MSBuild, SimHub's
`SimHub.Plugins.dll`, and Inno Setup installed:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Package-Setup.ps1 `
  -Version NEWER_VERSION -Repository HapticFlight/HapticFlight_Bridge_Beta
```

The script builds fresh Release binaries, stages the approved files, creates a
SHA-256 manifest, compiles the Inno Setup definition, and writes the Setup
checksum. Use the official Inno Setup 6.7.3 compiler; `-CompilerPath` can point
to its `ISCC.exe` in an alternate location. Other compiler builds are rejected
until they are assessed and the pinned hash is deliberately updated. For a
publishable filename, provide `-SigningThumbprint` and `-SignToolPath`; the
script signs and verifies every shipped binary and the installer. An explicitly
approved unsigned test beta may instead use `-AllowUnsignedBeta`, which checks
that all shipped binaries remain unsigned and uses the canonical Setup filename.

Run `scripts\Test-SetupReleaseGate.ps1` against the signed Setup EXE and its
staging directory before publication. It requires clean tracked package inputs
at `origin/main`, matching version and source commit, a complete manifest, and
a matching Setup checksum. It also requires clean Microsoft Defender scans of
the staged files and final Setup EXE; Defender may quarantine a rejected EXE.
The default requires matching publisher signatures;
pass `-AllowUnsignedBeta` only for an explicitly approved unsigned test release.

Before publishing, sign and timestamp the shipped EXE and DLL files and the
final Setup EXE with the project's Authenticode identity when signing is
available. Verify signatures or the explicit unsigned status,
publisher, Setup checksum, clean-machine installation, repair, upgrade,
uninstall, and DCS/SimHub operation with the application closed. The unsigned
local build is a development preview unless `-AllowUnsignedBeta` is used; an
unsigned beta can be blocked by Windows App Control and is not a signed release.

The installer registers one per-user application and Start menu entry. The app
then lets the user choose DCS and SimHub locations and install or remove the
integrations separately. Only the integration helper requests administrator
approval. The coordinator keeps a receipt in `%LOCALAPPDATA%\DcsExtraTelemetry`
so Windows uninstall can remove the integration files without the original installer.
