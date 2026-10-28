$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$code = Get-Content -LiteralPath (Join-Path $repositoryRoot 'tools\DcsExtraTelemetry.Launcher\MainWindow.xaml.cs') -Raw

if ($code -notmatch 'recorderProcess\.Start\(\)') { throw 'Recorder process is not started by the launcher.' }
if ($code -notmatch 'recorderStopSignal\.Set\(\)') { throw 'Recorder stop signal is not sent by the launcher.' }
if ($code -notmatch 'GetRecordingsFolder\(\)') { throw 'Launcher does not retain its recordings-folder workflow.' }

Write-Host 'PASS: recorder lifecycle and recordings folder remain available.'
