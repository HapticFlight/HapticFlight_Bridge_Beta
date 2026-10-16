[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+(?:-test\.\d+)?$')]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$SetupPath,

    [Parameter(Mandatory = $true)]
    [string]$StagePath
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$setup = (Resolve-Path -LiteralPath $SetupPath).Path
$stage = (Resolve-Path -LiteralPath $StagePath).Path
$expectedName = "HapticFlight-Bridge-Setup-$Version.exe"
if ((Split-Path -Leaf $setup) -cne $expectedName) { throw "Expected signed release asset $expectedName." }

$dirty = @(git -C $repositoryRoot status --porcelain)
if ($dirty.Count -gt 0) { throw 'Release blocked: the checkout has uncommitted changes.' }
$head = (git -C $repositoryRoot rev-parse HEAD).Trim()
$main = (git -C $repositoryRoot rev-parse origin/main).Trim()
if ($head -ne $main) { throw 'Release blocked: HEAD does not match origin/main.' }

$info = Get-Content -LiteralPath (Join-Path $stage 'package-info.json') -Raw | ConvertFrom-Json
if ($info.version -cne $Version -or $info.sourceCommit -cne $head) {
    throw 'Release blocked: staged version or source commit does not match the candidate.'
}
$manifest = Join-Path $stage 'manifest.sha256'
$expectedFiles = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($line in Get-Content -LiteralPath $manifest) {
    if ($line -cnotmatch '^([0-9A-Fa-f]{64})  (.+)$') { throw "Malformed manifest entry: $line" }
    $hash = $Matches[1]
    $relative = $Matches[2]
    $file = [System.IO.Path]::GetFullPath((Join-Path $stage $relative))
    if (-not $file.StartsWith($stage.TrimEnd('\') + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Unsafe manifest path: $relative"
    }
    if (-not $expectedFiles.Add($file) -or -not (Test-Path -LiteralPath $file -PathType Leaf)) {
        throw "Missing or duplicate staged file: $relative"
    }
    if ((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $hash) {
        throw "Staged file hash mismatch: $relative"
    }
}
foreach ($file in Get-ChildItem -LiteralPath $stage -File -Recurse) {
    if ($file.FullName -ne $manifest -and -not $expectedFiles.Contains($file.FullName)) {
        throw "Unsigned staged file: $($file.FullName)"
    }
}

$binaries = @(
    (Join-Path $stage 'HapticFlight Bridge.exe'),
    (Join-Path $stage 'Tools\DcsExtraTelemetry.Monitor.exe'),
    (Join-Path $stage 'SimHub\DcsExtraTelemetry.Plugin.dll'),
    $setup
)
$publishers = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($binary in $binaries) {
    $signature = Get-AuthenticodeSignature -LiteralPath $binary
    if ($signature.Status -ne 'Valid') { throw "Release binary is not signed and trusted: $binary" }
    $null = $publishers.Add($signature.SignerCertificate.Subject)
}
if ($publishers.Count -ne 1) { throw 'Release binaries use different signing publishers.' }

$checksum = $setup + '.sha256'
if (-not (Test-Path -LiteralPath $checksum)) { throw 'Setup checksum asset is missing.' }
$expectedDigest = (Get-Content -LiteralPath $checksum).Split(' ')[0]
if ((Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash -ne $expectedDigest) {
    throw 'Setup checksum does not match the release asset.'
}

Write-Host "PASS: signed Setup release $Version; manifest and checksum verified."
Write-Host "Publisher: $($publishers | Select-Object -First 1)"
