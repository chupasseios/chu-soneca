using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.FlightSimulator.SimConnect;

namespace FlightWatch {
 public sealed class SimReader:IDisposable {
  const int MessageId=0x0492;
  enum Def { Flight } enum Req { Flight } enum Evt { Pause, Loaded }
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Ansi,Pack=1)]
  struct Data {public double Lat,Lon,Altitude,Vertical,Ground,Speed,Rate,Slew;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)]public string Title;}
  SimConnect sim;readonly Func<double> now;readonly IntPtr window;bool paused,closePending,faulted,dispatching;
  public Action<Telemetry> Received; public Action SessionChanged;public Action<string> Error;
  public SimReader(Func<double> time,IntPtr handle){now=time;window=handle;}
  public void Open(){if(sim!=null||faulted)return;try {
   sim=new SimConnect("FlightWatch",window,MessageId,null,0);
   sim.OnRecvOpen+=(s,d)=>{
    string[] names={"PLANE LATITUDE","PLANE LONGITUDE","PRESSURE ALTITUDE","VERTICAL SPEED","SIM ON GROUND","GROUND VELOCITY","SIMULATION RATE","IS SLEW ACTIVE","TITLE"};
    string[] units={"degrees","degrees","feet","feet per minute","bool","knots","number","bool",null};
    for(int i=0;i<names.Length;i++)s.AddToDataDefinition(Def.Flight,names[i],units[i],i==8?SIMCONNECT_DATATYPE.STRING256:SIMCONNECT_DATATYPE.FLOAT64,0,SimConnect.SIMCONNECT_UNUSED);
    s.RegisterDataDefineStruct<Data>(Def.Flight);
    s.RequestDataOnSimObject(Req.Flight,Def.Flight,SimConnect.SIMCONNECT_OBJECT_ID_USER,SIMCONNECT_PERIOD.SECOND,SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT,0,0,0);
    s.SubscribeToSystemEvent(Evt.Pause,"Pause_EX1");s.SubscribeToSystemEvent(Evt.Loaded,"FlightLoaded");
   };
   sim.OnRecvSimobjectData+=(s,d)=>{var f=(Data)d.dwData[0];Received?.Invoke(new Telemetry{Latitude=f.Lat,Longitude=f.Lon,Altitude=f.Altitude,VerticalSpeed=f.Vertical,OnGround=f.Ground!=0,GroundSpeed=f.Speed,Rate=f.Rate,Aircraft=f.Title??"",Paused=paused||f.Slew!=0,Received=now()});};
   sim.OnRecvEvent+=(s,d)=>{if(d.uEventID==(uint)Evt.Pause)paused=d.dwData!=0;};
   sim.OnRecvEventFilename+=(s,d)=>SessionChanged?.Invoke();
   sim.OnRecvQuit+=(s,d)=>{faulted=true;closePending=true;SessionChanged?.Invoke();};
   sim.OnRecvException+=(s,d)=>Fail("SimConnect: "+d.dwException+"; pacote "+d.dwSendID+"; parâmetro "+d.dwIndex);
  }catch(Exception e){Fail("SimConnect indisponível: "+e.Message);CloseConnection();}}
  void Fail(string message){faulted=true;closePending=true;Error?.Invoke(message);}
  public void Dispatch(int message){if(message!=MessageId||dispatching)return;dispatching=true;try{if(!faulted)sim?.ReceiveMessage();}catch(Exception e){Fail(e.ToString());}finally{dispatching=false;if(closePending)CloseConnection();}}
  void CloseConnection(){if(dispatching){closePending=true;return;}var old=sim;sim=null;closePending=false;try{old?.Dispose();}catch(Exception e){faulted=true;Error?.Invoke("Falha ao liberar leitor SimConnect: "+e.Message);}}
  public void Dispose(){CloseConnection();}
 }
}
