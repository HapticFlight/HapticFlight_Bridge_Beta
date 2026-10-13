const fs=require('fs'),path=require('path'),crypto=require('crypto');
const root=path.resolve(__dirname,'..'),source=path.join(root,'downloads/realism-test10'),out=path.join(root,'downloads/realism-test11');
const id=s=>crypto.createHash('sha256').update(s).digest('hex').slice(0,32).replace(/(.{8})(.{4})(.{4})(.{4})(.{12})/,'$1-$2-$3-$4-$5');
const timing="DcsExtraTelemetryPlugin.DCSExtra.Haptic.Timing.DeltaSeconds.Intensity";
let corrected=0;
fs.mkdirSync(out,{recursive:true});
for(const file of fs.readdirSync(source).filter(file=>file.endsWith('.siprofile')).sort()){
 const p=JSON.parse(fs.readFileSync(path.join(source,file),'utf8'));
 p.Name=p.Name.replace('test.10','test.11');p.ProfileId=id(p.Name);
 p.Description='Realism test.11; requires Engine 1.3.0. Profile smoothing uses recorded model-time deltas for consistent response at 30, 60, and 120 Hz. Detailed wheel-roll telemetry with speed fallback; optional unconfirmed and augmented cues disabled. Hardware-neutral output routing.';
 for(const group of p.EffectsContainers||[]){
  group.ContainerId=id(p.Name+'/'+group.Description);
  for(const e of group.EffectsContainers||[]){
   e.ContainerId=id(p.Name+'/'+group.Description+'/'+e.Description);
   let expression=e.FrontLeftFormula.Expression.replace(/\(major > 1 \|\| \(major === 1 && minor >= 2\)\)/g,'(major > 1 || (major === 1 && minor >= 3))');
   expression=expression.replace(/var coefficient = target > root\["([^"]+)"\] \? (0\.\d+) : (0\.\d+);/g,
    (match,key,attack,release)=>{
     corrected++;
     return `var frameDelta = Number($prop('${timing}'));\nif (!isFinite(frameDelta) || frameDelta <= 0 || frameDelta > 0.25) frameDelta = 1 / 60;\nvar baseCoefficient = target > root["${key}"] ? ${attack} : ${release};\nvar coefficient = 1 - Math.pow(1 - baseCoefficient, frameDelta * 60);`;
    });
   e.FrontLeftFormula.Expression=expression;
  }
 }
 const destination=file.replace('test.10','test.11'),text=JSON.stringify(p,null,2)+'\n',target=path.join(out,destination);
 if(fs.existsSync(target)&&fs.readFileSync(target,'utf8')!==text)throw Error('Version exists with different contents: '+target);
 fs.writeFileSync(target,text);
}
if(corrected<60)throw Error('Too few fixed-step smoothers were corrected: '+corrected);
console.log('Built six separate test.11 time-corrected profiles; corrected '+corrected+' smoothers.');
