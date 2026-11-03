using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using FlightWatch;
using Vatsim.Xpilot.PluginSdk;
using Vatsim.Xpilot.PluginSdk.Events;
namespace ChuSoneca.Xpilot {
 public sealed class Plugin:IPlugin {
  public string Name=>"Chu Soneca "+typeof(Plugin).Assembly.GetName().Version.ToString(3);
  IBroker broker;SynchronizationContext context;BridgeMessage state=new BridgeMessage{Kind="state",Session=Guid.NewGuid().ToString("N")};
  Process child;Channel<string> output;CancellationTokenSource childStop;Timer heartbeat;EventWaitHandle open;RegisteredWaitHandle wait;bool ended,ownsDisconnect,awaitingDisconnect;long disconnectAt;
  readonly System.Collections.Generic.HashSet<string> commands=new System.Collections.Generic.HashSet<string>();
  readonly JsonSerializerOptions json=new JsonSerializerOptions{IncludeFields=true};
  void Post(Action action){context?.Post(_=>{if(!ended)try{action();}catch(Exception ex){broker.PostDebugMessage("Chu Soneca: "+ex.Message);}},null);}
  public void Initialize(IBroker host){
   if(!OperatingSystem.IsWindows())return;
   try{if(host.ApiVersion!=new Version(0,2,0,0)){host.PostDebugMessage("Chu Soneca requer SDK xPilot 0.2.0.0. Integração não iniciada.");return;}}catch{return;}
   context=SynchronizationContext.Current;broker=host;if(context==null){host.PostDebugMessage("Chu Soneca: contexto de interface indisponível.");return;}
   broker.NetworkConnected+=Connected;broker.NetworkDisconnected+=Disconnected;broker.PrivateMessageReceived+=PrivateMessage;broker.SessionEnded+=Ended;
   open=new EventWaitHandle(false,EventResetMode.AutoReset,ManualLaunch.XpilotEventName);wait=ThreadPool.RegisterWaitForSingleObject(open,(_,__)=>Post(Open),null,Timeout.Infinite,false);
   heartbeat=new Timer(_=>Post(()=>{if(child!=null){if(child.HasExited){StopChild();return;}Send(state);}}),null,1000,1000);
   bool auto=true;try{var path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FlightWatch","xPilot","preferences.json");if(File.Exists(path)){using var preferences=JsonDocument.Parse(File.ReadAllText(path));if(preferences.RootElement.TryGetProperty("OpenWithVpilot",out var value))auto=value.GetBoolean();}}catch{}
   if(auto)Post(Open);
  }
  void Connected(object sender,NetworkConnectedEventArgs e){ownsDisconnect=awaitingDisconnect=false;commands.Clear();state=new BridgeMessage{Kind="state",Connected=true,Cid=e.Cid,Callsign=e.Callsign,Aircraft=e.TypeCode,Selcal=e.SelcalCode,Observer=e.ObserverMode,Session=Guid.NewGuid().ToString("N")};Send(state);}
  void Disconnected(object sender,EventArgs e){ownsDisconnect=awaitingDisconnect&&DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()-disconnectAt<=15000;awaitingDisconnect=false;state.Connected=false;Send(state);}
  void PrivateMessage(object sender,PrivateMessageReceivedEventArgs e){Send(new BridgeMessage{Kind="private",From=e.From,Session=state.Session});}
  void Send(BridgeMessage message){if(output!=null&&!output.Writer.TryWrite(JsonSerializer.Serialize(message,json)))StopChild();}
  void Open(){
   if(child!=null&&!child.HasExited){Send(new BridgeMessage{Kind="restore"});return;}StopChild();
   var path=Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location),"ChuSoneca.XpilotUI.exe");
   var process=new Process{StartInfo=new ProcessStartInfo(path){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true}};
   process.Start();child=process;var stop=childStop=new CancellationTokenSource();var queue=output=Channel.CreateBounded<string>(64);var token=stop.Token;
   _=Task.Run(async()=>{try{await foreach(var line in queue.Reader.ReadAllAsync(token)){await process.StandardInput.WriteLineAsync(line.AsMemory(),token);await process.StandardInput.FlushAsync(token);}}catch{}});
   _=Task.Run(async()=>{try{await process.StandardError.ReadToEndAsync(token);}catch{}});
   _=Task.Run(async()=>{try{string line;while((line=await process.StandardOutput.ReadLineAsync(token))!=null){if(line.Length>65536)break;var message=JsonSerializer.Deserialize<BridgeMessage>(line,json);Post(()=>{if(child==process)Command(message);});}}catch{}finally{Post(()=>{if(child==process)StopChild();});}});
   Send(state);
  }
  void Command(BridgeMessage message){
   if(message==null||string.IsNullOrEmpty(message.Id)||commands.Count>=1024||!commands.Add(message.Id))return;
   bool ok=false;string error="CID, sessão, prazo ou telemetria inválidos.";
   try{if((message.Kind!="reconnect"||ownsDisconnect)&&BridgeGuard.Allows(message,state,broker.IsConnected,broker.Callsign,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())){
     if(message.Kind=="disconnect"){awaitingDisconnect=true;disconnectAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();broker.RequestDisconnect();}else {ownsDisconnect=false;broker.RequestConnect(state.Callsign,state.Aircraft,state.Selcal);}ok=true;error="";
   }}catch(Exception ex){awaitingDisconnect=false;error=ex.Message;}
   Send(new BridgeMessage{Kind="result",Id=message.Id,Ok=ok,Error=error});
  }
  void StopChild(){ownsDisconnect=awaitingDisconnect=false;var process=child;child=null;output?.Writer.TryComplete();output=null;childStop?.Cancel();childStop?.Dispose();childStop=null;if(process!=null)_=Task.Run(()=>{try{process.StandardInput.Close();if(!process.WaitForExit(5000))process.Kill();}catch{}finally{process.Dispose();}});}
  void Ended(object sender,EventArgs e){ended=true;broker.NetworkConnected-=Connected;broker.NetworkDisconnected-=Disconnected;broker.PrivateMessageReceived-=PrivateMessage;broker.SessionEnded-=Ended;heartbeat?.Dispose();wait?.Unregister(null);open?.Dispose();StopChild();}
 }
}
