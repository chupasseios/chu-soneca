using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
namespace FlightWatch {
 public static class XpilotCompanion {
  [STAThread]public static void Main(){
   if(!Console.IsInputRedirected||!Console.IsOutputRedirected)return;
   Preferences.IsXpilot=true;Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   using(var window=new FlightWindow(false,true)){
    var json=new JavaScriptSerializer();var pending=new Dictionary<string,Action<bool,string>>();var watch=Stopwatch.StartNew();double heartbeat=-100;string session="";bool connected=false;
    window.ExternalSessionValid=()=>watch.Elapsed.TotalSeconds-heartbeat<4&&session==window.HostSessionId;
    Action<bool,Action<bool,string>> command=(reconnect,done)=>{
     if(!window.CanRequest(reconnect)||!window.ExternalSessionValid()){done(false,"Sessão xPilot indisponível ou ação bloqueada.");return;}
     string id=Guid.NewGuid().ToString("N");pending[id]=done;var t=window.CurrentTelemetry;
     try{Console.WriteLine(json.Serialize(new BridgeMessage{Kind=reconnect?"reconnect":"disconnect",Id=id,Cid=window.SelectedCid,Session=window.HostSessionId,Expires=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()+2000,SampleTime=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()-(long)((window.Now-t.Received)*1000),Telemetry=t}));Console.Out.Flush();}catch{pending.Remove(id);done(false,"Canal xPilot encerrado.");}
    };
    window.DisconnectRequested=done=>command(false,done);window.ReconnectRequested=done=>command(true,done);
    Action<BridgeMessage> receive=m=>{
     if(m.Kind=="restore"){window.Restore();return;}
     if(m.Kind=="state"){
      heartbeat=watch.Elapsed.TotalSeconds;
      if(m.Connected&&(!connected||session!=m.Session)){session=m.Session;connected=true;window.NetworkConnected(m.Cid,m.Callsign,m.Aircraft,m.Selcal,m.Observer,m.Session);}
      else if(!m.Connected&&connected){connected=false;window.NetworkDisconnected();}
     }else if(m.Kind=="private"&&connected&&m.Session==session){_ = window.CheckSupervisorMessage(m.From);}
     else if(m.Kind=="result"&&m.Id!=null&&pending.TryGetValue(m.Id,out var callback)){pending.Remove(m.Id);callback(m.Ok,m.Error);}
    };
    window.Shown+=(s,e)=>{_ = Task.Run(async()=>{try{string line;while((line=await Console.In.ReadLineAsync())!=null){if(line.Length>65536)break;var message=new JavaScriptSerializer().Deserialize<BridgeMessage>(line);window.BeginInvoke(new Action(()=>{if(!window.IsDisposed&&message!=null)receive(message);}));}}catch{}finally{try{window.BeginInvoke(new Action(()=>window.Shutdown()));}catch{}}});};
    using(var watchdog=new Timer{Interval=1000}){watchdog.Tick+=(s,e)=>{if(watch.Elapsed.TotalSeconds-heartbeat>6&&watch.Elapsed.TotalSeconds>10)window.Shutdown();};watchdog.Start();Application.Run(window);}
   }
  }
 }
}
