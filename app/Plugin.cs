using System;
using System.Threading;
using System.Windows.Forms;
using System.Security.AccessControl;
using System.Security.Principal;
using RossCarlson.Vatsim.Vpilot.Plugins;
namespace FlightWatch {
 public sealed class Plugin:IPlugin {
  public string Name=>"Chu Soneca";
  public void Initialize(IBroker broker){
   var hostContext=SynchronizationContext.Current;FlightWindow window=null;
   var gate=new object();string cid=null,call=null,type=null,selcal=null,hostSession="";bool observer=false,connected=false,opening=false,ended=false;
   Action<Action<FlightWindow>> onWindow=a=>{FlightWindow w;lock(gate)w=window;if(w!=null&&!w.IsDisposed&&w.IsHandleCreated)try{w.BeginInvoke(new Action(()=>{if(!w.IsDisposed)a(w);}));}catch(InvalidOperationException){}};
   Action openWindow=()=>{
    lock(gate){if(ended||opening)return;if(window!=null&&!window.IsDisposed){onWindow(w=>w.Restore());return;}opening=true;}
    var thread=new Thread(()=>{FlightWindow w=null;try{
     w=new FlightWindow(false);lock(gate){if(ended){w.Dispose();return;}window=w;}
     w.DisconnectRequested=done=>{
      if(hostContext==null){done(false,"Contexto do vPilot indisponivel.");return;}string expected=w.HostSessionId;
      hostContext.Post(_=>{try{lock(gate){if(ended||w.IsDisposed||!connected||cid!=w.SelectedCid||expected!=hostSession||!w.CanRequest(false)){done(false,"CID/sessao divergente ou monitoramento cancelado.");return;}broker.RequestDisconnect();}done(true,"");}catch(Exception ex){done(false,ex.Message);}},null);
     };
     w.ReconnectRequested=done=>{
      if(hostContext==null){done(false,"Contexto do vPilot indisponivel.");return;}string expected=w.HostSessionId;
      hostContext.Post(_=>{try{lock(gate){if(ended||w.IsDisposed||connected||observer||cid!=w.SelectedCid||expected!=hostSession||!w.CanRequest(true)){done(false,"Identidade mudou, sessao ja conectada ou automacao cancelada.");return;}broker.RequestConnect(call,type,selcal);}done(true,"");}catch(Exception ex){done(false,ex.Message);}},null);
     };
     w.Shown+=(s,e)=>{lock(gate){opening=false;if(ended){w.Shutdown();return;}if(connected)w.NetworkConnected(cid,call,type,selcal,observer,hostSession);}};
     Application.Run(w);
    }catch(Exception ex){broker.PostDebugMessage("Chu Soneca: "+ex.Message);}finally{w?.Dispose();lock(gate){if(window==w)window=null;opening=false;}}}){IsBackground=true,Name="Chu Soneca UI"};thread.SetApartmentState(ApartmentState.STA);thread.Start();
   };
   broker.NetworkConnected+=(s,e)=>{if(SynchronizationContext.Current!=null)hostContext=SynchronizationContext.Current;string token=Guid.NewGuid().ToString("N");lock(gate){cid=e.Cid;call=e.Callsign;type=e.TypeCode;selcal=e.SelcalCode;observer=e.ObserverMode;connected=true;hostSession=token;}onWindow(w=>w.NetworkConnected(e.Cid,e.Callsign,e.TypeCode,e.SelcalCode,e.ObserverMode,token));};
   broker.NetworkDisconnected+=(s,e)=>{lock(gate)connected=false;onWindow(w=>w.NetworkDisconnected());};
   broker.PrivateMessageReceived+=(s,e)=>{string sender=e.From;onWindow(w=>{var task=w.CheckSupervisorMessage(sender);});};
   EventWaitHandle signal=null;RegisteredWaitHandle wait=null;
   try{var security=new EventWaitHandleSecurity();security.SetAccessRuleProtection(true,false);security.AddAccessRule(new EventWaitHandleAccessRule(WindowsIdentity.GetCurrent().User,EventWaitHandleRights.FullControl,AccessControlType.Allow));bool created;signal=new EventWaitHandle(false,EventResetMode.AutoReset,ManualLaunch.EventName,out created,security);wait=ThreadPool.RegisterWaitForSingleObject(signal,(state,timedOut)=>openWindow(),null,Timeout.Infinite,false);}catch(Exception ex){broker.PostDebugMessage("Chu Soneca: abertura manual indisponivel: "+ex.Message);}
   broker.SessionEnded+=(s,e)=>{lock(gate)ended=true;wait?.Unregister(null);signal?.Dispose();onWindow(w=>w.Shutdown());};
   if(Preferences.Read().OpenWithVpilot)openWindow();
  }
 }
}
