const assert = require('assert');
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const profilePath = path.join(root, 'downloads', 'ah6-v1.3', 'DCS-AH-6J-Haptic-Engine-v2.0.0-AH6-v1.3-Impact-Headroom.siprofile');
const profile = JSON.parse(fs.readFileSync(profilePath, 'utf8'));
const ground = profile.EffectsContainers.find(item => item.Description === 'Ground Interaction');
const effect = name => ground.EffectsContainers.find(item => item.Description === name);

assert(profile.Name.includes('v1.3 Impact Headroom'));
for (const name of ['Ground bump impact', 'Individual skid contact detail', 'Ground settling']) {
  const formula = effect(name).FrontLeftFormula.Expression;
  assert(formula.includes('Haptic.AH6.LightLanding.Envelope'), `${name} does not use the confirmed AH-6 landing envelope.`);
  assert(formula.includes('priorityDuck'), `${name} does not reserve headroom for a firm touchdown.`);
}
assert(effect('Individual skid contact detail').FrontLeftFormula.Expression.includes('Haptic.Skid.FrontLeft.Envelope'));
assert.equal(effect('Touchdown body impact (recording-calibrated)').Gain, 72, 'The body-impact priority must retain the calibrated v1.2 landing cue.');
console.log('PASS: AH-6J v1.3 reserves hard-landing event headroom while retaining the v1.2 body-impact cue.');
