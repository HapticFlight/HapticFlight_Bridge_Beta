// Stage 1: additive, reproducible realism baseline; never writes previous profiles.
const fs = require('fs'), path = require('path'), crypto = require('crypto');
const root = path.resolve(__dirname, '..');
const source = path.join(root, 'downloads/ground-effects-v2');
const output = path.join(root, 'downloads/realism-test7');
const digest = text => crypto.createHash('sha256').update(text).digest('hex');
const id = text => digest(text).slice(0,32).replace(/(.{8})(.{4})(.{4})(.{4})(.{12})/, '$1-$2-$3-$4-$5');
const optional = /maneuver load|vertical energy|collective movement thump|powered low-speed descent cue|hard maneuver pulse|flare dispenser|chaff dispenser|liftoff unloading/i;
const inventory = [];
fs.mkdirSync(output, {recursive:true});
for (const file of fs.readdirSync(source).filter(f=>f.endsWith('.siprofile')).sort()) {
  const bytes = fs.readFileSync(path.join(source,file));
  const profile = JSON.parse(bytes.toString('utf8').replace(/^\uFEFF/,''));
  profile.Name = profile.Name.replace('test.6','test.7') + ' Realism';
  profile.ProfileId = id(profile.Name);
  profile.Description = 'Realism baseline test.7. Requires Engine 1.1.0. Optional augmented cues are disabled and explicitly labelled. Aircraft tuning is independent of hardware output routing. Not live calibrated.';
  profile.IncludeOutputSettingsInProfile = false;
  profile.CarChoices = []; profile.CarChoice = null;
  const entries = [];
  function walk(items, group, ancestorsEnabled) {
    for (const effect of items || []) {
      effect.ContainerId = id(profile.Name + '/' + group + '/' + effect.Description);
      if (effect.EffectsContainers) {
        walk(effect.EffectsContainers, effect.Description, ancestorsEnabled && effect.IsEnabled !== false);
        continue;
      }
      const originalName = effect.Description;
      if (optional.test(originalName)) {
        effect.IsEnabled = false;
        effect.Description = '[OPTIONAL augmented cue] ' + originalName;
      }
      if (/airframe impact pulse/i.test(originalName)) effect.Description = 'Airframe shock (cause unconfirmed)';
      const properties = [...new Set([...JSON.stringify(effect).matchAll(/DcsExtraTelemetryPlugin\.DCSExtra\.([A-Za-z0-9.]+)/g)].map(m=>m[1]))].sort();
      entries.push({group, name:effect.Description, baselineName:originalName,
        enabled:ancestorsEnabled && effect.IsEnabled !== false, gain:effect.Gain,
        frequency:effect.Output && effect.Output.Frequency, properties,
        amplitude:effect.FrontLeftFormula, frequencyFormula:effect.FrontLeftFrequencyFormula,
        fixedStepState:/root\[/.test(JSON.stringify(effect)),
        optionalAugmentedCue:optional.test(originalName)});
    }
  }
  walk(profile.EffectsContainers, '', true);
  const destination = file.replace('test.6','test.7-Realism');
  const text = JSON.stringify(profile,null,2)+'\n';
  const target = path.join(output,destination);
  if (fs.existsSync(target) && fs.readFileSync(target,'utf8') !== text)
    throw Error('Existing version differs; increment profile version instead of overwriting: '+destination);
  fs.writeFileSync(target,text);
  inventory.push({source:file,sourceSha256:digest(bytes),file:destination,sha256:digest(text),
    profileId:profile.ProfileId,requiredEngine:'1.1.0',effects:entries});
}
fs.writeFileSync(path.join(output,'effect-inventory.json'),JSON.stringify(inventory,null,2)+'\n');
console.log('Built '+inventory.length+' separate test.7 profiles and complete formula inventories.');
