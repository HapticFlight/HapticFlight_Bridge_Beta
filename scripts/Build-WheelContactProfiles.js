// Stage 14: distinct, stronger individual-wheel contact cues.  A full
// touchdown remains separate and is not inferred from a single wheel.
const fs = require('fs'), path = require('path'), crypto = require('crypto');
const root = path.resolve(__dirname, '..');
const source = path.join(root, 'downloads', 'realism-test13');
const output = path.join(root, 'downloads', 'realism-test14');
const id = s => crypto.createHash('sha256').update(s).digest('hex').slice(0, 32)
  .replace(/(.{8})(.{4})(.{4})(.{4})(.{12})/, '$1-$2-$3-$4-$5');
const prefix = 'DcsExtraTelemetryPlugin.DCSExtra.';

function wheelContactFormula() {
  return `var version = String($prop('${prefix}Haptic.EngineVersion') || '').split('.');
var major = Number(version[0]), minor = Number(version[1]);
if (!$prop('${prefix}Connected') || !(major > 1 || (major === 1 && minor >= 5))) return 0;
var left = Math.max(0, Math.min(1, $prop('${prefix}Haptic.Contact.Left.Envelope')));
var right = Math.max(0, Math.min(1, $prop('${prefix}Haptic.Contact.Right.Envelope')));
var tail = Math.max(0, Math.min(1, $prop('${prefix}Haptic.Contact.Tail.Envelope')));
var mains = (left > 0 ? 1 : 0) + (right > 0 ? 1 : 0);
var contact = Math.max(left, right, tail);
var amplitude = mains >= 2 ? 68 * contact : mains === 1 ? 58 * contact : 42 * tail;
var touchdown = Math.max(0, Math.min(1, $prop('${prefix}Haptic.Touchdown.Envelope')));
return Math.max(0, Math.min(100, amplitude * (1 - 0.35 * touchdown)));`;
}

fs.mkdirSync(output, { recursive: true });
for (const file of fs.readdirSync(source).filter(f => f.endsWith('.siprofile')).sort()) {
  const profile = JSON.parse(fs.readFileSync(path.join(source, file), 'utf8'));
  profile.Name = profile.Name.replace('test.13', 'test.14');
  profile.ProfileId = id(profile.Name);
  profile.Description = 'Realism test.14; requires Engine 1.5.0. Adds direct, individual wheel-contact feedback for one- and two-wheel landings while retaining a separate whole-airframe touchdown event.';
  for (const group of profile.EffectsContainers || []) {
    group.ContainerId = id(profile.Name + '/' + group.Description);
    for (const effect of group.EffectsContainers || []) {
      effect.ContainerId = id(profile.Name + '/' + group.Description + '/' + effect.Description);
      effect.FrontLeftFormula.Expression = effect.FrontLeftFormula.Expression
        .replace(/major === 1 && minor >= 4/g, 'major === 1 && minor >= 5');
      if (effect.Description === 'Wheel contact detail') {
        effect.Gain = 20;
        effect.Output.Frequency = 32;
        effect.FrontLeftFormula.Expression = wheelContactFormula();
      }
    }
  }
  const destination = file.replace('test.13', 'test.14');
  const text = JSON.stringify(profile, null, 2) + '\n';
  const target = path.join(output, destination);
  if (fs.existsSync(target) && fs.readFileSync(target, 'utf8') !== text)
    throw new Error('Existing version differs; increment profile version instead of overwriting: ' + target);
  fs.writeFileSync(target, text);
}
console.log('Built six separate test.14 profiles requiring Engine 1.5.0.');
