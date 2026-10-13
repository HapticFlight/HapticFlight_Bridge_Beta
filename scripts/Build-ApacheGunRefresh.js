const fs=require('fs'),path=require('path'),crypto=require('crypto');
const out=path.resolve('downloads/apache-test5');fs.mkdirSync(out,{recursive:true});
const p=JSON.parse(fs.readFileSync('downloads/rotor-refresh-v2/DCS-AH-64D-Apache-Haptic-Engine-v2.0.0-test.4.siprofile'));
p.Name='DCS AH-64D Apache - Haptic Engine v2.0.0-test.5 Gun Balance';p.ProfileId=crypto.randomUUID();
p.Description='Apache V2 test.5: September 7 recording-informed M230 balance. Recognizes Cannon alias, fixed 625/60 Hz M230 carrier, reduced gun ceilings. Test.4 rotor baseline and every non-gun effect retained. Live audio clipping is not measurable from telemetry alone.';
for(const e of p.EffectsContainers.flatMap(g=>g.EffectsContainers)){
 if(e.Description==='AH-64 M230 chain-gun rhythm'){
  e.Gain=12;e.FrontLeftFormula.Expression=e.FrontLeftFormula.Expression.replaceAll('70','50').replace('target = Math.min(50, 50 *',"var name = String($prop('DcsExtraTelemetryPlugin.DCSExtra.LastFiredWeaponName') || '').toLowerCase();\n    if (name === 'cannon' || name.indexOf('m230') >= 0) target = Math.min(50, 50 *");
  e.FrontLeftFrequencyFormula.Expression='return 625 / 60;';e.ForceFrequencies=true;
 }
 if(e.Description==='AH-64 M230 low-body thump'){
  e.Gain=4;e.FrontLeftFormula.Expression=e.FrontLeftFormula.Expression.replaceAll('48','18').replace("if (name.indexOf('m230') >= 0)","if (name === 'cannon' || name.indexOf('m230') >= 0)");
  e.FrontLeftFrequencyFormula.Expression='return (625 / 60) * 0.5;';e.ForceFrequencies=true;
 }
}
fs.writeFileSync(path.join(out,'DCS-AH-64D-Apache-Haptic-Engine-v2.0.0-test.5.siprofile'),JSON.stringify(p,null,2));
console.log('Built test.5: M230 primary 6.00 maximum after gain; body 0.72 maximum; non-gun effects unchanged.');
