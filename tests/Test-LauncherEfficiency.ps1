[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$analyzer = Get-Content -LiteralPath (Join-Path $repositoryRoot 'tools\DcsExtraTelemetry.Launcher\TelemetryRecordingAnalyzer.cs') -Raw
$window = Get-Content -LiteralPath (Join-Path $repositoryRoot 'tools\DcsExtraTelemetry.Launcher\MainWindow.xaml.cs') -Raw
$charts = Get-Content -LiteralPath (Join-Path $repositoryRoot 'tools\DcsExtraTelemetry.Launcher\RecordingAnalysisCharts.cs') -Raw
$xaml = Get-Content -LiteralPath (Join-Path $repositoryRoot 'tools\DcsExtraTelemetry.Launcher\MainWindow.xaml') -Raw

function Assert-Contains([string]$Text, [string]$Value, [string]$Message) {
    if (-not $Text.Contains($Value)) { throw $Message }
}

function Assert-NotContains([string]$Text, [string]$Value, [string]$Message) {
    if ($Text.Contains($Value)) { throw $Message }
}

Assert-Contains $analyzer 'FindBounds(path, out firstMs, out lastMs)' 'The analyzer is missing its lightweight bounds pass.'
Assert-Contains $analyzer 'ScanScalars(line, values, out aircraft)' 'The analyzer is missing its single-pass scalar reader.'
Assert-Contains $analyzer 'BuildLoadMetrics(result, tracks, bucketCount, bucketSeconds)' 'The analyzer is not precomputing load metrics.'
Assert-NotContains $analyzer 'List<EffectMapSample>' 'The analyzer still retains every recording sample.'
Assert-Contains $charts 'private DrawingGroup cachedChart;' 'The recording charts are missing cached static drawings.'
Assert-Contains $charts 'Result.CategoryLoads[g][b]' 'The combined-load chart still recomputes category totals.'
Assert-Contains $charts 'public bool HideUnusedEffects' 'The recording charts are missing the unused-effect filter.'
Assert-Contains $xaml 'x:Name="HideUnusedEffectsCheckBox"' 'The Flights tab is missing the unused-effect filter control.'
Assert-Contains $window 'HideUnusedEffectsCheckBox_Changed' 'The unused-effect filter is not wired to the recording charts.'
Assert-NotContains $xaml 'x:Name="FlightOverviewCanvas"' 'The collapsed legacy whole-flight chart still exists.'

$refreshStart = $window.IndexOf('private void RefreshPlaybackFrame()', [StringComparison]::Ordinal)
$refreshEnd = $window.IndexOf('private void ReplayTimer_Tick', $refreshStart, [StringComparison]::Ordinal)
if ($refreshStart -lt 0 -or $refreshEnd -le $refreshStart) { throw 'Could not inspect the replay refresh method.' }
$refreshBody = $window.Substring($refreshStart, $refreshEnd - $refreshStart)
Assert-NotContains $refreshBody 'Children.Clear()' 'Replay still reconstructs WPF controls every timer tick.'
Assert-NotContains $refreshBody 'Buckets.Count' 'Replay still scans whole effect tracks every timer tick.'

Write-Output 'Launcher efficiency structural checks passed.'
