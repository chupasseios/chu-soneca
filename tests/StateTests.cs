using System;
using FlightWatch;
using M=FlightWatch.Monitor;
class Test {
 static int count;
 static Controller atc=new Controller{cid="1",callsign="TEST_CTR",frequency="125.000",logon_time="first"};
 static void Assert(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);count++;}
 static M Setup(){var m=new M{SelectedCid="7654321"};m.Connect("7654321","TEST123","A320",1000);m.ApplyFlightPlan(new Plan{cid=m.SessionCid,callsign=m.Callsign,altitude="35000"});m.Start();m.Telemetry=new Telemetry{Altitude=35000,GroundSpeed=450,Received=1000};m.Tick(1000,true);Fresh(m,1121,true);return m;}
 static void Fresh(M m,double now,bool covered){m.Telemetry.Received=now;m.Coverage=new Coverage{ok=true,Received=now,Sampled=now,clearConfirmed=!covered,controllers=covered?new[]{atc}:new Controller[0]};m.Coverage.boundary=new Boundary{ready=true,exitNm=15,exitClear=m.Coverage.clearConfirmed,controllers=m.Coverage.controllers};}
 static void Main(){
  var m=Setup();Assert(m.Tick(1121,true)==Effect.Alert,"alert after stable cruise");Assert(m.Deadline==1241,"120 second deadline");
  Fresh(m,1181,true);m.Tick(1181,true);Assert(m.Deadline==1241,"poll does not extend deadline");Fresh(m,1240,true);Assert(m.Tick(1240,true)==Effect.None,"not early");
  m.Telemetry.Received=1241;Assert(m.Tick(1241,true)==Effect.None,"requires post-deadline spatial check");Fresh(m,1242,true);Assert(m.Tick(1242,true)==Effect.Disconnect,"timeout disconnect");
  m.Disconnected(1243);Assert(m.OwnedDisconnect,"confirmed disconnect provenance");Fresh(m,1300,true);Assert(m.Tick(1300,true)==Effect.None,"still covered stays offline");Fresh(m,1360,false);Assert(m.Tick(1360,true)==Effect.None,"first clear sample insufficient");Fresh(m,1420,false);Assert(m.Tick(1420,true)==Effect.Reconnect,"two clear samples reconnect");
  m.Connect("7654321","TEST123","A320",1421);Assert(m.Armed&&m.Connected&&!m.OwnedDisconnect,"restore bound session");
  m=Setup();m.Tick(1121,true);m.Acknowledge(1200);Fresh(m,1250,true);Assert(m.Tick(1250,true)==Effect.None&&m.Deadline<0,"ack suppresses same controller");
  m=Setup();m.Tick(1121,true);m.Acknowledge(1241);Fresh(m,1242,true);Assert(m.Tick(1242,true)==Effect.Disconnect,"late ack rejected");
  m=Setup();m.SelectedCid="111111";Assert(m.Tick(1121,true)==Effect.None&&!m.Armed,"CID mismatch blocks");
  m=Setup();Assert(m.Tick(1121,false)==Effect.None&&!m.Armed,"account change blocks");
  m=Setup();m.Tick(1121,true);m.Connect("7654321","NEW123","B738",1150);Assert(!m.Armed&&m.Deadline<0,"new flight cancels timer");
  m=Setup();m.Tick(1121,true);Fresh(m,1181,false);m.Tick(1181,true);Assert(m.Deadline<0,"controller offline cancels timer");
  m=Setup();m.Tick(1121,true);m.Telemetry.VerticalSpeed=-800;m.Tick(1122,true);Assert(m.Deadline==1241&&!m.InCruise,"ATC deadline continues outside cruise");
  m=Setup();m.Disconnected(1121);Assert(!m.OwnedDisconnect&&!m.Armed,"manual disconnect cannot reconnect");
  m=Setup();m.Tick(1121,true);m.Telemetry.Received=1200;m.Tick(1200,true);Assert(m.Deadline<0,"stale coverage cancels deadline");
  m=Setup();m.Telemetry.Received=1000;Assert(m.Tick(1121,true)==Effect.None&&!m.InCruise,"stale telemetry blocks");
  m=Setup();m.Telemetry.OnGround=true;Assert(m.Tick(1121,true)==Effect.None&&!m.InCruise,"ground blocks");
  m=Setup();m.Telemetry.Paused=true;Assert(m.Tick(1121,true)==Effect.None&&!m.InCruise,"paused blocks");
  m=Setup();m.Telemetry.Rate=4;Assert(m.Tick(1121,true)==Effect.None&&!m.InCruise,"accelerated sim blocks");
  m=Setup();m.Tick(1121,true);Fresh(m,1242,true);m.Tick(1242,true);m.Disconnected(1243);m.Telemetry.Altitude=34000;m.Telemetry.Received=1244;m.Tick(1244,true);Assert(m.Armed&&m.OwnedDisconnect,"leaving cruise preserves owned return");
  m=Setup();m.Tick(1121,true);Fresh(m,1242,true);m.Tick(1242,true);m.Telemetry.Received=1260;m.Tick(1260,true);Assert(!m.Armed,"unconfirmed disconnect stops");
  m=Setup();m.Telemetry.GroundSpeed=double.PositiveInfinity;Assert(m.Tick(1121,true)==Effect.None&&!m.InCruise,"nonfinite telemetry blocks");
  m=Setup();m.Tick(1121,true);m.Telemetry.OnGround=true;m.Telemetry.Received=1242;Fresh(m,1242,true);Assert(m.Tick(1242,true)==Effect.None&&m.Deadline<0,"ground cancels expired alert before disconnect");
  m=Setup();m.Tick(1121,true);Fresh(m,1242,true);m.Tick(1242,true);m.Disconnected(1243);Fresh(m,1300,false);m.Tick(1300,true);Fresh(m,1360,false);m.Telemetry.OnGround=true;Assert(m.Tick(1360,true)==Effect.None&&!m.OwnedDisconnect,"ground blocks scheduled reconnection");
  m=Setup();m.DisableAutoReconnect=true;Assert(m.Tick(1121,true)==Effect.Alert,"manual return preference preserves ATC alert");Fresh(m,1242,true);Assert(m.Tick(1242,true)==Effect.Disconnect&&m.CanCommand(false,1242,true),"manual return preference preserves disconnect");m.Disconnected(1243);
  Fresh(m,1300,false);Assert(m.Tick(1300,true)==Effect.None,"manual return blocks first clear sample");Fresh(m,1360,false);Assert(m.Tick(1360,true)==Effect.None&&!m.AwaitingReconnect&&m.Armed,"manual return remains offline after two clear samples");
  m.AwaitingReconnect=true;Assert(!m.CanCommand(true,1360,true),"manual return blocks queued broker command");m.AwaitingReconnect=false;
  m.DisableAutoReconnect=false;Fresh(m,1420,false);Assert(m.Tick(1420,true)==Effect.None,"enabling return requires new clear confirmation");Fresh(m,1480,false);Assert(m.Tick(1480,true)==Effect.Reconnect&&m.CanCommand(true,1480,true),"enabled return works after fresh clear samples");
  m.DisableAutoReconnect=true;Assert(!m.AwaitingReconnect&&!m.CanCommand(true,1480,true),"changing preference cancels pending reconnect");
  m=Setup();m.Tick(1121,true);Fresh(m,1242,true);m.Tick(1242,true);m.Disconnected(1243);Fresh(m,1300,false);m.Coverage.boundary.exitNm=10;Assert(m.Tick(1300,true)==Effect.None&&m.Coverage==null,"10 NM boundary rejected by fixed margin");
  Console.WriteLine(count+" assertions passed");NewFeaturesTests.Run();SupervisorStateTests.Run();RecoveryTests.Run();PlanTests.Run();BoundaryTests.Run();
 }
}
