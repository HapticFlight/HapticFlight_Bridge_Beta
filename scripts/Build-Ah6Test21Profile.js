const crypto = require('crypto');
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const source = path.join(root, 'downloads', 'ah6-test20', 'DCS-AH-6J-Haptic-Engine-v2.0.0-test.20-Startup-Skids-ETL.siprofile');
const outputDirectory = path.join(root, 'downloads', 'ah6-test21');
const output = path.join(outputDirectory, 'DCS-AH-6J-Haptic-Engine-v2.0.0-test.21-Proxy-VRS-ETL-Weapons.siprofile');
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
function heavyGunFrequency() {
  return `if (!$prop('${prefix}Connected') || !$prop('${prefix}Has.WeaponEffects')) return 30;
var weaponClass = String($prop('${prefix}WeaponClass') || '').toUpperCase();
var detected = Number($prop('${prefix}WeaponRateHz'));
// The AH-6J recording identifies this mounted gun as UNKNOWN but supplies a
// stable 40-60 Hz detected cadence. Use it only for a bounded heavy-gun cue.
if (weaponClass === 'HEAVY_GUN' && isFinite(detected) && detected >= 20 && detected <= 80) return detected;
var name = String($prop('${prefix}LastFiredWeaponName') || '').toLowerCase();
if (name.indexOf('m230') >= 0) return 625 / 60;
if (name.indexOf('m3p') >= 0) return 1100 / 60;
if (name.indexOf('hmp400') >= 0) return 1025 / 60;
return 30;`;
}
function heavyGunOnsetFormula() {
  return `var version = String($prop('${prefix}Haptic.EngineVersion') || '').split('.');
var major = Number(version[0]), minor = Number(version[1]), patch = Number(version[2] || 0);
if (!$prop('${prefix}Connected') || !(major > 1 || (major === 1 && (minor > 7 || (minor === 7 && patch >= 1))))) return 0;
var raw = $prop('${prefix}Has.WeaponEffects') ? Math.max(0, Math.min(1, $prop('${prefix}Haptic.Weapon.HeavyGun.Intensity'))) : 0;
var dt = Number($prop('${prefix}Haptic.Timing.DeltaSeconds.Intensity'));
if (!isFinite(dt) || dt <= 0 || dt > 0.25) dt = 1 / 60;
var prior = Number(root["ah6HeavyGunPrior"] || 0);
var kick = Number(root["ah6HeavyGunKick"] || 0);
if (raw > 0.20 && prior <= 0.05) kick = 1;
else kick = kick * Math.exp(-dt / 0.12);
root["ah6HeavyGunPrior"] = raw;
root["ah6HeavyGunKick"] = kick;
return 60 * Math.max(0, Math.min(1, kick));`;
}

const profile = JSON.parse(fs.readFileSync(source, 'utf8'));
profile.Name = 'DCS AH 6J - Haptic Engine v2.0.0-test.21 Proxy VRS, ETL and Weapons';
profile.ProfileId = id(profile.Name);
profile.Description = 'AH-6J test.21; requires Engine 1.7.1 plus the matching Hook. Calibrates VRS against the AH-6 N1 proxy, suppresses false proxy droop, and strengthens ETL/heavy-gun/rocket cues from the 2026-10-26 recording. VRS remains a derived risk texture, not a DCS VRS state.';

const rotor = group(profile, 'Rotor and Powertrain');
const droop = effect(rotor, 'Low rotor droop roughness');
droop.IsEnabled = false;
droop.Description = '[Disabled for AH-6 N1 proxy] Low rotor droop roughness';

const aero = group(profile, 'Aerodynamics');
const etl = effect(aero, 'Translational lift transition');
etl.Gain = 26;
etl.FrontLeftFormula.Expression = etl.FrontLeftFormula.Expression
  .replace('return 50 * Math.pow(raw, 0.65);', 'return 60 * Math.pow(raw, 0.60);');
if (!etl.FrontLeftFormula.Expression.includes('60 * Math.pow(raw, 0.60)')) throw new Error('ETL formula was not updated.');
const vrs = effect(aero, '[OPTIONAL derived cue] VRS-risk texture (not a DCS VRS state)');
vrs.Description = 'Derived VRS-risk texture (AH-6 proxy calibrated)';
vrs.IsEnabled = true;
vrs.Gain = 24;
vrs.FrontLeftFormula.Expression = vrs.FrontLeftFormula.Expression
  .replace('return Math.min(32, 32 * Math.pow(risk, 0.86));', 'return Math.min(52, 52 * Math.pow(risk, 0.72));');
if (!vrs.FrontLeftFormula.Expression.includes('52 * Math.pow(risk, 0.72)')) throw new Error('VRS formula was not updated.');

const weapons = group(profile, 'Weapons and Countermeasures');
effect(weapons, 'M134 and light-gun sustained fire').Gain = 34;
effect(weapons, 'M134 low body harmonic').Gain = 16;
const heavy = effect(weapons, 'AH-6 HMP400 heavy-gun rhythm');
heavy.Description = 'AH-6 heavy-gun rhythm (recorded generic gun)';
heavy.Gain = 38;
heavy.FrontLeftFrequencyFormula.Expression = heavyGunFrequency();
const recoil = JSON.parse(JSON.stringify(heavy));
recoil.Description = 'AH-6 heavy-gun recoil onset';
recoil.Gain = 30;
recoil.ContainerId = id(profile.Name + weapons.Description + recoil.Description);
recoil.FrontLeftFormula.PreExpression = 'root["ah6HeavyGunPrior"] = 0; root["ah6HeavyGunKick"] = 0;';
recoil.FrontLeftFormula.Expression = heavyGunOnsetFormula();
recoil.FrontLeftFrequencyFormula.Expression = heavyGunFrequency();
weapons.EffectsContainers.push(recoil);
effect(weapons, 'Rocket launch kick').Gain = 42;
effect(weapons, 'Rocket launch body reaction').Gain = 26;

for (const effectsGroup of profile.EffectsContainers) {
  effectsGroup.ContainerId = id(profile.Name + effectsGroup.Description);
  for (const item of effectsGroup.EffectsContainers || []) item.ContainerId = id(profile.Name + effectsGroup.Description + item.Description);
}

const text = JSON.stringify(profile, null, 2) + '\n';
fs.mkdirSync(outputDirectory, { recursive: true });
if (fs.existsSync(output) && fs.readFileSync(output, 'utf8') !== text && !regenerate) {
  throw new Error(`Refusing to overwrite a different test.21 profile: ${output}`);
}
fs.writeFileSync(output, text);
console.log(`Built ${path.relative(root, output)}.`);
