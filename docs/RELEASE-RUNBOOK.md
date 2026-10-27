# Public-beta release runbook

This is the single release path for HapticFlight Bridge public betas. The older
portable-ZIP process remains only for historical release reproduction; do not
use it for new public releases.

## Before merging

1. Open a focused pull request against `main` and make `build-and-smoke-test`
   pass.
2. Confirm the release version is fresh and increasing, and update release notes
   and user-facing documentation in the same pull request.
3. Record the intended installer signing state. Unsigned output is allowed only
   for an explicitly labelled test prerelease.

## Build and verify

1. Squash-merge the passing pull request to `main`; do not publish from a local
   feature branch.
2. Refresh the release checkout with `git fetch origin main --tags` and
   `git pull --ff-only origin main`. Confirm it is clean and at `origin/main`.
3. Build with `scripts/Package-Setup.ps1`, using the pinned Inno Setup compiler
   and signing inputs for a signed release. For an approved unsigned beta, use
   `-AllowUnsignedBeta`; never publish a `-preview.exe`.
4. Run `scripts/Test-SetupReleaseGate.ps1` against that exact Setup EXE. Keep
   its package integrity, version, signing-state, and Defender evidence.
5. Tag the merged commit with the exact `v<version>` tag and create a GitHub
   **pre-release**. Upload only the matching Setup EXE and `.sha256` file.
6. Download the published assets, recalculate the checksum, then manually run
   `Setup wizard smoke test` with the tag.

## Before announcement

- Verify GitHub shows the intended tag, prerelease state, asset names, asset
  sizes, and checksums.
- Test install/launch on a clean Windows test account.
- Test Hook and plugin installation only against disposable DCS Saved Games and
  SimHub fixtures.
- Import the included matching SimHub profile and describe what was actually
  live-tested versus formula- or capture-validated.

## Windows Security reports

A local Defender pass only means the scanned files did not match the local
definitions. If Windows Security reacts, preserve the release and record the
product message, detection name, affected file, and action. Compare it to the
last known working release before withdrawing, replacing, or repackaging
anything.
