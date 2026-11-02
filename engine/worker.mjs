import {importOfp,verifyRoute,filterRoute} from './route-filter.mjs';
import {simulate,pluginBoundary} from './proximity-test.mjs';
import fs from 'node:fs/promises';
import path from 'node:path';
import AdmZip from 'adm-zip';
import {confirmSupervisor} from './supervisor.mjs';
import {evaluate,compileVatSpy,combineJsonFiles,convertCoords} from './coverage.mjs';
async function get(url,json=true){
 if(!url.startsWith('https://'))throw Error('Fonte precisa usar HTTPS');
 const r=await fetch(url,{headers:{'User-Agent':'FlightWatch/0.1 personal noncommercial test'},signal:AbortSignal.timeout(20000)});
 if(!r.ok)throw Error(`Fonte indisponÃ­vel (${r.status}): ${new URL(url).hostname}`);
 return json?r.json():r.text();
}
async function staticData(cache){
 const file=path.join(cache,'datasets.json');
 try {const c=JSON.parse(await fs.readFile(file,'utf8'));if(Date.now()-c.created<86400000)return c;}catch{}
 const [map,release]=await Promise.all([get('https://api.vatsim.net/api/map_data/'),get('https://api.github.com/repos/vatsimnetwork/simaware-tracon-project/releases/latest')]);
 const asset=release.assets.find(a=>a.name==='TRACONBoundaries.geojson');if(!asset)throw Error('SimAware indisponÃ­vel');
 const [dat,geo,simaware,zipResponse]=await Promise.all([get(map.vatspy_dat_url,false),get(map.fir_boundaries_geojson_url,false),get(asset.browser_download_url),fetch('https://codeload.github.com/lennycolton/vatglasses-data/zip/refs/heads/main',{signal:AbortSignal.timeout(60000)})]);
 if(!zipResponse.ok)throw Error('VATGlasses indisponÃ­vel');
 const vg=convertCoords(combineJsonFiles(new AdmZip(Buffer.from(await zipResponse.arrayBuffer()))));
 if(!Object.keys(vg).length)throw Error('VATGlasses vazio');
 const c={created:Date.now(),vatspy:compileVatSpy(map.current_commit_hash,dat,geo),simaware,vg,versions:{vatspy:map.current_commit_hash,simaware:release.tag_name,vgFetched:new Date().toISOString()}};
 await fs.mkdir(cache,{recursive:true});await fs.writeFile(file+'.tmp',JSON.stringify(c));await fs.rename(file+'.tmp',file);return c;
}
async function main(){
let input='';for await(const chunk of process.stdin)input+=chunk;
try {
 const request=JSON.parse(input);
 if(request.mode==='importRoute'){
  const id=String(request.simbrief??'').trim();if(!id)throw Error('Informe o usuário ou Pilot ID do SimBrief.');
  const ofp=await get('https://www.simbrief.com/api/xml.fetcher.php?'+(/^[0-9]+$/.test(id)?'userid=':'username=')+encodeURIComponent(id)+'&json=1&client=ChuSoneca');
  process.stdout.write(JSON.stringify({ok:true,route:importOfp(ofp)}));return;
 }
 if(request.mode==='supervisor'){
  const feed=await get('https://data.vatsim.net/v3/vatsim-data.json');
  process.stdout.write(JSON.stringify(confirmSupervisor(feed,request.sender,request.messageTimeUtc)));return;
 }
 const [datasets,feed,dynamic]=await Promise.all([staticData(request.cache),get('https://data.vatsim.net/v3/vatsim-data.json'),get('https://api3.vatglasses.uk/live/activeownership')]);
 const timestamp=Date.parse(feed.general?.update_timestamp);const age=Date.now()-timestamp;
 if(!Array.isArray(feed.controllers))throw Error('Lista de controladores ausente no feed');
 if(!Number.isFinite(age)||age>90000||age< -30000)throw Error('Feed da VATSIM desatualizado');
 if(request.mode==='remoteTest'){
  const matches=feed.pilots?.filter(p=>String(p.cid)===String(request.cid))??[];
  if(matches.length!==1)throw Error('CID nÃ£o encontrado em um Ãºnico voo online');
  const pilot=matches[0];
  const result=await evaluate({...datasets,feed,dynamic,longitude:pilot.longitude,latitude:pilot.latitude,flightLevel:pilot.altitude/100,captureZones:true});
  const test=simulate({previous:request.previous,pilot,timestamp,result,exitNm:request.exitNm});
  process.stdout.write(JSON.stringify({ok:true,...test,timestamp,callsign:pilot.callsign,latitude:pilot.latitude,longitude:pilot.longitude,altitude:pilot.altitude,unmapped:result.unmapped}));return;
 }
 let result=await evaluate({...datasets,feed,dynamic,...request,captureZones:true});
 const pilot=feed.pilots?.find(p=>String(p.cid)===String(request.cid));
 if(pilot&&pilot.callsign!==request.callsign)throw Error('CID online com outro callsign.');
 const route=request.route;
 if(pilot)verifyRoute(route,pilot);
 else if(!route||!request.routeValidated||route.callsign!==request.callsign)throw Error('Rota não validada para esta sessão.');
 result=filterRoute(result,route,[request.longitude,request.latitude],request.previousBoundary,request.exitNm);
 const onlineControllers=(feed.controllers??[]).map(c=>({cid:String(c.cid),callsign:c.callsign,frequency:c.frequency,logon_time:c.logon_time}));
 const boundary=pluginBoundary({previous:request.previousBoundary,pilot:{cid:request.cid,callsign:request.callsign,logon_time:request.sessionId,longitude:request.longitude,latitude:request.latitude},timestamp:request.sampleTimeUtc,result,exitNm:request.exitNm},onlineControllers);
 delete result.zones;
 process.stdout.write(JSON.stringify({ok:true,...result,boundary,onlineControllers,timestamp,dataAgeSeconds:(Date.now()-timestamp)/1000,versions:datasets.versions,flightPlan:pilot?.flight_plan?{cid:String(pilot.cid),callsign:pilot.callsign,departure:pilot.flight_plan.departure,arrival:pilot.flight_plan.arrival,altitude:pilot.flight_plan.altitude}:null}));
}catch(e){process.stdout.write(JSON.stringify({ok:false,error:e.message}));process.exitCode=1;}
}
main();
