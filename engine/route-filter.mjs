import pointInPolygon from '@turf/boolean-point-in-polygon';
import {boundaryNm,legDistanceNm} from './proximity-test.mjs';
const valid=p=>Array.isArray(p)&&p.length===2&&p.every(Number.isFinite)&&Math.abs(p[0])<=180&&Math.abs(p[1])<=90;
export function normalizedRoute(text){let words=String(text??'').toUpperCase().split(/\s+/).map(x=>x.split('/')[0]).filter(x=>x&&!/^[NM]\d{4}[FA]\d{3}$/.test(x));for(let i=0;i<words.length-2;i++)if(/^[A-Z]{1,2}\d+$/.test(words[i])&&words[i]===words[i+2]){words.splice(i+1,2);i--;}return words.join(' ');}
export function importOfp(ofp){
 const fixes=ofp.navlog?.fix; if(!Array.isArray(fixes)||fixes.length<2)throw Error('SimBrief sem navlog detalhado. Gere o plano com Detailed Navlog.');
 const points=[ofp.origin,...fixes,ofp.destination].map(x=>[Number(x.pos_long),Number(x.pos_lat)]);
 if(points.some(p=>!valid(p)))throw Error('SimBrief com coordenadas inválidas');
 return {departure:ofp.origin.icao_code,arrival:ofp.destination.icao_code,callsign:String(ofp.general.icao_airline??'')+String(ofp.general.flight_number??''),route:ofp.general.route,points,imported:Date.now()};
}
export function verifyRoute(route,pilot){
 if(!route||!Array.isArray(route.points)||route.points.length<2||route.points.some(p=>!valid(p)))throw Error('Importe a rota do SimBrief em Preferências.');
 if(route.callsign!==pilot.callsign||route.departure!==pilot.flight_plan?.departure||route.arrival!==pilot.flight_plan?.arrival)throw Error('Plano SimBrief não corresponde ao callsign/origem/destino do voo VATSIM.');
 const a=normalizedRoute(route.route),b=normalizedRoute(pilot.flight_plan.route);
 if(!b||!(a===b||/^[A-Z]{3,6}[0-9][A-Z]?$/.test(a.split(' ')[0])&&a.split(' ').slice(1).join(' ')===b))throw Error('Rota VATSIM diferente do SimBrief. Atualize e importe o plano novamente.');
}
const unwrap=(x,ref)=>{while(x-ref>180)x-=360;while(x-ref< -180)x+=360;return x;};
function cross(a,b,c){return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0]);}
function intersects(a,b,c,d){const eps=1e-9;const box=(p,q,r)=>r[0]>=Math.min(p[0],q[0])-eps&&r[0]<=Math.max(p[0],q[0])+eps&&r[1]>=Math.min(p[1],q[1])-eps&&r[1]<=Math.max(p[1],q[1])+eps;const v=[cross(a,b,c),cross(a,b,d),cross(c,d,a),cross(c,d,b)];return v[0]*v[1]<0&&v[2]*v[3]<0||Math.abs(v[0])<eps&&box(a,b,c)||Math.abs(v[1])<eps&&box(a,b,d)||Math.abs(v[2])<eps&&box(c,d,a)||Math.abs(v[3])<eps&&box(c,d,b);}
export function routeCrosses(points,geometry){
 const polys=geometry.type==='Polygon'?[geometry.coordinates]:geometry.coordinates;
 for(let i=1;i<points.length;i++){
  const a=points[i-1],b=[unwrap(points[i][0],a[0]),points[i][1]];
  for(const polygon of polys){const rings=polygon.map(r=>{let ref=a[0];return r.map(p=>{const x=unwrap(p[0],ref);ref=x;return [x,p[1]];});});const f={type:'Polygon',coordinates:rings};
   if(pointInPolygon(a,f)||pointInPolygon(b,f))return true;
   for(const ring of rings)for(let k=1;k<ring.length;k++)if(intersects(a,b,ring[k-1],ring[k]))return true;
  }
 }return false;
}
export function remainingRoute(points,position){let best=Infinity,index=1;for(let i=1;i<points.length;i++){const d=legDistanceNm(position,points[i-1],points[i]);if(d<best){best=d;index=i;}}if(best>30)throw Error('Aeronave a mais de 30 NM da rota importada. Reimporte a rota atual.');return [position,...points.slice(index)];}
export function filterRoute(result,route,position,previous,exitNm=15){
 const remaining=remainingRoute(route.points,position);
 const protectedNames=new Set(previous?.disconnected?String(previous.signature).split('|'):[]);
 const zones=result.zones.filter(z=>routeCrosses(remaining,z.geometry)||z.controllers.some(c=>protectedNames.has(c))&&boundaryNm(position,z.geometry)<exitNm);const names=new Set(zones.flatMap(z=>z.controllers));
 const controllers=result.controllers.filter(c=>names.has(c.callsign));
 return {...result,zones,controllers,clearConfirmed:!controllers.length&&!result.unmapped.length,routeStatus:`Rota validada: ${route.departure} → ${route.arrival}; ${zones.length} área(s) no trecho restante`};
}
