using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace FlightWatch {
 public sealed class XPlaneSamples {
  public static readonly string[] Names={"sim/flightmodel/position/latitude","sim/flightmodel/position/longitude","sim/flightmodel/position/elevation","sim/flightmodel/position/vh_ind","sim/flightmodel/position/groundspeed","sim/flightmodel/failures/onground_any","sim/time/paused","sim/time/sim_speed"};
  readonly float[] values=new float[Names.Length];readonly double[] times=Enumerable.Repeat(double.NegativeInfinity,Names.Length).ToArray();
  public static byte[] Subscription(int index,int frequency){var bytes=new byte[413];Encoding.ASCII.GetBytes("RREF\0").CopyTo(bytes,0);BitConverter.GetBytes(frequency).CopyTo(bytes,5);BitConverter.GetBytes(index).CopyTo(bytes,9);Encoding.ASCII.GetBytes(Names[index]).CopyTo(bytes,13);return bytes;}
  public Telemetry Accept(byte[] packet,double now){
   if(packet.Length<13||(packet.Length-5)%8!=0||Encoding.ASCII.GetString(packet,0,4)!="RREF")return null;
   for(int at=5;at<packet.Length;at+=8){int index=BitConverter.ToInt32(packet,at);if(index<0||index>=Names.Length)continue;float v=BitConverter.ToSingle(packet,at+4);values[index]=v;times[index]=float.IsNaN(v)||float.IsInfinity(v)?double.NegativeInfinity:now;}
   if(times.Any(t=>now-t>2||now<t))return null;
   if((values[5]!=0&&values[5]!=1)||(values[6]!=0&&values[6]!=1))return null;
   var sample=new Telemetry{Latitude=values[0],Longitude=values[1],Altitude=values[2]*3.280839895,VerticalSpeed=values[3]*196.8503937,GroundSpeed=values[4]*1.943844492,OnGround=values[5]!=0,Paused=values[6]!=0,Rate=values[7],Received=times.Min(),Aircraft="X-Plane"};
   return sample.Fresh(now)?sample:null;
  }
 }
 public sealed class XPlaneTelemetry:ITelemetrySource {
  readonly Func<double> now;readonly Control owner;readonly int receivePort,replyPort;UdpClient socket;int generation;double last,nextSubscribe;bool enabled,reported;
  public Action<Telemetry> Received {get;set;}public Action<string> Error {get;set;}
  public XPlaneTelemetry(Func<double> time,Control control,int requestPort=49000,int responsePort=49001){now=time;owner=control;receivePort=requestPort;replyPort=responsePort;}
  void Post(int token,Action action){try{if(owner.IsHandleCreated&&!owner.IsDisposed)owner.BeginInvoke(new Action(()=>{if(enabled&&token==generation)action();}));}catch(InvalidOperationException){}}
  public void Open(){Dispose();enabled=true;last=now();nextSubscribe=0;reported=false;try{socket=new UdpClient(new IPEndPoint(IPAddress.Loopback,0));int token=generation;var local=socket;_ = Task.Run(()=>Read(local,token));Tick();}catch(Exception ex){Error?.Invoke("X-Plane UDP: "+ex.Message);}}
  async Task Read(UdpClient local,int token){var samples=new XPlaneSamples();while(enabled&&token==generation){try{var result=await local.ReceiveAsync();if(!result.RemoteEndPoint.Address.Equals(IPAddress.Loopback)||(result.RemoteEndPoint.Port!=receivePort&&result.RemoteEndPoint.Port!=replyPort))continue;var t=samples.Accept(result.Buffer,now());if(t!=null)Post(token,()=>{last=now();reported=false;Received?.Invoke(t);});}catch(ObjectDisposedException){return;}catch(SocketException){await Task.Delay(250);}catch{await Task.Delay(250);}}}
  public void Tick(){if(!enabled)return;if(socket==null){Open();return;}if(now()>=nextSubscribe){nextSubscribe=now()+10;for(int i=0;i<XPlaneSamples.Names.Length;i++)try{var b=XPlaneSamples.Subscription(i,2);socket.Send(b,b.Length,new IPEndPoint(IPAddress.Loopback,receivePort));}catch(SocketException){}}
   if(now()-last>5&&!reported){reported=true;Error?.Invoke("Aguardando X-Plane local em UDP 49000. Verifique a rede do simulador; tentando novamente automaticamente.");}}
  public void Dispose(){enabled=false;generation++;var old=socket;socket=null;if(old==null)return;for(int i=0;i<XPlaneSamples.Names.Length;i++)try{var b=XPlaneSamples.Subscription(i,0);old.Send(b,b.Length,new IPEndPoint(IPAddress.Loopback,receivePort));}catch{}old.Close();}
 }
}
