param([string]$SimHubDirectory = 'C:\Program Files (x86)\SimHub', [string]$ProfileDirectory = '', [string]$EngineVersion = '1.0.0')
$ErrorActionPreference = 'Stop'
$loadedDependencies = @{}
foreach ($dependency in @('System.Runtime.CompilerServices.Unsafe.dll', 'System.Buffers.dll',
    'System.Memory.dll', 'Microsoft.Bcl.AsyncInterfaces.dll', 'Acornima.dll', 'Jint.dll')) {
    $dependencyPath = Join-Path $SimHubDirectory $dependency
    if (Test-Path -LiteralPath $dependencyPath) {
        $assembly = [Reflection.Assembly]::LoadFrom($dependencyPath)
        $loadedDependencies[$assembly.GetName().Name] = $assembly
    }
}
$resolveHandler = [ResolveEventHandler] {
    param($sender, $eventArgs)
    $requested = [Reflection.AssemblyName]::new($eventArgs.Name).Name
    if ($loadedDependencies.ContainsKey($requested)) { return $loadedDependencies[$requested] }
    return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolveHandler)
$profileRoot = Join-Path $PSScriptRoot '..\downloads\haptic-engine-v2'
$contract = Get-Content -Raw (Join-Path $profileRoot 'V2-Effect-Audit.json') | ConvertFrom-Json
if ($ProfileDirectory) { $profileRoot = $ProfileDirectory }
$tested = 0
foreach ($file in Get-ChildItem $profileRoot -Recurse -Filter '*.siprofile') {
    $profile = Get-Content -Raw -LiteralPath $file.FullName | ConvertFrom-Json
    foreach ($group in $profile.EffectsContainers) {
        foreach ($effect in $group.EffectsContainers) {
            try { $engine = [Jint.Engine]::new() }
            catch { throw $_.Exception.ToString() }
            $engine.Execute(@'
var root = {};
var values = {'Haptic.EngineVersion': '1.0.0', Connected: true, 'Has.RotorDynamics': true, RotorRunning: true,
 MainRotorHz: 6, MainRotorRpmPct: 100, BladePassHz: 24,
 'Has.ControlPositions': true, CollectivePosition: 0.6, RotorLoadIntensity: 0.5};
function $prop(name) { var key = name.replace('DcsExtraTelemetryPlugin.DCSExtra.', ''); return values[key] === undefined ? 0 : values[key]; }
'@) | Out-Null
            $engine.Execute([string]$effect.FrontLeftFormula.PreExpression) | Out-Null
            $engine.Execute("values['Haptic.EngineVersion']=" + ($EngineVersion | ConvertTo-Json -Compress) + "; values['Haptic.Mix.ContinuousGain.Intensity']=1; values['Haptic.Timing.DeltaSeconds.Intensity']=1/60; values['Haptic.Ground.Bump.Envelope']=0.5; values['Haptic.Ground.Settling.Envelope']=0.5;") | Out-Null
            foreach ($property in $contract.registered) {
                if ($property.StartsWith('Has.')) {
                    $engine.Execute('values[' + ($property | ConvertTo-Json -Compress) + ']=true;') | Out-Null
                } elseif ($property.StartsWith('Haptic.') -and $property -match '(Intensity|Envelope)$') {
                    $engine.Execute('values[' + ($property | ConvertTo-Json -Compress) + ']=0.5;') | Out-Null
                }
            }
            $engine.Execute('function amplitude() {' + $effect.FrontLeftFormula.Expression + '}') | Out-Null
            $result = $engine.Evaluate('for(var i=0;i<120;i++) amplitude(); amplitude();').ToString()
            $result = [double]::Parse($result, [Globalization.CultureInfo]::InvariantCulture)
            if ([double]::IsNaN($result) -or [double]::IsInfinity($result)) { throw "Invalid output: $($effect.Description)" }
            if ($effect.Description -match '1-rev|blade-pass|two-blade rotor' -and $result -le 0) { throw "Silent rotor: $($effect.Description)" }
            $result = $engine.Evaluate('values.Connected=false; amplitude();').ToString()
            if ($result -ne 0) { throw "Disconnected output: $($effect.Description)" }
            $missingEngine = $engine.Evaluate("values.Connected=true; values['Haptic.EngineVersion']=0; amplitude();").ToString()
            if ($missingEngine -ne 0) { throw "Missing-engine output: $($effect.Description)" }
            if ($effect.FrontLeftFrequencyFormula.Expression) {
                $engine.Evaluate('(function(){' + $effect.FrontLeftFrequencyFormula.Expression + '})()') | Out-Null
            }
            $tested++
        }
    }
}
Write-Output "PASS: $tested effects executed in installed SimHub Jint $([Jint.Engine].Assembly.GetName().Version); missing identity, running rotor and immediate disconnect checks. V2 profile formulas; no live SimHub output was tested."
