import {chooseRoute,navaidPositions} from './filed-route.mjs';
import {filterRoute} from './route-filter.mjs';
import {simulate,pluginBoundary} from './proximity-test.mjs';
import fs from 'node:fs/promises';
import path from 'node:path';
import AdmZip from 'adm-zip';
import {confirmSupervisor} from './supervisor.mjs';
import {evaluate,compileVatSpy,combineJsonFiles,convertCoords} from './coverage.mjs';
async function get(url,json=true){
 if(!url.startsWith('https://'))throw Error('Fonte precisa usar HTTPS');
 const r=await fetch(url,{headers:{'User-Agent':'FlightWatch/0.1 personal noncommercial test'},signal:AbortSignal.timeout(20000)});
 if(!r.ok)throw Error(`Fonte indisponível (${r.status}): ${new URL(url).hostname}`);
 return json?r.json():r.text();
}
async function lookupFixes(ids,cache){
 const fixIds=ids.filter(id=>id.length===5);
 const radioIds=ids.filter(id=>id.length<5);
 const [fixes,radios]=await Promise.all([
  fixIds.length?getNamedFixes(fixIds):[],
  radioIds.length?getNavaids(cache,radioIds):[],
 ]);
 return [...fixes,...radios];
}
async function getNamedFixes(names){
 const url=new URL('https://aviationweather.gov/api/data/fix');
 url.searchParams.set('ids',names.join(','));url.searchParams.set('format','json');
 const response=await fetch(url,{signal:AbortSignal.timeout(8000),headers:{'User-Agent':'Chu-Soneca (github.com/chupasseios/chu-soneca)'}});
 if(response.status===204)return [];
 if(!response.ok)throw Error('Consulta de fixos indisponível ('+response.status+').');
 return response.json();
}
async function getNavaids(cache,ids){
 const file=path.join(cache,'navaids.csv');
 let csv;
 try{if(Date.now()-(await fs.stat(file)).mtimeMs<86400000)csv=await fs.readFile(file,'utf8');}catch{}
 if(!csv){
  const response=await fetch('https://raw.githubusercontent.com/davidmegginson/ourairports-data/main/navaids.csv',{signal:AbortSignal.timeout(10000)});
  if(!response.ok)throw Error('Catálogo de auxílios indisponível ('+response.status+').');
  csv=await response.text();
  const temp=file+'.'+process.pid+'.tmp';
  try{await fs.mkdir(cache,{recursive:true});await fs.writeFile(temp,csv);await fs.rename(temp,file);}finally{await fs.rm(temp,{force:true});}
 }
 return navaidPositions(csv,new Set(ids));
}
async function fetchOfp(id){
 const url=new URL('https://www.simbrief.com/api/xml.fetcher.php');
 url.searchParams.set(/^\d+$/.test(id)?'userid':'username',id);
 url.searchParams.set('json','1');url.searchParams.set('client','ChuSoneca');
 const response=await fetch(url,{signal:AbortSignal.timeout(10000)});
 if(response.status===400||response.status===404)throw Error('Confira o usuário/Pilot ID e se há um plano gerado nessa conta.');
 if(!response.ok)throw Error('Serviço indisponível ('+response.status+').');
 return response.json();
}
async function staticData(cache){
 const file=path.join(cache,'datasets.json');
 try {const c=JSON.parse(await fs.readFile(file,'utf8'));if(Date.now()-c.created<86400000)return c;}catch{}
 const [map,release]=await Promise.all([get('https://api.vatsim.net/api/map_data/'),get('https://api.github.com/repos/vatsimnetwork/simaware-tracon-project/releases/latest')]);
 const asset=release.assets.find(a=>a.name==='TRACONBoundaries.geojson');if(!asset)throw Error('SimAware indisponível');
 const [dat,geo,simaware,zipResponse]=await Promise.all([get(map.vatspy_dat_url,false),get(map.fir_boundaries_geojson_url,false),get(asset.browser_download_url),fetch('https://codeload.github.com/lennycolton/vatglasses-data/zip/refs/heads/main',{signal:AbortSignal.timeout(60000)})]);
 if(!zipResponse.ok)throw Error('VATGlasses indisponível');
 const vg=convertCoords(combineJsonFiles(new AdmZip(Buffer.from(await zipResponse.arrayBuffer()))));
 if(!Object.keys(vg).length)throw Error('VATGlasses vazio');
 const c={created:Date.now(),vatspy:compileVatSpy(map.current_commit_hash,dat,geo),simaware,vg,versions:{vatspy:map.current_commit_hash,simaware:release.tag_name,vgFetched:new Date().toISOString()}};
 await fs.mkdir(cache,{recursive:true});await fs.writeFile(file+'.tmp',JSON.stringify(c));await fs.rename(file+'.tmp',file);return c;
}
async function main(){
const context = {};
let input='';for await(const chunk of process.stdin)input+=chunk;
try {
 const request=JSON.parse(input);
 if(request.mode==='supervisor'){
  const feed=await get('https://data.vatsim.net/v3/vatsim-data.json');
  process.stdout.write(JSON.stringify(confirmSupervisor(feed,request.sender,request.messageTimeUtc)));return;
 }
 const feed=await get('https://data.vatsim.net/v3/vatsim-data.json');
 const timestamp=Date.parse(feed.general?.update_timestamp);const age=Date.now()-timestamp;
 if(!Array.isArray(feed.controllers))throw Error('Lista de controladores ausente no feed');
 if(!Number.isFinite(age)||age>90000||age< -30000)throw Error('Feed da VATSIM desatualizado');
 if(!Array.isArray(feed.pilots))throw Error('Lista de pilotos ausente no feed.');
 const matches=feed.pilots.filter(p=>String(p.cid)===String(request.cid));
 const pilot=matches.length===1 && matches[0].callsign===request.callsign?matches[0]:null;
 if(request.mode!=='remoteTest'){
  context.planChecked=Boolean(request.connected || pilot);
  context.flightPlan=pilot?.flight_plan?{cid:String(pilot.cid),callsign:pilot.callsign,departure:pilot.flight_plan.departure,arrival:pilot.flight_plan.arrival,altitude:pilot.flight_plan.altitude}:null;
  if(matches.length && !pilot)throw Error('CID ou indicativo divergente na VATSIM.');
  if(request.planOnly){process.stdout.write(JSON.stringify({ok:true,...context}));return;}
 }
 const [datasets,dynamic]=await Promise.all([staticData(request.cache),get('https://api3.vatglasses.uk/live/activeownership')]);
 if(request.mode==='remoteTest'){
  const matches=feed.pilots?.filter(p=>String(p.cid)===String(request.cid))??[];
  if(matches.length!==1)throw Error('CID não encontrado em um único voo online');
  const pilot=matches[0];
  const result=await evaluate({...datasets,feed,dynamic,longitude:pilot.longitude,latitude:pilot.latitude,flightLevel:pilot.altitude/100,captureZones:true});
  const test=simulate({previous:request.previous,pilot,timestamp,result,exitNm:request.exitNm});
  process.stdout.write(JSON.stringify({ok:true,...test,timestamp,callsign:pilot.callsign,latitude:pilot.latitude,longitude:pilot.longitude,altitude:pilot.altitude,unmapped:result.unmapped}));return;
 }
 context.routeChecked=true;
 const route=await chooseRoute({request,pilot,airports:datasets.vatspy.keyAirports.realIcao,lookup:ids=>lookupFixes(ids,request.cache),fetchOfp});
 context.routeSnapshot=route;
 const previous=request.routeSnapshot && request.routeSnapshot.route===route.route &&
  request.routeSnapshot.departure===route.departure && request.routeSnapshot.arrival===route.arrival?request.previousBoundary:null;
 let result=await evaluate({...datasets,feed,dynamic,...request,captureZones:true});
 result=filterRoute(result,route,[request.longitude,request.latitude],previous,request.exitNm);
 result.routeStatus=route.source+' · '+result.routeStatus;
 const onlineControllers=(feed.controllers??[]).map(c=>({cid:String(c.cid),callsign:c.callsign,frequency:c.frequency,logon_time:c.logon_time}));
 const boundary=pluginBoundary({previous,pilot:{cid:request.cid,callsign:request.callsign,logon_time:request.sessionId,longitude:request.longitude,latitude:request.latitude},timestamp:request.sampleTimeUtc,result,exitNm:request.exitNm},onlineControllers);
 delete result.zones;
 process.stdout.write(JSON.stringify({ok:true,...result,boundary,onlineControllers,timestamp,dataAgeSeconds:(Date.now()-timestamp)/1000,versions:datasets.versions,...context}));
}catch(e){process.stdout.write(JSON.stringify({ok:false,error:e.message,...context}));process.exitCode=1;}
}
main();
