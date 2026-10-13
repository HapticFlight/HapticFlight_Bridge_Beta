// Preserve the latest rotor/weapon tuning; replace only ground interaction.
const fs = require('fs'), path = require('path'), crypto = require('crypto');
const root = path.resolve(__dirname, '..');
const out = path.join(root, 'downloads/ground-effects-v2');
const read = p => JSON.parse(fs.readFileSync(path.join(root, p), 'utf8').replace(/^\uFEFF/, ''));
const id = s => crypto.createHash('sha256').update(s).digest('hex').slice(0, 32).replace(/(.{8})(.{4})(.{4})(.{4})(.{12})/, '$1-$2-$3-$4-$5');
const configs = [
 ['UH60L','uh60','downloads/haptic-engine-v2/DCS-UH60L-Haptic-Engine-v2.0.0-test.3.siprofile','Touchdown','wheels'],
 ['AH-64D-Apache','ah64','downloads/apache-test5/DCS-AH-64D-Apache-Haptic-Engine-v2.0.0-test.5.siprofile','AH64.HeavyLanding','wheels'],
 ['AH-6J','ah6','downloads/rotor-refresh-v2/DCS-AH-6J-Haptic-Engine-v2.0.0-test.4.siprofile','AH6.LightLanding','skids'],
 ['MH-6J','ah6','downloads/haptic-engine-v2/DCS-MH-6J-Haptic-Engine-v2.0.0-test.3.siprofile','AH6.LightLanding','skids'],
 ['UH-1H-Huey','uh1','downloads/haptic-engine-v2/DCS-UH-1H-Huey-Haptic-Engine-v2.0.0-test.3.siprofile','UH1.WeightOnSkidsLanding','binary'],
 ['OH-58D-Kiowa','oh58','downloads/haptic-engine-v2/DCS-OH-58D-Kiowa-Haptic-Engine-v2.0.0-test.3.siprofile','Touchdown','skids']
];
const P = n => `$prop('DcsExtraTelemetryPlugin.DCSExtra.${n}')`;
const V = n => `Math.max(0, Math.min(1, ${P('Haptic.'+n)}))`;
const gate = `if (!${P('Connected')} || !(parseFloat(${P('Haptic.EngineVersion')}) >= 1.1)) return 0;\n`;
fs.mkdirSync(out, {recursive:true});
const audit = [];
for (const [aircraft, folder, source, landing, kind] of configs) {
 const p = read(source), g = p.EffectsContainers.find(g => g.Description === 'Ground Interaction');
 const template = g.EffectsContainers.find(e => e.Description === 'Touchdown body impact');
 p.Name = `DCS ${aircraft.replaceAll('-',' ')} - Haptic Engine v2.0.0-test.6`;
 p.ProfileId = id(p.Name); p.CarChoices = []; p.CarChoice = null; p.IncludeOutputSettingsInProfile = false;
 p.Description = 'Ground effects test.6; requires Haptic Engine 1.1.0 or newer. Latest aircraft rotor/weapon tuning preserved. Provisional ground calibration; manual selection, preserve your output routing.';
 const effects = [];
 function effect(name, gain, frequency, expression, noise=false) {
  const e = JSON.parse(JSON.stringify(template));
  e.Description = name; e.ContainerId = id(p.Name+name); e.Gain = gain; e.IsEnabled = true;
  e.FrontLeftFormula.PreExpression = ''; e.FrontLeftFormula.Expression = gate + expression;
  e.FrontLeftFrequencyFormula.Expression = ''; e.ForceFrequencies = false;
  e.Output.Frequency = frequency; e.Output.UseWhiteNoise = noise; e.Output.WhiteNoise = noise ? 8 : 0;
  effects.push(e); return e;
 }
 effect('Touchdown body impact', kind==='skids'?28:34, kind==='skids'?25:22, `return 85 * ${V(landing+'.Envelope')};`);
 effect('Ground settling', 12, 18, `return 40 * ${V('Ground.Settling.Envelope')};`);
 effect('Liftoff unloading', 9, 18, `return 25 * ${V('Liftoff.Envelope')};`);
 if (kind !== 'binary') {
  effect('Ground bump impact', kind==='skids'?22:27, kind==='skids'?27:23, `return 75 * ${V('Ground.Bump.Envelope')};`);
  const names = kind==='wheels' ? ['Contact.Left','Contact.Right','Contact.Tail'] : ['Skid.FrontLeft','Skid.FrontRight','Skid.RearLeft','Skid.RearRight'];
  const contactMax = `Math.max(${names.map(n=>V(n+'.Envelope')).join(',')})`;
  effect(kind==='wheels'?'Wheel contact detail':'Individual skid contact detail', 14, 31,
   `return 40 * ${contactMax} * (1 - 0.65 * ${V(landing+'.Envelope')});`);
  effect('Suspension surface roughness', 14, 29, `return 45 * ${V('Ground.Suspension.Intensity')};`, true);
  effect('Ground support rocking', 9, 19, `return 30 * Math.max(${V('Ground.PitchRock.Intensity')}, ${V('Ground.RollRock.Intensity')});`);
 }
 if (kind === 'wheels') {
  const e = effect('Ground roll base', 16, 26,
   `if (!${P('OnGround')}) return 0; return 45 * ${V('Ground.Roll.Intensity')} * (1 - 0.75 * Math.max(${V(landing+'.Envelope')}, ${V('Ground.Bump.Envelope')}));`);
  e.FrontLeftFrequencyFormula.Expression = `return Math.max(18, Math.min(36, 18 + ${P('GroundSpeedMps')}));`; e.ForceFrequencies = true;
  effect('Wheel braking load', 20, 22, `if (!${P('OnGround')}) return 0; return 60 * ${V('Braking.Intensity')};`);
  effect('Wheel shimmy', 8, 38, `if (!${P('OnGround')}) return 0; return 28 * ${V('Ground.WheelShimmy.Intensity')};`, true);
  effect('Tailwheel caster', 6, 22, `if (!${P('OnGround')}) return 0; return 24 * ${V('Ground.TailwheelCaster.Intensity')};`);
 } else {
  effect('Skid slide and surface texture', 15, 26, `if (!${P('OnGround')}) return 0; return 38 * ${V('Ground.SkidScrub.Intensity')} * (1 - 0.6 * ${V(landing+'.Envelope')});`, true);
 }
 effect('Gear-borne rotor vibration', 8, 20,
  `if (!${P('OnGround')}) return 0; return 24 * Math.min(${V('Rotor.Presence.Intensity')}, ${V('Ground.Resonance.Intensity')}) * (1 - 0.65 * Math.max(${V(landing+'.Envelope')}, ${V('Ground.Bump.Envelope')}));`);
 g.EffectsContainers = effects;
 const file = `DCS-${aircraft}-Haptic-Engine-v2.0.0-test.6.siprofile`;
 fs.writeFileSync(path.join(out,file), JSON.stringify(p,null,2)+'\n');
 fs.writeFileSync(path.join(root,'profiles',folder,file), JSON.stringify(p,null,2)+'\n');
 audit.push({aircraft, source, file, kind, preservedGroups:p.EffectsContainers.filter(x=>x!==g).map(x=>x.Description), groundEffects:effects.map(e=>e.Description)});
}
fs.writeFileSync(path.join(out,'Ground-Effect-Audit.json'), JSON.stringify(audit,null,2)+'\n');
console.log('Built six ground-effects test.6 profiles from their latest published baselines.');
