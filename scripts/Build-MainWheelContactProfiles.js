// Stage 17: preserve the direct per-wheel contact signal while giving soft
// main-wheel arrivals enough tactile headroom to remain distinct from a
// tail-first strike. Surface texture and touchdown-body effects are unchanged.
const fs = require('fs'), path = require('path'), crypto = require('crypto');
const root = path.resolve(__dirname, '..');
const source = path.join(root, 'downloads', 'realism-test16');
const output = path.join(root, 'downloads', 'realism-test17');
const id = s => crypto.createHash('sha256').update(s).digest('hex').slice(0, 32)
  .replace(/(.{8})(.{4})(.{4})(.{4})(.{12})/, '$1-$2-$3-$4-$5');
const prefix = 'DcsExtraTelemetryPlugin.DCSExtra.';

function gate() {
  return `var version = String($prop('${prefix}Haptic.EngineVersion') || '').split('.');
var major = Number(version[0]), minor = Number(version[1]), patch = Number(version[2] || 0);
if (!$prop('${prefix}Connected') || !(major > 1 || (major === 1 && (minor > 7 || (minor === 7 && patch >= 1))))) return 0;\n`;
}

function mainWheelContactFormula() {
  return `${gate()}var left = Math.max(0, Math.min(1, $prop('${prefix}Haptic.Contact.Left.Envelope')));
var right = Math.max(0, Math.min(1, $prop('${prefix}Haptic.Contact.Right.Envelope')));
var tail = Math.max(0, Math.min(1, $prop('${prefix}Haptic.Contact.Tail.Envelope')));
var mains = (left > 0 ? 1 : 0) + (right > 0 ? 1 : 0);
var main = Math.max(left, right);
var amplitude = mains >= 2 ? 82 * main : mains === 1 ? 74 * main : 34 * tail;
var touchdown = Math.max(0, Math.min(1, $prop('${prefix}Haptic.Touchdown.Envelope')));
var duck = mains > 0 ? 1 - 0.12 * touchdown : 1 - 0.35 * touchdown;
return Math.max(0, Math.min(100, amplitude * duck));`;
}

const oldGate = /var version = String\(\$prop\('DcsExtraTelemetryPlugin\.DCSExtra\.Haptic\.EngineVersion'\) \|\| ''\)\.split\('\.'\);\nvar major = Number\(version\[0\]\), minor = Number\(version\[1\]\);\nif \(!\$prop\('DcsExtraTelemetryPlugin\.DCSExtra\.Connected'\) \|\| !\(major > 1 \|\| \(major === 1 && minor >= 7\)\)\) return 0;\n/g;

fs.mkdirSync(output, { recursive: true });
for (const file of fs.readdirSync(source).filter(f => f.endsWith('.siprofile')).sort()) {
  const profile = JSON.parse(fs.readFileSync(path.join(source, file), 'utf8'));
  profile.Name = profile.Name.replace('test.16', 'test.17');
  profile.ProfileId = id(profile.Name);
  profile.Description = 'Realism test.17; requires Engine 1.7.1. Raises direct main-wheel contact headroom for soft rolling landings while limiting tail-contact dominance. Touchdown-body, surface texture, and braking behavior are unchanged.';
  for (const group of profile.EffectsContainers || []) {
    for (const effect of group.EffectsContainers || []) {
      effect.FrontLeftFormula.Expression = effect.FrontLeftFormula.Expression.replace(oldGate, gate());
      if (effect.Description === 'Wheel contact detail') {
        effect.Gain = 22;
        effect.Output.Frequency = 30;
        effect.FrontLeftFormula.Expression = mainWheelContactFormula();
      }
    }
    group.ContainerId = id(profile.Name + '/' + group.Description);
    for (const effect of group.EffectsContainers || []) effect.ContainerId = id(profile.Name + '/' + group.Description + '/' + effect.Description);
  }
  const destination = file.replace('test.16', 'test.17');
  const text = JSON.stringify(profile, null, 2) + '\n';
  const target = path.join(output, destination);
  if (fs.existsSync(target) && fs.readFileSync(target, 'utf8') !== text)
    throw new Error('Existing version differs; increment profile version instead of overwriting: ' + target);
  fs.writeFileSync(target, text);
}
console.log('Built six separate test.17 profiles requiring Engine 1.7.1.');
