const fs=require('fs'),path=require('path'),assert=require('assert');
const base=path.resolve(__dirname,'../downloads');
function run(e,version,gain,connected=true){
 const state={};
 const prop=n=>n.endsWith('.EngineVersion')?version:n.endsWith('.Connected')?connected:n.endsWith('.Mix.ContinuousGain.Intensity')?gain:0.7;
 new Function('root','$prop',e.FrontLeftFormula.PreExpression||'')(state,prop);
 return new Function('root','$prop',e.FrontLeftFormula.Expression)(state,prop);
}
let count=0,ducked=0;
for(const file of fs.readdirSync(path.join(base,'realism-test8')).filter(f=>f.endsWith('.siprofile'))){
 const p=JSON.parse(fs.readFileSync(path.join(base,'realism-test8',file))),old=JSON.parse(fs.readFileSync(path.join(base,'realism-test7',file.replace('test.8','test.7'))));
 assert.notEqual(p.ProfileId,old.ProfileId);assert.equal(p.IncludeOutputSettingsInProfile,false);
 for(let g=0;g<p.EffectsContainers.length;g++)for(let i=0;i<p.EffectsContainers[g].EffectsContainers.length;i++){
  const e=p.EffectsContainers[g].EffectsContainers[i],before=old.EffectsContainers[g].EffectsContainers[i]; count++;
  assert.equal(run(e,'1.1.0',1),0);assert.equal(run(e,'garbage',1),0);assert.equal(run(e,'1.2.0',1,false),0);
  const nominal=run(e,'1.2.0',1),original=run(before,'1.2.0',1);
  assert(Number.isFinite(nominal)&&nominal>=0&&nominal<=100);
  assert.equal(nominal,Math.max(0,Math.min(100,original)));
  assert.equal(run(e,'1.10.0',1),nominal,'multi-digit minor misparsed');
  if(e.FrontLeftFormula.Expression.includes('var gain =')){
   ducked++;assert.equal(run(e,'1.2.0',0),0);assert(Math.abs(run(e,'1.2.0',.5)-nominal*.5)<1e-9);
   assert.equal(run(e,'1.2.0',NaN),0);
  }
  assert.equal(e.IsEnabled,before.IsEnabled);assert.deepEqual(e.Output,before.Output);
 }
}
assert(ducked>0);console.log('PASS: '+count+' formulas; '+ducked+' ducked layers; semantic version gates and baseline amplitude preservation.');
