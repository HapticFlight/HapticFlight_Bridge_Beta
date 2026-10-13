[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$LauncherPath,

    [Parameter(Mandatory = $true)]
    [string]$PackageRoot,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath,

    [int]$Width = 1283,
    [int]$Height = 893,

    [switch]$Dark
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase

$launcherAssembly = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $LauncherPath).Path)
[System.Windows.Application]::ResourceAssembly = $launcherAssembly
$appType = $launcherAssembly.GetType('DcsExtraTelemetry.Launcher.App', $true)
$windowType = $launcherAssembly.GetType('DcsExtraTelemetry.Launcher.MainWindow', $true)
$app = [Activator]::CreateInstance($appType)
$app.InitializeComponent()
if ($Dark) {
    $themeType = $launcherAssembly.GetType('DcsExtraTelemetry.Launcher.ThemePreference', $true)
    $darkTheme = [Enum]::Parse($themeType, 'Dark')
    $appType.GetProperty('ThemePreference').SetValue($app, $darkTheme)
    $appType.GetMethod('ApplyTheme', [Reflection.BindingFlags]'Instance,NonPublic').Invoke($app, @()) | Out-Null
}
$window = [Activator]::CreateInstance($windowType)

$bindingFlags = [Reflection.BindingFlags]'Instance,NonPublic'
$packageRootField = $windowType.GetField('packageRoot', $bindingFlags)
$packageRootField.SetValue($window, (Resolve-Path -LiteralPath $PackageRoot).Path)
$refreshProfiles = $windowType.GetMethod('RefreshBundledProfiles', $bindingFlags)
$refreshProfiles.Invoke($window, @()) | Out-Null

$content = [System.Windows.FrameworkElement]$window.Content
$size = [System.Windows.Size]::new($Width, $Height)
$content.Measure($size)
$content.Arrange([System.Windows.Rect]::new(0, 0, $Width, $Height))
$content.UpdateLayout()

$bitmap = [System.Windows.Media.Imaging.RenderTargetBitmap]::new(
    $Width, $Height, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
$bitmap.Render($content)
$encoder = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()
$encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
$outputDirectory = Split-Path -Parent $OutputPath
if ($outputDirectory -and -not (Test-Path -LiteralPath $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}
$stream = [IO.File]::Open($OutputPath, [IO.FileMode]::Create)
try { $encoder.Save($stream) } finally { $stream.Dispose() }
$window.Close()
$app.Shutdown()
Write-Host "Captured launcher Home layout: $OutputPath"
