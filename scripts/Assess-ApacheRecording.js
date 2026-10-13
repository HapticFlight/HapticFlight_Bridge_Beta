const fs=require('fs'),path=require('path');
const file=process.argv[2];if(!file)throw Error('Pass recording path');
const packets=fs.readFileSync(file,'utf8').split(/\r?\n/).filter(Boolean).map(JSON.parse);
const out=path.resolve('analysis/apache-20260907');fs.mkdirSync(out,{recursive:true});
const clamp=v=>Math.max(0,Math.min(1,Number(v)||0));
const stats=a=>{const s=a.filter(Number.isFinite).sort((a,b)=>a-b);return s.length?{n:s.length,min:s[0],median:s[Math.floor((s.length-1)*.5)],p95:s[Math.floor((s.length-1)*.95)],max:s.at(-1)}:null;};
const fields={};function flatten(x,p=''){for(const [k,v]of Object.entries(x||{})){const key=p?p+'.'+k:k;if(v&&typeof v==='object')flatten(v,key);else(fields[key]??=[]).push(v);}}
packets.forEach(p=>flatten(p));
const parameters=Object.fromEntries(Object.entries(fields).map(([k,v])=>[k,typeof v[0]==='number'?{...stats(v),missing:packets.length-v.length}: {n:v.length,missing:packets.length-v.length,values:[...new Set(v)].slice(0,30)}]));
const oldPath='downloads/rotor-refresh-v2/DCS-AH-64D-Apache-Haptic-Engine-v2.0.0-test.4.siprofile';
const newPath='downloads/apache-test5/DCS-AH-64D-Apache-Haptic-Engine-v2.0.0-test.5.siprofile';
const contract=JSON.parse(fs.readFileSync('downloads/haptic-engine-v2/V2-Effect-Audit.json'));
const map=contract.migrationMap;
function create(p){return p.EffectsContainers.flatMap(g=>g.EffectsContainers.map(e=>{const root={};new Function('root','$prop',e.FrontLeftFormula.PreExpression||'')(root,()=>0);return {e,g,root,fn:new Function('root','$prop',e.FrontLeftFormula.Expression),freq:new Function('root','$prop',e.FrontLeftFrequencyFormula.Expression||`return ${e.Output?.Frequency||0};`),all:[],gun:[],frequency:[],errors:0};}));}
const old=JSON.parse(fs.readFileSync(oldPath)),profiles=[{name:'test4',p:old,r:create(old)}];
if(fs.existsSync(newPath)){const p=JSON.parse(fs.readFileSync(newPath));profiles.push({name:'test5',p,r:create(p)});}
let prev=null,lateral=null,vertical=null,turbulence=null,groundRoll=null,braking=null;
const smooth=(prior,target,dt,up,down)=>prior===null?target:dt<=0?prior:prior+(target-prior)*(1-Math.exp(-Math.min(.25,dt)/(target>prior?up:down)));
const sums=Object.fromEntries(profiles.map(p=>[p.name,{all:[],gun:[],noGun:[],events:[]}])) ,missing=new Set(),gunRows=[];
let duplicates=0,reversals=0,gaps=0,frozen=0;
for(const p of packets){
 if(prev){if(p.sequence<=prev.sequence)duplicates++;if(p.modelTime<prev.modelTime)reversals++;if(p.modelTime-prev.modelTime>.5)gaps++;if(p.modelTime===prev.modelTime)frozen++;}
 const dt=prev?p.modelTime-prev.modelTime:0,d=p.values.dynamics||{},g=p.values.ground||{},w=p.values.weapons||{},c=p.capabilities||{};
 const props={Connected:true,'Haptic.EngineVersion':'1.0.0',AircraftType:p.aircraft.dcsType,AircraftFamily:p.aircraft.family};
 for(const group of [p.values,d,g,w,p.values.effects,p.values.countermeasures,p.values.cockpit])for(const [k,v]of Object.entries(group||{}))if(typeof v!=='object')props[k[0].toUpperCase()+k.slice(1)]=v;
 for(const[k,v]of Object.entries(c))props['Has.'+k[0].toUpperCase()+k.slice(1)]=v;
 Object.assign(props,{AccelXG:p.values.acceleration?.x,AccelYG:p.values.acceleration?.y,AccelZG:p.values.acceleration?.z,EngineRpmLeftPct:p.values.engineRpmPct?.left,EngineRpmRightPct:p.values.engineRpmPct?.right,LeftMainStrutCompression:g.leftStrutCompression,RightMainStrutCompression:g.rightStrutCompression,TailStrutCompression:g.tailStrutCompression});
 for(const[k,v]of Object.entries(map))props[v]=props[k]??0;
 const airborne=c.aerodynamicEffects&&!g.onGround,burst=!!w.weaponBurstActive;
 if(!prev||dt<0||dt>.5){lateral=vertical=turbulence=groundRoll=braking=null;}
 lateral=smooth(lateral,burst?0:clamp(d.lateralManeuverIntensity),dt,.09,.24);vertical=smooth(vertical,burst?0:clamp(d.maneuverResponseIntensity),dt,.08,.22);
 turbulence=smooth(turbulence,burst||w.heavyGunIntensity>.01||w.lightGunIntensity>.01?0:clamp(d.turbulenceIntensity),dt,.08,.22);
 groundRoll=smooth(groundRoll,clamp(g.groundRollIntensity),dt,.08,.22);braking=smooth(braking,clamp(g.brakingIntensity),dt,.08,.22);
 Object.assign(props,{'Haptic.Maneuver.Lateral.Intensity':airborne?lateral:0,'Haptic.Maneuver.Vertical.Intensity':airborne?vertical:0,'Haptic.Maneuver.ControlResponse.Intensity':airborne&&!burst?clamp(d.controlResponseIntensity):0,'Haptic.Maneuver.Hard.Envelope':airborne&&!burst?clamp(d.hardManeuverPulse):0,'Haptic.Aerodynamic.Turbulence.Intensity':airborne?turbulence:0,'Haptic.Ground.Roll.Intensity':c.groundMotion?groundRoll:0,'Haptic.Braking.Intensity':c.brakes?braking:0,'Haptic.AH64.HeavyLanding.Envelope':props.TouchdownEnvelope||0});
 for(const kind of ['Buffet','TranslationalLift','SettlingWithPower','BladeSlap'])if(!airborne)props['Haptic.Aerodynamic.'+kind+'.Intensity']=0;
 if(burst||w.heavyGunIntensity>.01||w.lightGunIntensity>.01){props['Haptic.Aerodynamic.Buffet.Intensity']=0;props['Haptic.Airframe.Shock.Envelope']=0;}
 if(g.onGround||props.TouchdownEnvelope>0)props['Haptic.Airframe.Shock.Envelope']=0;
 const gun=w.heavyGunIntensity>0 && (String(w.lastFiredWeaponName).toLowerCase().includes('m230') || String(w.lastFiredWeaponName).toLowerCase()==='cannon');
 if(gun)gunRows.push({time:p.modelTime,G:props.AccelYG,heavy:w.heavyGunIntensity,rounds:w.gunRoundsFired,burst:w.weaponBurstActive,rate:w.weaponRateHz});
 for(const pr of profiles){let sum=0;for(const x of pr.r){const get=n=>{const key=n.replace('DcsExtraTelemetryPlugin.DCSExtra.','');if(!(key in props))missing.add(key);return props[key]??0;};const amp=x.fn(x.root,get),freq=x.freq(x.root,get);if(!Number.isFinite(amp)||!Number.isFinite(freq))x.errors++;const weighted=amp*x.e.Gain/100*x.g.Gain/100*(pr.p.UseProfileGain?pr.p.GlobalGain/100:1);x.all.push(weighted);x.frequency.push(freq);if(gun)x.gun.push(weighted);sum+=Math.abs(weighted);}
  const s=sums[pr.name];s.all.push(sum);(gun?s.gun:s.noGun).push(sum);if(sum>80)s.events.push({time:p.modelTime,sum,gun});
 }
 prev=p;
}
const result={recording:path.basename(file),count:packets.length,duration:packets.at(-1).modelTime-packets[0].modelTime,aircraft:[...new Set(packets.map(p=>p.aircraft.dcsType))],duplicates,reversals,gaps,frozen,parameters,missingProfileInputs:[...missing],gun:{samples:gunRows.length,G:stats(gunRows.map(p=>p.G)),heavy:stats(gunRows.map(p=>p.heavy)),rate:stats(gunRows.map(p=>p.rate))},profiles:{}};
for(const pr of profiles)result.profiles[pr.name]={sums:Object.fromEntries(Object.entries(sums[pr.name]).map(([k,v])=>[k,k==='events'?v.slice(0,10):stats(v)])),effects:pr.r.map(x=>({name:x.e.Description,group:x.g.Description,gain:x.e.Gain,weighted:stats(x.all),duringGun:stats(x.gun),frequency:stats(x.frequency),errors:x.errors}))};
fs.writeFileSync(path.join(out,'assessment.json'),JSON.stringify(result,null,2));
console.log(JSON.stringify({count:result.count,duration:result.duration,aircraft:result.aircraft,timing:{duplicates,reversals,gaps,frozen},gun:result.gun,missing:result.missingProfileInputs,profiles:Object.fromEntries(Object.entries(result.profiles).map(([k,v])=>[k,{sums:v.sums,effects:v.effects.filter(x=>x.duringGun?.max>1).map(x=>({name:x.name,gain:x.gain,gun:x.duringGun,max:x.weighted.max}))}]))},null,2));
