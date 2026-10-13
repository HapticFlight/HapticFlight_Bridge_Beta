// Migrate the aircraft tuning to the same signal contract as the working UH60 V2 profile.
const fs=require('fs'),path=require('path'),crypto=require('crypto');
const root=path.resolve(__dirname,'..');
const engineRoot=process.argv[2];if(!engineRoot)throw Error('Pass the checkout containing the Haptic Engine source.');
const read=p=>fs.readFileSync(p,'utf8').replace(/^\uFEFF/,'');
const generator=read(path.join(engineRoot,'scripts/Build-HapticEngineProfiles.ps1'));
const block=generator.split('$commonReplacements = [ordered]@{')[1].split('\n}')[0];
const map=Object.fromEntries([...block.matchAll(/'([^']+)'\s*=\s*'([^']+)'/g)].map(m=>[m[1],m[2]]));
Object.assign(map,{HardManeuverPulse:'Haptic.Maneuver.Hard.Envelope',TailWheelContactPulse:'Haptic.Contact.Tail.Envelope',TouchdownPulse:'Haptic.Touchdown.Envelope',SkidFrontLeftContactPulse:'Haptic.Skid.FrontLeft.Envelope',SkidFrontRightContactPulse:'Haptic.Skid.FrontRight.Envelope',SkidRearLeftContactPulse:'Haptic.Skid.RearLeft.Envelope',SkidRearRightContactPulse:'Haptic.Skid.RearRight.Envelope'});
const plugin=read(path.join(engineRoot,'src/DcsExtraTelemetry.Plugin/DcsExtraTelemetryPlugin.cs'));
const registered=[...plugin.matchAll(/AttachDelegate\("DCSExtra\.([^"]+)"/g)].map(m=>m[1]);
for(const m of plugin.matchAll(/RegisterHapticSignal\("([^"]+)"/g)){
 for(const suffix of ['Active','Intensity','Envelope','Phase','EventId','Confidence','Source'])registered.push('Haptic.'+m[1]+'.'+suffix);
 registered.push('Has.Haptic'+m[1].replaceAll('.',''));
}
const out=path.join(root,'downloads/haptic-engine-v2');fs.mkdirSync(out,{recursive:true});
const P=n=>`$prop('DcsExtraTelemetryPlugin.DCSExtra.${n}')`;
const configs=[['AH-64D Apache','AH64.HeavyLanding'],['OH-58D Kiowa','Touchdown'],['AH-6J','AH6.LightLanding'],['MH-6J','AH6.LightLanding'],['UH-1H Huey','UH1.WeightOnSkidsLanding'],['UH60L','Touchdown']];
const audit=[];
for(const [aircraft,landing] of configs){
 const uh=aircraft==='UH60L';
 const source=uh?path.join(root,'profiles/uh60/DCS World - DCS UH60L - Haptic Engine v2.0.0-test.1.siprofile'):
 path.join(root,'downloads/helicopter-profiles',`DCS World - DCS ${aircraft} - DCSExtra Repaired v0.4.siprofile`);
 const p=JSON.parse(read(source));
 p.Name=`DCS ${aircraft} - Haptic Engine v2.0.0-test.3`;
 p.ProfileId=crypto.createHash('sha256').update(p.Name).digest('hex').slice(0,32).replace(/(.{8})(.{4})(.{4})(.{4})(.{12})/,'$1-$2-$3-$4-$5');
 p.CarChoices=[];p.CarChoice=null;p.IncludeOutputSettingsInProfile=false;
 p.Description='V2 haptic-engine profile migration. Manual selection; engine Haptic.* properties required. Aircraft effect gains retained; all included effects enabled. Source/fixture verified, not live-flight calibrated.';
 for(const g of p.EffectsContainers){g.IsEnabled=true;for(const e of g.EffectsContainers){
  e.IsEnabled=true;const before=e.FrontLeftFormula.Expression;
  for(const k of ['FrontLeftFormula','FrontLeftFrequencyFormula']){
   if(!uh)e[k].Expression=e[k].Expression.replace(/DcsExtraTelemetryPlugin\.DCSExtra\.([A-Za-z0-9.]+)/g,(full,n)=>'DcsExtraTelemetryPlugin.DCSExtra.'+(map[n]||n));
   if(!uh)e[k].Expression=e[k].Expression.replaceAll(P('Haptic.Touchdown.Envelope'),P('Haptic.'+landing+'.Envelope'));
  }
  if(!uh&&e.Description==='Touchdown body impact')e.FrontLeftFormula.Expression=`return 85 * Math.max(0, Math.min(1, ${P('Haptic.'+landing+'.Envelope')}));`;
  if(!uh&&/skid ground motion|Skid slide/.test(e.Description)){
   e.FrontLeftFormula.PreExpression='';
   e.FrontLeftFormula.Expression=`if (!${P('OnGround')}) return 0; return 38 * Math.max(0, Math.min(1, ${P('Haptic.Ground.SkidScrub.Intensity')}));`;
  }
  // Use the engine's per-frame metadata as a presence check, never legacy fallback.
  e.FrontLeftFormula.Expression=`if (!${P('Connected')} || !${P('Haptic.EngineVersion')}) return 0;\n`+e.FrontLeftFormula.Expression;
  const deps=[...new Set([...JSON.stringify(e).matchAll(/DcsExtraTelemetryPlugin\.DCSExtra\.([A-Za-z0-9.]+)/g)].map(m=>m[1]))];
  const missing=deps.filter(n=>!registered.includes(n));if(missing.length)throw Error(aircraft+': '+missing);
  if(!uh&&deps.some(n=>map[n]))throw Error('Legacy derived property remains');
  audit.push({aircraft,effect:e.Description,gain:e.Gain,enabled:e.IsEnabled,dependencies:deps,changed:before!==e.FrontLeftFormula.Expression});
 }}
 fs.writeFileSync(path.join(out,`DCS-${aircraft.replaceAll(' ','-')}-Haptic-Engine-v2.0.0-test.3.siprofile`),JSON.stringify(p,null,2));
}
const sources=['scripts/Build-HapticEngineProfiles.ps1','src/DcsExtraTelemetry.Plugin/DcsExtraTelemetryPlugin.cs','src/DcsExtraTelemetry.Plugin/Haptics/HapticStateEngine.cs','src/DcsExtraTelemetry.Plugin/Haptics/Models/aircraft-haptic-models.json'].map(file=>({file,sha256:crypto.createHash('sha256').update(fs.readFileSync(path.join(engineRoot,file))).digest('hex')}));
fs.writeFileSync(path.join(out,'V2-Effect-Audit.json'),JSON.stringify({sources,registered,migrationMap:map,effects:audit},null,2));
console.log(`Built six V2 profiles; ${audit.length} enabled effects, validated against ${registered.length} registered properties.`);
