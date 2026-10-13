const fs = require('fs');
const path = require('path');
const readline = require('readline');

const SIGNALS = {
  'timing.intervalMs': p => p.sentAtMs,
  'flight.trueAirSpeedMps': p => p.values?.trueAirSpeedMps,
  'flight.verticalVelocityMps': p => p.values?.verticalVelocityMps,
  'flight.normalG': p => p.values?.dynamics?.normalG,
  'rotor.mainRotorRpmPct': p => p.values?.dynamics?.mainRotorRpmPct,
  'rotor.rotorLoadIntensity': p => p.values?.dynamics?.rotorLoadIntensity,
  'rotor.transmissionLoadIntensity': p => p.values?.dynamics?.transmissionLoadIntensity,
  'rotor.governorResponseIntensity': p => p.values?.dynamics?.engineGovernorResponseIntensity,
  'rotor.spoolIntensity': p => p.values?.dynamics?.rotorSpoolIntensity,
  'aero.maneuverResponseIntensity': p => p.values?.dynamics?.maneuverResponseIntensity,
  'aero.turbulenceIntensity': p => p.values?.dynamics?.turbulenceIntensity,
  'aero.airframeBuffetIntensity': p => p.values?.dynamics?.airframeBuffetIntensity,
  'aero.bladeSlapIntensity': p => p.values?.dynamics?.bladeSlapIntensity,
  'ground.groundSpeedMps': p => p.values?.ground?.groundSpeedMps,
  'ground.groundRollIntensity': p => p.values?.ground?.groundRollIntensity,
  'ground.groundResonanceIntensity': p => p.values?.ground?.groundResonanceIntensity,
  'ground.suspensionMovementIntensity': p => p.values?.ground?.suspensionMovementIntensity,
  'ground.groundPitchRockIntensity': p => p.values?.ground?.groundPitchRockIntensity,
  'ground.groundRollRockIntensity': p => p.values?.ground?.groundRollRockIntensity,
  'ground.wheelShimmyIntensity': p => p.values?.ground?.wheelShimmyIntensity,
  'ground.tailWheelCasterIntensity': p => p.values?.ground?.tailWheelCasterIntensity,
  'ground.skidScrubIntensity': p => p.values?.ground?.skidScrubIntensity,
  'ground.brakingIntensity': p => p.values?.ground?.brakingIntensity,
  'ground.brakeDecelerationIntensity': p => p.values?.ground?.brakeDecelerationIntensity,
  'ground.wheelBrakesPosition': p => p.values?.ground?.wheelBrakesPosition,
  'ground.touchdownStrength': p => p.values?.ground?.touchdownStrength,
  'ground.touchdownVerticalSpeedMps': p => p.values?.ground?.touchdownVerticalSpeedMps,
  'effects.touchdownEnvelope': p => p.values?.effects?.touchdownEnvelope,
  'effects.brakeEnvelope': p => p.values?.effects?.brakeEnvelope,
  'effects.weaponRecoilEnvelope': p => p.values?.effects?.weaponRecoilEnvelope,
  'weapons.lightGunIntensity': p => p.values?.weapons?.lightGunIntensity,
  'weapons.heavyGunIntensity': p => p.values?.weapons?.heavyGunIntensity,
  'weapons.rocketLaunchIntensity': p => p.values?.weapons?.rocketLaunchIntensity,
  'weapons.missileLaunchIntensity': p => p.values?.weapons?.missileLaunchIntensity,
  'countermeasures.chaffReleasePulse': p => p.values?.countermeasures?.chaffReleasePulse,
  'countermeasures.flareReleasePulse': p => p.values?.countermeasures?.flareReleasePulse,
  'doors.movementIntensity': p => p.values?.cockpit?.doors?.doorMovementIntensity
};

function number(value) {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : null;
}

function percentile(sorted, fraction) {
  if (!sorted.length) return 0;
  const index = Math.min(sorted.length - 1, Math.max(0, Math.ceil(sorted.length * fraction) - 1));
  return sorted[index];
}

function summarize(values) {
  if (!values.length) return { samples: 0, nonZero: 0, mean: 0, p50: 0, p90: 0, p95: 0, p99: 0, max: 0 };
  values.sort((a, b) => a - b);
  const nonZero = values.reduce((count, value) => count + (Math.abs(value) > 1e-9 ? 1 : 0), 0);
  const total = values.reduce((sum, value) => sum + value, 0);
  return {
    samples: values.length,
    nonZero,
    nonZeroPercent: +(100 * nonZero / values.length).toFixed(3),
    mean: +(total / values.length).toFixed(6),
    p50: +percentile(values, .50).toFixed(6),
    p90: +percentile(values, .90).toFixed(6),
    p95: +percentile(values, .95).toFixed(6),
    p99: +percentile(values, .99).toFixed(6),
    max: +values[values.length - 1].toFixed(6)
  };
}

async function analyze(recordingPath) {
  const values = Object.fromEntries(Object.keys(SIGNALS).map(name => [name, []]));
  const capabilityTrue = {};
  const eventCounts = { touchdownPulse: 0, liftoffPulse: 0, lightGun: 0, heavyGun: 0, rocket: 0, missile: 0, chaff: 0, flare: 0, doorMovement: 0 };
  const state = { touchdownPulse: false, liftoffPulse: false, lightGun: false, heavyGun: false, rocket: false, missile: false, chaff: false, flare: false, doorMovement: false };
  const sessions = new Set();
  const aircraft = new Set();
  let samples = 0, malformed = 0, firstSentAt = null, lastSentAt = null, previousSentAt = null;
  let previousSequence = null, previousSession = null, sequenceGaps = 0, duplicateSequences = 0;
  let modelTimeBackwards = 0, modelTimeFrozen = 0, previousModelTime = null;
  let groundSamples = 0, airborneSamples = 0, contactMismatchSamples = 0;

  const input = fs.createReadStream(recordingPath, { encoding: 'utf8' });
  for await (const line of readline.createInterface({ input, crlfDelay: Infinity })) {
    if (!line.trim()) continue;
    let packet;
    try { packet = JSON.parse(line); } catch { malformed++; continue; }
    if (!packet || !packet.values || !packet.aircraft) { malformed++; continue; }
    samples++;
    aircraft.add(`${packet.aircraft.dcsType || 'UNKNOWN'} (${packet.aircraft.family || 'UNKNOWN'})`);
    sessions.add(String(packet.sessionId || ''));
    const sentAt = number(packet.sentAtMs);
    if (sentAt !== null) {
      if (firstSentAt === null) firstSentAt = sentAt;
      lastSentAt = sentAt;
      if (previousSentAt !== null) values['timing.intervalMs'].push(Math.max(0, sentAt - previousSentAt));
      previousSentAt = sentAt;
    }
    const sequence = number(packet.sequence);
    const session = String(packet.sessionId || '');
    if (session === previousSession && sequence !== null && previousSequence !== null) {
      if (sequence === previousSequence) duplicateSequences++;
      else if (sequence > previousSequence + 1) sequenceGaps += sequence - previousSequence - 1;
    }
    previousSequence = sequence;
    previousSession = session;
    const modelTime = number(packet.modelTime);
    if (modelTime !== null && previousModelTime !== null) {
      if (modelTime < previousModelTime) modelTimeBackwards++;
      else if (modelTime === previousModelTime) modelTimeFrozen++;
    }
    previousModelTime = modelTime;

    const ground = packet.values.ground || {};
    const onGround = Number(ground.onGround) >= .5;
    if (onGround) groundSamples++; else airborneSamples++;
    const anyContact = ['tailWheelContact','leftWheelContact','rightWheelContact','skidFrontLeftContact','skidFrontRightContact','skidRearLeftContact','skidRearRightContact']
      .some(key => Number(ground[key]) >= .5);
    if (onGround !== anyContact) contactMismatchSamples++;

    for (const [name, getter] of Object.entries(SIGNALS)) {
      if (name === 'timing.intervalMs') continue;
      const value = number(getter(packet));
      if (value !== null) values[name].push(value);
    }
    for (const [name, enabled] of Object.entries(packet.capabilities || {})) {
      if (enabled === true) capabilityTrue[name] = (capabilityTrue[name] || 0) + 1;
    }

    const current = {
      touchdownPulse: Number(ground.touchdownPulse) > 0,
      liftoffPulse: Number(ground.liftoffPulse) > 0,
      lightGun: Number(packet.values.weapons?.lightGunIntensity) > 0,
      heavyGun: Number(packet.values.weapons?.heavyGunIntensity) > 0,
      rocket: Number(packet.values.weapons?.rocketLaunchIntensity) > 0,
      missile: Number(packet.values.weapons?.missileLaunchIntensity) > 0,
      chaff: Number(packet.values.countermeasures?.chaffReleasePulse) > 0,
      flare: Number(packet.values.countermeasures?.flareReleasePulse) > 0,
      doorMovement: Number(packet.values.cockpit?.doors?.doorMovementIntensity) > .02
    };
    for (const key of Object.keys(current)) {
      if (current[key] && !state[key]) eventCounts[key]++;
      state[key] = current[key];
    }
  }

  const statistics = Object.fromEntries(Object.entries(values).map(([name, samples]) => [name, summarize(samples)]));
  const durationSeconds = firstSentAt === null || lastSentAt === null ? 0 : (lastSentAt - firstSentAt) / 1000;
  return {
    schemaVersion: 1,
    recording: path.resolve(recordingPath),
    aircraft: [...aircraft],
    samples,
    malformedLines: malformed,
    durationSeconds: +durationSeconds.toFixed(3),
    averageRateHz: durationSeconds > 0 ? +(samples / durationSeconds).toFixed(3) : 0,
    sessions: sessions.size,
    timing: { sequenceGaps, duplicateSequences, modelTimeBackwards, modelTimeFrozen },
    phaseCoverage: {
      groundSamples,
      airborneSamples,
      groundPercent: samples ? +(100 * groundSamples / samples).toFixed(3) : 0,
      contactMismatchSamples
    },
    eventCounts,
    capabilityCoveragePercent: Object.fromEntries(Object.entries(capabilityTrue).sort().map(([name, count]) => [name, +(100 * count / samples).toFixed(3)])),
    statistics,
    caveat: 'Recorded telemetry and formula replay support engineering calibration; final tactile gain requires repeatable hardware seat testing.'
  };
}

async function main() {
  const inputPath = process.argv[2];
  const outputPath = process.argv[3];
  if (!inputPath) throw new Error('Usage: node scripts/Analyze-TelemetryRecording.js <recording.jsonl> [output.json]');
  const result = await analyze(path.resolve(inputPath));
  const json = JSON.stringify(result, null, 2) + '\n';
  if (outputPath) {
    const resolvedOutput = path.resolve(outputPath);
    fs.mkdirSync(path.dirname(resolvedOutput), { recursive: true });
    fs.writeFileSync(resolvedOutput, json);
  }
  process.stdout.write(json);
}

main().catch(error => { console.error(error.stack || error.message); process.exitCode = 1; });
