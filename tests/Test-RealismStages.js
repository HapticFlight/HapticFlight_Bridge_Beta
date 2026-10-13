const fs=require('fs'),path=require('path'),assert=require('assert');
const downloads=path.resolve(__dirname,'../downloads');
const files=stage=>fs.readdirSync(path.join(downloads,stage)).filter(file=>file.endsWith('.siprofile')).sort();
const load=(stage,file)=>JSON.parse(fs.readFileSync(path.join(downloads,stage,file),'utf8'));
const effects=p=>p.EffectsContainers.flatMap(group=>group.EffectsContainers||[]);
function prop(name,version,delta){
 if(name.endsWith('.EngineVersion'))return version;
 if(name.endsWith('.Connected'))return true;
 if(name.endsWith('.Timing.DeltaSeconds.Intensity'))return delta;
 if(name.endsWith('.OnGround'))return false;
 if(name.endsWith('.RotorRunning')||name.endsWith('.ApuRunning')||name.endsWith('.Engine1StarterActive')||name.endsWith('.WeaponBurstActive'))return true;
 if(name.endsWith('.LastFiredWeaponName'))return 'm230';
 if(name.endsWith('.MainRotorRpmPct'))return 100;
 if(name.endsWith('.MainRotorHz'))return 4.3;
 if(name.endsWith('.BladePassHz'))return 17.2;
 if(name.endsWith('.AccelYG'))return 1.2;
 if(name.endsWith('.VerticalVelocityMps'))return 4;
 if(name.endsWith('.GroundSpeedMps'))return 5;
 if(name.includes('.Has.'))return true;
 return .7;
}
function evaluate(effect,version,hz,seconds=2){
 const root=new Proxy({}, {get:(target,key)=>key in target?target[key]:0,set:(target,key,value)=>(target[key]=value,true)});
 const pre=new Function('root','$prop',effect.FrontLeftFormula.PreExpression||'');
 const expression=new Function('root','$prop',effect.FrontLeftFormula.Expression);
 const getter=name=>prop(name,version,1/hz);pre(root,getter);
 let output=0;
 for(let frame=0;frame<hz*seconds;frame++)output=expression(root,getter);
 return output;
}
for(const stage of ['realism-test9','realism-test10','realism-test11','realism-test12','realism-test13','realism-test14','realism-test15','realism-test16','realism-test17','realism-test18','realism-test19'])assert.equal(files(stage).length,6,stage+' profile count');
const ids=new Set();let formulaCount=0,timeCorrected=0;
for(const file11 of files('realism-test11')){
 const file10=file11.replace('test.11','test.10'),file9=file11.replace('test.11','test.9'),file8=file11.replace('test.11','test.8');
 const p8=load('realism-test8',file8),p9=load('realism-test9',file9),p10=load('realism-test10',file10),p11=load('realism-test11',file11);
 for(const p of [p9,p10,p11]){assert(!ids.has(p.ProfileId),'duplicate profile ID');ids.add(p.ProfileId);assert.equal(p.IncludeOutputSettingsInProfile,false);}
 const shock=effects(p9).find(e=>/Airframe shock/i.test(e.Description));assert(shock&&shock.IsEnabled===false&&/OPTIONAL/.test(shock.Description));
 assert.deepEqual(effects(p10).map(e=>e.FrontLeftFormula),effects(p9).map(e=>e.FrontLeftFormula),'test.10 unexpectedly changed formulas');
 const before=effects(p10),after=effects(p11);assert.equal(before.length,after.length);
 for(let index=0;index<after.length;index++){
  const oldExpression=before[index].FrontLeftFormula.Expression,newExpression=after[index].FrontLeftFormula.Expression;formulaCount++;
  assert(!newExpression.includes('minor >= 2'),'old engine gate remains');
  if(/var coefficient = target > root\["/.test(oldExpression)){
   timeCorrected++;assert(newExpression.includes('Timing.DeltaSeconds.Intensity'));assert(newExpression.includes('Math.pow(1 - baseCoefficient'));
  }
  assert.equal(after[index].IsEnabled,before[index].IsEnabled);assert.deepEqual(after[index].Output,before[index].Output);
 }
 assert.notEqual(p8.ProfileId,p9.ProfileId);assert.notEqual(p9.ProfileId,p10.ProfileId);assert.notEqual(p10.ProfileId,p11.ProfileId);
}
assert(timeCorrected>=60);console.log(`PASS: ${formulaCount} formulas across six profiles; ${timeCorrected} fixed-step smoothers made time-correct.`);

let remainingCorrected=0;
for(const file12 of files('realism-test12')){
 const p11=load('realism-test11',file12.replace('test.12','test.11')),p12=load('realism-test12',file12);
  assert(!ids.has(p12.ProfileId),'duplicate test.12 profile ID');ids.add(p12.ProfileId);assert.equal(p12.IncludeOutputSettingsInProfile,false);
 const before=effects(p11),after=effects(p12);assert.equal(before.length,after.length);
 for(let index=0;index<after.length;index++){
  if(/var coefficient = target > root\['/.test(before[index].FrontLeftFormula.Expression))remainingCorrected++;
  assert(!/var coefficient = target > root\[['"]/.test(after[index].FrontLeftFormula.Expression),'fixed-step smoother remains in latest profile');
  assert.equal(evaluate(after[index],'1.2.9',60,1),0,'Engine 1.2 activated a test.12 effect');
  const values=[30,60,120].map(hz=>evaluate(after[index],'1.3.0',hz));
  for(const value of values)assert(Number.isFinite(value)&&value>=0&&value<=100,'invalid test.12 amplitude');
  if(after[index].FrontLeftFormula.Expression.includes('Timing.DeltaSeconds.Intensity'))
   assert(Math.max(...values)-Math.min(...values)<.001,after[index].Description+' changed with update rate: '+values.join(', '));
  assert.equal(after[index].IsEnabled,before[index].IsEnabled);assert.deepEqual(after[index].Output,before[index].Output);
 }
}
assert.equal(remainingCorrected,6);console.log('PASS: test.12 completed all six remaining AH-6J/MH-6J rotor smoothers.');

for(const file13 of files('realism-test13')){
 const p12=load('realism-test12',file13.replace('test.13','test.12')),p13=load('realism-test13',file13);
 assert(!ids.has(p13.ProfileId),'duplicate test.13 profile ID');ids.add(p13.ProfileId);
 assert.equal(p13.IncludeOutputSettingsInProfile,false);
 const before=effects(p12),after=effects(p13);assert.equal(before.length,after.length);
 for(let index=0;index<after.length;index++){
  assert.equal(after[index].IsEnabled,before[index].IsEnabled);
  assert.deepEqual(after[index].Output,before[index].Output);
  assert.equal(evaluate(after[index],'1.3.9',60,1),0,'Engine 1.3 activated a test.13 effect');
  const values=[30,60,120].map(hz=>evaluate(after[index],'1.4.0',hz));
  for(const value of values)assert(Number.isFinite(value)&&value>=0&&value<=100,'invalid test.13 amplitude');
 }
}
console.log('PASS: test.13 profiles preserve prior tuning and require Engine 1.4.0.');

for(const file14 of files('realism-test14')){
 const p13=load('realism-test13',file14.replace('test.14','test.13')),p14=load('realism-test14',file14);
 assert(!ids.has(p14.ProfileId),'duplicate test.14 profile ID');ids.add(p14.ProfileId);
 assert.equal(p14.IncludeOutputSettingsInProfile,false);
 const before=effects(p13),after=effects(p14);assert.equal(before.length,after.length);
 for(let index=0;index<after.length;index++){
  assert.equal(evaluate(after[index],'1.4.9',60,1),0,'Engine 1.4 activated a test.14 effect');
  assert(Number.isFinite(evaluate(after[index],'1.5.0',60,1)),'invalid test.14 amplitude');
 }
 if(file14.includes('UH60L')){
  const contact=after.find(e=>e.Description==='Wheel contact detail');
  assert(contact&&contact.Gain===20&&contact.Output.Frequency===32,'test.14 wheel contact tuning missing');
  assert(contact.FrontLeftFormula.Expression.includes('mains === 1')&&contact.FrontLeftFormula.Expression.includes('Haptic.Contact.Left.Envelope'),
    'test.14 does not distinguish individual main-wheel contact');
 }
}
console.log('PASS: test.14 profiles require Engine 1.5.0 and retain distinct wheel-contact output.');

for(const file15 of files('realism-test15')){
 const p14=load('realism-test14',file15.replace('test.15','test.14')),p15=load('realism-test15',file15);
 assert(!ids.has(p15.ProfileId),'duplicate test.15 profile ID');ids.add(p15.ProfileId);
 assert.equal(p15.IncludeOutputSettingsInProfile,false);
 const before=effects(p14),after=effects(p15);assert.equal(before.length,after.length);
 for(let index=0;index<after.length;index++){
  assert.equal(evaluate(after[index],'1.5.9',60,1),0,'Engine 1.5 activated a test.15 effect');
  assert(Number.isFinite(evaluate(after[index],'1.6.0',60,1)),'invalid test.15 amplitude');
 }
 const vrs=after.find(e=>/VRS-risk texture/.test(e.Description));
 assert(vrs&&vrs.IsEnabled===false&&/OPTIONAL derived/.test(vrs.Description),'test.15 VRS-risk cue is not clearly optional');
 assert(vrs.FrontLeftFormula.Expression.includes('Haptic.Aerodynamic.VrsRisk.Intensity'),'test.15 VRS-risk does not use the derived haptic signal');
 assert(vrs.Output.UseWhiteNoise===true,'test.15 VRS-risk texture is not irregular');
}
console.log('PASS: test.15 profiles require Engine 1.6.0 and preserve an optional derived VRS-risk texture.');

for(const file16 of files('realism-test16')){
 const p15=load('realism-test15',file16.replace('test.16','test.15')),p16=load('realism-test16',file16);
 assert(!ids.has(p16.ProfileId),'duplicate test.16 profile ID');ids.add(p16.ProfileId);
 assert.equal(p16.IncludeOutputSettingsInProfile,false);
 const before=effects(p15),after=effects(p16);
 assert(after.length===before.length || after.length===before.length+2,'unexpected test.16 effect count');
 for(const effect of after){
  assert.equal(evaluate(effect,'1.6.9',60,1),0,'Engine 1.6 activated a test.16 effect');
  assert(Number.isFinite(evaluate(effect,'1.7.0',60,1)),'invalid test.16 amplitude');
 }
 for(const effect of after.filter(e=>/OPTIONAL low-frequency extension/.test(e.Description)))
  assert.equal(effect.IsEnabled,false,'low-frequency rotor extension is not optional');
 const decel=after.find(e=>e.Description==='Brake deceleration and airframe load');
 if(decel){
  const pedal=after.find(e=>e.Description==='Brake pedal application');
  const skid=after.find(e=>/Lateral wheel skid/.test(e.Description));
  assert(pedal&&skid,'test.16 missing distinct brake stages');
  assert(decel.FrontLeftFormula.Expression.includes('Braking.Deceleration'));
  assert(pedal.FrontLeftFormula.Expression.includes('Braking.Application'));
  assert(skid.FrontLeftFormula.Expression.includes('Ground.WheelSkid')&&skid.Output.UseWhiteNoise===true);
 }
}
console.log('PASS: test.16 separates brake stages, keeps wheel-skid distinct, and makes low-frequency rotor layers optional.');

for(const file17 of files('realism-test17')){
 const p16=load('realism-test16',file17.replace('test.17','test.16')),p17=load('realism-test17',file17);
 assert(!ids.has(p17.ProfileId),'duplicate test.17 profile ID');ids.add(p17.ProfileId);
 assert.equal(p17.IncludeOutputSettingsInProfile,false);
 const before=effects(p16),after=effects(p17);
 assert.equal(after.length,before.length,'test.17 unexpectedly changed effect count');
 for(const effect of after){
  assert.equal(evaluate(effect,'1.7.0',60,1),0,'Engine 1.7.0 activated a test.17 effect');
  assert(Number.isFinite(evaluate(effect,'1.7.1',60,1)),'invalid test.17 amplitude');
 }
 if(file17.includes('UH60L')){
  const contact=after.find(e=>e.Description==='Wheel contact detail');
  assert(contact&&contact.Gain===22&&contact.Output.Frequency===30,'test.17 contact output tuning missing');
  const expression=contact.FrontLeftFormula.Expression;
  assert(expression.includes('82 * main')&&expression.includes('74 * main')&&expression.includes('34 * tail'),
    'test.17 does not prioritize direct main-wheel contact');
  assert(expression.includes('0.12 * touchdown')&&expression.includes('0.35 * touchdown'),
    'test.17 does not keep landing-body and tail contact separate');
 }
}
console.log('PASS: test.17 requires Engine 1.7.1 and preserves direct main-wheel contact over tail-contact dominance.');

for(const file18 of files('realism-test18')){
 const p17=load('realism-test17',file18.replace('test.18','test.17')),p18=load('realism-test18',file18);
 assert(!ids.has(p18.ProfileId),'duplicate test.18 profile ID');ids.add(p18.ProfileId);
 assert.equal(p18.IncludeOutputSettingsInProfile,false);
 const before=effects(p17),after=effects(p18);assert.equal(after.length,before.length,'test.18 unexpectedly changed effect count');
 const liftoff=after.find(e=>e.Description==='Liftoff unloading');
 assert(liftoff&&liftoff.IsEnabled===true,'confirmed liftoff cue is not enabled by default');
 assert(liftoff.FrontLeftFormula.Expression.includes('Haptic.Liftoff.Envelope'),'liftoff cue lost its confirmed detector signal');
 assert(Number.isFinite(evaluate(liftoff,'1.7.1',60,1)),'invalid test.18 liftoff amplitude');
 assert(!before.find(e=>e.Description==='Liftoff unloading'),'test.17 was unexpectedly modified');
}
console.log('PASS: test.18 enables the confirmed liftoff-unloading cue without changing prior profiles.');

for(const file19 of files('realism-test19')){
 const p18=load('realism-test18',file19.replace('test.19','test.18')),p19=load('realism-test19',file19);
 assert(!ids.has(p19.ProfileId),'duplicate test.19 profile ID');ids.add(p19.ProfileId);
 assert.equal(p19.IncludeOutputSettingsInProfile,false);
 const before=effects(p18),after=effects(p19);assert.equal(after.length,before.length,'test.19 unexpectedly changed effect count');
 for(const name of ['Flare dispenser pulse','Chaff dispenser pulse']){
  const effect=after.find(e=>e.Description===name);
  assert(effect&&effect.IsEnabled===true,name+' is not enabled by default');
  assert(effect.FrontLeftFormula.Expression.includes('Haptic.Countermeasure.'),name+' lost its direct release signal');
  assert(Number.isFinite(evaluate(effect,'1.7.1',60,1)),'invalid test.19 '+name+' amplitude');
  assert(!before.find(e=>e.Description===name),'test.18 was unexpectedly modified');
 }
 for(const name of ['Liftoff unloading','Flare dispenser pulse','Chaff dispenser pulse']){
  const effect=after.find(e=>e.Description===name);
  assert(effect&&effect.IsEnabled===true&&!/^\[OPTIONAL/.test(effect.Description),name+' was misclassified as optional');
 }
 const allowedDisabled=[
  /\[OPTIONAL low-frequency extension\]/,
  /\[OPTIONAL augmented cue\] Collective movement thump/,
  /\[OPTIONAL augmented cue\] Maneuver load and vertical energy/,
  /\[OPTIONAL derived cue\] VRS-risk texture/,
  /\[OPTIONAL augmented cue\] Hard maneuver pulse/,
  /\[OPTIONAL unconfirmed physical cue\] Airframe shock/
 ];
 for(const effect of after.filter(e=>e.IsEnabled===false))
  assert(allowedDisabled.some(pattern=>pattern.test(effect.Description)),
    'unexpected disabled test.19 effect: '+effect.Description);
}
console.log('PASS: test.19 keeps direct liftoff and countermeasure event cues enabled by default; every remaining disabled effect is intentional.');
