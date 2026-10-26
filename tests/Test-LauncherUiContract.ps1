$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$xamlPath = Join-Path $repoRoot 'tools\DcsExtraTelemetry.Launcher\MainWindow.xaml'
$codePath = Join-Path $repoRoot 'tools\DcsExtraTelemetry.Launcher\MainWindow.xaml.cs'
$dashboardCodePath = Join-Path $repoRoot 'tools\DcsExtraTelemetry.Launcher\MainWindow.Dashboard.cs'
$paletteCodePath = Join-Path $repoRoot 'tools\DcsExtraTelemetry.Launcher\GlassThemePalette.cs'
$appXamlPath = Join-Path $repoRoot 'tools\DcsExtraTelemetry.Launcher\App.xaml'
$appCodePath = Join-Path $repoRoot 'tools\DcsExtraTelemetry.Launcher\App.xaml.cs'
$chartCodePath = Join-Path $repoRoot 'tools\DcsExtraTelemetry.Launcher\RecordingAnalysisCharts.cs'
$projectPath = Join-Path $repoRoot 'tools\DcsExtraTelemetry.Launcher\DcsExtraTelemetry.Launcher.csproj'
$xaml = Get-Content -LiteralPath $xamlPath -Raw
$code = (Get-Content -LiteralPath $codePath -Raw) + (Get-Content -LiteralPath $dashboardCodePath -Raw)
$paletteCode = Get-Content -LiteralPath $paletteCodePath -Raw
$appXaml = Get-Content -LiteralPath $appXamlPath -Raw
$appCode = Get-Content -LiteralPath $appCodePath -Raw
$chartCode = Get-Content -LiteralPath $chartCodePath -Raw
$projectXml = Get-Content -LiteralPath $projectPath -Raw

$requiredXaml = @(
    'HomeRequiredActionCard',
    'RecordingSelectorComboBox',
    'AnalysisEffectsFilterButton',
    'AnalysisEffectSearchTextBox',
    'AnalysisEffectCategoryPanel',
    'AnalysisEffectChecklistPanel',
    'TimelineTabPanel',
    'PhaseTabPanel',
    'LoadTabPanel',
    'AnalysisHoverInspectorText',
    'AnalysisEmptyStateText',
    'NewRecordingButton',
    'SelectedEffectPreviousButton',
    'SelectedEffectNextButton',
    'ExportEffectReportButton',
    'TelemetryTabButton',
    'TelemetrySignalComboBox',
    'TelemetryCanvas',
    'DebriefTabButton',
    'DebriefEventsGrid',
    'CompareRecordingComboBox',
    'TuningEvidenceText',
    'TuningBoundaryText',
    'FlightDashboard',
    'ReplayDashboardCard',
    'SelectedEffectStatsText',
    'MyAircraftPanel',
    'AircraftPage',
    'AircraftNavButton'
)

foreach ($name in $requiredXaml) {
    if ($xaml -notmatch [regex]::Escape('x:Name="' + $name + '"')) {
        throw "Missing required launcher UI element: $name"
    }
}

if ($xaml -notmatch 'HideUnusedEffectsCheckBox[^>]+IsChecked="True"') {
    throw 'Unused effects must be hidden by default.'
}

$requiredHandlers = @(
    'SetAnalysisTab',
    'PopulateAnalysisEffectFilter',
    'VisibleEffectNames',
    'RecordingSelectorComboBox_SelectionChanged',
    'HomeRequiredActionButton_Click',
    'NewRecordingButton_Click',
    'SelectedEffectPreviousButton_Click',
    'SelectedEffectNextButton_Click',
    'SelectAdjacentEffect',
    'ExportEffectReportButton_Click',
    'PopulateTelemetrySignals',
    'PopulateDebrief',
    'CopySimHubHandoffButton_Click',
    'CopyTuningWorksheetButton_Click',
    'CompareRecordingComboBox_SelectionChanged',
    'ReturnToRecorder',
    'RefreshDashboard',
    'RenderDashboard'
)

foreach ($handler in $requiredHandlers) {
    if ($code -notmatch [regex]::Escape($handler + '(')) {
        throw "Missing required launcher UI behavior: $handler"
    }
}

if ($xaml -notmatch 'x:Name="OverviewPage"' -or $xaml -match 'x:Name="OverviewPage"[^>]+MinHeight=') {
    throw 'The dashboard must size to its content instead of expanding into unused vertical space.'
}

$flightDashboardMatch = [regex]::Match($xaml, '<Grid x:Name="FlightDashboard"[\s\S]*?<Grid.RowDefinitions>([\s\S]*?)</Grid.RowDefinitions>')
if (-not $flightDashboardMatch.Success -or ([regex]::Matches($flightDashboardMatch.Groups[1].Value, '<RowDefinition')).Count -lt 9) {
    throw 'Flights dashboard requires rows through Debrief; out-of-range grid rows overlap the selected-effect panel.'
}

if ($xaml -match 'DropShadowEffect' -or $xaml -match 'Glow\}' -or $appCode -match 'DropShadowEffect') {
    throw 'The dark glass design uses flat status lights; glow effects must not return.'
}

# FindResource throws "resource not found" at runtime for a missing or null resource, and
# only on the code path that reaches it (test.129 crashed once setup was fully installed).
$glassStylesXaml = Get-Content -LiteralPath (Join-Path $repoRoot 'tools\DcsExtraTelemetry.Launcher\GlassStyles.xaml') -Raw
$definedResources = @{}
foreach ($match in [regex]::Matches($paletteCode, '\{ "([A-Za-z]+)", "#')) { $definedResources[$match.Groups[1].Value] = $true }
foreach ($match in [regex]::Matches($appXaml + $glassStylesXaml, 'x:Key="([A-Za-z]+)"')) { $definedResources[$match.Groups[1].Value] = $true }
foreach ($match in [regex]::Matches($appCode, 'Resources\["([A-Za-z]+)"\]\s*=\s*new')) { $definedResources[$match.Groups[1].Value] = $true }
foreach ($match in [regex]::Matches($code + $appCode + $chartCode, 'FindResource\("([A-Za-z]+)"\)')) {
    if (-not $definedResources.ContainsKey($match.Groups[1].Value)) {
        throw "Code looks up a resource that is never defined: $($match.Groups[1].Value)"
    }
}
if ($code -match 'Glow"') {
    throw 'Status lights are flat; code must not look up glow resources.'
}

if ($code -match 'new RecordingAnalysisWindow' -or (Test-Path -LiteralPath (Join-Path $repoRoot 'tools\DcsExtraTelemetry.Launcher\RecordingAnalysisWindow.xaml'))) {
    throw 'Recording analysis belongs on the Flights page, not in a separate window.'
}

if ($xaml -match 'Suggested ShakeIt gain' -or $xaml -match 'Content="Test effect"' -or $code -match 'SuggestedGainText') {
    throw 'The launcher must not imply it can measure, trigger, or prescribe SimHub shaker output.'
}

# The flights analyzer scrolls inside a bounded window.  Measuring it at
# infinite height while the window width is changing can re-enter WPF layout
# with invalid child dimensions and crash text/chart rendering.
if ($code -match 'ActionArea\.Measure\(new Size\([^\)]*double\.PositiveInfinity' -or
    $code -match 'UpdateLayout\(\);\s*double measureWidth') {
    throw 'Flights analysis must not force an infinite-height layout measure while fitting the window.'
}

if ($code -notmatch 'isFlightAnalysis\s*\?\s*maxHeight') {
    throw 'Flights analysis must use a bounded window height while its scrollable content lays out.'
}

if ($code -match 'Text\s*=\s*"• "\s*\+\s*relativePath' -or
    $code -notmatch 'rollback profiles included') {
    throw 'Home must summarize bundled profile history instead of listing every archived file.'
}

$requiredThemeTokens = @(
    'PrimaryPressed',
    'SecondaryButtonPressed',
    'DisabledBackground',
    'DisabledForeground',
    'FocusRing',
    'ControlBorder',
    'NeutralStatus',
    'ChartLane',
    'ChartPhaseBand',
    'ChartCursor',
    'ChartCategoryAirframe',
    'ChartCategoryGround',
    'ChartCategoryWeapons',
    'ChartCategoryRotor',
    'ScrollThumbHover'
)

foreach ($token in $requiredThemeTokens) {
    if ($appXaml -notmatch [regex]::Escape('x:Key="' + $token + '"')) {
        throw "Missing Industrial Signal theme token: $token"
    }
    if ($paletteCode -notmatch [regex]::Escape('{ "' + $token + '",')) {
        throw "Theme token is not defined in the runtime palette: $token"
    }
}

if ($appXaml -notmatch 'x:Key="PrimaryButton"' -or
    $appXaml -notmatch '<Style TargetType="Button">[\s\S]+SecondaryButtonBackground') {
    throw 'Ordinary buttons must be neutral and primary actions must opt into PrimaryButton.'
}

if ($chartCode -notmatch 'ResourceColor\("ChartBackground"' -or
    $chartCode -notmatch 'public void RefreshTheme\(\)') {
    throw 'Recording charts must resolve dynamic theme resources and support immediate repainting.'
}

$retiredColors = @('#4FC9B8', '#2F9D8A', '#55C7BC', '#69C78C', '#D578A4', '#293E3B')
foreach ($retired in $retiredColors) {
    if (($appXaml + $appCode + $xaml + $code + $chartCode) -match [regex]::Escape($retired)) {
        throw "Retired teal, blue, pink, or green UI color remains: $retired"
    }
}

foreach ($logo in @('HapticFlightLogoLight.png', 'HapticFlightLogoDark.png')) {
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot ('tools\DcsExtraTelemetry.Launcher\Assets\' + $logo)))) {
        throw "Missing flat launcher logo asset: $logo"
    }
}

$launcherIcon = Join-Path $repoRoot 'tools\DcsExtraTelemetry.Launcher\Assets\HapticFlightOrangeIcon.ico'
if (-not (Test-Path -LiteralPath $launcherIcon)) {
    throw 'Missing orange HapticFlight launcher icon asset.'
}

if ($xaml -notmatch 'Icon="Assets/HapticFlightOrangeIcon\.ico"' -or
    $projectXml -notmatch '<ApplicationIcon>Assets\\HapticFlightOrangeIcon\.ico</ApplicationIcon>') {
    throw 'Launcher window and executable must use the orange HapticFlight icon.'
}

Write-Host 'Launcher UI contract passed.'
