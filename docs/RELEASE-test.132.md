# HapticFlight Bridge 0.1.0-test.132

This public-beta preparation build replaces the private, GitHub-CLI-dependent
update path with public GitHub Release updates.

## Updater changes

- The packaged `repository` field now selects the public `owner/repository`
  release source; it is validated before use.
- Update checks and downloads use the public GitHub Releases API directly.
  Beta testers do not need GitHub CLI or a GitHub sign-in.
- The updater accepts GitHub's SHA-256 asset digest when present. If GitHub
  does not expose one, it reads the separately published matching
  `DcsExtraTelemetry-<version>.zip.sha256` release asset instead.
- A downloaded package must identify the same release repository as the
  currently running package, in addition to passing ZIP, size, path, and
  `manifest.sha256` validation.

## Publishing requirements

Publish each beta as a non-draft GitHub Release in the same public repository
specified by its `package-info.json`. Attach both of these exact files:

1. `DcsExtraTelemetry-<version>.zip`
2. `DcsExtraTelemetry-<version>.zip.sha256`

Use a tag matching the package version, for example `v0.1.0-test.132`.

## Included bridge and profiles

The public-beta packaging script builds the current Hook, SimHub plugin,
launcher, monitor, installation scripts, and the maintained current profiles:
the six-aircraft V2 test.19 set, CH-47F test.2, and the UH-60L v1.9.0 tuned
alternative. The CH-47F rotor-speed source remains an engine-RPM proxy and
requires live validation.
