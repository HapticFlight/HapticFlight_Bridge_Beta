# Building the Windows Setup installer

The user-facing release asset is `HapticFlight-Bridge-Setup-<version>.exe` plus
its `.sha256` file. Without signing inputs, the build writes a `-preview.exe`
that the public updater will not offer. The Setup EXE contains the compiled application, DCS hook,
SimHub plugin, profiles, and support files. Users do not extract a source tree.

Build from a reviewed source checkout with Visual Studio MSBuild, SimHub's
`SimHub.Plugins.dll`, and Inno Setup installed:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Package-Setup.ps1 `
  -Version NEWER_VERSION -Repository HapticFlight/HapticFlight_Bridge_Beta
```

The script builds fresh Release binaries, stages the approved files, creates a
SHA-256 manifest, compiles the Inno Setup definition, and writes the Setup
checksum. `-CompilerPath` can point to an alternate `ISCC.exe` location. For a
publishable filename, provide `-SigningThumbprint` and `-SignToolPath`; the
script signs and verifies every shipped binary and the installer.

Run `scripts\Test-SetupReleaseGate.ps1` against the signed Setup EXE and its
staging directory before publication. It requires a clean checkout at
`origin/main`, matching version and source commit, a complete manifest,
matching publisher signatures, and a matching Setup checksum.

Before publishing, sign and timestamp the shipped EXE and DLL files and the
final Setup EXE with the project's Authenticode identity. Verify signatures,
publisher, Setup checksum, clean-machine installation, repair, upgrade,
uninstall, and DCS/SimHub operation with the application closed. The unsigned
local build is a development preview, not a signed public release.

The installer registers one per-user application and Start menu entry. It asks
for the DCS and SimHub locations and invokes the compiled install coordinator.
Only the integration helper requests administrator approval. The coordinator
keeps a receipt in `%LOCALAPPDATA%\DcsExtraTelemetry` so Windows uninstall can
remove the two integration files without the original installer.
