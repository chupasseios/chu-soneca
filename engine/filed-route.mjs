import {importOfp, verifyRoute} from './route-filter.mjs';

function filedCoordinate(token) {
 // North Atlantic H-prefix half degrees and N-suffix whole degrees (FAA NAT guide).
 const half = /^H(\d{2})(\d{2})$/.exec(token);
 if (half && Number(half[1])<90) return [-Number(half[2]),Number(half[1])+0.5];
 const whole = /^(\d{2})(\d{2})N$/.exec(token);
 if (whole && Number(whole[1])<=90) return [-Number(whole[2]),Number(whole[1])];
 const match = /^(\d{2})(\d{2})?([NS])(\d{3})(\d{2})?([EW])$/.exec(token);
 if (!match || Number(match[2] || 0) >= 60 || Number(match[5] || 0) >= 60) return null;
 const lat = (Number(match[1]) + Number(match[2] || 0) / 60) * (match[3] === 'S' ? -1 : 1);
 const lon = (Number(match[4]) + Number(match[5] || 0) / 60) * (match[6] === 'W' ? -1 : 1);
 return Math.abs(lat) <= 90 && Math.abs(lon) <= 180 ? [lon, lat] : null;
}

function airportPoint(airports, code) {
 const airport = airports?.[code];
 return airport && Number.isFinite(airport.lon) && Number.isFinite(airport.lat) ? [airport.lon, airport.lat] : null;
}

// Direct legs follow a great circle, including on polar and transoceanic routes.
function traceDirectLegs(points) {
 const radians = Math.PI / 180;
 const vector = ([lon,lat]) => [Math.cos(lat*radians)*Math.cos(lon*radians),Math.cos(lat*radians)*Math.sin(lon*radians),Math.sin(lat*radians)];
 const traced = [points[0]];
 for (let i=1;i<points.length;i++){
  const a=vector(points[i-1]), b=vector(points[i]);
  const angle=Math.acos(Math.min(1,Math.max(-1,a.reduce((n,x,j)=>n+x*b[j],0))));
  if (Math.PI-angle<1e-7) throw Error('Trecho direto com trajetória ambígua.');
  const steps=Math.ceil(angle*3440.065/20);
  for(let j=1;j<steps;j++){
   const t=j/steps;
   const v=a.map((x,k)=>x*Math.sin((1-t)*angle)+b[k]*Math.sin(t*angle));
   traced.push([Math.atan2(v[1],v[0])/radians,Math.atan2(v[2],Math.hypot(v[0],v[1]))/radians]);
  }
  traced.push(points[i]);
 }
 return traced;
}

// Resolve only legs described by the filing. Never bridge an unknown procedure or airway.
export async function resolveFiled(plan, airports, lookup) {
 const departure = airportPoint(airports, plan.departure);
 const arrival = airportPoint(airports, plan.arrival);
 if (!departure || !arrival) throw Error('Aeroporto do plano sem coordenadas no VATSpy.');
 const tokens = plan.route.trim().toUpperCase().split(/\s+/).filter(t => !/^[NM]\d{4}[FA]\d{3}$/.test(t))
  .map(t => t.replace(/\/[NM]\d{4}[FA]\d{3}$/, ''));
 if (tokens[0]?.split('/')[0] === plan.departure) tokens.shift();
 if (tokens.at(-1)?.split('/')[0] === plan.arrival) tokens.pop();
 if (!tokens.length) throw Error('O plano não descreve o trajeto.');
 const legs = tokens.filter(t => t !== 'DCT');
 const named = new Set();
 for (const token of legs) {
  if (filedCoordinate(token) || (token.length===4 && airportPoint(airports, token))) continue;
  if (!/^(?:[A-Z]{5}|[A-Z]{2,3})$/.test(token)) throw Error('O trecho '+token+' precisa de um navlog detalhado.');
  named.add(token);
 }
 const rows = named.size ? await lookup([...named]) : [];
 const points = legs.map(token => {
  const point = filedCoordinate(token) || (token.length===4 && airportPoint(airports, token));
  if (point) return point;
  const matches = rows.filter(r => r.id === token && Number.isFinite(r.lon) && Number.isFinite(r.lat));
  const unique = new Map(matches.map(r => [[r.lon,r.lat].join(','), [r.lon,r.lat]]));
  if (unique.size !== 1) throw Error('Posição de '+token+' ausente ou ambígua.');
  return [...unique.values()][0];
 });
 return traceDirectLegs([departure, ...points, arrival]);
}

export async function chooseRoute({request, pilot, airports, lookup, fetchOfp}) {
 const cached = request.routeSnapshot;
 const sameSession = cached?.cid === String(request.cid) && cached?.callsign === request.callsign &&
  cached?.sessionId === request.sessionId && Array.isArray(cached.points) && cached.points.length >= 2;
 if (!pilot) {
  if (request.connected === false && request.ownedDisconnect === true && sameSession) return cached;
  throw Error('Aguardando o voo e o plano na VATSIM.');
 }
 if (String(pilot.cid) !== String(request.cid) || pilot.callsign !== request.callsign) throw Error('Identidade do voo VATSIM divergente.');
 const plan = pilot.flight_plan;
 if (!plan?.departure || !plan?.arrival || !plan.route?.trim()) throw Error('Aguardando rota no plano de voo da VATSIM.');
 const filed = {cid:String(pilot.cid), callsign:pilot.callsign, sessionId:request.sessionId,
  departure:plan.departure, arrival:plan.arrival, route:plan.route.trim().toUpperCase().replace(/\s+/g,' ')};
 if (sameSession && ['departure','arrival','route'].every(k => cached[k] === filed[k]) &&
     (cached.source === 'VATSIM' || cached.simbrief === request.simbrief && request.simbrief)) return cached;
 try {
  return {...filed, source:'VATSIM', points:await resolveFiled(filed, airports, lookup)};
 } catch (reason) {
  if (!request.simbrief) throw Error(reason.message+' Configure o SimBrief opcional em Preferências para completar a rota VATSIM.');
  try {
   const supplement = importOfp(await fetchOfp(request.simbrief));
   verifyRoute(supplement, pilot);
   return {...filed, source:'VATSIM + SimBrief', simbrief:request.simbrief, points:supplement.points};
  } catch (error) {
   throw Error(reason.message+' SimBrief: '+error.message);
  }
 }
}


export function navaidPositions(csv, requested) {
 // Identifiers and coordinates are single-line fields. Skip incomplete records.
 const columns = line => Array.from(line.matchAll(/(?:^|,)(?:"((?:[^"]|"")*)"|([^",]*))(?=,|$)/g),
  match => (match[1] ?? match[2]).replace(/""/g,'"'));
 const lines = csv.trim().split(/\r?\n/);
 const header = columns(lines.shift());
 const indices = ['ident','longitude_deg','latitude_deg'].map(key => header.indexOf(key));
 if (indices.includes(-1)) throw Error('Catálogo de auxílios sem coordenadas.');
 const result = [];
 for (const line of lines) {
  const fields = columns(line);
  if(fields.length !== header.length) continue;
  const [id,lon,lat] = indices.map(i => fields[i]);
  if(requested.has(id) && lon && lat && Number.isFinite(+lon) && Number.isFinite(+lat) && Math.abs(+lon)<=180 && Math.abs(+lat)<=90)
   result.push({id,lon:+lon,lat:+lat});
 }
 return result;
}
