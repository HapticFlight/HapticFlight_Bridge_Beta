const crypto = require('crypto');
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const source = path.join(root, 'downloads', 'ah6-test21', 'DCS-AH-6J-Haptic-Engine-v2.0.0-test.21-Proxy-VRS-ETL-Weapons.siprofile');
const outputDirectory = path.join(root, 'downloads', 'ah6-ship');
const output = path.join(outputDirectory, 'DCS-AH-6J-Haptic-Engine-v2.0.0-AH6-v1.1-Calibrated.siprofile');
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
function groundEffectFormula() {
  return `var version = String($prop('${prefix}Haptic.EngineVersion') || '').split('.');
var major = Number(version[0]), minor = Number(version[1]);
if (!$prop('${prefix}Connected') || !(major > 1 || (major === 1 && minor >= 8))) return 0;
if (!$prop('${prefix}Has.AerodynamicEffects')) return 0;
var raw = Math.max(0, Math.min(1, $prop('${prefix}Haptic.Aerodynamic.GroundEffect.Intensity')));
return 42 * Math.pow(raw, 0.70);`;
}

const profile = JSON.parse(fs.readFileSync(source, 'utf8'));
profile.Name = 'DCS AH-6J - Haptic Engine v2.0.0 AH-6J v1.1 Calibrated';
profile.ProfileId = id(profile.Name);
profile.Description = 'Ship profile for CKDesigns AH-6J/MH-6J v1.1. Requires Engine 1.8.0 and matching Hook. Rotor speed is direct phase-derived from AH-6 argument 37; argument 311 is used only as torque. Includes calibrated ETL, derived VRS/ground-effect cues, four-point skid work, and AH-6 M134 mapping.';

const rotor = group(profile, 'Rotor and Powertrain');
const droop = effect(rotor, '[Disabled for AH-6 N1 proxy] Low rotor droop roughness');
droop.Description = '[Unavailable: AH-6J has no direct droop signal] Low rotor droop roughness';

const aero = group(profile, 'Aerodynamics');
const etl = effect(aero, 'Translational lift transition');
etl.Gain = 38;
etl.Description = 'AH-6J ETL transition (8.5-13.5 m/s / 16.5-26.2 kt)';
const vrs = effect(aero, 'Derived VRS-risk texture (AH-6 proxy calibrated)');
vrs.Description = 'Derived AH-6J VRS-risk texture (phase rotor + torque; not an EFM VRS state)';
vrs.Gain = 26;
const groundEffect = JSON.parse(JSON.stringify(etl));
groundEffect.Description = 'Derived AH-6J ground-effect cushion (below one rotor diameter)';
groundEffect.Gain = 24;
groundEffect.ContainerId = id(profile.Name + aero.Description + groundEffect.Description);
groundEffect.FrontLeftFormula.PreExpression = '';
groundEffect.FrontLeftFormula.Expression = groundEffectFormula();
groundEffect.FrontLeftFrequencyFormula.Expression = `if (!$prop('${prefix}Connected')) return 18; return 18;`;
aero.EffectsContainers.push(groundEffect);

const ground = group(profile, 'Ground Interaction');
effect(ground, 'Touchdown body impact').Gain = 58;
effect(ground, 'Ground bump impact').Gain = 30;
const skid = effect(ground, 'Individual skid contact detail');
skid.Gain = 45;
skid.FrontLeftFormula.Expression = skid.FrontLeftFormula.Expression.replace('return 40 *', 'return 55 *');
if (!skid.FrontLeftFormula.Expression.includes('return 55 *')) throw new Error('Skid-contact amplitude was not updated.');

const weapons = group(profile, 'Weapons and Countermeasures');
const heavy = effect(weapons, 'AH-6 heavy-gun rhythm (recorded generic gun)');
heavy.Description = 'AH-6 HMP400 heavy-gun rhythm';
heavy.Gain = 30;
heavy.FrontLeftFrequencyFormula.Expression = `if (!$prop('${prefix}Connected') || !$prop('${prefix}Has.WeaponEffects')) return 18;
var name = String($prop('${prefix}LastFiredWeaponName') || '').toLowerCase();
if (name.indexOf('hmp400') >= 0) return 1025 / 60;
return 18;`;
const recoil = effect(weapons, 'AH-6 heavy-gun recoil onset');
recoil.Description = 'AH-6 HMP400 recoil onset';
recoil.Gain = 24;

for (const effectsGroup of profile.EffectsContainers) {
  effectsGroup.ContainerId = id(profile.Name + effectsGroup.Description);
  for (const item of effectsGroup.EffectsContainers || []) item.ContainerId = id(profile.Name + effectsGroup.Description + item.Description);
}

const text = JSON.stringify(profile, null, 2) + '\n';
fs.mkdirSync(outputDirectory, { recursive: true });
if (fs.existsSync(output) && fs.readFileSync(output, 'utf8') !== text && !regenerate) {
  throw new Error(`Refusing to overwrite a different ship profile: ${output}`);
}
fs.writeFileSync(output, text);
console.log(`Built ${path.relative(root, output)}.`);
