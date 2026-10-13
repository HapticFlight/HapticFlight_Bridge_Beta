// Stage 16: cross-shaker rotor defaults and separately felt wheel-braking
// stages. Surface texture formulas are deliberately copied unchanged.
const fs = require('fs'), path = require('path'), crypto = require('crypto');
const root = path.resolve(__dirname, '..');
const source = path.join(root, 'downloads', 'realism-test15');
const output = path.join(root, 'downloads', 'realism-test16');
const id = s => crypto.createHash('sha256').update(s).digest('hex').slice(0, 32)
  .replace(/(.{8})(.{4})(.{4})(.{4})(.{12})/, '$1-$2-$3-$4-$5');
const prefix = 'DcsExtraTelemetryPlugin.DCSExtra.';

function gate() {
  return `var version = String($prop('${prefix}Haptic.EngineVersion') || '').split('.');
var major = Number(version[0]), minor = Number(version[1]);
if (!$prop('${prefix}Connected') || !(major > 1 || (major === 1 && minor >= 7))) return 0;\n`;
}
function brakingFormula(signal, scale) {
  return `${gate()}if (!$prop('${prefix}OnGround') || !$prop('${prefix}Has.Brakes')) return 0;
var value = Math.max(0, Math.min(1, $prop('${prefix}Haptic.${signal}.Intensity')));
return Math.min(${scale}, ${scale} * value);`;
}
function cloneEffect(effect, description, gain, frequency, expression, noise) {
  const copy = JSON.parse(JSON.stringify(effect));
  copy.Description = description;
  copy.IsEnabled = true;
  copy.Gain = gain;
  copy.Output.Frequency = frequency;
  copy.Output.UseWhiteNoise = !!noise;
  copy.Output.WhiteNoise = noise || 0;
  copy.FrontLeftFormula.Expression = expression;
  copy.FrontLeftFormula.PreExpression = '';
  return copy;
}

fs.mkdirSync(output, { recursive: true });
for (const file of fs.readdirSync(source).filter(f => f.endsWith('.siprofile')).sort()) {
  const profile = JSON.parse(fs.readFileSync(path.join(source, file), 'utf8'));
  profile.Name = profile.Name.replace('test.15', 'test.16');
  profile.ProfileId = id(profile.Name);
  profile.Description = 'Realism test.16; requires Engine 1.7.0. Makes low-frequency rotor extensions optional and separates wheel-brake application, deceleration load, and derived lateral wheel-skid feedback. Surface-texture behavior is unchanged.';
  for (const group of profile.EffectsContainers || []) {
    const additions = [];
    for (const effect of group.EffectsContainers || []) {
      effect.FrontLeftFormula.Expression = effect.FrontLeftFormula.Expression
        .replace(/major === 1 && minor >= 6/g, 'major === 1 && minor >= 7');
      if (/\b1-rev\b|\b2-rev\b|two-blade rotor beat/i.test(effect.Description)) {
        effect.IsEnabled = false;
        effect.Description = '[OPTIONAL low-frequency extension] ' + effect.Description;
      }
      if (effect.Description === 'Wheel braking load') {
        effect.Description = 'Brake deceleration and airframe load';
        effect.Gain = 16;
        effect.Output.Frequency = 22;
        effect.FrontLeftFormula.Expression = brakingFormula('Braking.Deceleration', 46);
        additions.push(cloneEffect(effect, 'Brake pedal application', 9, 18,
          brakingFormula('Braking.Application', 30), 0));
        additions.push(cloneEffect(effect, '[DERIVED wheel-motion cue] Lateral wheel skid / scrub', 16, 34,
          brakingFormula('Ground.WheelSkid', 44), 30));
      }
    }
    group.EffectsContainers.push(...additions);
    group.ContainerId = id(profile.Name + '/' + group.Description);
    for (const effect of group.EffectsContainers) effect.ContainerId = id(profile.Name + '/' + group.Description + '/' + effect.Description);
  }
  const destination = file.replace('test.15', 'test.16');
  const text = JSON.stringify(profile, null, 2) + '\n';
  const target = path.join(output, destination);
  if (fs.existsSync(target) && fs.readFileSync(target, 'utf8') !== text)
    throw new Error('Existing version differs; increment profile version instead of overwriting: ' + target);
  fs.writeFileSync(target, text);
}
console.log('Built six separate test.16 profiles requiring Engine 1.7.0.');
