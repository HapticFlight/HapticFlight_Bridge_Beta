[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$packageRoot = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $packageRoot 'manifest.sha256'

if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw 'manifest.sha256 is missing.'
}

$failures = [System.Collections.Generic.List[string]]::new()
foreach ($line in Get-Content -LiteralPath $manifestPath) {
    if (-not $line.Trim()) { continue }
    if ($line -notmatch '^([0-9A-Fa-f]{64})  (.+)$') {
        $failures.Add("Malformed manifest line: $line")
        continue
    }

    $expected = $Matches[1].ToUpperInvariant()
    $relativePath = $Matches[2]
    $path = Join-Path $packageRoot $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        $failures.Add("Missing: $relativePath")
        continue
    }

    $actual = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    if ($actual -ne $expected) {
        $failures.Add("Hash mismatch: $relativePath")
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    throw 'Package verification failed.'
}

Write-Host 'PASS: Every packaged file matches manifest.sha256.'
