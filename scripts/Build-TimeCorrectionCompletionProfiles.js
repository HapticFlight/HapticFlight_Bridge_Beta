const fs=require('fs'),path=require('path'),crypto=require('crypto');
const root=path.resolve(__dirname,'..'),source=path.join(root,'downloads/realism-test11'),out=path.join(root,'downloads/realism-test12');
const id=s=>crypto.createHash('sha256').update(s).digest('hex').slice(0,32).replace(/(.{8})(.{4})(.{4})(.{4})(.{12})/,'$1-$2-$3-$4-$5');
const timing="DcsExtraTelemetryPlugin.DCSExtra.Haptic.Timing.DeltaSeconds.Intensity";
let corrected=0;
fs.mkdirSync(out,{recursive:true});
for(const file of fs.readdirSync(source).filter(file=>file.endsWith('.siprofile')).sort()){
 const p=JSON.parse(fs.readFileSync(path.join(source,file),'utf8'));
 p.Name=p.Name.replace('test.11','test.12');p.ProfileId=id(p.Name);
 p.Description='Realism test.12; requires Engine 1.3.0. Completes model-time smoothing for every stateful profile layer, including AH-6J and MH-6J rotor harmonics. Detailed wheel-roll telemetry with speed fallback; optional unconfirmed and augmented cues disabled. Hardware-neutral output routing.';
 for(const group of p.EffectsContainers||[]){
  group.ContainerId=id(p.Name+'/'+group.Description);
  for(const e of group.EffectsContainers||[]){
   e.ContainerId=id(p.Name+'/'+group.Description+'/'+e.Description);
   e.FrontLeftFormula.Expression=e.FrontLeftFormula.Expression.replace(/var coefficient = target > root\['([^']+)'\] \? (0\.\d+) : (0\.\d+);/g,
    (match,key,attack,release)=>{
     corrected++;
     return `var frameDelta = Number($prop('${timing}'));\nif (!isFinite(frameDelta) || frameDelta <= 0 || frameDelta > 0.25) frameDelta = 1 / 60;\nvar baseCoefficient = target > root['${key}'] ? ${attack} : ${release};\nvar coefficient = 1 - Math.pow(1 - baseCoefficient, frameDelta * 60);`;
    });
  }
 }
 const destination=file.replace('test.11','test.12'),text=JSON.stringify(p,null,2)+'\n',target=path.join(out,destination);
 if(fs.existsSync(target)&&fs.readFileSync(target,'utf8')!==text)throw Error('Version exists with different contents: '+target);
 fs.writeFileSync(target,text);
}
if(corrected!==6)throw Error('Expected six remaining AH-6/MH-6 smoothers; corrected '+corrected);
console.log('Built six separate test.12 profiles; corrected the six remaining light-helicopter rotor smoothers.');
