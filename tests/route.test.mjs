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
