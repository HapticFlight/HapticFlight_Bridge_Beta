[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'Medium')]
param(
    [string[]]$DcsProfilePath,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')

if (Get-Process -Name 'DCS' -ErrorAction SilentlyContinue) {
    throw 'Close DCS before removing the Hook.'
}

$packageRoot = Split-Path -Parent $PSScriptRoot
$source = Join-Path $packageRoot 'DCS\DCSExtraTelemetry.lua'
$sourceHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
$profiles = @(Find-DcsProfiles -RequestedPaths $DcsProfilePath)

foreach ($profile in $profiles) {
    $destination = Join-Path $profile 'Scripts\Hooks\DCSExtraTelemetry.lua'
    if (-not (Test-Path -LiteralPath $destination -PathType Leaf)) {
        Write-Host "Not installed: $destination"
        continue
    }

    $installedHash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
    if ($installedHash -ne $sourceHash -and -not $Force) {
        throw "The installed Hook differs from this package. Refusing to remove it without -Force: $destination"
    }

    if ($PSCmdlet.ShouldProcess($destination, 'Remove only the DCS Extra Telemetry Hook')) {
        Remove-Item -LiteralPath $destination -Force
        Write-Host "Removed: $destination"
    }
}

Write-Host 'No directory, Export.lua, or third-party Hook was removed.'
