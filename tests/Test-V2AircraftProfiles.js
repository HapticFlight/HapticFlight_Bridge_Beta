const fs=require('fs'),path=require('path'),assert=require('assert');
const dir=path.resolve(__dirname,'../downloads/haptic-engine-v2');
const audit=JSON.parse(fs.readFileSync(path.join(dir,'V2-Effect-Audit.json')));
const ids=new Set();let total=0;
const base={'Connected':true,'Haptic.EngineVersion':'1.0.0',RotorRunning:true,MainRotorRpmPct:100,MainRotorHz:4.3,BladePassHz:17.2,CollectivePosition:0.6,GroundSpeedMps:5,OnGround:false,LastFiredWeaponName:'m230',RotorSource:'UH60RotorPhase'};
for(const n of audit.registered)if(n.startsWith('Has.'))base[n]=true;
for(const n of audit.registered)if(n.startsWith('Haptic.')&&/Intensity$|Envelope$/.test(n))base[n]=0.5;
function runner(e){const root={};new Function('root','$prop',e.FrontLeftFormula.PreExpression||'')(root,()=>0);const fn=new Function('root','$prop',e.FrontLeftFormula.Expression);return state=>fn(root,name=>state[name.replace('DcsExtraTelemetryPlugin.DCSExtra.','')]??0);}
for(const file of fs.readdirSync(dir).filter(f=>f.endsWith('.siprofile'))){
 const p=JSON.parse(fs.readFileSync(path.join(dir,file)));assert(!ids.has(p.ProfileId));ids.add(p.ProfileId);
 assert.deepEqual(p.CarChoices,[]);assert.equal(p.IncludeOutputSettingsInProfile,false);
 const effects=p.EffectsContainers.flatMap(g=>{assert(g.IsEnabled);return g.EffectsContainers;});
 for(const e of effects){assert(e.IsEnabled);total++;
  for(const k of ['FrontLeftFormula','FrontLeftFrequencyFormula']){
   new Function('root','$prop',e[k].Expression||'');
   for(const m of (e[k].Expression||'').matchAll(/DcsExtraTelemetryPlugin\.DCSExtra\.([A-Za-z0-9.]+)/g))assert(audit.registered.includes(m[1]),m[1]);
  }
  const run=runner(e);assert.equal(run({...base,Connected:false}),0);assert.equal(run({...base,'Haptic.EngineVersion':0}),0);
  for(const cv of [v=>v,v=>typeof v==='number'?String(v):v]){
   const state=Object.fromEntries(Object.entries(base).map(([k,v])=>[k,cv(v)]));let result;for(let i=0;i<120;i++)result=run(state);assert(Number.isFinite(result),file+': '+e.Description);
   if(/1-rev|blade-pass|two-blade rotor/.test(e.Description))assert(result>0,e.Description);
  }
 }
 const touchdown=effects.find(e=>e.Description==='Touchdown body impact');const tdProp=touchdown.FrontLeftFormula.Expression.match(/DCSExtra\.(Haptic\.[A-Za-z0-9.]+\.Envelope)/)[1];
 assert(runner(touchdown)({...base,[tdProp]:0.8})>runner(touchdown)({...base,[tdProp]:0.2}),'Touchdown must preserve envelope severity');
 const skid=effects.find(e=>/skid ground motion|Skid slide/.test(e.Description));if(skid){assert(runner(skid)({...base,OnGround:true})>0);assert.equal(runner(skid)({...base,OnGround:false}),0);assert.equal(runner(skid)({...base,OnGround:true,'Haptic.Ground.SkidScrub.Intensity':0}),0);}
 if(file.includes('UH60L')){
  const old=JSON.parse(fs.readFileSync(path.resolve(__dirname,'../profiles/uh60/DCS World - DCS UH60L - Haptic Engine v2.0.0-test.1.siprofile')));
  const expected=old.EffectsContainers.flatMap(g=>g.EffectsContainers);
  effects.forEach((e,i)=>{const clean=structuredClone(e);clean.FrontLeftFormula.Expression=clean.FrontLeftFormula.Expression.slice(clean.FrontLeftFormula.Expression.indexOf('\n')+1);assert.deepEqual(clean,expected[i],'UH60 effect drift');});
 }else{
  const text=JSON.stringify(p);for(const legacy of Object.keys(audit.migrationMap))assert(!text.includes('DCSExtra.'+legacy+"'"),'Legacy '+legacy);
 }
 console.log(file+': '+effects.length+' effects passed.');
}
assert.equal(total,212);assert.equal(ids.size,6);console.log('PASS: V2 registration, unique IDs, all enabled, missing-engine/disconnect silence, numeric/string rotor, proportional landing, ground gating and UH60 V2 equivalence.');
