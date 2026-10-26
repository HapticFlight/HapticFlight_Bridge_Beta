const crypto = require('crypto');
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const source = path.join(root, 'downloads', 'realism-test19', 'DCS-AH-6J-Haptic-Engine-v2.0.0-test.19-Realism.siprofile');
const outputDirectory = path.join(root, 'downloads', 'ah6-test20');
const output = path.join(outputDirectory, 'DCS-AH-6J-Haptic-Engine-v2.0.0-test.20-Startup-Skids-ETL.siprofile');
const prefix = 'DcsExtraTelemetryPlugin.DCSExtra.';
const regenerate = process.argv.includes('--regenerate');

function id(value) {
  const hash = crypto.createHash('sha256').update(value).digest('hex').slice(0, 32);
  return hash.replace(/(.{8})(.{4})(.{4})(.{4})(.{12})/, '$1-$2-$3-$4-$5');
}

function effectByDescription(group, description) {
  const effect = (group.EffectsContainers || []).find(item => item.Description === description);
  if (!effect) throw new Error(`Missing ${description} in ${group.Description}.`);
  return effect;
}

function groupByDescription(profile, description) {
  const group = profile.EffectsContainers.find(item => item.Description === description);
  if (!group) throw new Error(`Missing ${description} group.`);
  return group;
}

function startupTextureFormula() {
  return `var version = String($prop('${prefix}Haptic.EngineVersion') || '').split('.');
var major = Number(version[0]), minor = Number(version[1]), patch = Number(version[2] || 0);
if (!$prop('${prefix}Connected') || !(major > 1 || (major === 1 && (minor > 7 || (minor === 7 && patch >= 1))))) return 0;
var amplitude = (function(){
if (!$prop('${prefix}Has.PowertrainEffects') || !$prop('${prefix}Has.RotorDynamics')) {
    root["ah6StartupRotorTexture"] = 0;
    return 0;
}
// A changing-rotor-speed texture, not an N1 percentage band: it expires when
// the spool rate settles and therefore cannot remain active in normal cruise.
var raw = Math.max(0, Math.min(1, $prop('${prefix}Haptic.Rotor.Spool.Intensity')));
var cue = Math.max(0, Math.min(1, (raw - 0.03) / 0.22));
var target = 34 * Math.pow(cue, 0.75);
var dt = Number($prop('${prefix}Haptic.Timing.DeltaSeconds.Intensity'));
if (!isFinite(dt) || dt <= 0 || dt > 0.25) dt = 1 / 60;
var coefficient = 1 - Math.pow(1 - (target > root["ah6StartupRotorTexture"] ? 0.16 : 0.06), dt * 60);
root["ah6StartupRotorTexture"] += (target - root["ah6StartupRotorTexture"]) * coefficient;
if (target === 0 && root["ah6StartupRotorTexture"] < 0.05) root["ah6StartupRotorTexture"] = 0;
return Math.max(0, Math.min(34, root["ah6StartupRotorTexture"]));
})();
if (typeof amplitude !== 'number' || !isFinite(amplitude)) return 0;
var gain = Number($prop('${prefix}Haptic.Mix.ContinuousGain.Intensity'));
if (!isFinite(gain)) return 0;
return Math.max(0, Math.min(100, amplitude)) * Math.max(0, Math.min(1, gain));`;
}

const profile = JSON.parse(fs.readFileSync(source, 'utf8'));
profile.Name = 'DCS AH 6J - Haptic Engine v2.0.0-test.20 Startup, Skids and ETL';
profile.ProfileId = id(profile.Name);
profile.Description = 'AH-6J test.20; requires Engine 1.7.1. Removes the unavailable starter cue. Strengthens the N1-proxy startup/spool, translational-lift texture, full-touchdown body impact, and individual skid-contact detail. Derived/proxy cues require live seat validation.';

const lifecycle = groupByDescription(profile, 'Lifecycle');
lifecycle.Description = 'Rotor startup and spool';
const starter = effectByDescription(lifecycle, 'Engine starters');
lifecycle.EffectsContainers = lifecycle.EffectsContainers.filter(effect => effect !== starter);

const spool = effectByDescription(lifecycle, 'Rotor spool transition');
spool.Gain = 42;
spool.FrontLeftFormula.Expression = spool.FrontLeftFormula.Expression.replace(
  '(raw - 0.10) / 0.35',
  '(raw - 0.04) / 0.26');
if (!spool.FrontLeftFormula.Expression.includes('(raw - 0.04) / 0.26')) {
  throw new Error('Rotor spool onset was not updated.');
}
const startupTexture = JSON.parse(JSON.stringify(spool));
startupTexture.Description = 'Startup/shutdown rotor texture (spool-rate)';
startupTexture.Gain = 20;
startupTexture.FrontLeftFormula.PreExpression = 'root["ah6StartupRotorTexture"] = 0;';
startupTexture.FrontLeftFormula.Expression = startupTextureFormula();
lifecycle.EffectsContainers.push(startupTexture);

const aerodynamics = groupByDescription(profile, 'Aerodynamics');
const etl = effectByDescription(aerodynamics, 'Translational lift transition');
etl.Gain = 22;
etl.FrontLeftFormula.Expression = etl.FrontLeftFormula.Expression.replace(
  "return Math.min(42, 42 * $prop('DcsExtraTelemetryPlugin.DCSExtra.Haptic.Aerodynamic.TranslationalLift.Intensity'));",
  `var raw = Math.max(0, Math.min(1, $prop('${prefix}Haptic.Aerodynamic.TranslationalLift.Intensity')));
// Retain a temporary texture rather than an impact, while making ordinary
// crossings of the derived ETL band perceptible.
return 50 * Math.pow(raw, 0.65);`);
if (!etl.FrontLeftFormula.Expression.includes('Math.pow(raw, 0.65)')) {
  throw new Error('ETL response curve was not updated.');
}

const ground = groupByDescription(profile, 'Ground Interaction');
effectByDescription(ground, 'Touchdown body impact').Gain = 48;
effectByDescription(ground, 'Individual skid contact detail').Gain = 28;
effectByDescription(ground, 'Ground settling').Gain = 15;

for (const group of profile.EffectsContainers) {
  group.ContainerId = id(profile.Name + group.Description);
  for (const effect of group.EffectsContainers || []) {
    effect.ContainerId = id(profile.Name + group.Description + effect.Description);
  }
}

const text = JSON.stringify(profile, null, 2) + '\n';
fs.mkdirSync(outputDirectory, { recursive: true });
if (fs.existsSync(output) && fs.readFileSync(output, 'utf8') !== text && !regenerate) {
  throw new Error(`Refusing to overwrite a different test.20 profile: ${output}`);
}
fs.writeFileSync(output, text);
console.log(`Built ${path.relative(root, output)}.`);
