// Stage 15: derived VRS-risk texture. It intentionally remains optional: DCS
// does not export a vortex-ring-state flag, so the model must never be treated
// as a confirmed aerodynamic state.
const fs = require('fs'), path = require('path'), crypto = require('crypto');
const root = path.resolve(__dirname, '..');
const source = path.join(root, 'downloads', 'realism-test14');
const output = path.join(root, 'downloads', 'realism-test15');
const id = s => crypto.createHash('sha256').update(s).digest('hex').slice(0, 32)
  .replace(/(.{8})(.{4})(.{4})(.{4})(.{12})/, '$1-$2-$3-$4-$5');
const prefix = 'DcsExtraTelemetryPlugin.DCSExtra.';

function vrsFormula() {
  return `var version = String($prop('${prefix}Haptic.EngineVersion') || '').split('.');
var major = Number(version[0]), minor = Number(version[1]);
if (!$prop('${prefix}Connected') || !(major > 1 || (major === 1 && minor >= 6))) return 0;
if (!$prop('${prefix}Has.AerodynamicEffects') || $prop('${prefix}OnGround')) return 0;
var risk = Math.max(0, Math.min(1, $prop('${prefix}Haptic.Aerodynamic.VrsRisk.Intensity')));
return Math.min(32, 32 * Math.pow(risk, 0.86));`;
}

fs.mkdirSync(output, { recursive: true });
for (const file of fs.readdirSync(source).filter(f => f.endsWith('.siprofile')).sort()) {
  const profile = JSON.parse(fs.readFileSync(path.join(source, file), 'utf8'));
  profile.Name = profile.Name.replace('test.14', 'test.15');
  profile.ProfileId = id(profile.Name);
  profile.Description = 'Realism test.15; requires Engine 1.6.0. Adds an optional, time-qualified derived VRS-risk texture from aircraft state. It is not a direct DCS vortex-ring-state export.';
  for (const group of profile.EffectsContainers || []) {
    group.ContainerId = id(profile.Name + '/' + group.Description);
    for (const effect of group.EffectsContainers || []) {
      effect.ContainerId = id(profile.Name + '/' + group.Description + '/' + effect.Description);
      effect.FrontLeftFormula.Expression = effect.FrontLeftFormula.Expression
        .replace(/major === 1 && minor >= 5/g, 'major === 1 && minor >= 6');
      if (/Powered low-speed descent cue/i.test(effect.Description)) {
        effect.Description = '[OPTIONAL derived cue] VRS-risk texture (not a DCS VRS state)';
        effect.Gain = 16;
        effect.Output.Frequency = 18;
        effect.Output.UseWhiteNoise = true;
        effect.Output.WhiteNoise = 28;
        effect.FrontLeftFormula.Expression = vrsFormula();
      }
    }
  }
  const destination = file.replace('test.14', 'test.15');
  const text = JSON.stringify(profile, null, 2) + '\n';
  const target = path.join(output, destination);
  if (fs.existsSync(target) && fs.readFileSync(target, 'utf8') !== text)
    throw new Error('Existing version differs; increment profile version instead of overwriting: ' + target);
  fs.writeFileSync(target, text);
}
console.log('Built six separate test.15 profiles requiring Engine 1.6.0.');
