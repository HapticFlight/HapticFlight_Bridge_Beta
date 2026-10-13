const fs=require('fs'),assert=require('assert');
const a=JSON.parse(fs.readFileSync('downloads/rotor-refresh-v2/DCS-AH-64D-Apache-Haptic-Engine-v2.0.0-test.4.siprofile'));
const b=JSON.parse(fs.readFileSync('downloads/apache-test5/DCS-AH-64D-Apache-Haptic-Engine-v2.0.0-test.5.siprofile'));
const aa=a.EffectsContainers.flatMap(g=>g.EffectsContainers),bb=b.EffectsContainers.flatMap(g=>g.EffectsContainers);assert.equal(bb.length,38);assert.notEqual(a.ProfileId,b.ProfileId);
for(const k of ['GlobalGain','UseProfileGain','CarChoices','IncludeOutputSettingsInProfile'])assert.deepEqual(a[k],b[k]);
let changed=0;
for(let i=0;i<38;i++){
 const e=bb[i];assert(e.IsEnabled);if(!e.Description.includes('M230')){assert.deepEqual(e,aa[i]);continue;}changed++;
 const root={};new Function('root','$prop',e.FrontLeftFormula.PreExpression)(root,()=>0);const fn=new Function('root','$prop',e.FrontLeftFormula.Expression),freq=new Function(e.FrontLeftFrequencyFormula.Expression);
 const state={Connected:true,'Haptic.EngineVersion':'1.0.0','Has.WeaponEffects':true,LastFiredWeaponName:'Cannon','Haptic.Weapon.HeavyGun.Intensity':1,NominalWeaponRateHz:1788811658};
 const call=()=>fn(root,n=>state[n.replace('DcsExtraTelemetryPlugin.DCSExtra.','')]??0);
 for(const name of ['Cannon','M230']){state.LastFiredWeaponName=name;let x;for(let t=0;t<300;t++)x=call();assert(x>0);assert(x*e.Gain/100<=(e.Gain===12?6:.72)+1e-9);}
 assert(Math.abs(freq()-(e.Gain===12?625/60:625/120))<1e-9);
 state.LastFiredWeaponName='unrelated';for(let t=0;t<300;t++)call();assert.equal(call(),0);
 state.LastFiredWeaponName='Cannon';state.Connected=false;assert.equal(call(),0);state.Connected=true;state['Haptic.EngineVersion']=0;assert.equal(call(),0);
}
assert.equal(changed,2);console.log('PASS: 38 effects enabled; only two gun effects changed; both aliases, no-clock frequency, ceilings, disconnection and missing-engine checks.');
