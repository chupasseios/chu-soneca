using System;
using System.IO;
using System.Text.Json;
using FlightWatch;
static class BoundaryTests {
 public sealed class Frame {public double time {get;set;}public Coverage coverage {get;set;}}
 static int count;static void Check(bool b,string name){if(!b)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
 public static void Run(){
  var path=Path.Combine(AppContext.BaseDirectory,"boundary-fixtures.json");var frames=JsonSerializer.Deserialize<Frame[]>(File.ReadAllText(path));
  var m=new Monitor{SelectedCid="7654321"};m.Connect("7654321","TEST123","A320",1000);m.Start();m.Telemetry=new Telemetry{GroundSpeed=450,Altitude=35000};
  foreach(var f in frames){m.Telemetry.Received=f.time;f.coverage.Received=f.coverage.Sampled=f.time;m.Coverage=f.coverage;var effect=m.Tick(f.time,true);
   if(f.time==1000)Check(effect==Effect.None,"outside 15 NM does not alert");
   if(f.time==1060)Check(effect==Effect.Alert&&m.Deadline==1180,"approaching 15 NM starts full 120-second response window");
   if(f.time==1120)Check(effect==Effect.None&&m.Deadline==1180,"near-boundary poll does not extend deadline");
   if(f.time==1181){Check(effect==Effect.Disconnect&&m.CanCommand(false,f.time,true),"fresh near-boundary geometry permits timed disconnect before entry");m.Telemetry.OnGround=true;Check(!m.CanCommand(false,f.time,true),"ground still blocks final broker gate");m.Telemetry.OnGround=false;m.Disconnected(f.time+1);}
   if(f.time==1241)Check(effect==Effect.None&&m.OwnedDisconnect,"inside area remains disconnected");
   if(f.time==1301)Check(effect==Effect.None&&!m.CanCommand(true,f.time,true),"outside but below exit margin cannot reconnect");
   if(f.time==1361)Check(effect==Effect.None,"first margin-clear sample is insufficient");
   if(f.time==1421){Check(effect==Effect.Reconnect&&m.CanCommand(true,f.time,true),"second margin-clear sample requests reconnect");m.Coverage.boundary=null;Check(!m.CanCommand(true,f.time,true),"missing boundary data blocks broker gate");}
  }
  Console.WriteLine(count+" plugin boundary integration assertions passed");
 }
}
