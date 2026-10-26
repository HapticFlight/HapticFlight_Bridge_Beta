const crypto = require('crypto');
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const source = path.join(root, 'downloads', 'ah6-v1.2', 'DCS-AH-6J-Haptic-Engine-v2.0.0-AH6-v1.2-Flight-Cues.siprofile');
const outputDirectory = path.join(root, 'downloads', 'ah6-v1.3');
const output = path.join(outputDirectory, 'DCS-AH-6J-Haptic-Engine-v2.0.0-AH6-v1.3-Impact-Headroom.siprofile');
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
function gate() {
  return `var version = String($prop('${prefix}Haptic.EngineVersion') || '').split('.');
var major = Number(version[0]), minor = Number(version[1]), patch = Number(version[2] || 0);
if (!$prop('${prefix}Connected') || !(major > 1 || (major === 1 && (minor > 7 || (minor === 7 && patch >= 1))))) return 0;`;
}
function priorityDuck() {
  return `var landing = Math.max(0, Math.min(1, $prop('${prefix}Haptic.AH6.LightLanding.Envelope')));
// Preserve soft and single-skid texture.  Above a firm landing, reserve event
// headroom for the body impact rather than summing several full-scale bursts.
var priorityDuck = Math.max(0, Math.min(1, (landing - 0.35) / 0.35));`;
}
function groundBumpFormula() {
  return `${gate()}
var raw = Math.max(0, Math.min(1, $prop('${prefix}Haptic.Ground.Bump.Envelope')));
${priorityDuck()}
return 75 * raw * (1 - priorityDuck);`;
}
function skidDetailFormula() {
  return `${gate()}
var skid = Math.max(
  Math.max(0, Math.min(1, $prop('${prefix}Haptic.Skid.FrontLeft.Envelope'))),
  Math.max(0, Math.min(1, $prop('${prefix}Haptic.Skid.FrontRight.Envelope'))),
  Math.max(0, Math.min(1, $prop('${prefix}Haptic.Skid.RearLeft.Envelope'))),
  Math.max(0, Math.min(1, $prop('${prefix}Haptic.Skid.RearRight.Envelope'))));
${priorityDuck()}
// The existing landing multiplier keeps contact detail present on soft landings;
// priorityDuck silences only the concurrent hard-landing stack.
return 55 * skid * (1 - 0.65 * landing) * (1 - priorityDuck);`;
}
function settlingFormula() {
  return `${gate()}
var raw = Math.max(0, Math.min(1, $prop('${prefix}Haptic.Ground.Settling.Envelope')));
${priorityDuck()}
return 40 * raw * (1 - 0.70 * priorityDuck);`;
}

const profile = JSON.parse(fs.readFileSync(source, 'utf8'));
profile.Name = 'DCS AH-6J - Haptic Engine v2.0.0 AH-6J v1.3 Impact Headroom';
profile.ProfileId = id(profile.Name);
profile.Description = 'AH-6J/MH-6J v1.3 hard-landing headroom calibration. Keeps the v1.2 phase-derived rotor, smoothed ETL, soft/skid detail, and liftoff cues. During a firm touchdown, the body impact takes priority and ducks concurrent ground-bump, skid-detail, and settling events to avoid summed shaker demand.';

const ground = group(profile, 'Ground Interaction');
effect(ground, 'Ground bump impact').FrontLeftFormula.Expression = groundBumpFormula();
effect(ground, 'Individual skid contact detail').FrontLeftFormula.Expression = skidDetailFormula();
effect(ground, 'Ground settling').FrontLeftFormula.Expression = settlingFormula();

for (const effectsGroup of profile.EffectsContainers) {
  effectsGroup.ContainerId = id(profile.Name + effectsGroup.Description);
  for (const item of effectsGroup.EffectsContainers || []) item.ContainerId = id(profile.Name + effectsGroup.Description + item.Description);
}

const text = JSON.stringify(profile, null, 2) + '\n';
fs.mkdirSync(outputDirectory, { recursive: true });
if (fs.existsSync(output) && fs.readFileSync(output, 'utf8') !== text && !regenerate) {
  throw new Error(`Refusing to overwrite a different v1.3 profile: ${output}`);
}
fs.writeFileSync(output, text);
console.log(`Built ${path.relative(root, output)}.`);
