const fs=require('fs'),path=require('path'),crypto=require('crypto');
const root=path.resolve(__dirname,'..'),source=path.join(root,'downloads/realism-test7'),out=path.join(root,'downloads/realism-test8');
const id=s=>crypto.createHash('sha256').update(s).digest('hex').slice(0,32).replace(/(.{8})(.{4})(.{4})(.{4})(.{12})/,'$1-$2-$3-$4-$5');
fs.mkdirSync(out,{recursive:true});
for(const file of fs.readdirSync(source).filter(f=>f.endsWith('.siprofile'))){
 const p=JSON.parse(fs.readFileSync(path.join(source,file),'utf8'));
 p.Name=p.Name.replace('test.7','test.8');p.ProfileId=id(p.Name);
 p.Description='Realism test.8; requires Engine 1.2.0. Smooth continuous-layer attenuation during physical shocks and weapons. Optional augmented cues disabled. Hardware-neutral routing; not live calibrated. Gain guidance does not measure or prevent final audio clipping.';
 for(const group of p.EffectsContainers){
  group.ContainerId=id(p.Name+group.Description);
  for(const e of group.EffectsContainers||[]){
   e.ContainerId=id(p.Name+group.Description+e.Description);
   const continuous=group.Description==='Rotor and Powertrain'||group.Description==='Lifecycle'||
    /Turbulence|buffet|High-speed airframe|Ground roll base|Suspension surface roughness|Gear-borne rotor/i.test(e.Description);
   const old=e.FrontLeftFormula.Expression;
   const prefix="DcsExtraTelemetryPlugin.DCSExtra.";
   e.FrontLeftFormula.Expression=`var version = String($prop('${prefix}Haptic.EngineVersion') || '').split('.');\nvar major = Number(version[0]), minor = Number(version[1]);\nif (!$prop('${prefix}Connected') || !(major > 1 || (major === 1 && minor >= 2))) return 0;\nvar amplitude = (function(){\n${old}\n})();\nif (typeof amplitude !== 'number' || !isFinite(amplitude)) return 0;\n`+
    (continuous?`var gain = Number($prop('${prefix}Haptic.Mix.ContinuousGain.Intensity'));\nif (!isFinite(gain)) return 0;\nreturn Math.max(0, Math.min(100, amplitude)) * Math.max(0, Math.min(1, gain));`:'return Math.max(0, Math.min(100, amplitude));');
  }
 }
 const target=path.join(out,file.replace('test.7','test.8')),text=JSON.stringify(p,null,2)+'\n';
 if(fs.existsSync(target)&&fs.readFileSync(target,'utf8')!==text)throw Error('Version already exists with different contents: '+target);
 fs.writeFileSync(target,text);
}
console.log('Built six separate test.8 profiles for Engine 1.2.0.');
