param(
    [int]$Port = 29381,
    [int]$Seconds = 10,
    [int]$RateHz = 60
)

$ErrorActionPreference = 'Stop'

if ($Port -lt 1 -or $Port -gt 65535) {
    throw 'Port must be between 1 and 65535.'
}

if ($Seconds -lt 1 -or $RateHz -lt 1) {
    throw 'Seconds and RateHz must be positive.'
}

$client = [System.Net.Sockets.UdpClient]::new()
$endpoint = [System.Net.IPEndPoint]::new([System.Net.IPAddress]::Loopback, $Port)
$intervalMilliseconds = 1000.0 / $RateHz
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$sequence = 0
$sessionId = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds().ToString()
$nextSendAtMilliseconds = 0.0
$invariantCulture = [System.Globalization.CultureInfo]::InvariantCulture
$packetTemplate = '{{"protocol":1,"sessionId":"{0}","sequence":{1},"sentAtMs":{2},"modelTime":{3},' +
    '"aircraft":{{"dcsType":"TEST_AIRCRAFT"}},' +
    '"capabilities":{{"acceleration":true,"angularVelocity":true,"airData":true,"engineRpm":true,' +
    '"gear":true,"flaps":true,"speedbrake":true,"shakeAmplitude":true,' +
    '"weaponPayload":true,"weaponEvents":true}},' +
    '"values":{{"acceleration":{{"x":{4},"y":{5},"z":{6}}},' +
    '"angularVelocity":{{"x":{7},"y":{8},"z":{9}}},' +
    '"indicatedAirSpeedMps":{10},"trueAirSpeedMps":{11},"verticalVelocityMps":{12},' +
    '"angleOfAttackRad":{13},"mach":{14},"shakeAmplitude":{15},' +
    '"engineRpmPct":{{"left":{16},"right":{17}}},' +
    '"weapons":{{"selectedStation":2,"selectedWeaponName":"TEST_ROCKET",' +
    '"totalPayloadCount":6,"cannonShells":497,"weaponFirePulse":1,"gunFirePulse":1,' +
    '"payloadReleasePulse":0,"gunRoundsFired":3,"payloadReleasedCount":0,"weaponEventCounter":7}},' +
    '"mechanical":{{"gearPosition":{18},"flapsPosition":{19},"speedbrakePosition":{20}}}}}}}'

try {
    Write-Host "Sending test DCS telemetry to 127.0.0.1:$Port for $Seconds seconds..."

    while ($stopwatch.Elapsed.TotalSeconds -lt $Seconds) {
        $remainingMilliseconds = $nextSendAtMilliseconds - $stopwatch.Elapsed.TotalMilliseconds
        if ($remainingMilliseconds -gt 1.0) {
            Start-Sleep -Milliseconds ([Math]::Max(1, [int][Math]::Floor($remainingMilliseconds)))
            continue
        }

        $phase = $stopwatch.Elapsed.TotalSeconds * 2.0
        $elapsedMilliseconds = $stopwatch.Elapsed.TotalMilliseconds
        $elapsedSeconds = $stopwatch.Elapsed.TotalSeconds
        $json = [string]::Format($invariantCulture, $packetTemplate, [object[]]@(
            $sessionId,
            $sequence,
            $elapsedMilliseconds,
            $elapsedSeconds,
            ([Math]::Sin($phase) * 0.1),
            (1.0 + ([Math]::Sin($phase * 0.5) * 0.05)),
            ([Math]::Cos($phase) * 0.1),
            ([Math]::Sin($phase * 0.2) * 0.02),
            ([Math]::Cos($phase * 0.2) * 0.02),
            0.0,
            (120.0 + [Math]::Sin($phase) * 5.0),
            (125.0 + [Math]::Sin($phase) * 5.0),
            ([Math]::Sin($phase * 0.5) * 2.0),
            0.12,
            0.65,
            ([Math]::Abs([Math]::Sin($phase)) * 0.15),
            82.0,
            83.0,
            1.0,
            0.5,
            0.25
        ))
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
        [void]$client.Send($bytes, $bytes.Length, $endpoint)
        $sequence++
        $nextSendAtMilliseconds += $intervalMilliseconds

        if ($nextSendAtMilliseconds -lt $stopwatch.Elapsed.TotalMilliseconds - $intervalMilliseconds) {
            $nextSendAtMilliseconds = $stopwatch.Elapsed.TotalMilliseconds
        }
    }
}
finally {
    $client.Dispose()
}

Write-Host "Sent $sequence packets."
