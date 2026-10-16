# HapticFlight Bridge 0.1.0-test.133

This is an unsigned public-beta ZIP for existing ZIP installations. It is not
the proposed Setup.exe release. Extract the complete ZIP, then run
`Tools\DcsExtraTelemetry.Launcher.exe`. Windows App Control may block unsigned
builds on protected PCs; do not disable security protections to run it.

## Changes

- The launcher has a visible whole-app uninstall entry for future Setup
  installations. ZIP users continue to manage the DCS hook and SimHub plugin
  through the launcher's component controls.
- Public update checks require the matching published `.sha256` asset and
  reject a checksum that disagrees with GitHub's asset digest.
- Launcher builds no longer load a temporary unsigned application binary
  during WPF markup compilation, avoiding the observed build-time App Control
  block. This does not make the distributed binaries signed.
- Setup packaging and a release gate are present in source, but no Setup.exe
  is included in this ZIP release.

## Validation and limits

The Release launcher build and disposable updater, install, repair, and
uninstall smoke tests passed. The updater found and verified the previous
public ZIP release. The new ZIP must pass its packaged manifest verifier and
public download check before it is announced. Live DCS, SimHub, and shaker
output were not retested for this code-only beta update.
