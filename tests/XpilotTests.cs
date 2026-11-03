using System;
using System.Reflection;
using FlightWatch;
using Vatsim.Xpilot.PluginSdk;
using Vatsim.Xpilot.PluginSdk.Events;
using Plugin=ChuSoneca.Xpilot.Plugin;
public class MockBroker:DispatchProxy {
 public bool Connected=true;public string Callsign="TEST123";public int Disconnects,Reconnects;public Action OnDisconnect;
 protected override object Invoke(MethodInfo method,object[] args){switch(method.Name){case "get_IsConnected":return Connected;case "get_Callsign":return Callsign;case "RequestDisconnect":Disconnects++;Connected=false;OnDisconnect?.Invoke();return null;case "RequestConnect":Reconnects++;Connected=true;return null;default:return null;}}
}
static class XpilotTests {
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
 static void Set(object o,string field,object value)=>o.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,value);
 static void Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,args);
 static BridgeMessage Request(string kind="disconnect")=>new BridgeMessage{Kind=kind,Id=Guid.NewGuid().ToString("N"),Cid="7654321",Session="session1",Expires=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()+2000,SampleTime=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),Telemetry=new Telemetry{Received=100,Altitude=35000,Latitude=-23,Longitude=-47,GroundSpeed=450}};
 static void Main(){
  var broker=DispatchProxy.Create<IBroker,MockBroker>();var mock=(MockBroker)(object)broker;var plugin=new Plugin();Set(plugin,"broker",broker);
  Call(plugin,"Connected",null,new NetworkConnectedEventArgs("7654321","TEST123","A320","",false));
  var state=new BridgeMessage{Kind="state",Connected=true,Cid="7654321",Callsign="TEST123",Aircraft="A320",Session="session1"};Set(plugin,"state",state);
  mock.OnDisconnect=()=>Call(plugin,"Disconnected",null,EventArgs.Empty);
  var request=Request();request.Cid="999999";Call(plugin,"Command",request);Check(mock.Disconnects==0,"xPilot rejects another CID");
  request=Request();request.Session="old";Call(plugin,"Command",request);Check(mock.Disconnects==0,"xPilot rejects stale session");
  request=Request();request.Telemetry.OnGround=true;Call(plugin,"Command",request);Check(mock.Disconnects==0,"xPilot blocks disconnect on ground");
  request=Request();request.Telemetry.Paused=true;Call(plugin,"Command",request);Check(mock.Disconnects==0,"xPilot blocks paused simulator");
  request=Request();request.Telemetry.Rate=2;Call(plugin,"Command",request);Check(mock.Disconnects==0,"xPilot blocks accelerated simulator");
  request=Request();request.Expires=0;Call(plugin,"Command",request);Check(mock.Disconnects==0,"xPilot rejects expired command");
  request=Request();request.SampleTime-=6000;Call(plugin,"Command",request);Check(mock.Disconnects==0,"xPilot rejects stale telemetry");
  request=Request();mock.Callsign="OTHER";Call(plugin,"Command",request);Check(mock.Disconnects==0,"xPilot rechecks broker callsign at dispatch");mock.Callsign="TEST123";
  request=Request();Call(plugin,"Command",request);Check(mock.Disconnects==1&&!mock.Connected,"valid command invokes xPilot RequestDisconnect exactly once");Call(plugin,"Command",request);Check(mock.Disconnects==1,"duplicate command ignored");
  request=Request("reconnect");request.Telemetry.OnGround=true;Call(plugin,"Command",request);Check(mock.Reconnects==0,"xPilot blocks reconnect on ground");
  request=Request("reconnect");Call(plugin,"Command",request);Check(mock.Reconnects==1,"owned disconnect can invoke RequestConnect");
  Call(plugin,"Disconnected",null,EventArgs.Empty);mock.Connected=false;Call(plugin,"Command",Request("reconnect"));Check(mock.Reconnects==1,"manual disconnect cannot trigger reconnect");
  Console.WriteLine("xPilot SDK adapter checks passed (mock broker; no live client commands).");
 }
}
