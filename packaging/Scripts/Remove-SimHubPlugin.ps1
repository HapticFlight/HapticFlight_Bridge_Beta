[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'Medium')]
param(
    [string]$SimHubPath,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')

if (Get-Process -Name 'SimHubWPF' -ErrorAction SilentlyContinue) {
    throw 'Close SimHub before removing the plugin.'
}

$packageRoot = Split-Path -Parent $PSScriptRoot
$resolvedSimHubPath = Find-SimHubPath -RequestedPath $SimHubPath
foreach ($file in @('DcsExtraTelemetry.Plugin.dll', 'DcsExtraTelemetry.Plugin.pdb')) {
    $source = Join-Path $packageRoot "SimHub\$file"
    $destination = Join-Path $resolvedSimHubPath $file
    if (-not (Test-Path -LiteralPath $destination -PathType Leaf)) {
        continue
    }

    if ((Test-Path -LiteralPath $source -PathType Leaf) -and -not $Force) {
        $sourceHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
        $installedHash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
        if ($sourceHash -ne $installedHash) {
            throw "The installed $file differs from this package. Refusing to remove it without -Force."
        }
    }

    if ($PSCmdlet.ShouldProcess($destination, "Remove only $file")) {
        Remove-Item -LiteralPath $destination -Force
        Write-Host "Removed: $destination"
    }
}

Write-Host 'No other SimHub plugin, configuration, profile, or file was removed.'
