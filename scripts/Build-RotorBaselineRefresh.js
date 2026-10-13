const fs=require('fs'),path=require('path'),crypto=require('crypto'),assert=require('assert');
const root=path.resolve(__dirname,'..'),out=path.join(root,'downloads/rotor-refresh-v2');fs.mkdirSync(out,{recursive:true});
const settings={
 'AH-6J':[{floor:42,gain:30,cap:54,base:'8 + 19 * governed'},{floor:34,gain:24,cap:48,base:'8 + 14 * governed'},{floor:44,gain:36,cap:56,base:'11 + 23 * governed'}],
 'AH-64D-Apache':[{floor:42,gain:30,cap:60,base:'9 + 21 * governed'},{floor:34,gain:24,cap:52,base:'6 + 16 * governed'},{floor:44,gain:36,cap:62,base:'7 + 20 * governed'}]
};
const audit=[];
for(const [id,layers]of Object.entries(settings)){
 const source=path.join(root,`downloads/haptic-engine-v2/DCS-${id}-Haptic-Engine-v2.0.0-test.3.siprofile`),p=JSON.parse(fs.readFileSync(source));
 p.Name=p.Name.replace('test.3','test.4 Rotor Baseline');p.ProfileId=crypto.randomUUID();
 p.Description='V2 test.4 rotor-baseline refresh assessed against September 6 AH6/Apache recordings. Three rotor layers have stronger independent presence and revised effect gains. Original amplitude caps, frequencies, other effects and output routing retained. RPM remains a module proxy; live tactile confirmation required.';
 let index=0;
 for(const g of p.EffectsContainers)for(const e of g.EffectsContainers){
  if(!/1-rev|2-rev|blade-pass/.test(e.Description))continue;
  const s=layers[index++],old=e.Gain;
  const formula=e.FrontLeftFormula.Expression;
  assert(formula.includes(s.base),'Baseline term not found '+e.Description);
  const injection=`// Steady rotor presence is independent of collective. Ramp conservatively across the existing RPM proxy.\nvar presence = Math.max(0, Math.min(1, (nr - 0.15) / 0.45));\npresence = presence * presence * (3 - 2 * presence);\nvar dynamicSwell = Math.max(0, target - (${s.base}));\ntarget = Math.min(${s.cap}, presence * (${s.floor} + dynamicSwell));\n`;
  const marker=formula.indexOf('var coefficient');assert(marker>0);
  // Insert inside the running-rotor block, before its closing brace.
  const close=formula.lastIndexOf('}',marker);assert(close>0);
  e.FrontLeftFormula.Expression=formula.slice(0,close)+injection+formula.slice(close);
  e.Gain=s.gain;
  audit.push({aircraft:id,effect:e.Description,oldGain:old,newGain:s.gain,runningFloor:s.floor,amplitudeCap:s.cap,maximumAfterEffectGain:s.cap*s.gain/100});
 }
 assert.equal(index,3);fs.writeFileSync(path.join(out,`DCS-${id}-Haptic-Engine-v2.0.0-test.4.siprofile`),JSON.stringify(p,null,2));
}
fs.writeFileSync(path.join(out,'Rotor-Changes.json'),JSON.stringify(audit,null,2));
console.log('Built AH6 and Apache test.4; changed only three rotor layers per profile plus identity/description.');
