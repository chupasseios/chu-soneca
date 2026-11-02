import fs from 'node:fs';
import {simulate,pluginBoundary} from '../engine/proximity-test.mjs';
import assert from 'node:assert/strict';
const geometry={type:'Polygon',coordinates:[[[0,0],[1,0],[1,1],[0,1],[0,0]]]};
const c={cid:'42',callsign:'TEST_CTR',frequency:'125.000',logon_time:'session'};
let state=null;const samples=[];
for(const [time,longitude,inside] of [[1000,-.4,false],[1060,-.24,false],[1120,-.2,false],[1181,-.1,false],[1241,.5,true],[1301,1.2,false],[1361,1.3,false],[1421,1.4,false]]){
 const args={previous:state,pilot:{cid:'7654321',callsign:'TEST123',logon_time:'test',latitude:.5,longitude},timestamp:time,result:{controllers:inside?[c]:[],unmapped:[],zones:[{geometry,controllers:[c.callsign]}]},exitNm:15};
 const test=simulate(args);const boundary=pluginBoundary(args,[c]);assert.deepEqual(boundary.state,test.state);state=boundary.state;
 samples.push({time,coverage:{ok:true,clearConfirmed:!inside,controllers:inside?[c]:[],boundary}});
}
fs.writeFileSync(new URL('./boundary-fixtures.json',import.meta.url),JSON.stringify(samples));
console.log('PASS private test and plugin share identical spatial state across approach, entry and departure');
