using System;
using FlightWatch;
using M=FlightWatch.Monitor;
static class SupervisorStateTests {
 static int n;static void Check(bool b,string s){if(!b)throw new Exception(s);n++;Console.WriteLine("PASS "+s);}
 static M Ready(){var m=new M{SelectedCid="1234567"};m.Connect("1234567","TEST123","A320",1000);m.Start();m.Telemetry=new Telemetry{Altitude=15000,VerticalSpeed=-700,GroundSpeed=300,Received=1000};return m;}
 public static void Run(){var m=Ready();Check(m.RequestSupervisorDisconnect(1000,true,true)==Effect.Disconnect,"confirmed supervisor can request disconnect airborne outside cruise");Check(m.CanCommand(false,1000,true)&&!m.CanCommand(true,1000,true),"supervisor broker gate only permits disconnect");Check(m.RequestSupervisorDisconnect(1000,true,true)==Effect.None,"duplicate supervisor does not issue second command");m.Tick(1001,true);Check(m.SupervisorDisconnect,"normal tick preserves supervisor reason");m.Disconnected(1001);Check(!m.Armed&&!m.OwnedDisconnect&&!m.AwaitingReconnect,"supervisor disconnect has no automatic reconnect");Check(m.Tick(1100,true)==Effect.None,"no followup automatic effect");
  m=Ready();m.Telemetry.OnGround=true;Check(!m.CanVerifySupervisor(1000,true)&&m.RequestSupervisorDisconnect(1000,true,true)==Effect.None,"ground blocks lookup and request");
  m=Ready();m.RequestSupervisorDisconnect(1000,true,true);m.Telemetry.OnGround=true;Check(!m.CanCommand(false,1000,true),"landing before broker execution blocks supervisor action");m.Tick(1000,true);Check(!m.AwaitingDisconnect&&!m.SupervisorDisconnect&&!m.OwnedDisconnect,"ground cancels pending supervisor action");
  m=Ready();Check(m.RequestSupervisorDisconnect(1000,false,true)==Effect.None,"unconfirmed supervisor cannot act");
  m=Ready();Check(m.RequestSupervisorDisconnect(1006,true,true)==Effect.None,"stale telemetry blocks supervisor request");
  m=Ready();m.SelectedCid="1111111";Check(m.RequestSupervisorDisconnect(1000,true,true)==Effect.None,"CID mismatch blocks supervisor request");
  m=Ready();Check(m.RequestSupervisorDisconnect(1000,true,false)==Effect.None,"account change blocks supervisor request");
  m=Ready();m.Stop();Check(m.RequestSupervisorDisconnect(1000,true,true)==Effect.None,"stopped monitor ignores supervisor automation");
  m=Ready();m.RequestSupervisorDisconnect(1000,true,true);m.Telemetry.Received=1016;m.Tick(1016,true);Check(!m.Armed&&!m.OwnedDisconnect,"unconfirmed broker response stops without reconnection");
  m=Ready();m.RequestSupervisorDisconnect(1000,true,true);m.Connect("1234567","NEW123","A320",1001);Check(!m.SupervisorDisconnect&&!m.CanCommand(false,1001,true),"new flight invalidates pending supervisor action");
  m=Ready();m.RequestSupervisorDisconnect(1000,true,true,1);m.Telemetry.Received=1002;Check(!m.CanCommand(false,1002,true),"expired supervisor proof blocks delayed broker execution");
  Console.WriteLine(n+" supervisor state assertions passed");
 }
}
