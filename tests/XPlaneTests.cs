using System;
using System.Text;
using FlightWatch;
static class XPlaneTests {
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
 static byte[] Packet(params float[] values){var p=new byte[5+8*values.Length];Encoding.ASCII.GetBytes("RREF,").CopyTo(p,0);for(int i=0;i<values.Length;i++){BitConverter.GetBytes(i).CopyTo(p,5+i*8);BitConverter.GetBytes(values[i]).CopyTo(p,9+i*8);}return p;}
 public static void Run(){
  var input=new XPlaneSamples();var packet=Packet(-23,-47,10668,-5,200,0,0,1);var t=input.Accept(packet,100);
  Check(t!=null&&Math.Abs(t.Altitude-35000)<.1&&Math.Abs(t.VerticalSpeed+984.25197)<.1&&Math.Abs(t.GroundSpeed-388.7689)<.1,"X-Plane units converted to feet, feet/min and knots");
  Check(t.Fresh(101)&&!t.OnGround&&!t.Paused&&t.Rate==1,"complete fresh airborne X-Plane sample");
  Check(new XPlaneSamples().Accept(Packet(1),100)==null,"partial first packet cannot authorize flight actions");
  Check(input.Accept(Packet(1),103)==null,"missing datarefs expire even while other packets arrive");
  Check(input.Accept(new byte[8],104)==null,"malformed UDP packet rejected");
  Check(input.Accept(Packet(float.NaN,-47,10668,-5,200,0,0,1),105)==null,"nonfinite X-Plane position rejected");
  Check(input.Accept(Packet(-23,-47,0,0,0,1,0,1),106).OnGround,"ground dataref preserved");
  Check(input.Accept(Packet(-23,-47,100,0,0,0,1,2),107).Paused,"pause dataref preserved");
  var sub=XPlaneSamples.Subscription(0,2);Check(sub.Length==413&&BitConverter.ToInt32(sub,5)==2&&Encoding.ASCII.GetString(sub,13,XPlaneSamples.Names[0].Length)==XPlaneSamples.Names[0],"read-only RREF subscription format");
  Check(BitConverter.ToInt32(XPlaneSamples.Subscription(0,0),5)==0,"shutdown unsubscribes RREF stream");
  using(var host=new System.Windows.Forms.Form())using(var requests=new System.Net.Sockets.UdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback,0)))using(var responses=new System.Net.Sockets.UdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback,0))){
   var handle=host.Handle;var clock=System.Diagnostics.Stopwatch.StartNew();Telemetry received=null;
   int port=((System.Net.IPEndPoint)requests.Client.LocalEndPoint).Port,reply=((System.Net.IPEndPoint)responses.Client.LocalEndPoint).Port;
   using(var client=new XPlaneTelemetry(()=>clock.Elapsed.TotalSeconds,host,port,reply)){
    client.Received=value=>received=value;client.Open();requests.Client.ReceiveTimeout=3000;System.Net.IPEndPoint remote=null;
    for(int i=0;i<XPlaneSamples.Names.Length;i++)requests.Receive(ref remote);
    var bytes=Packet(-23,-47,10668,0,200,0,0,1);responses.Send(bytes,bytes.Length,remote);
    while(received==null&&clock.Elapsed.TotalSeconds<5){System.Windows.Forms.Application.DoEvents();System.Threading.Thread.Sleep(10);}
    Check(received!=null&&Math.Abs(received.Altitude-35000)<.1,"UDP loopback reads replies from separate X-Plane send port");
    client.Dispose();var unsubscribe=requests.Receive(ref remote);Check(BitConverter.ToInt32(unsubscribe,5)==0,"live UDP transport unsubscribes on close");
   }
  }
  var old=Preferences.IsXpilot;try{Preferences.IsXpilot=true;using(var w=new FlightWindow(true,true)){Check(w.ClientName=="xPilot","xPilot window identifies correct host");w.NetworkConnected("7654321","TEST123","A320","",false,"xpilot-session");Check(w.SelectedCid=="7654321","xPilot CID comes only from host event");w.Shutdown();}}finally{Preferences.IsXpilot=old;}
 }
}
