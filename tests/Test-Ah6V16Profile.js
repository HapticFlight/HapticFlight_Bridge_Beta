const assert = require('assert');
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const profile = JSON.parse(fs.readFileSync(path.join(root, 'downloads', 'ah6-v1.6', 'DCS-AH-6J-Haptic-Engine-v2.0.0-AH6-v1.6-Rotor-Gain-Reduction.siprofile'), 'utf8'));
const rotor = profile.EffectsContainers.find(item => item.Description === 'Rotor and Powertrain');
const effect = name => rotor.EffectsContainers.find(item => item.Description === name);

assert(profile.Name.includes('v1.6 Rotor Gain Reduction'));
assert.strictEqual(effect('AH-6 rotor 1-rev airframe pulse').Gain, 9);
assert.strictEqual(effect('AH-6 rotor 2-rev structural harmonic').Gain, 6);
assert.strictEqual(effect('AH-6 main rotor blade-pass 5-rev').Gain, 24);
assert(effect('AH-6 rotor 1-rev airframe pulse').IsEnabled, '1/rev should remain available at a reduced default.');
assert(effect('AH-6 rotor 2-rev structural harmonic').IsEnabled, '2/rev should remain available at a reduced default.');
console.log('PASS: AH-6J v1.6 reduces the complete continuous-rotor gain stack.');
