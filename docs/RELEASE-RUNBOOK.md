# Test release runbook

This runbook prevents a telemetry or launcher change from being mixed with
unreviewed local UI work, a stale package, or the wrong GitHub release state.

## Required sequence

1. Work in one dedicated release checkout. Do not copy files from another
   checkout, especially one with uncommitted UI changes.
2. Commit the complete candidate and push it to `main`.
3. Build one normal ZIP with `Package-PortableTest.ps1`. Do not create or upload
   a self-extracting EXE.
4. Run `Test-ReleaseGate.ps1` against that exact ZIP. It refuses a dirty or
   stale checkout, a mismatched version, a wrapper EXE, an invalid manifest, or
   a failed Defender scan of either the ZIP or its extracted contents.
5. Publish that exact ZIP once under the matching tag. Test releases are normal
   visible releases unless an explicit request says otherwise. Do not silently
   create a draft or prerelease.
6. Read back the GitHub release API and verify the tag, title, ordinary release
   state, asset name, asset size, and download URL before reporting delivery.

## Windows Security report

A local Defender pass means the scanned files did not match the installed
definitions. It does not prove that Smart App Control, reputation, a different
Defender definition, or another protection layer on the test machine will make
the same decision.

When Windows Security reacts, preserve the release and record the exact product
message, detection name, affected file, and action. Compare that file with the
last known-working release before changing packaging or release state. Do not
withdraw, rename, replace, or create another release until the report has been
assessed.
