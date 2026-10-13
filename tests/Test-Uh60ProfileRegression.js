const fs = require('fs');
const path = require('path');

const repositoryRoot = path.resolve(__dirname, '..');
const profilePath = process.argv[2] || path.join(
    repositoryRoot,
    'profiles',
    'uh60',
    'DCS World - DCS UH60L - DCSExtra Refreshed v1.9.0.siprofile');
const profile = JSON.parse(fs.readFileSync(profilePath, 'utf8'));
const pluginSource = fs.readFileSync(path.join(
    repositoryRoot,
    'src',
    'DcsExtraTelemetry.Plugin',
    'DcsExtraTelemetryPlugin.cs'), 'utf8');

function assert(condition, message) {
    if (!condition) throw new Error(message);
}

const registered = new Set(Array.from(
    pluginSource.matchAll(/AttachDelegate\("(DCSExtra\.[^"]+)"/g),
    match => `DcsExtraTelemetryPlugin.${match[1]}`));
const referenced = new Set();
const effects = [];

for (const group of profile.EffectsContainers || []) {
    for (const effect of group.EffectsContainers || []) {
        effects.push(effect);
        for (const formulaName of ['FrontLeftFormula', 'FrontLeftFrequencyFormula']) {
            const formula = effect[formulaName] || {};
            const expression = formula.Expression || '';
            const preExpression = formula.PreExpression || '';
            for (const source of [expression, preExpression]) {
                for (const match of source.matchAll(/\$prop\('([^']+)'\)/g)) referenced.add(match[1]);
                if (source) Function('$prop', 'root', source);
            }
        }
    }
}

const missing = Array.from(referenced).filter(name => !registered.has(name));
assert(missing.length === 0, `Unregistered profile properties: ${missing.join(', ')}`);
assert(profile.Name.endsWith('v1.9.0'), 'Generated profile is not v1.9.0.');
assert(profile.EffectsContainers.length === 5, 'Expected five effect groups.');
assert(effects.length === 41, `Expected 41 effects, found ${effects.length}.`);

function findEffect(name) {
    const effect = effects.find(candidate => candidate.Description === name);
    assert(effect, `Effect not found: ${name}`);
    return effect;
}

function evaluator(effect) {
    const root = {};
    const formula = effect.FrontLeftFormula;
    if (formula.PreExpression) Function('$prop', 'root', formula.PreExpression)(() => 0, root);
    const run = Function('$prop', 'root', formula.Expression || 'return 0;');
    return properties => run(name => properties[name] ?? 0, root);
}

const prefix = 'DcsExtraTelemetryPlugin.DCSExtra.';
const base = {
    [`${prefix}Connected`]: true,
    [`${prefix}Has.GearStruts`]: true,
    [`${prefix}Has.AerodynamicEffects`]: true,
    [`${prefix}Has.WeaponEffects`]: true,
    [`${prefix}Has.PowertrainEffects`]: true
};

const touchdown = evaluator(findEffect('Touchdown body impact'));
assert(touchdown({ ...base, [`${prefix}TouchdownStrength`]: 1, [`${prefix}TouchdownEnvelope`]: 0 }) === 0,
    'Latched touchdown strength leaked into the v1.8.2 touchdown effect.');
assert(touchdown({ ...base, [`${prefix}TouchdownEnvelope`]: 0.5 }) > 0,
    'Touchdown envelope did not drive the touchdown effect.');

const impact = evaluator(findEffect('Airframe impact pulse'));
assert(impact({ ...base, [`${prefix}AirframeImpactPulse`]: 1, [`${prefix}WeaponBurstActive`]: 1 }) === 0,
    'Airframe impact was not suppressed during a weapon burst.');

const tailwheelSettle = findEffect('Tailwheel settle tick');
assert(tailwheelSettle.Gain === 18 && tailwheelSettle.Output.Frequency === 22,
    'Tailwheel settle cue is missing the v1.8.4 body-thump tuning.');
const tailwheelSettleFormula = evaluator(tailwheelSettle);
assert(tailwheelSettleFormula({
    ...base,
    [`${prefix}TailWheelContactPulse`]: 1
}) === 55, 'Tailwheel contact pulse did not produce the intended settle cue.');

const maneuverBody = findEffect('Maneuver load and vertical energy');
assert(maneuverBody.Gain === 26 && maneuverBody.Output.Frequency === 18,
    'Maneuver body tuning did not use the v1.8.2 restrained 18 Hz layer.');
assert(maneuverBody.Output.UseWhiteNoise === false
    && maneuverBody.FrontLeftFormula.PreExpression.includes('maneuverBody'),
    'Maneuver body smoothing was not retained.');

const turbulenceTexture = findEffect('Airframe buffet texture');
assert(turbulenceTexture.Gain === 5 && turbulenceTexture.Output.Frequency === 22
    && turbulenceTexture.Output.UseWhiteNoise === false
    && turbulenceTexture.FrontLeftFormula.Expression.includes('(raw - 0.19) / 0.38')
    && turbulenceTexture.FrontLeftFormula.Expression.includes('? 0.15 : 0.035'),
    'Buffet texture did not retain the validated low-onset tuning.');

const spoolEffect = findEffect('Rotor spool transition');
assert(spoolEffect.FrontLeftFormula.PreExpression.includes('rotorSpoolLevel')
    && spoolEffect.FrontLeftFormula.Expression.includes('(raw - 0.10) / 0.35'),
    'Rotor spool transition is missing the v1.8.2 deadband and smoothing state.');
const spool = evaluator(spoolEffect);
let spoolValue = 0;
for (let index = 0; index < 100; index += 1)
    spoolValue = spool({ ...base, [`${prefix}RotorSpoolIntensity`]: 0.05 });
assert(spoolValue === 0, `Sub-deadband rotor spool noise produced output: ${spoolValue}`);
for (let index = 0; index < 20; index += 1)
    spoolValue = spool({ ...base, [`${prefix}RotorSpoolIntensity`]: 0.5 });
assert(spoolValue > 20 && spoolValue <= 48,
    `Real rotor spool transition did not ramp into the intended range: ${spoolValue}`);

const governor = findEffect('Engine governor load response');
assert(governor.Gain === 14 && governor.Output.Frequency === 22
    && governor.Output.UseWhiteNoise === false,
    'Governor layer did not retain the v1.8.2 hum reduction.');

const rotorFourRev = findEffect('Main rotor blade-pass 4-rev');
assert(rotorFourRev.Gain === 30
    && rotorFourRev.FrontLeftFormula.Expression.includes('Math.min(62')
    && rotorFourRev.FrontLeftFormula.PreExpression.includes('rotorFourRevLevel'),
    'Continuous four-rev rotor layer did not retain its cap and smoothing state.');

for (const name of ['Rotor 1-rev airframe pulse', 'Rotor 2-rev structural harmonic', 'Main rotor blade-pass 4-rev']) {
    const rotor = findEffect(name);
    const expression = rotor.FrontLeftFormula.Expression;
    assert(expression.includes('Has.EngineTorque') && expression.includes('Has.TorqueGauge')
        && expression.includes('Has.ControlPositions'),
    `${name} is missing torque-first / collective-fallback power response.`);
    assert(expression.includes('Has.Acceleration') && expression.includes('AccelYG')
        && expression.includes('smoothG'),
    `${name} is missing signed progressive normal-G response.`);
    assert(expression.includes("var airborne = !$prop('DcsExtraTelemetryPlugin.DCSExtra.OnGround')")
        && expression.includes('&& airborne ?'),
    `${name} does not gate signed-G swell off on the ground.`);
}

const rotorCalibration = [
    ['Rotor 1-rev airframe pulse', 20, 2.8, 3.8],
    ['Rotor 2-rev structural harmonic', 16, 1.6, 2.3],
    ['Main rotor blade-pass 4-rev', 30, 3.8, 5.5]
];
let hotGroundCombined = 0;
let calmAirborneCombined = 0;
for (const [name, expectedGain, hotMinimum, calmMinimum] of rotorCalibration) {
    const effect = findEffect(name);
    assert(effect.Gain === expectedGain, `${name} does not use the seat-calibrated gain.`);

    const settle = properties => {
        const run = evaluator(effect);
        let value = 0;
        for (let index = 0; index < 160; index += 1) value = run(properties);
        return value * effect.Gain / 100 * profile.GlobalGain / 100;
    };
    const running = {
        ...base,
        [`${prefix}Has.RotorDynamics`]: true,
        [`${prefix}Has.ControlPositions`]: true,
        [`${prefix}RotorRunning`]: true,
        [`${prefix}MainRotorRpmPct`]: 100,
        [`${prefix}AccelYG`]: 1
    };
    const hot = settle({
        ...running,
        [`${prefix}OnGround`]: 1,
        [`${prefix}CollectivePosition`]: 0.1,
        [`${prefix}RotorLoadIntensity`]: 0.07
    });
    const calm = settle({
        ...running,
        [`${prefix}OnGround`]: 0,
        [`${prefix}CollectivePosition`]: 0.48,
        [`${prefix}RotorLoadIntensity`]: 0.48
    });
    assert(hot >= hotMinimum, `${name} hot-ground presence is below its tactile floor: ${hot}`);
    assert(calm >= calmMinimum, `${name} calm-airborne presence is below its tactile floor: ${calm}`);
    hotGroundCombined += hot;
    calmAirborneCombined += calm;

    const normalGround = evaluator(effect);
    const impactGround = evaluator(effect);
    let normalValue = 0;
    let impactValue = 0;
    for (let index = 0; index < 80; index += 1) {
        normalValue = normalGround({ ...running, [`${prefix}OnGround`]: 1, [`${prefix}Has.Acceleration`]: true, [`${prefix}AccelYG`]: 1 });
        impactValue = impactGround({ ...running, [`${prefix}OnGround`]: 1, [`${prefix}Has.Acceleration`]: true, [`${prefix}AccelYG`]: 159.5 });
    }
    assert(Math.abs(normalValue - impactValue) < 1e-9,
        `${name} allowed a ground-contact acceleration spike into signed-G swell.`);
}
assert(hotGroundCombined >= 8.2,
    `Combined hot-ground rotor presence is below the seat-calibrated floor: ${hotGroundCombined}`);
assert(calmAirborneCombined >= 11.5,
    `Combined calm-airborne rotor presence is below the seat-calibrated floor: ${calmAirborneCombined}`);

for (const [name, state] of [
    ['Rotor 1-rev airframe pulse', 'rotorOneRevLevel'],
    ['Rotor 2-rev structural harmonic', 'rotorTwoRevLevel'],
    ['Main rotor blade-pass 4-rev', 'rotorFourRevLevel']
]) {
    const effect = findEffect(name);
    assert(effect.FrontLeftFormula.PreExpression.includes(state),
        `${name} is missing its composite smoothing state.`);
    assert(effect.FrontLeftFormula.Expression.includes('? 0.35 : 0.14'),
        `${name} is missing the intended attack/release coefficients.`);

    const rotor = evaluator(effect);
    const running = {
        ...base,
        [`${prefix}Has.RotorDynamics`]: true,
        [`${prefix}RotorRunning`]: true,
        [`${prefix}MainRotorRpmPct`]: 100
    };
    const first = rotor(running);
    const second = rotor(running);
    assert(first > 0 && second > first,
        `${name} did not ramp smoothly toward its running target.`);

    let decayed = second;
    for (let index = 0; index < 100; index += 1) decayed = rotor({});
    assert(decayed === 0, `${name} retained stale output after shutdown: ${decayed}`);
}

const heavy = evaluator(findEffect('Weapon-specific heavy gun rhythm'));
const thump = evaluator(findEffect('M230 individual-shot body thump'));
const m230 = {
    ...base,
    [`${prefix}LastFiredWeaponName`]: 'Twin M230 30 mm Cannons',
    [`${prefix}HeavyGunIntensity`]: 1
};
let heavyValue = 0;
let thumpValue = 0;
for (let index = 0; index < 40; index += 1) {
    heavyValue = heavy(m230);
    thumpValue = thump(m230);
}
const effectiveAtGlobalGain = heavyValue * 0.24 * 0.5 + thumpValue * 0.10 * 0.5;
assert(effectiveAtGlobalGain <= 11.0,
    `M230 dedicated layers exceeded the v1.8.2 headroom target: ${effectiveAtGlobalGain}`);

const brakingEffect = findEffect('Wheel braking load');
assert(brakingEffect.Gain === 24 && brakingEffect.Output.Frequency === 22,
    'Wheel braking did not receive the stronger 22 Hz tuning.');
assert(brakingEffect.FrontLeftFormula.Expression.includes('Has.Brakes')
    && brakingEffect.FrontLeftFormula.Expression.includes('BrakeDecelerationIntensity')
    && brakingEffect.FrontLeftFormula.Expression.includes('Math.min(70'),
    'Wheel braking is missing its capability gate, deceleration input, or cap.');
const braking = evaluator(brakingEffect);
let brakingValue = 0;
for (let index = 0; index < 30; index += 1) brakingValue = braking({
    ...base,
    [`${prefix}Has.Brakes`]: true,
    [`${prefix}OnGround`]: true,
    [`${prefix}BrakingIntensity`]: 0.55,
    [`${prefix}BrakeDecelerationIntensity`]: 0.7
});
assert(brakingValue > 40 && brakingValue <= 70,
    `Wheel braking did not settle into its intended stronger range: ${brakingValue}`);

for (const [name, gain, frequency, property] of [
    ['Gear-borne rotor vibration', 8, 13, 'GroundResonanceIntensity'],
    ['Ground support rocking', 10, 16, 'GroundPitchRockIntensity']
]) {
    const effect = findEffect(name);
    assert(effect.Gain === gain && effect.Output.Frequency === frequency
        && effect.FrontLeftFormula.Expression.includes(property)
        && effect.FrontLeftFormula.PreExpression.length > 0,
    `${name} is missing its restrained gain, frequency, telemetry input, or smoothing.`);
}

const gearBorneEffect = findEffect('Gear-borne rotor vibration');
assert(gearBorneEffect.FrontLeftFormula.Expression.includes('MainRotorRpmPct')
    && gearBorneEffect.FrontLeftFormula.Expression.includes('WeightOnGroundIntensity')
    && gearBorneEffect.FrontLeftFormula.Expression.includes('RotorRunning'),
    'Gear-borne rotor vibration is missing rotor-RPM, supported-weight, or running-state input.');
const gearBorne = evaluator(gearBorneEffect);
let parkedHot = 0;
for (let index = 0; index < 100; index += 1) parkedHot = gearBorne({
    ...base,
    [`${prefix}Has.DetailedGroundEffects`]: true,
    [`${prefix}Has.RotorDynamics`]: true,
    [`${prefix}OnGround`]: true,
    [`${prefix}RotorRunning`]: true,
    [`${prefix}MainRotorRpmPct`]: 100,
    [`${prefix}WeightOnGroundIntensity`]: 0.20,
    [`${prefix}GroundResonanceIntensity`]: 0.12
});
assert(parkedHot >= 15 && parkedHot <= 16,
    `Parked-hot gear-borne rotor floor is outside its intended range: ${parkedHot}`);
let airborne = parkedHot;
for (let index = 0; index < 150; index += 1) airborne = gearBorne({
    ...base,
    [`${prefix}Has.DetailedGroundEffects`]: true,
    [`${prefix}Has.RotorDynamics`]: true,
    [`${prefix}OnGround`]: false,
    [`${prefix}RotorRunning`]: true,
    [`${prefix}MainRotorRpmPct`]: 100
});
assert(airborne === 0, `Gear-borne rotor vibration persisted airborne: ${airborne}`);

console.log(`UH-60 v1.9.0 profile regression passed (${effects.length} effects, ${referenced.size} properties).`);
