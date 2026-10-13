# Public beta readiness

## Current baseline

The current public-beta candidate is the merged `main` branch, with test package
`v0.1.0-test.94` built from that branch. The package
includes the current Hook, SimHub plugin, launcher, monitor, installation tools,
documentation, and the UH-60 v1.8.8 profile.

The following checks passed before that test package was published:

- release build;
- UDP transport smoke test;
- UH-60 profile regression;
- full UH-60 flight regression against the 30 July recording;
- package manifest verification;
- bounded recording-analysis and replay-efficiency regression;
- live private-release discovery, download, digest, and extracted-package verification.

## Complete for release preparation

- [x] Isolated Hook architecture that does not modify `Export.lua`.
- [x] Loopback-only default telemetry transport.
- [x] Narrow installation and uninstall scope.
- [x] Versioned UH-60 profile history under `profiles/uh60/`.
- [x] Generated build, installer, and analysis outputs excluded from version
  control.
- [x] Public-facing GitHub overview and beta installation path.
- [x] Public disclaimer and compatibility documentation.
- [x] Portable package manifest and file-hash verification.

## Blocking decisions

These require an owner decision before a public beta can be represented as a
normal end-user release:

1. **License:** select an open-source or proprietary license and add it to the
   repository.
2. **Code signing:** choose the verified publisher identity and Authenticode
   certificate provider.
3. **Installer:** choose a signed conventional installer, or a signed ZIP plus
   signed launcher, instead of relying on an unsigned self-extracting test EXE.
4. **Support contact:** choose the public security-reporting and support route.
5. **Supported example profile:** formally select the UH-60 v1.8.8 profile as
   the first public example, or replace it with a later tested revision.

## Remaining engineering gates

- Create a canonical machine-readable property catalog and generate the Markdown,
  PDF, and XLSX property references from it.
- Establish one authoritative semantic version source for assemblies, packages,
  docs, generated references, and release notes.
- Build a clean-machine installation and rollback matrix.
- Replace private/authenticated update behavior with public HTTPS release metadata
  and signed-asset verification.
- Sign and timestamp the final EXE and DLLs, scan the immutable hashes, and test
  Windows SmartScreen behavior.
- Complete live validation for every advertised aircraft/effect combination.

See [PUBLIC_RELEASE_PLAN.md](PUBLIC_RELEASE_PLAN.md) for the full staged plan and
acceptance criteria.
