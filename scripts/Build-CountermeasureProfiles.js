// Stage 19: chaff and flare are direct, confirmed release events. They must
// not inherit the old broad optional-cue classification.
const fs = require('fs'), path = require('path'), crypto = require('crypto');
const root = path.resolve(__dirname, '..');
const source = path.join(root, 'downloads', 'realism-test18');
const output = path.join(root, 'downloads', 'realism-test19');
const id = s => crypto.createHash('sha256').update(s).digest('hex').slice(0, 32)
  .replace(/(.{8})(.{4})(.{4})(.{4})(.{12})/, '$1-$2-$3-$4-$5');

fs.mkdirSync(output, { recursive: true });
for (const file of fs.readdirSync(source).filter(f => f.endsWith('.siprofile')).sort()) {
  const profile = JSON.parse(fs.readFileSync(path.join(source, file), 'utf8'));
  profile.Name = profile.Name.replace('test.18', 'test.19');
  profile.ProfileId = id(profile.Name);
  profile.Description = 'Realism test.19; requires Engine 1.7.1. Enables confirmed chaff and flare release pulses by default. Liftoff, main-wheel contact calibration, touchdown-body, surface texture, and braking behavior are unchanged.';
  const repaired = new Set();
  for (const group of profile.EffectsContainers || []) {
    group.ContainerId = id(profile.Name + '/' + group.Description);
    for (const effect of group.EffectsContainers || []) {
      if (effect.Description === '[OPTIONAL augmented cue] Flare dispenser pulse') {
        effect.Description = 'Flare dispenser pulse'; effect.IsEnabled = true; repaired.add('flare');
      }
      if (effect.Description === '[OPTIONAL augmented cue] Chaff dispenser pulse') {
        effect.Description = 'Chaff dispenser pulse'; effect.IsEnabled = true; repaired.add('chaff');
      }
      effect.ContainerId = id(profile.Name + '/' + group.Description + '/' + effect.Description);
    }
  }
  if (!repaired.has('flare') || !repaired.has('chaff')) throw new Error('Countermeasure effects are missing: ' + file);
  const target = path.join(output, file.replace('test.18', 'test.19'));
  const text = JSON.stringify(profile, null, 2) + '\n';
  if (fs.existsSync(target) && fs.readFileSync(target, 'utf8') !== text)
    throw new Error('Existing version differs; increment profile version instead of overwriting: ' + target);
  fs.writeFileSync(target, text);
}
console.log('Built six separate test.19 profiles with confirmed countermeasure pulses enabled.');
