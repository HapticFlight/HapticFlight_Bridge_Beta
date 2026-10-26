const assert = require('assert');
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const hook = fs.readFileSync(path.join(root, 'dcs-hook', 'DCSExtraTelemetry.lua'), 'utf8');
const profilePath = path.join(root, 'downloads', 'ah6-ship', 'DCS-AH-6J-Haptic-Engine-v2.0.0-AH6-v1.1-Calibrated.siprofile');
const profile = JSON.parse(fs.readFileSync(profilePath, 'utf8'));
const group = name => profile.EffectsContainers.find(item => item.Description === name);
const effect = (container, name) => container.EffectsContainers.find(item => item.Description === name);

assert(hook.includes('torqueGauge, hasTorqueGauge = readCockpitArgument(311)'), 'AH-6 torque argument must be read as torque.');
assert(hook.includes('local phase, hasPhase = readDrawArgument(37)'), 'AH-6 rotor phase argument must be captured.');
assert(hook.includes('rotorSource = "AH6RotorPhase37"'), 'AH-6 must expose phase-derived rotor source.');
assert(!hook.includes('rotorSource = "AH6N1Proxy"'), 'AH-6 must not retain the false N1 proxy.');
assert(hook.includes('key == "4:6:10:13600"'), 'AH-6 left M134 identifier mapping missing.');
assert(hook.includes('key == "4:6:10:13601"'), 'AH-6 right M134 identifier mapping missing.');
assert(hook.includes('groundEffectIntensity = groundEffectIntensity'), 'AH-6 ground-effect telemetry output missing.');

const aero = group('Aerodynamics');
assert.equal(effect(aero, 'AH-6J ETL transition (8.5-13.5 m/s / 16.5-26.2 kt)').Gain, 38);
assert.equal(effect(aero, 'Derived AH-6J ground-effect cushion (below one rotor diameter)').Gain, 24);
assert.equal(effect(aero, 'Derived AH-6J VRS-risk texture (phase rotor + torque; not an EFM VRS state)').Gain, 26);
const ground = group('Ground Interaction');
assert.equal(effect(ground, 'Touchdown body impact').Gain, 58);
assert.equal(effect(ground, 'Individual skid contact detail').Gain, 45);
assert(effect(ground, 'Individual skid contact detail').FrontLeftFormula.Expression.includes('return 55 *'));
const weapons = group('Weapons and Countermeasures');
assert.equal(effect(weapons, 'AH-6 HMP400 heavy-gun rhythm').Gain, 30);
assert.equal(effect(weapons, 'AH-6 HMP400 recoil onset').Gain, 24);
console.log('PASS: AH-6J ship calibration is module-specific and profile-complete.');
