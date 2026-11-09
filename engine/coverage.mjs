// The association algorithms below are compiled, unchanged, from pinned Radar source.
import atcCode from './vendor/atc.js.txt';
import vgCode from './vendor/vg.js.txt';
import runwayCode from './vendor/runway.js.txt';
import utilsCode from './vendor/utils.js.txt';
import duplicatesCode from './vendor/duplicates.js.txt';
import sharedCode from './vendor/shared.js.txt';
import vatspyCode from './vendor/vatspy.js.txt';
import vgcompileCode from './vendor/vgcompile.js.txt';
import pointInPolygon from '@turf/boolean-point-in-polygon';
import {polygon} from '@turf/helpers';
export const RADAR_COMMIT='49de87d7d88f04a90a8df88cd19007f59ce027f9';
function moduleOf(code,env={}) { const out={}; new Function(...Object.keys(env),'exports',code)(...Object.values(env),out); return out; }
export const {compileVatSpy}=moduleOf(vatspyCode);
export const {combineJsonFiles,convertCoords}=moduleOf(vgcompileCode);
const shared=moduleOf(sharedCode);
const facilities={OBS:0,FSS:1,DEL:2,GND:3,TWR:4,APP:5,CTR:6,RMP:-3,ATIS:-1};
const ref=value=>({value});
// Close source rings before both containment and proximity calculations.
// Never discard invalid areas: that could incorrectly authorize reconnection.
function closedGeometry(geometry){
 const close=ring=>{
  if(!Array.isArray(ring)||ring.length<3||ring.some(p=>!Array.isArray(p)||p.length<2||!Number.isFinite(p[0])||!Number.isFinite(p[1])||Math.abs(p[0])>180||Math.abs(p[1])>90))throw Error('Área de controle com coordenadas inválidas');
  if(new Set(ring.map(p=>`${p[0]},${p[1]}`)).size<3)throw Error('Área de controle sem três pontos distintos');
  const first=ring[0],last=ring.at(-1);
  return first[0]===last[0]&&first[1]===last[1]?ring:[...ring,[...first]];
 };
 const rings=coordinates=>{if(!Array.isArray(coordinates)||!coordinates.length)throw Error('Área de controle vazia');return coordinates.map(close);};
 if(geometry?.type==='Polygon')return {...geometry,coordinates:rings(geometry.coordinates)};
 if(geometry?.type==='MultiPolygon'&&Array.isArray(geometry.coordinates)&&geometry.coordinates.length)return {...geometry,coordinates:geometry.coordinates.map(rings)};
 throw Error('Geometria de controle não suportada');
}
export async function evaluate({feed,vatspy,simaware,vg,dynamic,longitude,latitude,flightLevel,captureZones=false}){
 if(!Number.isFinite(longitude)||!Number.isFinite(latitude)||Math.abs(latitude)>90||Math.abs(longitude)>180||!Number.isFinite(flightLevel))throw Error('Posição inválida');
 if(!Array.isArray(feed.controllers)||!Array.isArray(feed.atis))throw Error('Feed inválido');
 const controllers=feed.controllers.filter(x=>x.facility!==0).map(x=>({...x,facility:shared.getFacilityByCallsign(x.callsign)})).filter(x=>x.facility!==-1&&x.facility!==0);
 const groups={};
 for(let f of simaware.features){if(f.features)f=f.features[0];for(const p of f.properties.prefix??[])(groups[p.split('_')[0]]??=[]).push(f);}
 Object.values(groups).forEach(g=>g.sort((a,b)=>!!b.properties.suffix-!!a.properties.suffix));
 const positions={};
 for(const [country,data] of Object.entries(vg))for(const [id,p] of Object.entries(data.positions??{})){
  for(const pre of typeof p.pre==='string'?[p.pre]:p.pre??[])(positions[pre.split('_')[0]]??=[]).push({...p,id,countryId:country});
 }
 const ds={vatspy:ref({data:vatspy}),versions:ref({simaware:'pinned'}),vatsim:{data:{controllers:ref(controllers),atis:ref(feed.atis),facilities:ref(Object.entries(facilities).map(([short,id])=>({short,id}))) }},
 vatglassesActivePositions:ref({}),vatglassesDynamicData:ref({data:dynamic}),vatglassesCombiningInProgress:ref(false),
 vgData:{position:async p=>positions[p]??[],country:async c=>vg[c]},simaware:async p=>groups[p]??[],vatspyBoundary:async b=>vatspy.features[b]??[]};
 const store={initStatus:{vatglasses:'ready'},bookingOverride:false,bookings:[],config:{hideBookings:true}};
 const env={...shared,polygon,useStore:()=>store,useDataStore:()=>ds,useFacilitiesIds:()=>facilities,
  getKeyedValueFromSettings:k=>k==='map.vatglasses.active'||k==='map.visibility.artccTracons',
  useStorageLocal:(_k,v)=>ref(v),useLocalStorage:(_k,v)=>ref(v),isVatGlassesActive:ref(true),
  stringToArray:v=>typeof v==='string'?[v]:v,getCurrentThemeRgbColor:()=>[0,130,90],rgbToHex:()=> '#00825a',
  logBench:()=>()=>{},setInterval:()=>0,checkForVATSpy:async()=>{},getSimAwareFeatures:async(d,p)=>d.simaware(p),
  selectMapBookings:()=>[],filterVatsimControllers:c=>({controllers:c})};
 Object.assign(env,moduleOf(runwayCode),moduleOf(duplicatesCode));
 Object.assign(env,moduleOf(utilsCode,env));
 const atc=moduleOf(atcCode,env), glass=moduleOf(vgCode,env);
 const context={airports:{},sectors:{},atcAdded:null,airportsAdded:new Set()};
 // Supply airport records for the upstream runway-dependent sector logic.
 for(const a of vatspy.airports)context.airports[a.icao]={icao:a.icao,airport:a,atc:[],atis:{},aircraft:{},aircraftCount:0};
 await glass.updateVATGlasses(context);
 await atc.updateControllers(context);
 const hits=new Map(),mapped=new Set(),zones=[];
 const add=(atcs,geometry,min=0,max=999,isApp=false)=>{
  atcs=atcs.filter(c=>isApp?c.facility===5:c.facility!==5);if(!atcs.length)return;
  geometry=closedGeometry(geometry);
  atcs.forEach(c=>mapped.add(c.duplicatedBy??c.callsign));
  if(flightLevel<min||flightLevel>max)return;
  if(captureZones&&atcs.length)zones.push({geometry,entryNm:isApp?0:15,controllers:atcs.map(c=>c.duplicatedBy??c.callsign)});
  if(!pointInPolygon([longitude,latitude],{type:'Feature',properties:{},geometry}))return;
  for(const c of atcs){const callsign=c.duplicatedBy??c.callsign;hits.set(`${c.cid}:${callsign}`,{cid:c.cid,callsign,frequency:c.frequency,logon_time:c.logon_time});}
 };
 for(const country of Object.values(ds.vatglassesActivePositions.value))for(const p of Object.values(country))for(const s of p.sectors??[])add(p.atc,s.geometry,s.properties.min,s.properties.max);
 for(const sector of Object.values(context.sectors))add(sector.atc,sector.feature.geometry);
 for(const a of Object.values(context.airports))for(const c of a.atc){
  if(c.facility!==5&&c.facility!==4)continue;
  const candidates=[];
  for(const f of groups[c.callsign.split('_')[0]]??[]){const suffix=shared.getTraconSuffix(f);if(c.facility!==5&&!suffix||suffix&&!c.callsign.endsWith(suffix))continue;
   const match=shared.getLongestTraconPrefix(c.callsign,shared.getTraconPrefixes(f));if(match)candidates.push({feature:f,length:match.length});}
  const longest=Math.max(1,...candidates.map(x=>x.length));
  for(const candidate of candidates.filter(x=>x.length===longest))add([c],candidate.feature.geometry);
 }
 // APP/DEP uses the requested 65 NM airport circle, never visual_range.
 for(const c of controllers.filter(c=>c.facility===5)){
  const matched=Object.values(context.airports).find(a=>a.atc.some(x=>x.callsign===c.callsign)&&a.airport&&Number.isFinite(a.airport.lat)&&Number.isFinite(a.airport.lon));
  const airport=matched?.airport??vatspy.airports.find(a=>[a.icao,a.iata].includes(c.callsign.split('_')[0]));
  if(!airport)continue;
  const lat=airport.lat*Math.PI/180,lon=airport.lon*Math.PI/180,d=65/3440.065,ring=[];
  for(let i=0;i<360;i++){const bearing=i*Math.PI/180;const y=Math.asin(Math.sin(lat)*Math.cos(d)+Math.cos(lat)*Math.sin(d)*Math.cos(bearing));const x=lon+Math.atan2(Math.sin(bearing)*Math.sin(d)*Math.cos(lat),Math.cos(d)-Math.sin(lat)*Math.sin(y));ring.push([((x*180/Math.PI+540)%360)-180,y*180/Math.PI]);}ring.push([...ring[0]]);
  add([c],{type:'Polygon',coordinates:[ring]},0,999,true);
 }
 const unmapped=controllers.filter(c=>[1,5,6].includes(c.facility)&&!mapped.has(c.callsign)&&+c.frequency>=117&&+c.frequency<=137).map(c=>c.callsign);
 return {controllers:[...hits.values()],unmapped,clearConfirmed:hits.size===0&&unmapped.length===0,radarCommit:RADAR_COMMIT,...(captureZones?{zones}: {})};
}
