# Updating HapticFlight Bridge

## For beta testers

The application checks the public GitHub Releases feed configured in the package.
You do not need to create or sign into a GitHub account.

The current public beta is a ZIP release. ZIP installations continue to receive
verified ZIP updates. The Setup workflow below applies after a signed Setup
installer has been published.

1. Start HapticFlight Bridge.
2. Choose **Check for updates**.
3. When a newer release is offered, choose **Download update**.
4. Wait for the Setup download and SHA-256 verification to finish.
5. Close DCS and SimHub, complete Setup, and reopen the application.

If a Setup update cannot be applied, download the current signed Setup EXE from
Releases and run it again. The installed application also provides **Repair setup**.

## For release maintainers

Every published installer release must contain an exact, matching pair of assets:

- `HapticFlight-Bridge-Setup-<version>.exe`
- `HapticFlight-Bridge-Setup-<version>.exe.sha256`

Use a tag matching the package version, such as `v0.1.0-test.132`. Build the
installer with `scripts\Package-Setup.ps1` and pass the exact public GitHub
repository in `owner/repository` form. The updater verifies the Setup checksum
before launching it.
