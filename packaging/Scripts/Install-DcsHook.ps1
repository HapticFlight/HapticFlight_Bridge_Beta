[CmdletBinding(SupportsShouldProcess)]
param(
    [string[]]$DcsProfilePath,
    [switch]$ReplaceExisting
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')

if (Get-Process -Name 'DCS' -ErrorAction SilentlyContinue) {
    throw 'Close DCS before installing the Hook.'
}

$packageRoot = Split-Path -Parent $PSScriptRoot
$source = Join-Path $packageRoot 'DCS\DCSExtraTelemetry.lua'
$profiles = @(Find-DcsProfiles -RequestedPaths $DcsProfilePath)
if ($profiles.Count -eq 0) {
    throw 'No DCS profiles were found. Pass -DcsProfilePath with the Saved Games DCS profile folder.'
}

$sourceHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
foreach ($profile in $profiles) {
    $scriptsPath = Join-Path $profile 'Scripts'
    $hooksPath = Join-Path $scriptsPath 'Hooks'
    $destination = Join-Path $hooksPath 'DCSExtraTelemetry.lua'

    if (-not (Test-Path -LiteralPath $scriptsPath -PathType Container)) {
        New-Item -ItemType Directory -Path $scriptsPath -Force | Out-Null
    }

    $before = @(Get-HashRecords -Root $scriptsPath -ExcludedPaths @($destination) -Recurse)

    if (Test-Path -LiteralPath $destination -PathType Leaf) {
        $existingHash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
        if ($existingHash -eq $sourceHash) {
            Write-Host "Already installed: $destination"
            continue
        }
        if (-not $ReplaceExisting) {
            throw "A different DCSExtraTelemetry.lua already exists at $destination. Re-run with -ReplaceExisting to back it up and replace it."
        }

        $backupFolder = Join-Path $packageRoot ('Backups\DCS-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))
        New-Item -ItemType Directory -Path $backupFolder -Force | Out-Null
        Copy-Item -LiteralPath $destination -Destination (Join-Path $backupFolder ($profile.Split('\')[-1] + '-DCSExtraTelemetry.lua'))
    }

    if ($PSCmdlet.ShouldProcess($destination, 'Install isolated DCS GameGUI Hook')) {
        New-Item -ItemType Directory -Path $hooksPath -Force | Out-Null
        $temporary = Join-Path $hooksPath ('DCSExtraTelemetry.' + [guid]::NewGuid().ToString('N') + '.tmp')
        Copy-Item -LiteralPath $source -Destination $temporary
        Move-Item -LiteralPath $temporary -Destination $destination -Force
    }

    $after = @(Get-HashRecords -Root $scriptsPath -ExcludedPaths @($destination) -Recurse)
    $differences = @(Compare-HashRecords -Before $before -After $after)
    if ($differences.Count -gt 0) {
        $differences | Format-Table -AutoSize
        throw 'Safety check failed: an unrelated DCS Scripts file changed.'
    }

    Write-Host "Installed: $destination"
}

Write-Host 'Export.lua was not read, written, reordered, or used.'
