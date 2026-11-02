using System;
using FlightWatch;
static class RecoveryTests {
 static int count;static void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
 static Monitor New(){var m=new Monitor{SelectedCid="7654321"};m.Connect("7654321","TEST123","A320",1000);m.Start();m.Telemetry=new Telemetry{Received=1000,Altitude=5000,VerticalSpeed=1800,GroundSpeed=130};return m;}
 static void Feed(Monitor m,double time,bool clear=false){m.Telemetry.Received=time;m.Coverage=new Coverage{ok=true,Received=time,Sampled=time,clearConfirmed=clear,controllers=clear?new Controller[0]:new[]{new Controller{cid="1",callsign="TEST_APP",logon_time="start"}}};m.Coverage.boundary=new Boundary{ready=true,exitNm=15,exitClear=m.Coverage.clearConfirmed,controllers=m.Coverage.controllers};}
 public static void Run(){
  var m=New();Feed(m,1000);Check(m.Tick(1000,true)==Effect.Alert&&!m.InCruise,"ATC alert during initial climb without cruise wait");Feed(m,1120);Check(m.Tick(1120,true)==Effect.Disconnect&&m.CanCommand(false,1120,true),"ATC disconnect and final gate work outside cruise");m.Disconnected(1121);
  Check(m.Tick(1130,true)==Effect.None&&m.Armed&&m.OwnedDisconnect,"offline telemetry outage retains monitoring and ownership");Feed(m,1140,true);Check(m.Tick(1140,true)==Effect.None,"recovery requires first new clear sample");Feed(m,1200,true);Check(m.Tick(1200,true)==Effect.Reconnect&&m.CanCommand(true,1200,true),"second clear sample permits noncruise reconnect");
  m=New();Feed(m,1000);m.Tick(1000,true);m.Telemetry.Received=900;Check(m.Tick(1001,true)==Effect.None&&m.Armed&&m.Deadline<0,"telemetry outage suspends actions without disarming");Feed(m,1100);Check(m.Tick(1100,true)==Effect.Alert&&m.Deadline==1220,"fresh data restarts full response window");
  m=New();Feed(m,1000);m.Tick(1000,true);m.InvalidateCoverage();m.Telemetry.Received=1100;Check(m.Tick(1100,true)==Effect.None&&m.Armed,"feed outage does not cancel monitoring");Feed(m,1101);Check(m.Tick(1101,true)==Effect.Alert,"feed recovery resumes evaluation");
  m=New();m.Telemetry.OnGround=true;Feed(m,1000);Check(m.Tick(1000,true)==Effect.None&&m.Armed&&!m.CanCommand(false,1000,true),"ground remains blocked without disarming");m.Telemetry.OnGround=false;m.Telemetry.GroundSpeed=60;Feed(m,1001);Check(m.Tick(1001,true)==Effect.Alert,"airborne low speed no longer requires cruise speed");
  m.Stop();Feed(m,1002);Check(m.Tick(1002,true)==Effect.None&&!m.Armed,"manual stop survives data recovery");
  m=New();Feed(m,1000);m.Tick(1000,true);Feed(m,1120);m.Tick(1120,true);m.Disconnected(1121);m.Telemetry.OnGround=true;Feed(m,1122,true);m.Tick(1122,true);Check(m.Armed&&!m.OwnedDisconnect,"landing revokes reconnect provenance");
  var r=new RecoverySchedule();Check(r.Fail(10)==5&&r.Next==15,"retry begins after five seconds");Check(r.Fail(20)==10&&r.Fail(40)==20&&r.Fail(70)==40,"retry backoff avoids rapid restart loops");Check(r.Fail(120)==60&&r.Fail(190)==60,"retry delay capped at one minute");r.Reset();Check(r.Fail(300)==5,"healthy recovery resets retry schedule");
  Console.WriteLine(count+" recovery assertions passed");
 }
}
