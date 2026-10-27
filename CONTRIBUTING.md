# Contributing to HapticFlight Bridge

The repository is currently maintained by one developer. Changes still use a
branch and pull request so that CI, a written validation record, and a clean
history protect the public beta.

## Everyday workflow

1. Start from an up-to-date `main` and create a short-lived branch:
   `feature/...`, `fix/...`, `docs/...`, or `release/...`.
2. Keep a pull request focused on one concern. Do not mix launcher experiments,
   telemetry logic, profile calibration, and release mechanics.
3. Complete the pull-request template, wait for `build-and-smoke-test`, then
   squash-merge the PR. Until a second maintainer is active, self-review is a
   checklist rather than an approval requirement.
4. Delete the merged branch. Do not force-push or commit directly to `main`.

## Validation expectations

The hosted CI builds and runs the standalone haptic-engine, transport, updater,
and profile regression tests. It deliberately does not build the SimHub plugin:
the plugin requires a locally installed SimHub SDK and must be validated in a
controlled local environment.

For any telemetry or profile change, retain capture evidence, state exact
formulas/gains and expected maxima, and distinguish formula validation from
live DCS/SimHub/seat validation. Do not modify `Export.lua` or an installed DCS
or SimHub system unless the work is explicitly authorized.

## Releases

Follow [the release runbook](docs/RELEASE-RUNBOOK.md). A release is created
only from a clean, passing `main` commit, tagged after merge. The installer,
checksum, and release verification evidence must all describe the same version.

## Repository hygiene

`output/` and `tmp/` are local generated artifacts. Keep source documents and
approved public assets in their intended tracked locations; do not add broad
ignore rules for `assets/` without first classifying the files.
