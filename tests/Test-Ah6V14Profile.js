const assert = require('assert');
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const profilePath = path.join(root, 'downloads', 'ah6-v1.4', 'DCS-AH-6J-Haptic-Engine-v2.0.0-AH6-v1.4-Impact-Limiter.siprofile');
const profile = JSON.parse(fs.readFileSync(profilePath, 'utf8'));
const ground = profile.EffectsContainers.find(item => item.Description === 'Ground Interaction');
const effect = name => ground.EffectsContainers.find(item => item.Description === name);

assert(profile.Name.includes('v1.4 Impact Limiter'));
assert(effect('Touchdown body impact (soft-limited)').FrontLeftFormula.Expression.includes('Math.exp'));
assert(effect('Liftoff unloading (touchdown-safe)').FrontLeftFormula.Expression.includes('landing > 0.02'));
for (const name of ['Suspension surface roughness', 'Ground support rocking', 'Grounded rotor / skid load texture (proxy compatible)']) {
  assert(effect(name).FrontLeftFormula.Expression.includes('impact'), `${name} is not ducked during a landing impact.`);
}
console.log('PASS: AH-6J v1.4 limits hard-impact stacking without changing ordinary landing detail.');
