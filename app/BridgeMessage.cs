using System;
namespace FlightWatch {
 public sealed class BridgeMessage {
  public string Kind {get;set;} public string Cid {get;set;} public string Callsign {get;set;} public string Aircraft {get;set;} public string Selcal {get;set;} public string Session {get;set;} public string From {get;set;} public string Id {get;set;} public string Error {get;set;}
  public bool Connected {get;set;}public bool Observer {get;set;}public bool Ok {get;set;}
  public long Expires {get;set;}public long SampleTime {get;set;}public Telemetry Telemetry {get;set;}
 }
 public static class BridgeGuard {
  public static bool Allows(BridgeMessage command,BridgeMessage state,bool connected,string callsign,long now){
   if(command==null||state==null||string.IsNullOrEmpty(state.Cid)||string.IsNullOrEmpty(state.Session)||command.Cid!=state.Cid||command.Session!=state.Session||state.Observer||command.Expires<now||command.Expires-now>3000||command.SampleTime>now||now-command.SampleTime>3000)return false;
   var t=command.Telemetry;if(t==null||!t.Fresh(t.Received)||t.OnGround||t.Paused||Math.Abs(t.Rate-1)>.01||t.GroundSpeed<0)return false;
   return command.Kind=="disconnect"?connected&&state.Connected&&callsign==state.Callsign:command.Kind=="reconnect"&&!connected&&!state.Connected;
  }
 }
}
