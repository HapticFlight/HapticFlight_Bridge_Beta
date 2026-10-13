# Publish a public beta release

## Create the repository

Create a new **public** GitHub repository owned by the release publisher. Do
not initialize it with a README, license, or `.gitignore`; this project already
contains those files.

Recommended name:

```text
hapticflight-bridge-public-beta
```

After GitHub authentication, create it from the project folder with:

```powershell
gh repo create OWNER/hapticflight-bridge-public-beta --public --source . --remote origin --push
```

Replace `OWNER` with the GitHub account or organization name. The repository
must remain public for the beta auto-updater to work without each tester
signing in to GitHub.

## Build the release archive

From the project root:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Package-PublicBeta.ps1 `
  -Version 0.1.0-test.132 `
  -Repository OWNER/hapticflight-bridge-public-beta
```

The command creates two files in `dist\public-beta`:

- `DcsExtraTelemetry-0.1.0-test.132.zip`
- `DcsExtraTelemetry-0.1.0-test.132.zip.sha256`

Use a fresh, increasing version for every beta build. The updater accepts the
current numeric `x.y.z-test.n` convention.

## Publish the GitHub Release

1. Push the release commit to `main`.
2. Create an annotated Git tag exactly matching the package version, prefixed
   with `v` (for example, `v0.1.0-test.132`).
3. Create a GitHub Release from that tag.
4. Attach the matching ZIP and `.zip.sha256` files.
5. Mark prerelease builds as **Pre-release**.
6. Download both assets from the public Releases page and verify the ZIP before
   announcing it.

The updater requires exact asset names and a matching checksum. If either asset
is missing, testers will see no installable update.

## Verify before announcement

- Fresh-extract the release ZIP into a new writable folder.
- Run `Scripts\Verify-Package.ps1` and require `PASS`.
- Launch the app; check the displayed version and Home setup flow.
- Install the Hook and plugin to a disposable or test DCS Saved Games profile.
- Import one included SimHub profile and confirm its properties in a mission.
- Verify a second machine—or a clean extraction—can see the release through
  **Check for updates** without signing into GitHub.
