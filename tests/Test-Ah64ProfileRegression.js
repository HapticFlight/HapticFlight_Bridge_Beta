const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const profilePath = path.join(root, 'profiles', 'ah64', 'DCS World - DCS AH-64D - DCSExtra Baseline v0.1.siprofile');
const hookPath = path.join(root, 'dcs-hook', 'DCSExtraTelemetry.lua');
const profile = JSON.parse(fs.readFileSync(profilePath, 'utf8'));
const hook = fs.readFileSync(hookPath, 'utf8');

const effects = [];
function collect(container) {
  if (container.ContainerType === 'CustomEffectContainer') effects.push(container);
  for (const child of container.EffectsContainers || []) collect(child);
}
for (const group of profile.EffectsContainers || []) collect(group);
function effect(name) {
  const found = effects.find(item => item.Description === name);
  if (!found) throw new Error(`Missing AH-64 effect: ${name}`);
  return found;
}
function assert(condition, message) { if (!condition) throw new Error(message); }

assert(profile.Name === 'DCS AH-64D - DCSExtra Baseline v0.1', 'profile name is incorrect');
assert(profile.GlobalGain === 45, 'initial global gain is not the conservative AH-64 baseline');
assert(effects.length === 38, `expected 38 airframe-applicable effects, found ${effects.length}`);
assert(!effects.some(item => /M134|GAU-19/.test(item.Description)), 'irrelevant UH-60/AH-6 gun effects remain');

for (const item of effects) {
  for (const field of [item.FrontLeftFormula, item.FrontLeftFrequencyFormula]) {
    const source = `${field && field.PreExpression || ''}\n${field && field.Expression || ''}`;
    for (const match of source.matchAll(/\$prop\('([^']+)'\)/g))
      assert(match[1].startsWith('DcsExtraTelemetryPlugin.DCSExtra.'), `unqualified property in ${item.Description}: ${match[1]}`);
  }
}

const rotor1 = effect('AH-64 rotor 1-rev airframe pulse');
const rotor2 = effect('AH-64 rotor 2-rev structural harmonic');
const bladePass = effect('AH-64 main rotor blade-pass 4-rev');
assert(rotor1.ForceFrequencies && rotor1.FrontLeftFrequencyFormula.Expression.includes('MainRotorHz'), '1/rev frequency is not dynamic');
assert(rotor2.ForceFrequencies && rotor2.FrontLeftFrequencyFormula.Expression.includes('MainRotorHz') && rotor2.FrontLeftFrequencyFormula.Expression.includes('* 2'), '2/rev frequency is not dynamic');
assert(bladePass.ForceFrequencies && bladePass.FrontLeftFrequencyFormula.Expression.includes('BladePassHz'), 'blade-pass frequency is not dynamic');
for (const item of [rotor1, rotor2, bladePass]) {
  assert(item.FrontLeftFormula.Expression.includes("AircraftFamily') === 'AH64'"), `${item.Description} lacks the AH64 family gate`);
  assert(item.FrontLeftFormula.Expression.includes('Has.EngineTorque') && item.FrontLeftFormula.Expression.includes('Has.ControlPositions'), `${item.Description} lacks torque/collective fallback logic`);
}

assert(effect('AH-64 M230 chain-gun rhythm').FrontLeftFrequencyFormula.Expression.includes('NominalWeaponRateHz'), 'M230 rhythm does not use measured nominal rate');
assert(effect('AH-64 M230 low-body thump').FrontLeftFrequencyFormula.Expression.includes('* 0.5'), 'M230 body layer is not separated as a subharmonic');
assert(hook.includes('rotorSource = "AH64EngineRpmProxy"'), 'AH-64 engine-RPM rotor fallback is missing');
assert(hook.includes('readCockpitArgument(474)') && hook.includes('readCockpitArgument(475)'), 'AH-64 collective arguments are missing');

console.log(`AH-64 profile regression passed (${effects.length} effects).`);
