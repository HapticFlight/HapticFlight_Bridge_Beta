const crypto = require('crypto');
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const source = path.join(root, 'downloads', 'ah6-v1.3', 'DCS-AH-6J-Haptic-Engine-v2.0.0-AH6-v1.3-Impact-Headroom.siprofile');
const outputDirectory = path.join(root, 'downloads', 'ah6-v1.4');
const output = path.join(outputDirectory, 'DCS-AH-6J-Haptic-Engine-v2.0.0-AH6-v1.4-Impact-Limiter.siprofile');
const prefix = 'DcsExtraTelemetryPlugin.DCSExtra.';
const regenerate = process.argv.includes('--regenerate');

function id(value) {
  const hash = crypto.createHash('sha256').update(value).digest('hex').slice(0, 32);
  return hash.replace(/(.{8})(.{4})(.{4})(.{4})(.{12})/, '$1-$2-$3-$4-$5');
}
function effect(profile, description) {
  const ground = profile.EffectsContainers.find(item => item.Description === 'Ground Interaction');
  const found = ground && ground.EffectsContainers.find(item => item.Description === description);
  if (!found) throw new Error(`Missing ${description}.`);
  return found;
}
function gate() {
  return `var version = String($prop('${prefix}Haptic.EngineVersion') || '').split('.');
var major = Number(version[0]), minor = Number(version[1]), patch = Number(version[2] || 0);
if (!$prop('${prefix}Connected') || !(major > 1 || (major === 1 && (minor > 7 || (minor === 7 && patch >= 1))))) return 0;`;
}
function clamp(property) {
  return `Math.max(0, Math.min(1, $prop('${prefix}${property}')))`;
}
function impact() {
  return `var impact = Math.max(${clamp('Haptic.AH6.LightLanding.Envelope')}, ${clamp('Haptic.Ground.Bump.Envelope')});`;
}

const profile = JSON.parse(fs.readFileSync(source, 'utf8'));
profile.Name = 'DCS AH-6J - Haptic Engine v2.0.0 AH-6J v1.4 Impact Limiter';
profile.ProfileId = id(profile.Name);
profile.Description = 'AH-6J/MH-6J v1.4 hard-impact limiter. Preserves v1.3 soft and one-skid detail, compresses only the top end of a full landing impact, suppresses liftoff while touchdown is active, and ducks non-essential ground texture during the impact.';

const body = effect(profile, 'Touchdown body impact (recording-calibrated)');
body.Description = 'Touchdown body impact (soft-limited)';
body.FrontLeftFormula.Expression = `${gate()}
var raw = ${clamp('Haptic.AH6.LightLanding.Envelope')};
var shaped = 100 * Math.pow(raw, 0.62);
// Preserve normal and medium arrivals.  Above 68%, compress only the final
// hard-impact range so the 72% SimHub gain cannot consume all output headroom.
if (shaped > 68) shaped = 68 + 22 * (1 - Math.exp(-(shaped - 68) / 18));
return Math.max(0, Math.min(90, shaped));`;

const liftoff = effect(profile, 'Liftoff unloading (recording-calibrated)');
liftoff.Description = 'Liftoff unloading (touchdown-safe)';
liftoff.FrontLeftFormula.Expression = `${gate()}
var landing = ${clamp('Haptic.AH6.LightLanding.Envelope')};
if (landing > 0.02) return 0;
return 40 * ${clamp('Haptic.Liftoff.Envelope')};`;

const roughness = effect(profile, 'Suspension surface roughness');
roughness.FrontLeftFormula.Expression = `${gate()}
var amplitude = 45 * ${clamp('Haptic.Ground.Suspension.Intensity')};
var mix = Number($prop('${prefix}Haptic.Mix.ContinuousGain.Intensity'));
if (!isFinite(mix)) mix = 0;
${impact()}
return Math.max(0, Math.min(100, amplitude)) * Math.max(0, Math.min(1, mix)) * (1 - 0.55 * impact);`;

const rocking = effect(profile, 'Ground support rocking');
rocking.FrontLeftFormula.Expression = `${gate()}
var amplitude = 30 * Math.max(${clamp('Haptic.Ground.PitchRock.Intensity')}, ${clamp('Haptic.Ground.RollRock.Intensity')});
${impact()}
return amplitude * (1 - 0.75 * impact);`;

const groundedRotor = effect(profile, 'Grounded rotor / skid load texture (proxy compatible)');
groundedRotor.FrontLeftFormula.Expression = `${gate()}
if (!$prop('${prefix}OnGround')) return 0;
var rotor = ${clamp('Haptic.Rotor.Presence.Intensity')};
var resonance = ${clamp('Haptic.Ground.Resonance.Intensity')};
${impact()}
return 36 * Math.min(rotor, Math.sqrt(resonance)) * (1 - 0.85 * impact);`;

for (const group of profile.EffectsContainers) {
  group.ContainerId = id(profile.Name + group.Description);
  for (const item of group.EffectsContainers || []) item.ContainerId = id(profile.Name + group.Description + item.Description);
}

const text = JSON.stringify(profile, null, 2) + '\n';
fs.mkdirSync(outputDirectory, { recursive: true });
if (fs.existsSync(output) && fs.readFileSync(output, 'utf8') !== text && !regenerate) {
  throw new Error(`Refusing to overwrite a different v1.4 profile: ${output}`);
}
fs.writeFileSync(output, text);
console.log(`Built ${path.relative(root, output)}.`);
