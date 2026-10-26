const assert = require('assert');
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const hook = fs.readFileSync(path.join(root, 'dcs-hook', 'DCSExtraTelemetry.lua'), 'utf8');
const profilePath = path.join(root, 'downloads', 'ah6-test21', 'DCS-AH-6J-Haptic-Engine-v2.0.0-test.21-Proxy-VRS-ETL-Weapons.siprofile');
const profile = JSON.parse(fs.readFileSync(profilePath, 'utf8'));
const group = name => profile.EffectsContainers.find(item => item.Description === name);
const effect = (container, name) => container.EffectsContainers.find(item => item.Description === name);

assert(hook.includes('rotorSource = "AH6RotorPhase37"'), 'AH-6 phase-derived rotor calibration missing.');
assert(hook.includes('if aircraftType == "AH-6J" or aircraftType == "MH-6J" then droop = 0.0 end'), 'AH-6 false droop suppression missing.');

const rotor = group('Rotor and Powertrain');
assert.equal(effect(rotor, '[Disabled for AH-6 N1 proxy] Low rotor droop roughness').IsEnabled, false);
const aero = group('Aerodynamics');
const etl = effect(aero, 'Translational lift transition');
assert.equal(etl.Gain, 26);
assert(etl.FrontLeftFormula.Expression.includes('60 * Math.pow(raw, 0.60)'));
const vrs = effect(aero, 'Derived VRS-risk texture (AH-6 proxy calibrated)');
assert.equal(vrs.IsEnabled, true);
assert.equal(vrs.Gain, 24);
assert(vrs.FrontLeftFormula.Expression.includes('52 * Math.pow(risk, 0.72)'));
const weapons = group('Weapons and Countermeasures');
assert.equal(effect(weapons, 'AH-6 heavy-gun rhythm (recorded generic gun)').Gain, 38);
const onset = effect(weapons, 'AH-6 heavy-gun recoil onset');
assert.equal(onset.Gain, 30);
assert(onset.FrontLeftFormula.Expression.includes('ah6HeavyGunKick'));
assert(onset.FrontLeftFrequencyFormula.Expression.includes('WeaponRateHz'));
assert.equal(effect(weapons, 'Rocket launch kick').Gain, 42);
assert.equal(effect(weapons, 'Rocket launch body reaction').Gain, 26);
console.log('PASS: AH-6J test.21 profile remains structurally valid with the corrected AH-6 engine calibration.');
