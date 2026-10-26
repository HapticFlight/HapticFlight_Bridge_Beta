const assert = require('assert');
const fs = require('fs');
const path = require('path');

const profilePath = path.resolve(__dirname, '../downloads/ah6-test20/DCS-AH-6J-Haptic-Engine-v2.0.0-test.20-Startup-Skids-ETL.siprofile');
const profile = JSON.parse(fs.readFileSync(profilePath, 'utf8'));
const group = description => profile.EffectsContainers.find(item => item.Description === description);
const effect = (container, description) => container.EffectsContainers.find(item => item.Description === description);

const lifecycle = group('Rotor startup and spool');
assert(lifecycle, 'Lifecycle group was not renamed.');
assert(!effect(lifecycle, 'Engine starters'), 'Unavailable AH-6J starter cue remains enabled in test.20.');
const spool = effect(lifecycle, 'Rotor spool transition');
assert.equal(spool.Gain, 42);
assert(spool.FrontLeftFormula.Expression.includes('(raw - 0.04) / 0.26'));
const startupTexture = effect(lifecycle, 'Startup/shutdown rotor texture (spool-rate)');
assert.equal(startupTexture.Gain, 20);
assert(startupTexture.FrontLeftFormula.Expression.includes('Has.PowertrainEffects'));
assert(startupTexture.FrontLeftFormula.Expression.includes('ah6StartupRotorTexture'));
assert.equal(lifecycle.EffectsContainers.length, 2, 'AH-6J lifecycle group should contain only spool-rate-driven cues.');

const etl = effect(group('Aerodynamics'), 'Translational lift transition');
assert.equal(etl.Gain, 22);
assert(etl.FrontLeftFormula.Expression.includes('Math.pow(raw, 0.65)'));

const ground = group('Ground Interaction');
assert.equal(effect(ground, 'Touchdown body impact').Gain, 48);
assert.equal(effect(ground, 'Individual skid contact detail').Gain, 28);
assert.equal(effect(ground, 'Ground settling').Gain, 15);

console.log('PASS: AH-6J test.20 startup, spool, ETL, touchdown, and individual-skid settings are present.');
