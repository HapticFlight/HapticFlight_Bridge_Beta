[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+(?:-test\.\d+)?$')]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$SetupPath,

    [Parameter(Mandatory = $true)]
    [string]$StagePath,

    [switch]$AllowUnsignedBeta
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$setup = (Resolve-Path -LiteralPath $SetupPath).Path
$stage = (Resolve-Path -LiteralPath $StagePath).Path
$expectedName = "HapticFlight-Bridge-Setup-$Version.exe"
if ((Split-Path -Leaf $setup) -cne $expectedName) { throw "Expected release asset $expectedName." }
if ($AllowUnsignedBeta -and $Version -notmatch '-test\.\d+$') { throw 'An unsigned public Setup must use a test prerelease version.' }

$dirty = @(git -C $repositoryRoot status --porcelain --untracked-files=no)
$untrackedInputs = @(git -C $repositoryRoot ls-files --others --exclude-standard -- README.md docs packaging scripts dcs-hook src tools downloads profiles shared)
if ($untrackedInputs.Count -gt 0) { throw 'Release blocked: untracked package inputs exist.' }
if ($dirty.Count -gt 0) { throw 'Release blocked: the checkout has uncommitted changes.' }
$head = (git -C $repositoryRoot rev-parse HEAD).Trim()
$main = (git -C $repositoryRoot rev-parse origin/main).Trim()
if ($head -ne $main) { throw 'Release blocked: HEAD does not match origin/main.' }

# Inno Setup does not validate every ExpandConstant value at compile time.
# An unsupported constant can crash Setup before the first wizard page appears.
$installerScript = Get-Content -LiteralPath (Join-Path $repositoryRoot 'packaging\HapticFlightBridge.iss') -Raw
if ($installerScript.Contains('IntegrationPage') -or $installerScript.Contains('ExecAsOriginalUser')) {
    throw 'Release blocked: Setup must install only the application; integration belongs in the app.'
}
$supportedConstants = @('app', 'autodesktop', 'group', 'localappdata', 'pf32')
foreach ($constant in [regex]::Matches($installerScript, '(?<!\{)\{([a-z][a-z0-9_]*)\}', 'IgnoreCase')) {
    if ($constant.Groups[1].Value -notin $supportedConstants) {
        throw "Release blocked: unreviewed Inno Setup constant $($constant.Value)."
    }
}

$info = Get-Content -LiteralPath (Join-Path $stage 'package-info.json') -Raw | ConvertFrom-Json
if ($info.version -cne $Version -or $info.sourceCommit -cne $head) {
    throw 'Release blocked: staged version or source commit does not match the candidate.'
}
$expectedFileVersion = if ($Version -match '^([0-9]+\.[0-9]+\.[0-9]+)-test\.([0-9]+)$') {
    "$($Matches[1]).$($Matches[2])"
} else { "$Version.0" }
foreach ($binary in @(
    'HapticFlight Bridge.exe',
    'SimHub\DcsExtraTelemetry.Plugin.dll',
    'Tools\DcsExtraTelemetry.Monitor.exe'
)) {
    $file = Join-Path $stage $binary
    $details = (Get-Item -LiteralPath $file).VersionInfo
    if ($details.ProductVersion -cne $Version -or $details.FileVersion -cne $expectedFileVersion) {
        throw "Release blocked: $binary reports product $($details.ProductVersion), file $($details.FileVersion); expected $Version and $expectedFileVersion."
    }
}
$setupVersion = (Get-Item -LiteralPath $setup).VersionInfo
if ($setupVersion.ProductVersion -cne $Version -or $setupVersion.FileVersion -cne $expectedFileVersion) {
    throw "Release blocked: Setup reports product $($setupVersion.ProductVersion), file $($setupVersion.FileVersion); expected $Version and $expectedFileVersion."
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
    if ($AllowUnsignedBeta) {
        if ($signature.Status -ne 'NotSigned') { throw "Unsigned beta has an unexpected signature: $binary" }
    } else {
        if ($signature.Status -ne 'Valid') { throw "Release binary is not signed and trusted: $binary" }
        $null = $publishers.Add($signature.SignerCertificate.Subject)
    }
}
if (-not $AllowUnsignedBeta -and $publishers.Count -ne 1) { throw 'Release binaries use different signing publishers.' }

$checksum = $setup + '.sha256'
if (-not (Test-Path -LiteralPath $checksum)) { throw 'Setup checksum asset is missing.' }
$expectedDigest = (Get-Content -LiteralPath $checksum).Split(' ')[0]
if ((Get-Content -LiteralPath $checksum -Raw).Trim() -cne "$expectedDigest  $expectedName") {
    throw 'Setup checksum does not name the exact release asset.'
}
if ((Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash -ne $expectedDigest) {
    throw 'Setup checksum does not match the release asset.'
}

if ($AllowUnsignedBeta) {
    Write-Host "PASS: unsigned Setup beta $Version; source, manifest, and checksum verified."
} else {
    Write-Host "PASS: signed Setup release $Version; manifest and checksum verified."
    Write-Host "Publisher: $($publishers | Select-Object -First 1)"
}
