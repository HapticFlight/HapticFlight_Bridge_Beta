const fs=require('fs'),path=require('path'),assert=require('assert'),crypto=require('crypto');
const root=path.resolve(__dirname,'..','..'), out=path.join(root,'downloads/realism-test7');
const read=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
const audit=read(path.join(out,'effect-inventory.json')),ids=new Set();
assert.equal(audit.length,6);
for(const entry of audit){
 const bytes=fs.readFileSync(path.join(root,'downloads/ground-effects-v2',entry.source));
 assert.equal(crypto.createHash('sha256').update(bytes).digest('hex'),entry.sourceSha256);
 const old=JSON.parse(bytes),p=read(path.join(out,entry.file));
 assert.notEqual(p.ProfileId,old.ProfileId);assert(!ids.has(p.ProfileId));ids.add(p.ProfileId);
 assert.equal(p.IncludeOutputSettingsInProfile,false);assert.deepEqual(p.CarChoices,[]);
 const flatten=p=>p.EffectsContainers.flatMap(g=>g.EffectsContainers||[]);
 const before=flatten(old),after=flatten(p);assert.equal(after.length,before.length);
 for(let i=0;i<after.length;i++){
  const a=after[i],b=before[i];
  assert.notEqual(a.ContainerId,b.ContainerId);
  assert.deepEqual(a.FrontLeftFormula,b.FrontLeftFormula,'Stage 1 changed signal generation');
  assert.deepEqual(a.Output,b.Output,'Stage 1 changed hardware-frequency behavior');
  assert.equal(a.Gain,b.Gain);
  if(a.Description.startsWith('[OPTIONAL'))assert.equal(a.IsEnabled,false);
  else assert.equal(a.IsEnabled,b.IsEnabled);
 }
}
console.log('PASS: six unique profiles, unchanged source hashes/formulas/gains/output settings, optional cues disabled.');
