const fs=require('fs'),path=require('path'),crypto=require('crypto');
const root=path.resolve(__dirname,'..'),source=path.join(root,'downloads/realism-test9'),out=path.join(root,'downloads/realism-test10');
const id=s=>crypto.createHash('sha256').update(s).digest('hex').slice(0,32).replace(/(.{8})(.{4})(.{4})(.{4})(.{12})/,'$1-$2-$3-$4-$5');
fs.mkdirSync(out,{recursive:true});
for(const file of fs.readdirSync(source).filter(file=>file.endsWith('.siprofile')).sort()){
 const p=JSON.parse(fs.readFileSync(path.join(source,file),'utf8'));
 p.Name=p.Name.replace('test.9','test.10');p.ProfileId=id(p.Name);
 const uh60=/UH60L/i.test(file);
 p.Description='Realism test.10; requires Engine 1.3.0. Uses recorded detailed wheel-roll telemetry with speed-only fallback. '+
  (uh60?'UH-60 flight, rotor, landing, and ground behavior replayed against the 2026-09-19 test.121 recording; tactile gain is pending seat validation.':'Shared engine behavior applied; aircraft-specific live recording and seat validation remain pending.')+
  ' Optional unconfirmed and augmented cues remain disabled. Hardware-neutral output routing.';
 for(const group of p.EffectsContainers||[]){
  group.ContainerId=id(p.Name+'/'+group.Description);
  for(const e of group.EffectsContainers||[])e.ContainerId=id(p.Name+'/'+group.Description+'/'+e.Description);
 }
 const destination=file.replace('test.9','test.10'),text=JSON.stringify(p,null,2)+'\n',target=path.join(out,destination);
 if(fs.existsSync(target)&&fs.readFileSync(target,'utf8')!==text)throw Error('Version exists with different contents: '+target);
 fs.writeFileSync(target,text);
}
console.log('Built six separate test.10 recorded-ground profiles.');
