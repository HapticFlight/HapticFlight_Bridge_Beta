$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$xaml = Get-Content -LiteralPath (Join-Path $repoRoot 'tools\DcsExtraTelemetry.Launcher\MainWindow.xaml') -Raw
$code = (Get-Content -LiteralPath (Join-Path $repoRoot 'tools\DcsExtraTelemetry.Launcher\MainWindow.xaml.cs') -Raw) +
    (Get-Content -LiteralPath (Join-Path $repoRoot 'tools\DcsExtraTelemetry.Launcher\MainWindow.Dashboard.cs') -Raw)

foreach ($element in @('StartRecordingButton', 'StopRecordingButton', 'OpenRecordingsButton_Click', 'RecentFlightsPanel', 'RefreshRecentFlightsButton')) {
    if ($xaml -notmatch [regex]::Escape('x:Name="' + $element + '"') -and $code -notmatch [regex]::Escape($element)) {
        throw "Missing recorder workflow element: $element"
    }
}

if ($code -notmatch 'SetRecorderStatus\("Recording saved · " \+ Path.GetFileName\(completedRecording\), "Success"\)') {
    throw 'Stopping a recording must save it without launching analysis.'
}

if ($code -notmatch 'OpenRecordingsButton_Click\(sender, e\);') {
    throw 'Saved-flight actions must open the recordings folder.'
}

Write-Host 'PASS: launcher preserves recorder and recordings-folder workflow without analyzer launch.'
