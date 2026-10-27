const crypto = require('crypto');
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const source = path.join(root, 'downloads', 'ah6-v1.5', 'DCS-AH-6J-Haptic-Engine-v2.0.0-AH6-v1.5-Rotor-Baseline.siprofile');
const outputDirectory = path.join(root, 'downloads', 'ah6-v1.6');
const output = path.join(outputDirectory, 'DCS-AH-6J-Haptic-Engine-v2.0.0-AH6-v1.6-Rotor-Gain-Reduction.siprofile');
const regenerate = process.argv.includes('--regenerate');

function id(value) {
  const hash = crypto.createHash('sha256').update(value).digest('hex').slice(0, 32);
  return hash.replace(/(.{8})(.{4})(.{4})(.{4})(.{12})/, '$1-$2-$3-$4-$5');
}
function effect(profile, description) {
  const rotor = profile.EffectsContainers.find(item => item.Description === 'Rotor and Powertrain');
  const found = rotor && rotor.EffectsContainers.find(item => item.Description === description);
  if (!found) throw new Error(`Missing ${description}.`);
  return found;
}

const profile = JSON.parse(fs.readFileSync(source, 'utf8'));
profile.Name = 'DCS AH-6J - Haptic Engine v2.0.0 AH-6J v1.6 Rotor Gain Reduction';
profile.ProfileId = id(profile.Name);
profile.Description = 'AH-6J/MH-6J v1.6 reduces the combined continuous rotor load for high-output tactile hardware. It retains v1.5 phase-derived rotor tracking and v1.4 landing headroom protections.';

effect(profile, 'AH-6 rotor 1-rev airframe pulse').Gain = 9;
effect(profile, 'AH-6 rotor 2-rev structural harmonic').Gain = 6;
effect(profile, 'AH-6 main rotor blade-pass 5-rev').Gain = 24;

for (const group of profile.EffectsContainers) {
  group.ContainerId = id(profile.Name + group.Description);
  for (const item of group.EffectsContainers || []) item.ContainerId = id(profile.Name + group.Description + item.Description);
}

const text = JSON.stringify(profile, null, 2) + '\n';
fs.mkdirSync(outputDirectory, { recursive: true });
if (fs.existsSync(output) && fs.readFileSync(output, 'utf8') !== text && !regenerate) {
  throw new Error(`Refusing to overwrite a different v1.6 profile: ${output}`);
}
fs.writeFileSync(output, text);
console.log(`Built ${path.relative(root, output)}.`);
