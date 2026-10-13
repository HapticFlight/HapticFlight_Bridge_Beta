const fs=require('fs'),path=require('path'),assert=require('assert');
const root=path.resolve(__dirname,'..');
const prop=n=>`DcsExtraTelemetryPlugin.DCSExtra.${n}`;
function run(e,state){const r={};const get=n=>state[n.replace('DcsExtraTelemetryPlugin.DCSExtra.','')]??0;new Function('root','$prop',e.FrontLeftFormula.PreExpression)(r,get);const fn=new Function('root','$prop',e.FrontLeftFormula.Expression);let x;for(let i=0;i<300;i++)x=fn(r,get);return {x,next:s=>fn(r,n=>s[n.replace('DcsExtraTelemetryPlugin.DCSExtra.','')]??0)};}
for(const id of ['AH-6J','AH-64D-Apache']){
 const load=(dir,v)=>JSON.parse(fs.readFileSync(path.join(root,`downloads/${dir}/DCS-${id}-Haptic-Engine-v2.0.0-test.${v}.siprofile`)));
 const a=load('haptic-engine-v2',3),b=load('rotor-refresh-v2',4);let changed=0;
 assert.notEqual(a.ProfileId,b.ProfileId);assert.equal(a.GlobalGain,b.GlobalGain);assert.equal(a.UseProfileGain,b.UseProfileGain);assert.deepEqual(a.CarChoices,b.CarChoices);assert.equal(b.IncludeOutputSettingsInProfile,false);
 const aa=a.EffectsContainers.flatMap(g=>g.EffectsContainers),bb=b.EffectsContainers.flatMap(g=>g.EffectsContainers);assert.equal(aa.length,bb.length);
 for(let i=0;i<aa.length;i++){
  const old=aa[i],e=bb[i];if(!/1-rev|2-rev|blade-pass/.test(e.Description)){assert.deepEqual(old,e);continue;}
  changed++;const copy=structuredClone(e);copy.Gain=old.Gain;copy.FrontLeftFormula.Expression=old.FrontLeftFormula.Expression;assert.deepEqual(copy,old);
  const state={Connected:true,'Haptic.EngineVersion':'1.0.0','Has.RotorDynamics':true,RotorRunning:true,MainRotorRpmPct:id==='AH-6J'?62.5:86.25,OnGround:true,'Has.Acceleration':true,AccelYG:1,'Has.ControlPositions':true,CollectivePosition:0,'Haptic.Rotor.Load.Intensity':0};
  const r=run(e,state),before=run(old,state).x;assert(r.x*e.Gain>before*old.Gain*1.7);
  assert.equal(r.next({...state,Connected:false}),0);assert.equal(r.next({...state,'Haptic.EngineVersion':0}),0);
  assert.equal(run(e,{...state,RotorRunning:false,MainRotorRpmPct:0}).x,0);
  const low=run(e,{...state,MainRotorRpmPct:20}).x;assert(low<r.x*.1,'low RPM should not get full baseline');
  const high=run(e,{...state,CollectivePosition:.7,'Haptic.Rotor.Load.Intensity':.5}).x;assert(high>r.x,'load swell retained');
  const stress=run(e,{...state,CollectivePosition:1,MainRotorRpmPct:100,OnGround:false,AccelYG:3,'Haptic.Rotor.Load.Intensity':1,'Haptic.Maneuver.Lateral.Intensity':1,'Haptic.Maneuver.ControlResponse.Intensity':1,VerticalVelocityMps:20}).x;
  const cap=Number(e.FrontLeftFormula.Expression.match(/target = Math.min\((\d+)/)[1]);assert(stress<=cap+1e-9);
  console.log(id,e.Description,`steady weighted ${ (r.x*e.Gain/100).toFixed(2)}, cap ${cap}, original frequency retained`);
 }
 assert.equal(changed,3);
}
console.log('PASS: only rotor layers changed, steady baseline, load response, shutdown/disconnect, unchanged caps and frequencies.');
