[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+(?:-test\.\d+)?$')]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$PackagePath
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$expectedPackageName = "DcsExtraTelemetry-$Version.zip"
$packagePath = (Resolve-Path -LiteralPath $PackagePath).Path

if ((Split-Path -Leaf $packagePath) -ne $expectedPackageName) {
    throw "Release ZIP must be named $expectedPackageName."
}

$dirty = @(git -C $repositoryRoot status --porcelain)
if ($dirty.Count -gt 0) {
    throw "Release blocked: the candidate checkout has uncommitted changes. Commit or discard them before packaging."
}

$head = (git -C $repositoryRoot rev-parse HEAD).Trim()
$main = (git -C $repositoryRoot rev-parse origin/main).Trim()
if ($head -ne $main) {
    throw "Release blocked: HEAD ($head) does not match origin/main ($main)."
}

foreach ($assemblyInfo in @(
    'src\DcsExtraTelemetry.Plugin\Properties\AssemblyInfo.cs',
    'tools\DcsExtraTelemetry.Launcher\Properties\AssemblyInfo.cs'
)) {
    $content = Get-Content -LiteralPath (Join-Path $repositoryRoot $assemblyInfo) -Raw
    if ($content -notmatch [regex]::Escape('AssemblyInformationalVersion("' + $Version + '")')) {
        throw "Release blocked: $assemblyInfo does not identify $Version."
    }
}

$sfxPath = Join-Path (Split-Path -Parent $packagePath) ("DcsExtraTelemetry-$Version.exe")
if (Test-Path -LiteralPath $sfxPath) {
    throw "Release blocked: self-extracting wrapper detected: $sfxPath. Publish the ZIP only."
}

$verificationRoot = Join-Path $env:TEMP ("DcsExtraTelemetry-release-gate-" + [Guid]::NewGuid().ToString('N'))
try {
    Expand-Archive -LiteralPath $packagePath -DestinationPath $verificationRoot -Force
    $packageRoot = Join-Path $verificationRoot ("DcsExtraTelemetry-" + $Version)
    if (-not (Test-Path -LiteralPath $packageRoot -PathType Container)) {
        throw "Release blocked: expected package root is missing: $packageRoot"
    }

    & (Join-Path $packageRoot 'Scripts\Verify-Package.ps1')

    $defender = Join-Path $env:ProgramFiles 'Windows Defender\MpCmdRun.exe'
    if (-not (Test-Path -LiteralPath $defender)) {
        throw "Release blocked: Microsoft Defender command-line scanner is unavailable."
    }
    & $defender -Scan -ScanType 3 -File $packagePath
    if ($LASTEXITCODE -ne 0) { throw "Release blocked: Defender ZIP scan returned $LASTEXITCODE." }
    & $defender -Scan -ScanType 3 -File $packageRoot
    if ($LASTEXITCODE -ne 0) { throw "Release blocked: Defender extracted-package scan returned $LASTEXITCODE." }

    $hash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash
    [pscustomobject]@{
        Version = $Version
        Commit = $head
        Package = $packagePath
        Sha256 = $hash
        Wrapper = 'none'
        Manifest = 'verified'
        Defender = 'ZIP and extracted package passed'
    } | Format-List
}
finally {
    if (Test-Path -LiteralPath $verificationRoot) {
        Remove-Item -LiteralPath $verificationRoot -Recurse -Force
    }
}
