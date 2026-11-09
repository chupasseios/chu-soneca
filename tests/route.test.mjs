import test from 'node:test';import assert from 'node:assert/strict';
import {filterRoute,routeCrosses,remainingRoute,verifyRoute,normalizedRoute} from '../engine/route-filter.mjs';
import {simulate} from '../engine/proximity-test.mjs';
const square={type:'Polygon',coordinates:[[[0,0],[2,0],[2,2],[0,2],[0,0]]]};
test('route crosses polygon even when endpoints are outside',()=>assert.equal(routeCrosses([[-1,1],[3,1]],square),true));
test('parallel nearby route is excluded',()=>assert.equal(routeCrosses([[-1,-.1],[3,-.1]],square),false));
test('route inside a polygon hole is excluded',()=>assert.equal(routeCrosses([[.6,.6],[1.4,1.4]],{...square,coordinates:[...square.coordinates,[[.5,.5],[1.5,.5],[1.5,1.5],[.5,1.5],[.5,.5]]]}),false));
test('route filtering removes irrelevant controller even if legacy hit list contains it',()=>{const result=filterRoute({zones:[{geometry:square,controllers:['SLC_44_CTR']}],controllers:[{callsign:'SLC_44_CTR'}],unmapped:[]},{points:[[-1,-.1],[3,-.1]],departure:'A',arrival:'B'},[0,-.1]);assert.equal(result.controllers.length,0);assert.equal(result.zones.length,0);});
test('already flown segment is not part of remaining route',()=>{const points=[[-10,0],[0,0],[10,0]];assert.deepEqual(remainingRoute(points,[5,0]),[[5,0],[10,0]]);});
test('large deviation blocks decisions instead of inventing a direct route',()=>assert.throws(()=>remainingRoute([[0,0],[2,0]],[1,5])));
test('step climbs on same airway do not mismatch SimBrief',()=>assert.equal(normalizedRoute('A UL655 ESARO/N0495F360 UL655 B'),'A UL655 B'));
test('different callsign or route is rejected',()=>{const r={points:[[0,0],[1,1]],departure:'SBGR',arrival:'KLAX',callsign:'TEST8607',route:'SID1 A DCT B'};const p={callsign:'TEST8607',flight_plan:{departure:'SBGR',arrival:'KLAX',route:'A DCT B'}};verifyRoute(r,p);assert.throws(()=>verifyRoute(r,{...p,callsign:'OTHER'}));assert.throws(()=>verifyRoute(r,{...p,flight_plan:{...p.flight_plan,route:'A DCT C'}}));});
test('APP has no extra 15 NM pre-entry margin',()=>{const result={zones:[{geometry:square,controllers:['ABC_APP'],entryNm:0}],controllers:[],unmapped:[]};let pilot={cid:1,callsign:'X',logon_time:'1',longitude:-.2,latitude:1};const a=simulate({pilot,timestamp:1,result});pilot={...pilot,longitude:-.1};assert.equal(simulate({pilot,timestamp:2,result,previous:a.state}).state.disconnected,false);});

test('exited area remains protected until the exit margin is reached',()=>{const input={zones:[{geometry:square,controllers:['TEST_CTR']}],controllers:[],unmapped:[]};const route={points:[[1,1],[3,1]],departure:'A',arrival:'B'};const previous={disconnected:true,signature:'TEST_CTR'};assert.equal(filterRoute(input,route,[2.1,1],previous,15).zones.length,1);assert.equal(filterRoute(input,route,[2.4,1],previous,15).zones.length,0);});


import {chooseRoute,navaidPositions} from '../engine/filed-route.mjs';
import {importOfp} from '../engine/route-filter.mjs';
const request={cid:'12345',callsign:'TEST5',sessionId:'leg-1',connected:true,simbrief:'test'};
const airports={AAAA:{lon:-74,lat:40},BBBB:{lon:-72,lat:42}};
const flight=route=>({cid:12345,callsign:'TEST5',flight_plan:{departure:'AAAA',arrival:'BBBB',altitude:'35000',route}});
const unused=async()=>{throw Error('Unexpected network call');};
const choose=(pilot,options={})=>chooseRoute({request,pilot,airports,lookup:unused,fetchOfp:unused,...options});
const ofp={origin:{icao_code:'AAAA',pos_long:-74,pos_lat:40},destination:{icao_code:'BBBB',pos_long:-72,pos_lat:42},atc:{callsign:'TEST5',route:'FIXAA A1 FIXBB'},general:{flight_number:'9',icao_airline:'WRONG',route:'FIXAA A1 FIXBB'},navlog:{fix:[{pos_long:-73.8,pos_lat:41},{pos_long:-73,pos_lat:41.8}]}};
test('filed DCT and ICAO coordinates work without a SimBrief account or network lookup',async()=>{
 const route=await choose(flight('DCT 4100N07300W DCT'),{request:{...request,simbrief:''}});
 assert.equal(route.source,'VATSIM');assert.ok(route.points.some(p=>p[0]===-73&&p[1]===41));
});
test('named fixes are resolved from filed identifiers and take priority over SimBrief',async()=>{
 const route=await choose(flight('FIXAA DCT FIXBB'),{lookup:async ids=>{assert.deepEqual(ids,['FIXAA','FIXBB']);return [{id:'FIXAA',lon:-73.8,lat:41},{id:'FIXBB',lon:-73,lat:41.8}];}});
 assert.equal(route.source,'VATSIM');
 assert.equal(await choose(flight('FIXAA DCT FIXBB'),{request:{...request,routeSnapshot:route}}),route);
});
test('an airway uses a matching secondary OFP with the ATC callsign',async()=>{
 const route=await choose(flight('FIXAA A1 FIXBB'),{fetchOfp:async()=>ofp});
 assert.equal(route.source,'VATSIM + SimBrief');assert.equal(route.callsign,'TEST5');
});
test('amended plan and mismatched OFP cannot reuse cached geometry',async()=>{
 const original=await choose(flight('DCT'));
 await assert.rejects(choose(flight('FIXAA A2 FIXBB'),{request:{...request,routeSnapshot:original},fetchOfp:async()=>ofp}),/diferente/);
});
test('missing or ambiguous fixes never become direct shortcuts',async()=>{
 for(const rows of [[],[{id:'FIXAA',lat:1,lon:2},{id:'FIXAA',lat:2,lon:3}]]){
  await assert.rejects(choose(flight('FIXAA'),{request:{...request,simbrief:''},lookup:async()=>rows}),/ausente ou ambígua/);
 }
});
test('offline continuity requires owned disconnect, same CID, callsign and session',async()=>{
 const routeSnapshot=await choose(flight('DCT'));
 const offline={...request,routeSnapshot,connected:false,ownedDisconnect:true};
 assert.equal(await choose(null,{request:offline}),routeSnapshot);
 for(const patch of [{connected:true},{ownedDisconnect:false},{sessionId:'other'},{cid:'99999'},{callsign:'OTHER'}]){
  await assert.rejects(choose(null,{request:{...offline,...patch}}));
 }
});
test('incomplete SimBrief coordinates cannot be converted to zero',()=>{
 assert.throws(()=>importOfp({...ofp,navlog:{fix:[{pos_long:null,pos_lat:2},ofp.destination]}}),/coordenadas/);
});
test('only marked step climbs can be removed during route comparison',()=>{
 assert.notEqual(normalizedRoute('A UL655 FIRST UL655 B'),normalizedRoute('A UL655 OTHER UL655 B'));
});

test('North Atlantic filed route resolves half degrees, whole degrees and Torbay VOR without an OFP',async()=>{
 const route='H4135 DCT 43N038W DCT H4442 DCT H4545 DCT 4750N DCT YYT';
 const result=await choose(flight(route),{lookup:async ids=>{
  assert.deepEqual(ids,['YYT']);
  return navaidPositions('ident,name,longitude_deg,latitude_deg\nYYT,"Torbay, VOR",-52.8522,47.4853',new Set(ids));
 }});
 for(const [lon,lat] of [[-35,41.5],[-38,43],[-42,44.5],[-45,45.5],[-50,47],[-52.8522,47.4853]]){
  assert.ok(result.points.some(point=>point[0]===lon&&point[1]===lat));
 }
 assert.equal(result.source,'VATSIM');
});
test('OFP verification accepts filed runway endpoints, optional DCT and speed annotations',()=>{
 const imported=importOfp(ofp);
 verifyRoute(imported,flight('AAAA/10L N0496F320 FIXAA A1 FIXBB BBBB/25L'));
 assert.throws(()=>verifyRoute(imported,flight('FIXAA DCT FIXBB')));
});

test('bundled worker returns the VATSIM flight level even if secondary geometry fails',async()=>{
 const {mkdtemp,writeFile,rm}=await import('node:fs/promises');
 const {tmpdir}=await import('node:os');
 const {join}=await import('node:path');
 const {fileURLToPath}=await import('node:url');
 const {spawnSync}=await import('node:child_process');
 const dir=await mkdtemp(join(tmpdir(),'chu-filed-plan-'));
 try{
  const feed={general:{update_timestamp:new Date().toISOString()},pilots:[flight('FIXAA A1 FIXBB')],controllers:[]};
  const hook=join(dir,'fetch.cjs');
  await writeFile(hook,`global.fetch=async url=>String(url).includes('xml.fetcher') ? new Response('invalid account',{status:400}) : Response.json(String(url).includes('vatsim-data')?${JSON.stringify(feed)}:{});`);
  await writeFile(join(dir,'datasets.json'),JSON.stringify({created:Date.now(),vatspy:{keyAirports:{realIcao:airports}}}));
  const worker=fileURLToPath(new URL('../engine/dist/coverage.cjs',import.meta.url));
  const run=extra=>JSON.parse(spawnSync(process.execPath,['--require',hook,worker],{input:JSON.stringify({...request,cache:dir,...extra}),encoding:'utf8'}).stdout);
  const pending=run({});assert.equal(pending.ok,false);assert.equal(pending.flightPlan.altitude,'35000');assert.match(pending.error,/Pilot ID/);assert.ok(pending.planChecked);
  const planOnly=run({planOnly:true});assert.equal(planOnly.ok,true);assert.equal(planOnly.flightPlan.altitude,'35000');
 }finally{await rm(dir,{recursive:true,force:true});}
});
