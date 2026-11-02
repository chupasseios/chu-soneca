using System;
using System.Linq;
using FlightWatch;
using M=FlightWatch.Monitor;
static class NewFeaturesTests {
 static int count;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
 static M Ready(){var m=new M{SelectedCid="1234567"};m.Connect("1234567","TEST123","A320",1000);m.ApplyFlightPlan(new Plan{cid=m.SessionCid,callsign=m.Callsign,altitude="35000"});m.Start();m.Telemetry=new Telemetry{Altitude=35000,GroundSpeed=400,Received=1000};m.Tick(1000,true);m.Telemetry.Received=1121;m.Tick(1121,true);return m;}
 static Effect At(M m,double time){m.Telemetry.Received=time;return m.Tick(time,true);}
 static M Descending(){var m=Ready();m.Telemetry.Altitude=34000;m.Telemetry.VerticalSpeed=-800;for(int i=1200;i<1260;i++)CheckSilent(At(m,i)==Effect.None);Check(At(m,1260)==Effect.DescentAlert,"descent requires continuous 60s after cruise");return m;}
 static void CheckSilent(bool ok){if(!ok)throw new Exception("Early descent alarm");}
 public static void Run(){
  var m=Descending();Check(m.Deadline==1560,"descent deadline is exactly 300 seconds");m.InvalidateCoverage();Check(m.Deadline==1560,"feed failure does not erase descent warning");Check(At(m,1559)==Effect.None,"no early descent disconnect");Check(At(m,1560)==Effect.Disconnect,"descent timeout requests disconnect");Check(m.CanCommand(false,1560,true),"broker gate permits airborne descent disconnect");m.Telemetry.OnGround=true;Check(!m.CanCommand(false,1560,true)&&!m.CanCommand(true,1560,true),"last-moment ground blocks both broker commands");m.Telemetry.OnGround=false;m.Disconnected(1561);Check(!m.Armed&&!m.OwnedDisconnect&&!m.AwaitingReconnect,"descent disconnect cannot auto reconnect");Check(At(m,1700)==Effect.None,"no automatic action after descent disconnect");
  m=Descending();m.Acknowledge(1300);Check(At(m,1561)==Effect.None&&m.DescentAcknowledged,"descent acknowledgment cancels timeout");
  m=Descending();m.Acknowledge(1560);Check(At(m,1560)==Effect.Disconnect,"late descent acknowledgment rejected");
  m=Descending();m.Telemetry.OnGround=true;Check(At(m,1560)==Effect.None&&m.Deadline<0,"landing cancels descent alert before deadline action");
  m=Descending();m.SelectedCid="9999999";Check(At(m,1560)==Effect.None&&!m.Armed,"CID change cancels descent timeout");
  m=Descending();Check(!m.CanCommand(false,1560,false),"account mismatch blocks descent broker gate");
  m=Descending();m.Telemetry.Received=1250;Check(m.Tick(1560,true)==Effect.None&&m.Deadline<0,"stale telemetry cancels descent timeout");
  m=Descending();m.Telemetry.Paused=true;Check(At(m,1560)==Effect.None&&m.Deadline<0,"pause cancels descent warning");
  m=Descending();m.Stop();Check(At(m,1560)==Effect.None,"manual stop cancels descent");
  m=Ready();m.Telemetry.Altitude=34000;m.Telemetry.VerticalSpeed=-800;At(m,1200);Check(At(m,1260)==Effect.None,"telemetry gap does not count as sustained descent");
  m=Ready();m.Telemetry.Altitude=34000;m.Telemetry.VerticalSpeed=-800;for(int i=1200;i<1230;i++)At(m,i);m.Telemetry.VerticalSpeed=0;At(m,1230);m.Telemetry.VerticalSpeed=-800;for(int i=1231;i<1291;i++)CheckSilent(At(m,i)==Effect.None);Check(At(m,1291)==Effect.DescentAlert,"level-off resets descent qualification");
  m=Ready();m.Start();m.Telemetry.Altitude=34000;m.Telemetry.VerticalSpeed=-800;for(int i=1200;i<1300;i++)CheckSilent(At(m,i)==Effect.None);Check(!m.DescentTriggered,"descent without prior confirmed cruise ignored");
  var h=new ControllerHistory();var c=new Controller{cid="42",callsign="TEST_CTR",frequency="125.000",logon_time="2026-10-21T23:40:00Z"};var t=new DateTime(2026,10,21,23,59,0,DateTimeKind.Utc);var sample=new Coverage{ok=true,controllers=new[]{c},onlineControllers=new[]{c}};h.Observe(sample,t,"TEST123");Check(h.Entries.Count==1&&h.Entries[0].OnlineSinceUtc=="2026-10-21 23:40:00Z","history retains UTC login from feed");sample.controllers=new Controller[0];h.Observe(sample,t.AddMinutes(1),"TEST123");Check(h.Entries[0].State=="Online fora da cobertura"&&h.Entries[0].OfflineDetectedUtc==null,"leaving coverage is not an offline event");sample.ok=false;h.Observe(sample,t.AddMinutes(2),"TEST123");Check(h.Entries[0].OfflineDetectedUtc==null,"failed feed cannot declare offline");sample.ok=true;sample.onlineControllers=new Controller[0];h.Observe(sample,t.AddMinutes(3),"TEST123");Check(h.Entries[0].LastOnlineUtc=="2026-10-22 00:00:00Z"&&h.Entries[0].OfflineDetectedUtc=="2026-10-22 00:02:00Z","UTC rollover and offline uncertainty preserved");sample.controllers=new[]{c};sample.onlineControllers=new[]{c};h.Observe(sample,t.AddMinutes(4),"TEST123");h.End(t.AddMinutes(5));Check(h.Entries.Last().OfflineDetectedUtc==null&&h.Entries.Last().TrackingEndedUtc!=null,"stopping observation never fabricates logout");
  Console.WriteLine(count+" new feature assertions passed");
 }
}
