# HapticFlight Bridge public release plan

## Purpose

This document is the maintained release gate for publishing the plugin currently
developed as DCS Extra Telemetry. It separates public branding from the stable
technical identifiers already consumed by SimHub, ShakeIt profiles, installation
scripts, recordings, and tests.

The working public product name is **HapticFlight Bridge**.

Proposed public description:

> HapticFlight Bridge is an unofficial local telemetry integration for DCS World
> and SimHub, designed to expose effect-ready flight data for tactile feedback.

Until a compatibility migration is deliberately designed and tested, the following
technical identifiers remain unchanged:

- `DcsExtraTelemetry.Plugin.dll`
- `DCSExtra.*` SimHub display properties
- `DcsExtraTelemetryPlugin.DCSExtra.*` ShakeIt formula properties
- `DCSExtraTelemetry.lua`
- local application-data folders and saved installation state

Changing the public name must not silently break an installed plugin, an imported
ShakeIt profile, an updater, a recording, or an uninstall operation.

## Release policy

A package is not a public release candidate unless it is built from a clean,
identified Git commit and passes every blocking gate in this document. Existing
local `dist` packages and binaries under `bin` or `obj` are development artifacts;
they are not evidence that the current source passes.

Public release work must preserve the integration's core safety contract:

- never read, write, repair, reorder, or depend on `Export.lua`;
- install only uniquely named project-owned files;
- make installation repeatable and removal narrowly scoped;
- protect unrelated DCS and SimHub files with before/after verification;
- keep network traffic on loopback only;
- capability-gate unavailable telemetry instead of inventing values;
- clear or fade stale continuous values and never latch event outputs;
- keep failures inside this integration.

## Decision ledger

| Decision | Status | Current direction |
|---|---|---|
| Public product name | Working decision | HapticFlight Bridge |
| DCS relationship | Decided | Unofficial compatibility description only |
| Existing property namespace | Decided for first public release | Preserve for compatibility |
| First public channel | Proposed | Public beta before stable release |
| Software license | User decision required | Do not publish until selected |
| Code-signing provider | User decision required | Trusted Authenticode identity required |
| Installer format | Evaluation required | Prefer signed conventional installer or signed ZIP plus launcher |
| Monetization | Deferred | Keep initial release free unless licensing is reviewed |

## Stage 1 - Stabilize the source tree

- [ ] Finish or deliberately separate the current telemetry, launcher, test, and
  UH-60 profile work.
- [ ] Add every required source file to version control, including shared source
  used by project files.
- [ ] Decide which ShakeIt profile is the supported public example.
- [ ] Move historical profiles and design/audit outputs only after their rollback
  and evidence value has been reviewed.
- [ ] Ensure generated packages, recordings, logs, caches, credentials, and local
  machine paths are ignored.
- [ ] Confirm the repository contains no secrets or private release credentials.
- [ ] Reach a clean working tree before creating a release candidate.
- [ ] Create an annotated beta tag from the reviewed commit.

Exit criterion: a new checkout of the tagged commit contains every required source
file and can begin a build without relying on untracked local content.

## Stage 2 - Establish public project governance

- [ ] Select and add a software license.
- [ ] Add `THIRD_PARTY_NOTICES.md` and verify redistribution rights for all assets.
- [ ] Add `CHANGELOG.md`.
- [ ] Add `CONTRIBUTING.md`.
- [ ] Add `SECURITY.md` with a private reporting route.
- [ ] Add an issue template that requests narrow, non-sensitive diagnostics.
- [ ] Add the unofficial-project and trademark disclaimer to public surfaces.
- [ ] Confirm that the initial release remains freeware unless Eagle Dynamics
  licensing and trademark questions are resolved in writing.

Recommended disclaimer:

> DCS World and Digital Combat Simulator are trademarks of Eagle Dynamics SA.
> SimHub is a trademark of its respective owner. HapticFlight Bridge is an
> independent community project and is not affiliated with, endorsed by, or
> sponsored by Eagle Dynamics SA or SimHub.

Exit criterion: users can determine the project's owner, license, support status,
security contact, third-party relationships, and unofficial status before download.

## Stage 3 - Create one property catalog

- [ ] Create a machine-readable canonical property catalog.
- [ ] Record, for every property: display name, fully qualified ShakeIt name,
  category, type, unit/range, capability gate, source quality, applicable aircraft,
  description, caveat, and introduced version.
- [ ] Verify every registered plugin property appears exactly once in the catalog.
- [ ] Verify every catalog property is registered by the plugin.
- [ ] Generate the Markdown property reference from the catalog.
- [ ] Generate a versioned PDF property guide.
- [ ] Generate a versioned XLSX workbook with filters and aircraft-specific sheets.
- [ ] Fail the release build when generated artifacts are stale.

Exit criterion: source registration, GitHub documentation, PDF, and XLSX agree on
the complete public property contract.

## Stage 4 - Rewrite the public documentation

- [ ] Rewrite the root README as the GitHub landing page for HapticFlight Bridge.
- [ ] Explain the Hook -> loopback UDP -> SimHub plugin -> ShakeIt data path.
- [ ] Provide a short, nontechnical installation path before development details.
- [ ] Separate end-user installation, developer build, maintainer release, property
  reference, profile tuning, and troubleshooting documentation.
- [ ] Replace stale statements that describe implemented adapters or effects as
  future work.
- [ ] Use fully qualified `DcsExtraTelemetryPlugin.DCSExtra.*` names in ShakeIt
  formula examples.
- [ ] Document supported, proxy, experimental, and unavailable aircraft data.
- [ ] Document local data storage, recordings, update traffic, and uninstall scope.

Exit criterion: a new user can download, verify, install, test, troubleshoot, and
uninstall the plugin without development tools or private-repository access.

## Stage 5 - Build and version reproducibly

- [ ] Create one authoritative semantic version source.
- [ ] Apply it to assembly metadata, package metadata, filenames, updater logic,
  documentation, PDF, XLSX, and release notes.
- [ ] Use a standard prerelease such as `0.1.0-beta.1`.
- [ ] Clean generated output before every release build.
- [ ] Treat compiler warnings as release failures.
- [ ] Build, test, package, and verify in one controlled pipeline.
- [ ] Prevent packaging when the working tree is dirty or the tag/version differs.
- [ ] Produce deterministic binaries where supported.
- [ ] Generate checksums, a software bill of materials, and provenance evidence.
- [ ] Publish debug symbols separately from the end-user package.

Exit criterion: the same tagged source and documented environment produce the same
release inputs, and no package can silently reuse stale development binaries.

## Stage 6 - Reduce antivirus and installer risk

- [ ] Obtain a trusted Authenticode signing identity.
- [ ] Sign and RFC 3161 timestamp every shipped EXE and DLL with SHA-256.
- [ ] Verify signatures, timestamps, certificate chain, and expected publisher as a
  blocking pipeline step.
- [ ] Avoid shipping PDBs and PowerShell scripts in the normal user package.
- [ ] Evaluate replacing the self-extracting EXE with a signed conventional
  installer or a signed ZIP containing a signed launcher and visible payload.
- [ ] If the self-extractor remains, verify the embedded package before execution;
  bound file count, file size, total expanded size, duplicates, compression ratio,
  and extraction races; and repair corrupt caches safely.
- [ ] Make UAC show the verified publisher and elevate only the narrow SimHub copy
  operation.
- [ ] Scan the exact signed release candidate with current Microsoft Defender and a
  reputable multi-engine service.
- [ ] Test Edge/SmartScreen and Windows 11 Smart App Control behavior.
- [ ] Resolve known detections before publication; never instruct users to disable
  antivirus or create broad exclusions.

Exit criterion: all shipped binaries have a consistent verified publisher, there
are zero known malware detections for the final hashes, and normal installation does
not depend on an antivirus bypass.

## Stage 7 - Make installation transactional

- [ ] Preflight paths, permissions, free space, running applications, locked files,
  port conflicts, and package integrity before writing.
- [ ] Capture the unrelated-file baseline before installation.
- [ ] Use temporary files and atomic replacement.
- [ ] Verify installed hashes after writing.
- [ ] Roll back both integration points if either installation step fails.
- [ ] Store a validated installation receipt with version, paths, hashes, and time.
- [ ] Preserve a different or user-modified installed file unless replacement or
  forced removal is explicitly confirmed.
- [ ] Test fresh install, same-version repair, upgrade, failed upgrade, rollback,
  uninstall, and uninstall without the original package.
- [ ] Distinguish removal of installed components from optional cleanup of caches,
  backups, settings, baselines, and recordings.

Exit criterion: every tested failure either leaves the original state intact or
restores it automatically, with unrelated files unchanged.

## Stage 8 - Convert updates for public releases

- [ ] Remove the GitHub CLI and authenticated-private-repository requirement.
- [ ] Read public release metadata and assets over HTTPS.
- [ ] Support explicit stable and beta channels; default ordinary users to stable.
- [ ] Validate repository, tag, asset name, size, digest, version, manifest,
  Authenticode signature, and publisher before installation.
- [ ] Reject drafts, downgrades, malformed packages, unsigned packages, unexpected
  publishers, and unsafe archives.
- [ ] Retain and restore the last known-good signed package.
- [ ] Test offline, timeout, rate-limit, corrupt download, interrupted download,
  compromised cache, and malformed API responses.

Exit criterion: a public user can update without GitHub software or authentication,
and an update cannot bypass the package and publisher verification policy.

## Stage 9 - Validate runtime and aircraft support

- [ ] Add Lua syntax and fixture tests.
- [ ] Fuzz malformed, oversized, missing, null, incorrect-type, NaN, and infinite
  telemetry inputs.
- [ ] Test sequence resets, stale data, shutdown, occupied ports, invalid settings,
  DCS crashes, SimHub crashes, and long-running operation.
- [ ] Measure CPU, memory, packet loss, latency, and log growth during a soak test.
- [ ] Verify coexistence with common DCS integrations without touching `Export.lua`.
- [ ] Capture and review a current flight recording for every advertised airframe:
  UH-60L, UH-60L DAP, AH-6J, MH-6J, AH-64D, and UH-1H.
- [ ] Exercise applicable startup, rotor, hover, climb, descent, maneuver, landing,
  ground, weapon, countermeasure, door, and cockpit signals.
- [ ] Verify unsupported capabilities stay false and disconnected effects clear.
- [ ] Complete current live validation for blade slap, control response, governor
  response, and sustained aggressive-flight feedback before calling them stable.

Exit criterion: every advertised mapping has current evidence and no offline-only
test is presented as proof of live tactile behavior.

## Stage 10 - Publish a public beta

- [ ] Build from the clean beta tag.
- [ ] Complete a clean-machine Windows/SimHub/DCS installation matrix.
- [ ] Complete signing and antivirus gates for the final immutable hashes.
- [ ] Publish ZIP or installer, PDF, XLSX, checksums, license, release notes, known
  limitations, and rollback instructions as GitHub Release assets.
- [ ] Record hashes and scan results in the release evidence.
- [ ] Monitor installation, updater, antivirus, and compatibility reports.
- [ ] Promote to stable only after the beta exit criteria and a defined observation
  period are satisfied.

## Immediate next decisions

The first implementation work that requires user direction is:

1. Select the software license.
2. Select a code-signing approach and verified publisher identity.
3. Choose between a conventional installer, signed ZIP plus launcher, or a hardened
   self-extracting launcher.
4. Confirm which UH-60 ShakeIt profile will be the supported public example.

Work that does not require those decisions may proceed first: catalog design,
documentation cleanup, clean-build safeguards, tests, and public-updater design.
