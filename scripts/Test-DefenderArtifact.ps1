[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Path
)

$ErrorActionPreference = 'Stop'
$artifact = (Resolve-Path -LiteralPath $Path).Path
$scanner = Join-Path $env:ProgramFiles 'Windows Defender\MpCmdRun.exe'
if (-not (Test-Path -LiteralPath $scanner -PathType Leaf)) {
    throw 'Release blocked: Microsoft Defender command-line scanner is unavailable.'
}

$isFile = Test-Path -LiteralPath $artifact -PathType Leaf
$initialHash = if ($isFile) { (Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash } else { $null }
$scanOutput = (& $scanner -Scan -ScanType 3 -File $artifact 2>&1 | Out-String).Trim()
$scanExitCode = $LASTEXITCODE
Write-Host $scanOutput

# MpCmdRun can return exit code 0 even after finding and cleaning a threat.
# Require an explicit clean result and confirm the scanned file survived unchanged.
if ($scanExitCode -ne 0 -or $scanOutput -notmatch 'found no threats') {
    throw "Release blocked: Defender did not report a clean scan for $artifact."
}
if ($isFile -and ((-not (Test-Path -LiteralPath $artifact -PathType Leaf)) -or
    (Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash -ne $initialHash)) {
    throw "Release blocked: scanned artifact disappeared or changed: $artifact."
}
Write-Host "PASS: Defender scan found no threats: $artifact"
