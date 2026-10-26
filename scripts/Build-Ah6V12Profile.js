const crypto = require('crypto');
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const source = path.join(root, 'downloads', 'ah6-ship', 'DCS-AH-6J-Haptic-Engine-v2.0.0-AH6-v1.1-Calibrated.siprofile');
const outputDirectory = path.join(root, 'downloads', 'ah6-v1.2');
const output = path.join(outputDirectory, 'DCS-AH-6J-Haptic-Engine-v2.0.0-AH6-v1.2-Flight-Cues.siprofile');
const prefix = 'DcsExtraTelemetryPlugin.DCSExtra.';
const regenerate = process.argv.includes('--regenerate');

function id(value) {
  const hash = crypto.createHash('sha256').update(value).digest('hex').slice(0, 32);
  return hash.replace(/(.{8})(.{4})(.{4})(.{4})(.{12})/, '$1-$2-$3-$4-$5');
}
function group(profile, description) {
  const found = profile.EffectsContainers.find(item => item.Description === description);
  if (!found) throw new Error(`Missing ${description} group.`);
  return found;
}
function effect(container, description) {
  const found = container.EffectsContainers.find(item => item.Description === description);
  if (!found) throw new Error(`Missing ${description} in ${container.Description}.`);
  return found;
}
function qualifiedVersionGate() {
  return `var version = String($prop('${prefix}Haptic.EngineVersion') || '').split('.');
var major = Number(version[0]), minor = Number(version[1]), patch = Number(version[2] || 0);
if (!$prop('${prefix}Connected') || !(major > 1 || (major === 1 && (minor > 7 || (minor === 7 && patch >= 1))))) return 0;`;
}
function etlFormula() {
  return `${qualifiedVersionGate()}
if (!$prop('${prefix}Has.AerodynamicEffects')) return 0;
var raw = Math.max(0, Math.min(1, $prop('${prefix}Haptic.Aerodynamic.TranslationalLift.Intensity')));
var target = 60 * Math.pow(raw, 0.60);
var dt = Number($prop('${prefix}Haptic.Timing.DeltaSeconds.Intensity'));
if (!isFinite(dt) || dt <= 0 || dt > 0.25) dt = 1 / 60;
var prior = Number(root["ah6EtlEnvelope"] || 0);
var timeConstant = target > prior ? 0.15 : 0.40;
var smoothed = prior + (target - prior) * (1 - Math.exp(-dt / timeConstant));
if (target <= 0 && smoothed < 0.05) smoothed = 0;
root["ah6EtlEnvelope"] = smoothed;
return Math.max(0, Math.min(100, smoothed));`;
}
function touchdownFormula() {
  return `${qualifiedVersionGate()}
if (!$prop('${prefix}Connected') || !(parseFloat($prop('${prefix}Haptic.EngineVersion')) >= 1.1)) return 0;
var raw = Math.max(0, Math.min(1, $prop('${prefix}Haptic.AH6.LightLanding.Envelope')));
return 100 * Math.pow(raw, 0.62);`;
}
function liftoffFormula() {
  return `${qualifiedVersionGate()}
if (!$prop('${prefix}Connected') || !(parseFloat($prop('${prefix}Haptic.EngineVersion')) >= 1.1)) return 0;
var raw = Math.max(0, Math.min(1, $prop('${prefix}Haptic.Liftoff.Envelope')));
return 40 * raw;`;
}
function groundedRotorFormula() {
  return `${qualifiedVersionGate()}
if (!$prop('${prefix}OnGround')) return 0;
var rotor = Math.max(0, Math.min(1, $prop('${prefix}Haptic.Rotor.Presence.Intensity')));
var resonance = Math.max(0, Math.min(1, $prop('${prefix}Haptic.Ground.Resonance.Intensity')));
var impact = Math.max(Math.max(0, Math.min(1, $prop('${prefix}Haptic.AH6.LightLanding.Envelope'))), Math.max(0, Math.min(1, $prop('${prefix}Haptic.Ground.Bump.Envelope'))));
var continuous = Math.min(rotor, Math.sqrt(resonance));
return 36 * continuous * (1 - 0.65 * impact);`;
}

const profile = JSON.parse(fs.readFileSync(source, 'utf8'));
profile.Name = 'DCS AH-6J - Haptic Engine v2.0.0 AH-6J v1.2 Flight Cues';
profile.ProfileId = id(profile.Name);
profile.Description = 'AH-6J/MH-6J v1.2 recording-derived calibration. Requires Engine 1.8.0 and matching Hook. Uses direct phase-derived rotor data when available and an explicitly labelled engine-RPM proxy only when phase data is absent. Smooths ETL; strengthens hard touchdown, liftoff unloading, and grounded rotor loading without replacing individual-skid detail.';

const aero = group(profile, 'Aerodynamics');
const etl = effect(aero, 'AH-6J ETL transition (8.5-13.5 m/s / 16.5-26.2 kt)');
etl.Description = 'AH-6J ETL transition (smoothed onset / release)';
etl.Gain = 38;
etl.AlwaysExecute = true;
etl.FrontLeftFormula.PreExpression = 'root["ah6EtlEnvelope"] = 0;';
etl.FrontLeftFormula.Expression = etlFormula();

const ground = group(profile, 'Ground Interaction');
const touchdown = effect(ground, 'Touchdown body impact');
touchdown.Description = 'Touchdown body impact (recording-calibrated)';
touchdown.Gain = 72;
touchdown.FrontLeftFormula.Expression = touchdownFormula();
const liftoff = effect(ground, 'Liftoff unloading');
liftoff.Description = 'Liftoff unloading (recording-calibrated)';
liftoff.Gain = 28;
liftoff.FrontLeftFormula.Expression = liftoffFormula();
const groundedRotor = effect(ground, 'Gear-borne rotor vibration');
groundedRotor.Description = 'Grounded rotor / skid load texture (proxy compatible)';
groundedRotor.Gain = 24;
groundedRotor.FrontLeftFormula.Expression = groundedRotorFormula();

for (const effectsGroup of profile.EffectsContainers) {
  effectsGroup.ContainerId = id(profile.Name + effectsGroup.Description);
  for (const item of effectsGroup.EffectsContainers || []) item.ContainerId = id(profile.Name + effectsGroup.Description + item.Description);
}

const text = JSON.stringify(profile, null, 2) + '\n';
fs.mkdirSync(outputDirectory, { recursive: true });
if (fs.existsSync(output) && fs.readFileSync(output, 'utf8') !== text && !regenerate) {
  throw new Error(`Refusing to overwrite a different v1.2 profile: ${output}`);
}
fs.writeFileSync(output, text);
console.log(`Built ${path.relative(root, output)}.`);
