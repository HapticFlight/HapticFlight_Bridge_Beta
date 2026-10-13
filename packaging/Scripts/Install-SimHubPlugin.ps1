[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$SimHubPath,
    [switch]$ReplaceExisting
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')

if (Get-Process -Name 'SimHubWPF' -ErrorAction SilentlyContinue) {
    throw 'Close SimHub before installing the plugin.'
}

$packageRoot = Split-Path -Parent $PSScriptRoot
$resolvedSimHubPath = Find-SimHubPath -RequestedPath $SimHubPath
$files = @('DcsExtraTelemetry.Plugin.dll', 'DcsExtraTelemetry.Plugin.pdb')

foreach ($file in $files) {
    $source = Join-Path $packageRoot "SimHub\$file"
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
        if ($file.EndsWith('.pdb')) { continue }
        throw "Package file is missing: $source"
    }

    $destination = Join-Path $resolvedSimHubPath $file
    if (Test-Path -LiteralPath $destination -PathType Leaf) {
        $sourceHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
        $existingHash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
        if ($sourceHash -eq $existingHash) {
            Write-Host "Already installed: $destination"
            continue
        }
        if (-not $ReplaceExisting) {
            throw "A different $file already exists. Re-run with -ReplaceExisting to back it up and replace it."
        }

        $backupFolder = Join-Path $packageRoot ('Backups\SimHub-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))
        New-Item -ItemType Directory -Path $backupFolder -Force | Out-Null
        Copy-Item -LiteralPath $destination -Destination (Join-Path $backupFolder $file)
    }

    if ($PSCmdlet.ShouldProcess($destination, "Install $file")) {
        $temporary = Join-Path $resolvedSimHubPath ($file + '.' + [guid]::NewGuid().ToString('N') + '.tmp')
        Copy-Item -LiteralPath $source -Destination $temporary
        Move-Item -LiteralPath $temporary -Destination $destination -Force
        Write-Host "Installed: $destination"
    }
}

Write-Host 'Only DcsExtraTelemetry.Plugin.dll and its optional PDB were written.'
