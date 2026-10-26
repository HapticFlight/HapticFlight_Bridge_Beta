[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+(?:-test\.\d+)?$')]
    [string]$Version = '0.1.0-test.147',

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$')]
    [string]$Repository,

    [string]$CompilerPath,
    [string]$OutputDirectory,
    [string]$SigningThumbprint,
    [string]$SignToolPath,
    [switch]$AllowUnsignedBeta,
    [switch]$SkipBuild,
    [string]$TimestampUrl = 'http://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repositoryRoot 'dist\setup' }
$outputRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
$stageRoot = Join-Path $outputRoot ("stage-$Version")
$bundleRoot = Join-Path $outputRoot 'bundle'
if ($AllowUnsignedBeta -and $SigningThumbprint) { throw 'Choose signing or an unsigned beta, not both.' }
if ($AllowUnsignedBeta -and $Version -notmatch '-test\.\d+$') { throw 'An unsigned public Setup must use a test prerelease version.' }
$setupFileBase = if ($SigningThumbprint -or $AllowUnsignedBeta) { "HapticFlight-Bridge-Setup-$Version" } else { "HapticFlight-Bridge-Setup-$Version-preview" }
$installerPath = Join-Path $outputRoot ($setupFileBase + '.exe')
foreach ($path in @($stageRoot, $bundleRoot, $installerPath)) {
    if (Test-Path -LiteralPath $path) { throw "Setup output already exists: $path" }
}

if (-not $CompilerPath) {
    $candidates = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
    )
    $CompilerPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $CompilerPath -or -not (Test-Path -LiteralPath $CompilerPath -PathType Leaf)) {
    throw 'Install Inno Setup 6.7.3 or provide -CompilerPath to build the EXE.'
}
# The same staged files produced a Defender detection with Inno Setup 7.1.0,
# while the official 6.7.3 compiler produced a clean local scan. Pin the exact
# tested compiler instead of silently changing the installer stub on release.
$expectedCompilerHash = '0A8757031B33777E4C9CBFFEE40F11A5062B36D25CBE144C1DB73B6102B80AD7'
$compilerHash = (Get-FileHash -LiteralPath $CompilerPath -Algorithm SHA256).Hash
if ($compilerHash -ne $expectedCompilerHash) {
    throw "Untested Inno Setup compiler: $CompilerPath (SHA-256 $compilerHash). Expected official Inno Setup 6.7.3."
}

$publicBetaArguments = @{
    Version = $Version
    Repository = $Repository
    OutputDirectory = $bundleRoot
}
if ($SkipBuild) { $publicBetaArguments.SkipBuild = $true }
& (Join-Path $PSScriptRoot 'Package-PublicBeta.ps1') @publicBetaArguments
$sourceRoot = Join-Path $bundleRoot ("DcsExtraTelemetry-$Version")
New-Item -ItemType Directory -Path $stageRoot | Out-Null
foreach ($directory in @('DCS', 'SimHub', 'Profiles', 'Docs')) {
    Copy-Item -LiteralPath (Join-Path $sourceRoot $directory) -Destination (Join-Path $stageRoot $directory) -Recurse
}
New-Item -ItemType Directory -Path (Join-Path $stageRoot 'Tools') | Out-Null
Copy-Item -LiteralPath (Join-Path $sourceRoot 'Tools\DcsExtraTelemetry.Monitor.exe') -Destination (Join-Path $stageRoot 'Tools\DcsExtraTelemetry.Monitor.exe')
Copy-Item -LiteralPath (Join-Path $sourceRoot 'Tools\DcsExtraTelemetry.Launcher.exe') -Destination (Join-Path $stageRoot 'HapticFlight Bridge.exe')
Copy-Item -LiteralPath (Join-Path $sourceRoot 'package-info.json') -Destination (Join-Path $stageRoot 'package-info.json')
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'packaging\SETUP-README.md') -Destination (Join-Path $stageRoot 'README-FIRST.md')
Remove-Item -LiteralPath (Join-Path $stageRoot 'Docs\GETTING_STARTED.md')

if ($SigningThumbprint) {
    if ($SigningThumbprint -notmatch '^[0-9A-Fa-f]{40}$') { throw 'SigningThumbprint must be a SHA-1 certificate thumbprint.' }
    if (-not $SignToolPath -or -not (Test-Path -LiteralPath $SignToolPath -PathType Leaf)) {
        throw 'Provide -SignToolPath when building a signed release.'
    }
    foreach ($binary in @(
        (Join-Path $stageRoot 'HapticFlight Bridge.exe'),
        (Join-Path $stageRoot 'Tools\DcsExtraTelemetry.Monitor.exe'),
        (Join-Path $stageRoot 'SimHub\DcsExtraTelemetry.Plugin.dll')
    )) {
        & $SignToolPath sign /sha1 $SigningThumbprint /fd SHA256 /tr $TimestampUrl /td SHA256 $binary
        if ($LASTEXITCODE -ne 0) { throw "Signing failed: $binary" }
        $signature = Get-AuthenticodeSignature -LiteralPath $binary
        if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Thumbprint -ne $SigningThumbprint) {
            throw "Signature verification failed: $binary"
        }
    }
}
elseif ($AllowUnsignedBeta) {
    foreach ($binary in @(
        (Join-Path $stageRoot 'HapticFlight Bridge.exe'),
        (Join-Path $stageRoot 'Tools\DcsExtraTelemetry.Monitor.exe'),
        (Join-Path $stageRoot 'SimHub\DcsExtraTelemetry.Plugin.dll')
    )) {
        if ((Get-AuthenticodeSignature -LiteralPath $binary).Status -ne 'NotSigned') {
            throw "Unsigned beta contains a signed or invalidly signed binary: $binary"
        }
    }
}

$manifestPath = Join-Path $stageRoot 'manifest.sha256'
Get-ChildItem -LiteralPath $stageRoot -File -Recurse | Sort-Object FullName | ForEach-Object {
    $relative = $_.FullName.Substring($stageRoot.Length + 1)
    $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
    "$hash  $relative"
} | Set-Content -LiteralPath $manifestPath -Encoding ASCII

$compilerArgs = @(
    "/DStageRoot=$stageRoot",
    "/DProductVersion=$Version",
    "/DOutputRoot=$outputRoot",
    "/DSetupFileBase=$setupFileBase"
)
if ($SigningThumbprint) {
    $signCommand = '"' + [System.IO.Path]::GetFullPath($SignToolPath) + '" sign /sha1 ' + $SigningThumbprint + ' /fd SHA256 /tr ' + $TimestampUrl + ' /td SHA256 $f'
    $compilerArgs += '/DReleaseSigned=1'
    $compilerArgs += "/Sbridge=$signCommand"
}
$compilerArgs += (Join-Path $repositoryRoot 'packaging\HapticFlightBridge.iss')
& $CompilerPath @compilerArgs
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $installerPath)) {
    throw 'Inno Setup did not produce the expected installer.'
}
if ($SigningThumbprint) {
    $signature = Get-AuthenticodeSignature -LiteralPath $installerPath
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Thumbprint -ne $SigningThumbprint) {
        throw 'Setup signature verification failed.'
    }
}
elseif ($AllowUnsignedBeta -and (Get-AuthenticodeSignature -LiteralPath $installerPath).Status -ne 'NotSigned') {
    throw 'Unsigned beta Setup has an unexpected signature.'
}
& (Join-Path $PSScriptRoot 'Test-DefenderArtifact.ps1') -Path $stageRoot
& (Join-Path $PSScriptRoot 'Test-DefenderArtifact.ps1') -Path $installerPath
$digest = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash
"$digest  $(Split-Path -Leaf $installerPath)" | Set-Content -LiteralPath ($installerPath + '.sha256') -Encoding ASCII
Write-Host "Setup: $installerPath"
Write-Host "SHA-256: $digest"
