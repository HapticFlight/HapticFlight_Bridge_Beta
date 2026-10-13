const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const hook = fs.readFileSync(path.join(root, 'dcs-hook', 'DCSExtraTelemetry.lua'), 'utf8');

function assert(condition, message) { if (!condition) throw new Error(message); }
function load(relativePath) {
  const profile = JSON.parse(fs.readFileSync(path.join(root, relativePath), 'utf8'));
  const effects = [];
  function collect(container) {
    if (container.ContainerType === 'CustomEffectContainer') effects.push(container);
    for (const child of container.EffectsContainers || []) collect(child);
  }
  for (const group of profile.EffectsContainers || []) collect(group);
  return { profile, effects };
}
function effect(items, name) {
  const found = items.find(item => item.Description === name);
  assert(found, `missing effect: ${name}`);
  return found;
}
function assertQualified(items) {
  for (const item of items) {
    for (const field of [item.FrontLeftFormula, item.FrontLeftFrequencyFormula]) {
      const source = `${field && field.PreExpression || ''}\n${field && field.Expression || ''}`;
      for (const match of source.matchAll(/\$prop\('([^']+)'\)/g))
        assert(match[1].startsWith('DcsExtraTelemetryPlugin.DCSExtra.'), `unqualified property in ${item.Description}: ${match[1]}`);
    }
  }
}

const uh1 = load(path.join('profiles', 'uh1', 'DCS World - DCS UH-1H - DCSExtra Baseline v0.1.siprofile'));
assert(uh1.profile.Name === 'DCS UH-1H - DCSExtra Baseline v0.1', 'UH-1H profile name is incorrect');
assert(uh1.profile.GlobalGain === 42 && uh1.effects.length === 31, `unexpected UH-1H baseline: ${uh1.effects.length} effects`);
assertQualified(uh1.effects);
const uh1One = effect(uh1.effects, 'UH-1H rotor 1-rev airframe pulse');
const uh1Blade = effect(uh1.effects, 'UH-1H two-blade rotor beat');
assert(uh1One.FrontLeftFormula.Expression.includes("AircraftFamily') === 'UH1'"), 'UH-1H 1/rev family gate missing');
assert(uh1One.FrontLeftFrequencyFormula.Expression.includes('MainRotorHz'), 'UH-1H 1/rev is not dynamic');
assert(uh1Blade.FrontLeftFrequencyFormula.Expression.includes('BladePassHz'), 'UH-1H blade beat is not dynamic');
assert(!uh1.effects.some(item => item.Description.includes('structural harmonic')), 'UH-1H contains a duplicate 2/rev structural layer');
assert(!uh1.effects.some(item => /wheel|tailwheel|M230|GAU-19|missile/i.test(item.Description)), 'UH-1H contains an inapplicable effect');

const oh58 = load(path.join('profiles', 'oh58', 'DCS World - DCS OH-58D - DCSExtra Baseline v0.1.siprofile'));
assert(oh58.profile.Name === 'DCS OH-58D - DCSExtra Baseline v0.1', 'OH-58D profile name is incorrect');
assert(oh58.profile.GlobalGain === 42 && oh58.effects.length === 33, `unexpected OH-58D baseline: ${oh58.effects.length} effects`);
assertQualified(oh58.effects);
for (const name of ['OH-58D rotor 1-rev airframe pulse', 'OH-58D rotor 2-rev structural harmonic', 'OH-58D main rotor blade-pass 4-rev']) {
  const item = effect(oh58.effects, name);
  assert(item.FrontLeftFormula.Expression.includes("AircraftFamily') === 'OH58'"), `${name} family gate missing`);
}
assert(effect(oh58.effects, 'OH-58D rotor 1-rev airframe pulse').Output.Frequency === 6.58, 'OH-58D 1/rev baseline frequency is incorrect');
assert(effect(oh58.effects, 'OH-58D rotor 2-rev structural harmonic').Output.Frequency === 13.17, 'OH-58D 2/rev baseline frequency is incorrect');
assert(effect(oh58.effects, 'OH-58D main rotor blade-pass 4-rev').Output.Frequency === 26.33, 'OH-58D blade-pass baseline frequency is incorrect');
assert(effect(oh58.effects, 'OH-58D M3P .50-caliber gun rhythm').FrontLeftFrequencyFormula.Expression.includes('NominalWeaponRateHz'), 'M3P rhythm is not rate-driven');
assert(effect(oh58.effects, 'OH-58D M3P low-body thump').FrontLeftFrequencyFormula.Expression.includes('* 0.5'), 'M3P body layer is not a subharmonic');
assert(!oh58.effects.some(item => /wheel|tailwheel|M134|M230|GAU-19/i.test(item.Description)), 'OH-58D contains an inapplicable effect');

assert(hook.includes('nominalRpm, bladeCount = 324.0, 2.0'), 'UH-1H rotor geometry missing');
assert(hook.includes('readCockpitArgument(200)') && hook.includes('readCockpitArgument(124)'), 'UH-1H collective or torque source missing');
assert(hook.includes('nominalRpm, bladeCount = 395.0, 4.0'), 'OH-58D rotor geometry missing');
assert(hook.includes('rotorSource = "OH58EngineRpmProxy"') && hook.includes('readCockpitArgument(3)'), 'OH-58D rotor or collective source missing');
assert(hook.includes('source = "OH58SkidDrawArguments"'), 'OH-58D skid adapter missing');
assert(hook.includes('return 1100.0 / 60.0'), 'OH-58D M3P nominal rate missing');

console.log(`UH-1H/OH-58D profile regression passed (${uh1.effects.length}/${oh58.effects.length} effects).`);
