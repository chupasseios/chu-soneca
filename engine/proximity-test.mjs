// Shared spatial state machine. It emits decisions only, never broker commands.
const R=3440.065,rad=Math.PI/180;
const dot=(a,b)=>a.reduce((s,x,i)=>s+x*b[i],0);
const cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
const norm=a=>Math.sqrt(dot(a,a));
const unit=a=>a.map(x=>x/norm(a));
const vector=([lon,lat])=>[Math.cos(lat*rad)*Math.cos(lon*rad),Math.cos(lat*rad)*Math.sin(lon*rad),Math.sin(lat*rad)];
const angle=(a,b)=>Math.acos(Math.max(-1,Math.min(1,dot(a,b))));
function segment(p,a,b){let best=Math.min(angle(p,a),angle(p,b));const n=cross(a,b);if(norm(n)<1e-12)return best*R;const u=unit(n),projection=p.map((x,i)=>x-dot(p,u)*u[i]);if(norm(projection)<1e-12)return best*R;const q=unit(projection);for(const v of [q,q.map(x=>-x)])if(angle(a,v)+angle(v,b)<=angle(a,b)+1e-8)best=Math.min(best,angle(p,v));return best*R;}
export const legDistanceNm=(position,a,b)=>segment(vector(position),vector(a),vector(b));
export function boundaryNm(position,geometry){const polygons=geometry.type==='Polygon'?[geometry.coordinates]:geometry.type==='MultiPolygon'?geometry.coordinates:[];if(!polygons.length)throw Error('Geometria não suportada');let nearest=Infinity;const p=vector(position);for(const poly of polygons)for(const ring of poly)for(let i=1;i<ring.length;i++)nearest=Math.min(nearest,segment(p,vector(ring[i-1]),vector(ring[i])));if(!Number.isFinite(nearest))throw Error('Polígono sem limite válido');return nearest;}
export function simulate({previous,pilot,timestamp,result,exitNm=15}){
 if(![10,15].includes(exitNm))throw Error('Margem de saída deve ser 10 ou 15 NM');
 const key=`${pilot.cid}:${pilot.callsign}:${pilot.logon_time}`;
 let state=previous?.key===key?{...previous}:{key,disconnected:false,lastTimestamp:0,lastDistance:null,signature:'',entered:false};
 const inside=result.controllers.length>0;
 const distances=result.zones.map(z=>({distance:boundaryNm([pilot.longitude,pilot.latitude],z.geometry),controllers:z.controllers,entryNm:z.entryNm??15})).sort((a,b)=>a.distance-b.distance);
 const distance=distances[0]?.distance??null;
 const signature=[...new Set(result.zones.flatMap(z=>z.controllers))].sort().join('|');
 const events=[];let reason='Aguardando aproximação ao limite';
 if(timestamp<=state.lastTimestamp)return {state,distance,inside,events,reason:'Aguardando nova amostra do feed',nearest:distances[0]?.controllers??[]};
 const comparable=state.signature===signature&&state.lastDistance!==null&&distance!==null;
 const approaching=comparable&&distance<state.lastDistance-0.05;
 const leaving=comparable&&distance>state.lastDistance+0.05;
 if(!state.disconnected&&(inside||(distance!==null&&distance<=(distances[0]?.entryNm??15)&&approaching))){state.disconnected=true;state.entered=inside;events.push(inside?'DESCONEXÃO SIMULADA — voo dentro de área controlada':'DESCONEXÃO SIMULADA — aproximação a até 15 NM do limite');}
 else if(state.disconnected){
  if(inside){state.entered=true;reason='Dentro de área controlada';}
  else if(!result.unmapped.length&&(distance===null||distance>=exitNm&&(state.entered||leaving))){state.disconnected=false;state.entered=false;events.push(distance===null?'RECONEXÃO SIMULADA — não há áreas controladas online mapeadas':`RECONEXÃO SIMULADA — fora da área a pelo menos ${exitNm} NM`);}
  else reason=result.unmapped.length?'Saída inconclusiva: controlador sem geometria':'Aguardando afastamento da área';
 }
 if(events.length)reason=events[events.length-1];
 state.lastTimestamp=timestamp;state.lastDistance=distance;state.signature=signature;
 return {state,distance,inside,events,reason,entryNm:distances[0]?.entryNm??15,nearest:distances[0]?.controllers??[]};
}
export function pluginBoundary(args,onlineControllers){
 const decision=simulate(args);
 const names=new Set(decision.nearest);
 const controllers=decision.inside?args.result.controllers:decision.state.disconnected&&decision.distance!==null&&decision.distance<=(decision.entryNm??15)?onlineControllers.filter(c=>names.has(c.callsign)):[];
 return {ready:true,state:decision.state,distance:decision.distance,inside:decision.inside,controllers,
  exitClear:!decision.state.disconnected&&!decision.inside&&!args.result.unmapped.length&&(decision.distance===null||decision.distance>=args.exitNm),exitNm:args.exitNm};
}
