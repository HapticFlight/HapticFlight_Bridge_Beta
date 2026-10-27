# Development flow guide

This guide is the day-to-day path for a solo maintainer working on the public
beta. It protects `main` with pull requests and automation, but does not require
an unavailable second reviewer. The required self-review, CI, and release gates
are still real merge conditions.

## The short version

```text
Sync main -> create one focused branch -> change -> validate -> PR ->
passing CI + self-review -> squash merge -> release separately when needed
```

Never use a release to validate an unreviewed change. Never commit directly to
`main`.

## 1. Start a change safely

1. Open the repository root and inspect your workspace:

   ```powershell
   git status --short
   ```

   Stop if it contains unrelated changes. Commit them on their own branch,
   preserve them with a named branch, or ask before discarding anything.

2. Refresh your local view of the protected branch:

   ```powershell
   git fetch origin main --tags
   git switch main
   git pull --ff-only origin main
   ```

3. Create one short-lived branch for one concern. Use a name that makes the
   intent clear:

   ```powershell
   git switch -c fix/launcher-startup-error
   # or feature/ah6-spool-cue, docs/installation-clarity, release/test-156
   ```

4. Keep the branch narrow. Do not combine UI redesign, telemetry logic, profile
   tuning, generated assets, and packaging in the same pull request.

## 2. Make the change

Work only on the files required by the branch purpose. Before adding a file,
check whether it is source, a deliberate public asset, or generated output.

- `output/`, `tmp/`, `dist/`, `bin/`, and `obj/` are generated/local outputs;
  do not add them to Git.
- `assets/` is not broadly ignored. Inspect and deliberately track a source
  asset only when it belongs in the repository. Do not replace HapticFlight
  branding with a newly generated logo without an explicit redesign decision.
- Do not change `Export.lua`, DCS, or a live SimHub installation during routine
  validation. Use disposable fixtures when an install path must be tested.

For telemetry or profile work, preserve the current profile and generate a
separately named, importable version. Record the exact formulas, gains, expected
maxima, capture evidence, and live-seat testing gaps. Formula validation is not
proof of live shaker behavior.

## 3. Validate before a pull request

Run the smallest relevant checks first, then the shared checks below when the
change affects C#, telemetry, haptics, profiles, packaging, or a release.

```powershell
# Build the standalone test projects.
dotnet msbuild tests\DcsExtraTelemetry.HapticEngineTests\DcsExtraTelemetry.HapticEngineTests.csproj /t:Rebuild /p:Configuration=Release /v:minimal
dotnet msbuild tests\DcsExtraTelemetry.TransportSmokeTest\DcsExtraTelemetry.TransportSmokeTest.csproj /t:Rebuild /p:Configuration=Release /v:minimal
dotnet msbuild tests\DcsExtraTelemetry.UpdaterSmokeTest\DcsExtraTelemetry.UpdaterSmokeTest.csproj /t:Rebuild /p:Configuration=Release /v:minimal

# Run the smoke executables.
.\tests\DcsExtraTelemetry.HapticEngineTests\bin\Release\DcsExtraTelemetry.HapticEngineTests.exe
.\tests\DcsExtraTelemetry.TransportSmokeTest\bin\Release\DcsExtraTelemetry.TransportSmokeTest.exe
.\tests\DcsExtraTelemetry.UpdaterSmokeTest\bin\Release\DcsExtraTelemetry.UpdaterSmokeTest.exe

# Run current profile regressions.
Get-ChildItem tests -Filter 'Test-*.js' | Sort-Object Name | ForEach-Object {
  node $_.FullName
  if ($LASTEXITCODE -ne 0) { throw "Profile regression failed: $($_.Name)" }
}

# Reject whitespace errors before review.
git diff --check
```

The hosted `build-and-smoke-test` check repeats these validations on a Windows
runner. A local sandbox may block loopback UDP or temporary install operations;
record that fact rather than masking a real failure. Historical frozen checks in
`tests/legacy/` are not current-release gates.

For launcher/UI changes, also build the affected application and inspect the
rendered default and resized/maximized states. For DCS/SimHub behavior, state
precisely what was capture-validated, fixture-validated, and live-tested.

## 4. Create a pull request

1. Inspect exactly what will be committed:

   ```powershell
   git status --short
   git diff
   ```

2. Stage only the intended files—avoid `git add .` when generated or personal
   files are present:

   ```powershell
   git add path\to\changed-file
   git commit -m "fix: explain the user-facing change"
   git push --set-upstream origin fix/launcher-startup-error
   ```

3. Open a pull request to `main`. Complete its template honestly: validation,
   user impact, documentation, DCS/SimHub boundary, and rollback evidence.

4. Wait for both required checks:

   - `build-and-smoke-test`
   - `CodeQL`

   Fix a failure; never bypass, rename, or disable a required check to merge.

## 5. Self-review and merge

While there is one maintainer, GitHub requires zero approving reviews. Before
merging, perform a deliberate self-review:

1. Read the PR diff as if you did not author it.
2. Confirm the change matches the title and does not include generated output,
   secrets, unrelated cleanup, or unverified claims.
3. Confirm every required check is green and every conversation is resolved.
4. Squash-merge. This keeps `main` linear and makes one PR one history entry.
5. Delete the merged branch.

If a second maintainer joins, add their verified GitHub handle/team to
`CODEOWNERS` and raise the required approval count to one. Do not create a
placeholder owner that cannot actually review the work.

## 6. Release only after merge

Most PRs are not releases. For a release, start only after the versioned change
has merged and `main` is clean. Follow the complete
[public-beta release runbook](RELEASE-RUNBOOK.md): build the Setup installer,
run `Test-SetupReleaseGate.ps1` against that exact EXE, tag the merged commit,
create a prerelease with the matching EXE and checksum, download and verify the
published assets, then run the Setup wizard smoke test.

An unsigned beta must be explicitly labelled as such. A `-preview.exe` is local
review output and must never be published. Historical package verification does
not validate a new version.

## 7. Recover without losing work

- If your branch is stale, commit or preserve your work first, then refresh from
  `main` and resolve conflicts on the branch—not on `main`.
- If CI fails, reproduce the smallest failing command locally when possible and
  attach the relevant result to the PR.
- If a release appears missing, refresh with `git fetch origin main --tags` and
  verify the GitHub release/tag state before building a replacement.
- If you need to abandon work, keep a named branch or commit. Do not use a hard
  reset or delete a working tree until every unique change is preserved.

## Daily finish checklist

- [ ] Working tree contains only the change I intend to keep.
- [ ] A focused PR—not a direct push—contains the change.
- [ ] `build-and-smoke-test` and `CodeQL` are green.
- [ ] Documentation and rollback information match the actual validation.
- [ ] A release, if any, follows the release runbook separately.
