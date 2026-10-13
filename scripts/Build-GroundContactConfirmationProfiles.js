// Stage 13: preserve all previous profiles and require the engine that
// recognizes a Hook-confirmed tail-first rolling touchdown.
const fs = require('fs'), path = require('path'), crypto = require('crypto');
const root = path.resolve(__dirname, '..');
const source = path.join(root, 'downloads', 'realism-test12');
const output = path.join(root, 'downloads', 'realism-test13');
const id = s => crypto.createHash('sha256').update(s).digest('hex').slice(0, 32)
  .replace(/(.{8})(.{4})(.{4})(.{4})(.{12})/, '$1-$2-$3-$4-$5');

fs.mkdirSync(output, { recursive: true });
for (const file of fs.readdirSync(source).filter(f => f.endsWith('.siprofile')).sort()) {
  const profile = JSON.parse(fs.readFileSync(path.join(source, file), 'utf8'));
  profile.Name = profile.Name.replace('test.12', 'test.13');
  profile.ProfileId = id(profile.Name);
  profile.Description = 'Realism test.13; requires Engine 1.4.0. Preserves prior tuning and adds guarded Hook-confirmed tail-first rolling touchdown detection. Ground bumps remain established-support events; optional unconfirmed and augmented cues remain disabled.';
  for (const group of profile.EffectsContainers || []) {
    group.ContainerId = id(profile.Name + '/' + group.Description);
    for (const effect of group.EffectsContainers || []) {
      effect.ContainerId = id(profile.Name + '/' + group.Description + '/' + effect.Description);
      effect.FrontLeftFormula.Expression = effect.FrontLeftFormula.Expression
        .replace(/major === 1 && minor >= 3/g, 'major === 1 && minor >= 4');
    }
  }
  const destination = file.replace('test.12', 'test.13');
  const text = JSON.stringify(profile, null, 2) + '\n';
  const target = path.join(output, destination);
  if (fs.existsSync(target) && fs.readFileSync(target, 'utf8') !== text)
    throw new Error('Existing version differs; increment profile version instead of overwriting: ' + target);
  fs.writeFileSync(target, text);
}
console.log('Built six separate test.13 profiles requiring Engine 1.4.0.');
