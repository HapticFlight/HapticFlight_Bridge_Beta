# Updating HapticFlight Bridge

## For beta testers

The launcher checks the public GitHub Releases feed configured in the package.
You do not need to create or sign into a GitHub account.

1. Start the launcher.
2. Choose **Check for updates**.
3. When a newer release is offered, choose **Download update**.
4. Wait for the checksum and package verification to finish.
5. Restart the launcher when asked.

If an update cannot be applied, download the current ZIP from Releases, extract
it to a new folder, and run the launcher from that folder. Your installed DCS
Hook and SimHub plugin can then be repaired from Home.

## For release maintainers

Every published release must contain an exact, matching pair of assets:

- `DcsExtraTelemetry-<version>.zip`
- `DcsExtraTelemetry-<version>.zip.sha256`

Use a tag matching the package version, such as `v0.1.0-beta.1`. Build the
package with `scripts\Package-PublicBeta.ps1` and pass the exact public GitHub
repository in `owner/repository` form. The updater rejects packages from a
different repository and verifies the checksum before installation.
