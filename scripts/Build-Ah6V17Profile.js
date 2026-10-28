const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const root = path.resolve(__dirname, '..');
const source = path.join(root, 'downloads/ah6-v1.6/DCS-AH-6J-Haptic-Engine-v2.0.0-AH6-v1.6-Rotor-Gain-Reduction.siprofile');
const output = path.join(root, 'downloads/ah6-v1.7/DCS-AH-6J-Haptic-Engine-v2.0.0-AH6-v1.7-Rotor-Response.siprofile');
const profile = JSON.parse(fs.readFileSync(source, 'utf8'));
profile.Name = 'DCS AH-6J - Haptic Engine v2.0.0 AH-6J v1.7 Rotor Response';
profile.Description = 'AH-6J v1.7 separates governed rotor presence from manoeuvre response; flight and hardware validation required.';
function id(value) { return crypto.createHash('sha256').update(value).digest('hex').slice(0, 32).replace(/(.{8})(.{4})(.{4})(.{4})(.{12})/, '$1-$2-$3-$4-$5'); }
profile.ProfileId = id(profile.Name);
const rotor = profile.EffectsContainers.find(item => item.Description === 'Rotor and Powertrain');
for (const [name, baseline, cap, g, lateral, control] of [
  ['AH-6 rotor 1-rev airframe pulse', 28, 42, 6, 5, 3],
  ['AH-6 rotor 2-rev structural harmonic', 22, 36, 5, 6, 3],
  ['AH-6 main rotor blade-pass 5-rev', 30, 44, 5, 4, 2]
]) {
  const item = rotor.EffectsContainers.find(candidate => candidate.Description === name);
  const formula = item.FrontLeftFormula;
  const pattern = /target = Math\.min\(\d+, presence \* \(\d+ \+ dynamicSwell\)\);/;
  if (!pattern.test(formula.Expression)) throw new Error('Rotor formula shape changed: ' + name);
  formula.Expression = formula.Expression.replace(pattern, `target = Math.min(${cap}, presence * (${baseline} + ${g} * positiveG + ${lateral} * lateral + ${control} * control));`);
  if (name.includes('blade-pass')) {
    const marker = 'var dynamicSwell =';
    formula.Expression = formula.Expression.replace(marker, `var lateral = Math.max(0, Math.min(1, $prop('DcsExtraTelemetryPlugin.DCSExtra.Haptic.Maneuver.Lateral.Intensity')));\nvar control = Math.max(0, Math.min(1, $prop('DcsExtraTelemetryPlugin.DCSExtra.Haptic.Maneuver.ControlResponse.Intensity')));\nvar g = (!$prop('DcsExtraTelemetryPlugin.DCSExtra.OnGround') && $prop('DcsExtraTelemetryPlugin.DCSExtra.Has.Acceleration')) ? $prop('DcsExtraTelemetryPlugin.DCSExtra.AccelYG') : 1;\nvar positiveG = Math.max(0, Math.min(1, (g - 1.03) / 0.67));\n${marker}`);
  }
}
for (const group of profile.EffectsContainers) {
  group.ContainerId = id(profile.Name + group.Description);
  for (const item of group.EffectsContainers || []) item.ContainerId = id(profile.Name + group.Description + item.Description);
}
fs.mkdirSync(path.dirname(output), { recursive: true });
fs.writeFileSync(output, JSON.stringify(profile, null, 2) + '\n');
console.log('Built ' + output);
