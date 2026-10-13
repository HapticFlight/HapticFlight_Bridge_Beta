const fs=require('fs'),path=require('path'),assert=require('assert');
const root=path.resolve(__dirname,'..'), dir=path.join(root,'downloads/ground-effects-v2');
const read=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
const plugin=fs.readFileSync(path.join(root,'src/DcsExtraTelemetry.Plugin/DcsExtraTelemetryPlugin.cs'),'utf8');
const registered=new Set([...plugin.matchAll(/AttachDelegate\("DCSExtra\.([^"]+)"/g)].map(m=>m[1]));
for(const m of plugin.matchAll(/RegisterHapticSignal\("([^"]+)"/g)) {
 for(const suffix of ['Active','Intensity','Envelope','Phase','EventId','Confidence','Source']) registered.add('Haptic.'+m[1]+'.'+suffix);
 registered.add('Has.Haptic'+m[1].replaceAll('.',''));
}
const base={Connected:true,'Haptic.EngineVersion':'1.1.0',OnGround:true,GroundSpeedMps:5};
for(const n of registered) if(n.endsWith('.Intensity')||n.endsWith('.Envelope'))base[n]=0.5;
const run=(e,state)=>new Function('$prop',e.FrontLeftFormula.Expression)(n=>state[n.replace('DcsExtraTelemetryPlugin.DCSExtra.','')]??0);
const ids=new Set();let total=0;
for(const entry of read(path.join(dir,'Ground-Effect-Audit.json'))) {
 const p=read(path.join(dir,entry.file)), old=read(path.join(root,entry.source));
 const nonground=p=>p.EffectsContainers.filter(g=>g.Description!=='Ground Interaction');
 assert.deepStrictEqual(nonground(p),nonground(old),'Rotor/weapon/non-ground tuning changed');
 assert(!ids.has(p.ProfileId));ids.add(p.ProfileId); assert.notEqual(p.ProfileId,old.ProfileId);
 assert.equal(p.IncludeOutputSettingsInProfile,false);assert.deepEqual(p.CarChoices,[]);
 const ground=p.EffectsContainers.find(g=>g.Description==='Ground Interaction');
 for(const e of ground.EffectsContainers) {
  total++; assert(e.IsEnabled); assert.equal(e.FrontLeftFormula.PreExpression,'');
  for(const m of JSON.stringify(e).matchAll(/DcsExtraTelemetryPlugin\.DCSExtra\.([A-Za-z0-9.]+)/g))assert(registered.has(m[1]),m[1]);
  assert.equal(run(e,{...base,Connected:false}),0);
  assert.equal(run(e,{...base,'Haptic.EngineVersion':'1.0.0'}),0,'Old engine should not activate updated ground effects');
  assert.equal(run(e,{...base,'Haptic.EngineVersion':0}),0);
  for(const value of [0,0.2,0.8,1]) {
   const state={...base};for(const n of registered)if(n.endsWith('.Intensity')||n.endsWith('.Envelope'))state[n]=value;
   const amplitude=run(e,state);assert(Number.isFinite(amplitude)&&amplitude>=0&&amplitude<=100,e.Description);
  }
 }
 for(const name of ['Touchdown body impact','Ground bump impact']) {
  const e=ground.EffectsContainers.find(e=>e.Description===name);if(!e)continue;
  const key=e.FrontLeftFormula.Expression.match(/DCSExtra\.(Haptic\.[A-Za-z0-9.]+\.Envelope)/)[1];
  assert(run(e,{...base,[key]:0.8})>run(e,{...base,[key]:0.2}),'Envelope severity flattened');
  assert.equal(run(e,{...base,[key]:0}),0);
 }
 if(entry.kind==='binary') assert(!ground.EffectsContainers.some(e=>/bump|Suspension|rocking|Individual/.test(e.Description)),'Binary skid profile invented travel');
 console.log(entry.aircraft+': preserved all non-ground groups; validated '+ground.EffectsContainers.length+' ground effects.');
}
assert.equal(ids.size,6);console.log('PASS: '+total+' ground effects, live property contract, version/disconnect gates, envelopes, baseline preservation.');
