const fs=require('fs'),path=require('path'),readline=require('readline');
const out=path.resolve('analysis/rotor-recordings');fs.mkdirSync(out,{recursive:true});
const clamp=x=>Math.max(0,Math.min(1,Number(x)||0));
const stats=a=>{if(!a.length)return null;const s=[...a].sort((a,b)=>a-b);return {min:s[0],p05:s[Math.floor((s.length-1)*.05)],median:s[Math.floor((s.length-1)*.5)],p95:s[Math.floor((s.length-1)*.95)],max:s.at(-1)};};
function evalEffect(e){const root={};new Function('root','$prop',e.FrontLeftFormula.PreExpression||'')(root,()=>0);const fn=new Function('root','$prop',e.FrontLeftFormula.Expression),freq=new Function('root','$prop',e.FrontLeftFrequencyFormula.Expression);return props=>{const get=n=>props[n.replace('DcsExtraTelemetryPlugin.DCSExtra.','')]??0;return {amplitude:fn(root,get),frequency:freq(root,get)};};}
async function analyze(file){
 let n=0,first,last,aircraft,profile,effects,runners,newRunners,newProfile,lateral=null,previous=null,bad=0,duplicates=0;
 const groups={};for(const name of ['all','parkedSteady','airborneSteady'])groups[name]={n:0,rpm:[],engine:[],collective:[],load:[],frequency:[[],[],[]],before:[[],[],[]],after:[[],[],[]],weightedBefore:[[],[],[]],weightedAfter:[[],[],[]]};
 const sources=new Set();
 for await(const line of readline.createInterface({input:fs.createReadStream(file),crlfDelay:Infinity})){
  let p;try{p=JSON.parse(line);}catch{bad++;continue;}
  if(!profile){aircraft=p.aircraft.dcsType;const id=aircraft==='AH-6J'?'AH-6J':'AH-64D-Apache';profile=JSON.parse(fs.readFileSync(`downloads/haptic-engine-v2/DCS-${id}-Haptic-Engine-v2.0.0-test.3.siprofile`));effects=profile.EffectsContainers.flatMap(g=>g.EffectsContainers).filter(e=>/1-rev|2-rev|blade-pass/.test(e.Description));runners=effects.map(evalEffect);const np=`downloads/rotor-refresh-v2/DCS-${id}-Haptic-Engine-v2.0.0-test.4.siprofile`;if(fs.existsSync(np)){newProfile=JSON.parse(fs.readFileSync(np));newRunners=newProfile.EffectsContainers.flatMap(g=>g.EffectsContainers).filter(e=>/1-rev|2-rev|blade-pass/.test(e.Description)).map(evalEffect);}first=p.modelTime;}
  if(previous&&p.sequence<=previous.sequence&&p.sessionId===previous.sessionId){duplicates++;continue;}
  const d=p.values.dynamics,c=p.capabilities,g=p.values.ground,w=p.values.weapons,dt=previous?p.modelTime-previous.modelTime:0;const reset=!previous||p.sessionId!==previous.sessionId||dt<0||dt>.5;
  const target=w.weaponBurstActive?0:clamp(d.lateralManeuverIntensity);if(reset||lateral===null)lateral=target;else if(dt>0)lateral+=(target-lateral)*(1-Math.exp(-Math.min(.25,dt)/(target>lateral?.09:.24)));
  const props={Connected:true,'Haptic.EngineVersion':'1.0.0',OnGround:g.onGround,AccelYG:p.values.acceleration?.y??1,VerticalVelocityMps:p.values.verticalVelocityMps,WeaponBurstActive:w.weaponBurstActive,EngineRpmLeftPct:p.values.engineRpmPct?.left,EngineRpmRightPct:p.values.engineRpmPct?.right};
  for(const [k,v]of Object.entries(c))props['Has.'+k[0].toUpperCase()+k.slice(1)]=v;
  for(const [k,v]of Object.entries(d))props[k[0].toUpperCase()+k.slice(1)]=v;
  props['Haptic.Rotor.Load.Intensity']=c.powertrainEffects?clamp(d.rotorLoadIntensity):0;
  props['Haptic.Maneuver.Lateral.Intensity']=c.aerodynamicEffects&&!g.onGround?lateral:0;
  props['Haptic.Maneuver.ControlResponse.Intensity']=c.aerodynamicEffects&&!g.onGround&&!w.weaponBurstActive?clamp(d.controlResponseIntensity):0;
  const a=runners.map(fn=>fn(props)),b=newRunners?.map(fn=>fn(props));
  const steady=d.rotorRunning&&Math.abs(d.collectiveMovementRate)<.03;
  const selected=['all'];if(steady&&g.onGround&&g.groundSpeedMps<.3)selected.push('parkedSteady');if(steady&&!g.onGround&&Math.abs(p.values.verticalVelocityMps)<1&&Math.abs(props.AccelYG-1)<.12)selected.push('airborneSteady');
  for(const key of selected){const x=groups[key];x.n++;x.rpm.push(d.mainRotorRpmPct);x.engine.push(p.values.engineRpmPct?.left??0);x.collective.push(d.collectivePosition);x.load.push(d.rotorLoadIntensity);for(let i=0;i<3;i++){x.frequency[i].push(a[i].frequency);x.before[i].push(a[i].amplitude);x.weightedBefore[i].push(a[i].amplitude*effects[i].Gain/100);if(b){x.after[i].push(b[i].amplitude);const ne=newProfile.EffectsContainers.flatMap(g=>g.EffectsContainers).filter(e=>/1-rev|2-rev|blade-pass/.test(e.Description))[i];x.weightedAfter[i].push(b[i].amplitude*ne.Gain/100);}}}
  n++;last=p.modelTime;previous=p;sources.add(d.rotorSource);
 }
 const summary={file:path.basename(file),aircraft,n,bad,duplicates,first,last,duration:last-first,rotorSources:[...sources],effects:effects.map(e=>({name:e.Description,gain:e.Gain})),profileGlobalGain:profile.GlobalGain,useProfileGain:profile.UseProfileGain,groups:{}};
 for(const [key,g]of Object.entries(groups)){const result={n:g.n};for(const [field,data]of Object.entries(g))if(field!=='n')result[field]=Array.isArray(data[0])?data.map(stats):stats(data);summary.groups[key]=result;}
 fs.writeFileSync(path.join(out,aircraft+'.json'),JSON.stringify(summary,null,2));console.log(JSON.stringify(summary));
}
(async()=>{for(const f of process.argv.slice(2))await analyze(f);})();
