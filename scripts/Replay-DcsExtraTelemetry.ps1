[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory = $true)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string]$InputPath,

    [ValidateRange(1, 65535)]
    [int]$Port = 29381,

    [ValidateRange(0, 20)]
    [double]$Speed = 1,

    [switch]$Loop
)

<#
.SYNOPSIS
Replays a DCS Extra Telemetry JSONL recording to the local SimHub receiver.

.DESCRIPTION
Use this only while DCS is stopped. It sends the original JSON frames to the
loopback UDP receiver, allowing property and ShakeIt formula checks without a
live mission. Speed 0 sends frames as fast as possible; Speed 1 preserves the
recorded cadence; higher values accelerate playback.
#>

$resolvedInput = (Resolve-Path -LiteralPath $InputPath).Path
$destination = [System.Net.IPEndPoint]::new([System.Net.IPAddress]::Loopback, $Port)
$client = [System.Net.Sockets.UdpClient]::new()

try {
    do {
        $previousSentAtMs = $null
        $sent = 0
        Get-Content -LiteralPath $resolvedInput | ForEach-Object {
            $line = $_
            if ([string]::IsNullOrWhiteSpace($line)) { return }
            $frame = $line | ConvertFrom-Json
            if ($previousSentAtMs -ne $null -and $Speed -gt 0) {
                $delayMs = [Math]::Max(0, ($frame.sentAtMs - $previousSentAtMs) / $Speed)
                if ($delayMs -gt 0) { Start-Sleep -Milliseconds ([int][Math]::Min($delayMs, 1000)) }
            }
            $previousSentAtMs = $frame.sentAtMs
            $bytes = [System.Text.Encoding]::UTF8.GetBytes($line)
            if ($PSCmdlet.ShouldProcess("127.0.0.1:$Port", "Send telemetry frame $($frame.sequence)")) {
                [void]$client.Send($bytes, $bytes.Length, $destination)
                $sent++
            }
        }
        Write-Host "Replayed $sent telemetry frames to 127.0.0.1:$Port."
    } while ($Loop)
}
finally {
    $client.Dispose()
}
