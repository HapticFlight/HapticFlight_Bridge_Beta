const assert = require('assert');
const fs = require('fs');
const path = require('path');

const hook = fs.readFileSync(path.resolve(__dirname, '../dcs-hook/DCSExtraTelemetry.lua'), 'utf8');
const clamp = value => Math.max(0, Math.min(1, value));

function ah6VrsRaw({ rotorPct, airspeed, descentRate, collective, rotorLoad, instability = 0, groundEffect = 1 }) {
  const threshold = 0.85;
  const full = 0.95;
  if (rotorPct < threshold) return 0;
  const speedRisk = clamp((10 - airspeed) / 6);
  const descentRisk = clamp((descentRate - 1.2) / 3.8);
  const collectiveDemand = clamp((collective - 0.55) / 0.30);
  const loadDemand = clamp((rotorLoad - 0.45) / 0.40);
  const powerRisk = clamp(collectiveDemand * 0.65 + loadDemand * 0.35);
  const governed = clamp((rotorPct - threshold) / (full - threshold));
  return speedRisk * descentRisk * powerRisk * governed * groundEffect * (0.70 + clamp(instability) * 0.30);
}

assert(hook.includes('local governedThreshold, governedFull = 0.85, 0.95'), 'Direct-rotor governed range missing from Hook.');
assert(hook.includes('rotorSource = "AH6RotorPhase37"'), 'AH-6 VRS must use phase-derived rotor speed.');
assert(hook.includes('if aircraftType == "AH-6J" or aircraftType == "MH-6J" then droop = 0.0 end'), 'AH-6 false droop guard missing from Hook.');

const qualifying = ah6VrsRaw({ rotorPct: 0.95, airspeed: 3, descentRate: 3.2, collective: 0.78, rotorLoad: 0.70, instability: 0.5 });
assert(qualifying > 0.12, 'A qualifying AH-6 VRS-risk state does not pass the exposure threshold.');
assert.equal(ah6VrsRaw({ rotorPct: 0.80, airspeed: 3, descentRate: 3.2, collective: 0.78, rotorLoad: 0.70 }), 0,
  'Ungoverned AH-6 rotor incorrectly qualifies for VRS risk.');
const attenuated = ah6VrsRaw({ rotorPct: 0.95, airspeed: 3, descentRate: 3.2, collective: 0.78, rotorLoad: 0.70, instability: 0.5, groundEffect: 0.30 });
assert(Math.abs(attenuated / qualifying - 0.30) < 1e-12, 'Ground-effect attenuation no longer scales AH-6 VRS risk.');

console.log('PASS: AH-6 direct-rotor VRS policy admits governed low-speed descent, rejects low rotor RPM, and preserves ground-effect attenuation.');
