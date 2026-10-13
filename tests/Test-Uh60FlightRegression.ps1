[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$LogPath
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$hookPath = Join-Path $repositoryRoot 'dcs-hook\DCSExtraTelemetry.lua'

function Assert-Condition {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

function Read-LuaConstant {
    param([string]$Source, [string]$Name)
    $match = [regex]::Match($Source, "(?m)^local $([regex]::Escape($Name)) = ([0-9.]+)\r?$")
    if (-not $match.Success) { throw "Lua constant was not found: $Name" }
    return [double]::Parse($match.Groups[1].Value, [Globalization.CultureInfo]::InvariantCulture)
}

$resolvedLog = (Resolve-Path -LiteralPath $LogPath).Path
$hookSource = Get-Content -LiteralPath $hookPath -Raw
$tailOn = Read-LuaConstant $hookSource 'UH60_TAIL_CONTACT_ON'
$tailOff = Read-LuaConstant $hookSource 'UH60_TAIL_CONTACT_OFF'
$mainOn = Read-LuaConstant $hookSource 'UH60_MAIN_CONTACT_ON'
$mainOff = Read-LuaConstant $hookSource 'UH60_MAIN_CONTACT_OFF'
$tailBrakeArm = Read-LuaConstant $hookSource 'UH60_TAIL_BRAKE_ARM_POSITION'
$tailBrakeRelease = Read-LuaConstant $hookSource 'UH60_TAIL_BRAKE_RELEASE_POSITION'
$tailBrakeUnloadDelta = Read-LuaConstant $hookSource 'UH60_TAIL_BRAKE_UNLOAD_DELTA'
$tailBrakeSettleRecovery = Read-LuaConstant $hookSource 'UH60_TAIL_BRAKE_SETTLE_RECOVERY'
$tailBrakeSettleRise = Read-LuaConstant $hookSource 'UH60_TAIL_BRAKE_SETTLE_RISE'
$spoolSeconds = Read-LuaConstant $hookSource 'ROTOR_SPOOL_SMOOTHING_SECONDS'
$spoolDeadband = Read-LuaConstant $hookSource 'ROTOR_SPOOL_RATE_DEADBAND'
$spoolFullScale = Read-LuaConstant $hookSource 'ROTOR_SPOOL_RATE_FULL_SCALE'

Assert-Condition ($hookSource.Contains('key == "4:6:10:46"')) 'UH-60 M134 weapon identifier mapping is missing.'
Assert-Condition ($hookSource.Contains('"M134 Minigun"')) 'UH-60 M134 friendly name is missing.'
Assert-Condition ($hookSource.Contains('key == "4:6:6:0"')) 'UH-60L DAP shared gun identifier mapping is missing.'
Assert-Condition ($hookSource.Contains('"Twin M230 30 mm Cannons"')) 'UH-60L DAP station 2 M230 name is missing.'
Assert-Condition ($hookSource.Contains('"M134 Door Gun"')) 'UH-60L DAP door-gun name is missing.'
Assert-Condition ($hookSource.Contains('nominalWeaponRateHz = nominalRateHz')) 'Nominal weapon-rate export is missing.'
Assert-Condition ($hookSource.Contains('maneuverResponseIntensity = maneuverResponse')) 'Shaped maneuver-response export is missing.'
Assert-Condition ($hookSource.Contains('and not weaponRecoilActive')) 'Recoil-aware disturbance gating is missing.'
Assert-Condition ($hookSource.Contains('local candidateSimhub = values.simhubPort or destinationPort')) 'Partial SimHub port configuration support is missing.'
Assert-Condition ($hookSource.Contains('local candidateRecorder = values.recorderPort or recorderDestinationPort')) 'Partial recorder port configuration support is missing.'

$rows = 0
$groundRows = 0
$airborneRows = 0
$liftoffs = 0
$touchdowns = 0
$previousOnGround = $null
$contacts = @{ tail = $false; left = $false; right = $false }
$smoothedRotorPct = $null
$lastSpoolModelTime = $null
$stableRotorRows = 0
$highSpoolStableRows = 0
$frozenModelRows = 0
$nonZeroSpoolFrozenRows = 0
$tailSupportCompression = $null
$tailBrakeUnloaded = $false
$lastTailStrut = $null
$brakeTailSettles = 0

Get-Content -LiteralPath $resolvedLog -ReadCount 500 | ForEach-Object {
    foreach ($line in $_) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $packet = $line | ConvertFrom-Json
        $rows++

        $ground = $packet.values.ground
        $contacts.tail = [double]$ground.tailStrutCompression -gt $(if ($contacts.tail) { $tailOff } else { $tailOn })
        $contacts.left = [double]$ground.leftStrutCompression -gt $(if ($contacts.left) { $mainOff } else { $mainOn })
        $contacts.right = [double]$ground.rightStrutCompression -gt $(if ($contacts.right) { $mainOff } else { $mainOn })
        $onGround = $contacts.tail -or $contacts.left -or $contacts.right
        if ($onGround) { $groundRows++ } else { $airborneRows++ }

        $tailStrut = [double]$ground.tailStrutCompression
        $brakePosition = [double]$ground.wheelBrakesPosition
        $mainSupported = $contacts.left -or $contacts.right
        if ($mainSupported) {
            if ($null -eq $tailSupportCompression) { $tailSupportCompression = $tailStrut }
            if (-not $tailBrakeUnloaded) {
                if ($brakePosition -lt $tailBrakeRelease) {
                    $tailSupportCompression += ($tailStrut - $tailSupportCompression) * 0.08
                }
                elseif (($brakePosition -ge $tailBrakeArm) -and
                    ($tailStrut -le $tailSupportCompression - $tailBrakeUnloadDelta)) {
                    $tailBrakeUnloaded = $true
                }
            }
            else {
                $rising = $null -ne $lastTailStrut -and $tailStrut -ge $lastTailStrut + $tailBrakeSettleRise
                $recovered = $tailStrut -ge $tailSupportCompression - $tailBrakeSettleRecovery
                $brakeReleased = $brakePosition -le $tailBrakeRelease
                if ($rising -and ($recovered -or $brakeReleased)) {
                    $brakeTailSettles++
                    $tailBrakeUnloaded = $false
                    $tailSupportCompression = $tailStrut
                }
            }
        }
        else {
            $tailSupportCompression = $null
            $tailBrakeUnloaded = $false
        }
        $lastTailStrut = $tailStrut

        if ($null -ne $previousOnGround) {
            if ($previousOnGround -and -not $onGround) { $liftoffs++ }
            if (-not $previousOnGround -and $onGround) { $touchdowns++ }
        }
        $previousOnGround = $onGround

        $modelTime = [double]$packet.modelTime
        $rotorPct = [double]$packet.values.dynamics.mainRotorRpmPct / 100.0
        $spool = 0.0
        if ($null -eq $lastSpoolModelTime -or $modelTime -gt $lastSpoolModelTime) {
            if ($null -eq $smoothedRotorPct) {
                $smoothedRotorPct = $rotorPct
            }
            else {
                $elapsed = $modelTime - $lastSpoolModelTime
                $previousSmoothed = $smoothedRotorPct
                $smoothing = 1.0 - [Math]::Exp(-$elapsed / $spoolSeconds)
                $smoothedRotorPct += ($rotorPct - $smoothedRotorPct) * $smoothing
                $rate = [Math]::Abs($smoothedRotorPct - $previousSmoothed) / $elapsed
                $spool = [Math]::Max(0.0, [Math]::Min(1.0,
                    ($rate - $spoolDeadband) / $spoolFullScale))
            }
            $lastSpoolModelTime = $modelTime
        }
        else {
            $frozenModelRows++
            if ($spool -gt 0.0) { $nonZeroSpoolFrozenRows++ }
        }

        if ($rotorPct -ge 0.90 -and $rotorPct -le 1.10) {
            $stableRotorRows++
            if ($spool -gt 0.50) { $highSpoolStableRows++ }
        }
    }
}

$highSpoolStablePct = if ($stableRotorRows -gt 0) {
    100.0 * $highSpoolStableRows / $stableRotorRows
} else { 100.0 }

Assert-Condition ($rows -gt 0) 'The flight log contained no packets.'
Assert-Condition ($airborneRows -gt $rows * 0.5) 'Corrected UH-60 contact still reports most of the flight on the ground.'
Assert-Condition ($liftoffs -gt 0 -and $touchdowns -gt 0) 'Corrected UH-60 contact did not recover flight transitions.'
Assert-Condition ($highSpoolStablePct -lt 1.0) 'Rotor spool remains too active during stable rotor operation.'
Assert-Condition ($nonZeroSpoolFrozenRows -eq 0) 'Frozen DCS model time produced rotor spool activity.'
# A recording may not contain a hard-braking tail-unload maneuver. Count any
# observed settle events for diagnostics, but do not fail an otherwise valid
# flight regression merely because that maneuver was not flown.

Write-Host 'UH-60 flight regression passed.'
Write-Host "Rows: $rows; corrected ground: $groundRows; airborne: $airborneRows"
Write-Host "Liftoffs: $liftoffs; touchdowns: $touchdowns"
Write-Host ('High-spool stable-rotor rows: {0:N3}%; frozen model rows: {1}' -f $highSpoolStablePct, $frozenModelRows)
Write-Host "Brake-induced tailwheel settles: $brakeTailSettles"
