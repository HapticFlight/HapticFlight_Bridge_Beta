const fs = require('fs');
const path = require('path');
const { spawnSync } = require('child_process');

const repositoryRoot = path.resolve(__dirname, '..');

function parseArguments(argv) {
  const values = {};
  for (let index = 0; index < argv.length; index += 2) {
    const key = argv[index];
    const value = argv[index + 1];
    if (!key || !key.startsWith('--') || value === undefined) {
      throw new Error('Arguments must use --name value pairs.');
    }
    values[key.slice(2)] = value;
  }
  return values;
}

function usage() {
  console.error([
    'Usage:',
    '  node scripts/Benchmark-Uh60Profiles.js',
    '    --log <recording.jsonl>',
    '    --baseline <baseline.siprofile>',
    '    --candidate <candidate.siprofile>',
    '    [--output <directory>]',
    '    [--goals <profile-kpi-goals.json>]'
  ].join('\n'));
}

function finite(value, label) {
  const number = Number(value);
  if (!Number.isFinite(number)) throw new Error(`Missing or invalid benchmark value: ${label}`);
  return number;
}

function effect(result, name) {
  const found = result.effects.find(item => item.name === name);
  if (!found) throw new Error(`Effect not found in ${result.profile.name}: ${name}`);
  return found;
}

function segment(result, name) {
  const found = result.diagnosticSegments && result.diagnosticSegments[name];
  if (!found || !found.samples) throw new Error(`Diagnostic segment is unavailable: ${name}`);
  return found;
}

function rotorMeanAbsStep(result) {
  return [
    'Rotor 1-rev airframe pulse',
    'Rotor 2-rev structural harmonic',
    'Main rotor blade-pass 4-rev'
  ].reduce((sum, name) => sum + finite(effect(result, name).meanAbsStep, `${name} meanAbsStep`), 0);
}

function stationaryRotorMean(result) {
  const effects = segment(result, 'stationaryHotGround').effects;
  return [
    'Rotor 1-rev airframe pulse',
    'Rotor 2-rev structural harmonic',
    'Main rotor blade-pass 4-rev'
  ].reduce((sum, name) => sum + finite(effects[name].mean, `${name} stationary mean`), 0);
}

function segmentEffectMean(result, segmentName, effectName) {
  const effects = segment(result, segmentName).effects;
  if (!effects[effectName]) throw new Error(`Effect not found in ${segmentName}: ${effectName}`);
  return finite(effects[effectName].mean, `${effectName} ${segmentName} mean`);
}

function calmRotorMean(result) {
  return [
    'Rotor 1-rev airframe pulse',
    'Rotor 2-rev structural harmonic',
    'Main rotor blade-pass 4-rev'
  ].reduce((sum, name) => sum + segmentEffectMean(result, 'calmAirborne', name), 0);
}

function formulaErrors(result) {
  return result.effects.reduce((sum, item) => sum + finite(item.formulaErrors || 0, `${item.name} formulaErrors`), 0);
}

function missingProperties(result) {
  const coverage = new Map(result.propertyCoverage.map(item => [item.name, item]));
  const capabilityFallbacks = new Map([
    ['EngineTorqueLeftPct', 'Has.EngineTorque'],
    ['EngineTorqueRightPct', 'Has.EngineTorque'],
    ['TorqueGaugeNormalized', 'Has.TorqueGauge']
  ]);
  return result.propertyCoverage.filter(item => {
    if (finite(item.coveragePercent, `${item.name} coveragePercent`) !== 0) return false;
    const capabilityName = capabilityFallbacks.get(item.name);
    if (!capabilityName) return true;
    const capability = coverage.get(capabilityName);
    return !capability || finite(capability.trueSamples || 0, `${capabilityName} trueSamples`) > 0;
  }).length;
}

function evaluateMetrics(baseline, candidate, goals) {
  const baselineCalm = finite(segment(baseline, 'calmAirborne').combined.mean, 'baseline calm mean');
  const candidateCalm = finite(segment(candidate, 'calmAirborne').combined.mean, 'candidate calm mean');
  const turbulenceName = 'Turbulence body thump';

  const definitions = [
    {
      category: 'Primary: smoothness',
      metric: 'Combined mean absolute step',
      baseline: finite(baseline.combined.meanAbsStep, 'baseline combined meanAbsStep'),
      candidate: finite(candidate.combined.meanAbsStep, 'candidate combined meanAbsStep'),
      target: `<= ${goals.combinedMeanAbsStepMax}`,
      pass: value => value <= goals.combinedMeanAbsStepMax
    },
    {
      category: 'Primary: smoothness',
      metric: 'Combined p99 step',
      baseline: finite(baseline.combined.p99Step, 'baseline combined p99Step'),
      candidate: finite(candidate.combined.p99Step, 'candidate combined p99Step'),
      target: `<= ${goals.combinedP99StepMax}`,
      pass: value => value <= goals.combinedP99StepMax
    },
    {
      category: 'Primary: smoothness',
      metric: 'Rotor step ratio to baseline',
      baseline: 1,
      candidate: rotorMeanAbsStep(candidate) / rotorMeanAbsStep(baseline),
      target: `<= ${goals.rotorStepRatioToBaselineMax}`,
      pass: value => value <= goals.rotorStepRatioToBaselineMax
    },
    {
      category: 'Primary: proportionality',
      metric: 'Clean strong-dive / calm mean',
      baseline: finite(segment(baseline, 'cleanStrongDive').combined.mean, 'baseline clean dive mean') / baselineCalm,
      candidate: finite(segment(candidate, 'cleanStrongDive').combined.mean, 'candidate clean dive mean') / candidateCalm,
      target: `>= ${goals.cleanDiveToCalmRatioMin}`,
      pass: value => value >= goals.cleanDiveToCalmRatioMin
    },
    {
      category: 'Primary: proportionality',
      metric: 'Strong-climb / calm mean',
      baseline: finite(segment(baseline, 'strongClimb').combined.mean, 'baseline strong climb mean') / baselineCalm,
      candidate: finite(segment(candidate, 'strongClimb').combined.mean, 'candidate strong climb mean') / candidateCalm,
      target: `>= ${goals.strongClimbToCalmRatioMin}`,
      pass: value => value >= goals.strongClimbToCalmRatioMin
    },
    {
      category: 'Primary: ground presence',
      metric: 'Stationary hot-ground combined rotor mean',
      baseline: stationaryRotorMean(baseline),
      candidate: stationaryRotorMean(candidate),
      target: `>= ${goals.stationaryHotGroundRotorMeanMin}`,
      pass: value => value >= goals.stationaryHotGroundRotorMeanMin
    },
    ...[
      ['Rotor 1-rev airframe pulse', goals.stationaryHotGroundOneRevMeanMin],
      ['Rotor 2-rev structural harmonic', goals.stationaryHotGroundTwoRevMeanMin],
      ['Main rotor blade-pass 4-rev', goals.stationaryHotGroundFourRevMeanMin]
    ].map(([name, minimum]) => ({
      category: 'Primary: ground presence',
      metric: `Stationary hot-ground ${name}`,
      baseline: segmentEffectMean(baseline, 'stationaryHotGround', name),
      candidate: segmentEffectMean(candidate, 'stationaryHotGround', name),
      target: `>= ${minimum}`,
      pass: value => value >= minimum
    })),
    {
      category: 'Primary: calm-flight presence',
      metric: 'Calm-airborne combined rotor mean',
      baseline: calmRotorMean(baseline),
      candidate: calmRotorMean(candidate),
      target: `>= ${goals.calmAirborneRotorMeanMin}`,
      pass: value => value >= goals.calmAirborneRotorMeanMin
    },
    ...[
      ['Rotor 1-rev airframe pulse', goals.calmAirborneOneRevMeanMin],
      ['Rotor 2-rev structural harmonic', goals.calmAirborneTwoRevMeanMin],
      ['Main rotor blade-pass 4-rev', goals.calmAirborneFourRevMeanMin]
    ].map(([name, minimum]) => ({
      category: 'Primary: calm-flight presence',
      metric: `Calm-airborne ${name}`,
      baseline: segmentEffectMean(baseline, 'calmAirborne', name),
      candidate: segmentEffectMean(candidate, 'calmAirborne', name),
      target: `>= ${minimum}`,
      pass: value => value >= minimum
    })),
    {
      category: 'Guardrail: strength',
      metric: 'Combined mean ratio to baseline',
      baseline: 1,
      candidate: finite(candidate.combined.mean, 'candidate combined mean') / finite(baseline.combined.mean, 'baseline combined mean'),
      target: `${goals.combinedMeanRatioMin} to ${goals.combinedMeanRatioMax}`,
      pass: value => value >= goals.combinedMeanRatioMin && value <= goals.combinedMeanRatioMax
    },
    {
      category: 'Guardrail: strength',
      metric: 'Combined p95 ratio to baseline',
      baseline: 1,
      candidate: finite(candidate.combined.p95, 'candidate combined p95') / finite(baseline.combined.p95, 'baseline combined p95'),
      target: `${goals.combinedP95RatioMin} to ${goals.combinedP95RatioMax}`,
      pass: value => value >= goals.combinedP95RatioMin && value <= goals.combinedP95RatioMax
    },
    {
      category: 'Guardrail: nuisance',
      metric: 'Turbulence mean output',
      baseline: finite(effect(baseline, turbulenceName).mean, 'baseline turbulence mean'),
      candidate: finite(effect(candidate, turbulenceName).mean, 'candidate turbulence mean'),
      target: `<= ${goals.turbulenceMeanMax}`,
      pass: value => value <= goals.turbulenceMeanMax
    },
    {
      category: 'Guardrail: nuisance',
      metric: 'Turbulence active percent',
      baseline: finite(effect(baseline, turbulenceName).activePercent, 'baseline turbulence activePercent'),
      candidate: finite(effect(candidate, turbulenceName).activePercent, 'candidate turbulence activePercent'),
      target: `<= ${goals.turbulenceActivePercentMax}%`,
      pass: value => value <= goals.turbulenceActivePercentMax
    },
    {
      category: 'Guardrail: reliability',
      metric: 'Formula evaluation errors',
      baseline: formulaErrors(baseline),
      candidate: formulaErrors(candidate),
      target: `<= ${goals.formulaErrorsMax}`,
      pass: value => value <= goals.formulaErrorsMax
    },
    {
      category: 'Guardrail: reliability',
      metric: 'Missing required referenced properties',
      baseline: missingProperties(baseline),
      candidate: missingProperties(candidate),
      target: `<= ${goals.missingPropertiesMax}`,
      pass: value => value <= goals.missingPropertiesMax
    },
    {
      category: 'Guardrail: transients',
      metric: 'Combined large-step count',
      baseline: finite(baseline.combined.largeSteps, 'baseline largeSteps'),
      candidate: finite(candidate.combined.largeSteps, 'candidate largeSteps'),
      target: `<= baseline + ${goals.largeStepsIncreaseMax}`,
      pass: value => value <= finite(baseline.combined.largeSteps, 'baseline largeSteps') + goals.largeStepsIncreaseMax
    }
  ];

  return definitions.map(item => ({
    category: item.category,
    metric: item.metric,
    baseline: item.baseline,
    candidate: item.candidate,
    deltaPercent: item.baseline === 0 ? null : 100 * (item.candidate / item.baseline - 1),
    target: item.target,
    status: item.pass(item.candidate) ? 'PASS' : 'GAP'
  }));
}

function csvEscape(value) {
  const text = value === null || value === undefined ? '' : String(value);
  return /[",\n]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text;
}

function format(value) {
  if (value === null || value === undefined) return '';
  return Number.isInteger(value) ? String(value) : Number(value).toFixed(4);
}

function writeOutputs(outputDirectory, report) {
  fs.mkdirSync(outputDirectory, { recursive: true });
  const jsonPath = path.join(outputDirectory, 'benchmark.json');
  const csvPath = path.join(outputDirectory, 'benchmark.csv');
  const markdownPath = path.join(outputDirectory, 'benchmark.md');
  fs.writeFileSync(jsonPath, JSON.stringify(report, null, 2));

  const headers = ['Category', 'Metric', 'Baseline', 'Candidate', 'DeltaPercent', 'Target', 'Status'];
  const csvRows = report.metrics.map(item => [
    item.category, item.metric, item.baseline, item.candidate,
    item.deltaPercent, item.target, item.status
  ].map(csvEscape).join(','));
  fs.writeFileSync(csvPath, [headers.join(','), ...csvRows].join('\n') + '\n');

  const tableRows = report.metrics.map(item =>
    `| ${item.category} | ${item.metric} | ${format(item.baseline)} | ${format(item.candidate)} | ${item.target} | ${item.status} |`);
  const markdown = [
    '# UH-60 profile KPI benchmark',
    '',
    `- Recording: \`${report.recording.path}\``,
    `- Baseline: **${report.baseline.name}**`,
    `- Candidate: **${report.candidate.name}**`,
    `- Result: **${report.summary.gaps === 0 ? 'ENGINEERING TARGETS MET' : `${report.summary.gaps} KPI GAP(S)`}**`,
    '',
    '| Category | Metric | Baseline | Candidate | Goal | Status |',
    '|---|---|---:|---:|---:|---|',
    ...tableRows,
    '',
    'Offline replay KPIs are engineering evidence, not a substitute for a structured seat test.'
  ].join('\n');
  fs.writeFileSync(markdownPath, markdown + '\n');
  return { jsonPath, csvPath, markdownPath };
}

function analyze(profilePath, logPath, resultPath) {
  const analyzer = path.join(repositoryRoot, 'analysis', 'uh60-v1.8.1-20260728', 'analyze-profile.js');
  const child = spawnSync(process.execPath, [analyzer, profilePath, logPath, resultPath], {
    cwd: repositoryRoot,
    encoding: 'utf8'
  });
  if (child.status !== 0) throw new Error(child.stderr || child.stdout || 'Profile replay failed.');
  return JSON.parse(fs.readFileSync(resultPath, 'utf8'));
}

function main() {
  let args;
  try {
    args = parseArguments(process.argv.slice(2));
  } catch (error) {
    usage();
    throw error;
  }
  if (!args.log || !args.baseline || !args.candidate) {
    usage();
    process.exit(2);
  }

  const outputDirectory = path.resolve(args.output || path.join(repositoryRoot, 'analysis', 'profile-benchmark'));
  const goalsPath = path.resolve(args.goals || path.join(repositoryRoot, 'benchmarks', 'uh60', 'profile-kpi-goals.json'));
  const baselineResultPath = path.join(outputDirectory, 'baseline-results.json');
  const candidateResultPath = path.join(outputDirectory, 'candidate-results.json');
  fs.mkdirSync(outputDirectory, { recursive: true });

  const baseline = analyze(path.resolve(args.baseline), path.resolve(args.log), baselineResultPath);
  const candidate = analyze(path.resolve(args.candidate), path.resolve(args.log), candidateResultPath);
  if (baseline.recording.uh60Samples !== candidate.recording.uh60Samples) {
    throw new Error('Baseline and candidate were not evaluated over the same sample count.');
  }

  const goalsDocument = JSON.parse(fs.readFileSync(goalsPath, 'utf8'));
  const metrics = evaluateMetrics(baseline, candidate, goalsDocument.goals);
  const gaps = metrics.filter(item => item.status !== 'PASS').length;
  const report = {
    generatedAtUtc: new Date().toISOString(),
    goalsPath,
    recording: {
      path: path.resolve(args.log),
      samples: candidate.recording.uh60Samples,
      durationSeconds: candidate.recording.analyzedDurationSeconds
    },
    baseline: { name: baseline.profile.name, path: path.resolve(args.baseline) },
    candidate: { name: candidate.profile.name, path: path.resolve(args.candidate) },
    summary: { metrics: metrics.length, passes: metrics.length - gaps, gaps },
    metrics,
    caveat: 'Offline replay must be paired with structured seat-test ratings before promotion.'
  };
  const outputs = writeOutputs(outputDirectory, report);
  console.log(JSON.stringify({ ...report.summary, ...outputs }, null, 2));
  if (gaps > 0) process.exitCode = 3;
}

main();
