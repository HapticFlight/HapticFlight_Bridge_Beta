# Publish a public beta release

The public repository is `HapticFlight/HapticFlight_Bridge_Beta`. Keep it public
so testers can update without signing in to GitHub. The currently published
`v0.1.0-test.132` release is ZIP-only; publishing source changes does not create
a Setup release.

## Build the release installer

From the project root:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Package-Setup.ps1 `
  -Version NEWER_VERSION `
  -Repository HapticFlight/HapticFlight_Bridge_Beta `
  -CompilerPath 'C:\Program Files\Inno Setup 7\ISCC.exe' `
  -SignToolPath 'C:\Program Files (x86)\Windows Kits\10\bin\x64\signtool.exe' `
  -SigningThumbprint 'YOUR_CERTIFICATE_THUMBPRINT'
```

The signed build creates two release files in `dist\setup`:

- `HapticFlight-Bridge-Setup-<version>.exe`
- `HapticFlight-Bridge-Setup-<version>.exe.sha256`

Without signing inputs the script creates a `-preview.exe` for local review.
Do not publish the preview as a release asset.

Use a fresh, increasing version for every beta build. The updater accepts the
current numeric `x.y.z-test.n` convention.

## Publish the GitHub Release

1. Push the release commit to `main`.
2. Create an annotated Git tag exactly matching the package version, prefixed
   with `v` (for example, `v0.1.0-test.133`).
3. Create a GitHub Release from that tag.
4. Attach the matching Setup EXE and `.exe.sha256` files.
5. Mark prerelease builds as **Pre-release**.
6. Download both assets from the public Releases page and verify Setup before
   announcing it.

The updater requires exact asset names and a matching checksum. If either asset
is missing, testers will see no installable update.

## Verify before announcement

- Check Setup's SHA-256 and Authenticode publisher.
- Install on a clean Windows test account and check the Start menu launch.
- Confirm the Hook and plugin install to a disposable or test DCS Saved Games profile.
- Import one included SimHub profile and confirm its properties in a mission.
- Close HapticFlight Bridge and confirm SimHub properties and shaker output
  continue during a DCS mission.
- Verify a second machine can see the release through
  **Check for updates** without signing into GitHub.
