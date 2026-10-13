const fs=require('fs'),path=require('path'),crypto=require('crypto');
const root=path.resolve(__dirname,'..'),source=path.join(root,'downloads/realism-test8'),out=path.join(root,'downloads/realism-test9');
const id=s=>crypto.createHash('sha256').update(s).digest('hex').slice(0,32).replace(/(.{8})(.{4})(.{4})(.{4})(.{12})/,'$1-$2-$3-$4-$5');
fs.mkdirSync(out,{recursive:true});
for(const file of fs.readdirSync(source).filter(file=>file.endsWith('.siprofile')).sort()){
 const p=JSON.parse(fs.readFileSync(path.join(source,file),'utf8'));
 p.Name=p.Name.replace('test.8','test.9');p.ProfileId=id(p.Name);
 p.Description='Realism test.9; requires Engine 1.2.0 or newer. Capability-gated physical effects; unconfirmed and augmented cues are optional and disabled. Hardware-neutral output routing; module recordings and seat validation remain required.';
 for(const group of p.EffectsContainers||[]){
  group.ContainerId=id(p.Name+'/'+group.Description);
  for(const e of group.EffectsContainers||[]){
   if(/Airframe shock \(cause unconfirmed\)/i.test(e.Description)){
    e.Description='[OPTIONAL unconfirmed physical cue] Airframe shock';
    e.IsEnabled=false;
   }
   e.ContainerId=id(p.Name+'/'+group.Description+'/'+e.Description);
  }
 }
 const destination=file.replace('test.8','test.9'),text=JSON.stringify(p,null,2)+'\n',target=path.join(out,destination);
 if(fs.existsSync(target)&&fs.readFileSync(target,'utf8')!==text)throw Error('Version exists with different contents: '+target);
 fs.writeFileSync(target,text);
}
console.log('Built six separate test.9 capability-truth profiles.');
