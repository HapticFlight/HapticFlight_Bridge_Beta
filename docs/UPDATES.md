# Updating HapticFlight Bridge

## For beta testers

The application checks the public GitHub Releases feed configured in the package.
You do not need to create or sign into a GitHub account.

The current public beta uses a single Setup EXE. Older ZIP installations are
legacy and must download Setup manually to switch formats.

1. Start HapticFlight Bridge.
2. Choose **Check for updates**.
3. When a newer release is offered, choose **Download update**.
4. Wait for the Setup download and SHA-256 verification to finish.
5. Complete Setup and reopen the application. Setup does not install or change
   the DCS Hook or SimHub plugin; use the in-app controls to update those components.

If a Setup update cannot be applied, download the current Setup EXE from
Releases and run it again. The installed application also provides **Repair setup**.

The test.139 Setup beta is unsigned, so Windows App Control may block it on
protected PCs. Do not disable security protections to force it to run.

## For release maintainers

Every published installer release must contain an exact, matching pair of assets:

- `HapticFlight-Bridge-Setup-<version>.exe`
- `HapticFlight-Bridge-Setup-<version>.exe.sha256`

Use a tag matching the package version, such as `v0.1.0-test.139`. Build the
installer with `scripts\Package-Setup.ps1` and pass the exact public GitHub
repository in `owner/repository` form. The updater verifies the Setup checksum
before launching it. The release gate also checks that the app, SimHub plugin,
and monitor file properties match the package version.
