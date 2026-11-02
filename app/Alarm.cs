using System;
using System.IO;
using System.Media;
namespace FlightWatch {
 public sealed class Alarm:IDisposable {
  readonly MemoryStream wave;readonly SoundPlayer player;
  public bool Playing {get;private set;}
  public Alarm(){wave=new MemoryStream();using(var w=new BinaryWriter(wave,System.Text.Encoding.ASCII,true)){
   const int rate=22050,count=rate*2;w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+count*2);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(rate);w.Write(rate*2);w.Write((short)2);w.Write((short)16);w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(count*2);
   for(int i=0;i<count;i++){double t=(double)i/rate;int pulse=(int)(t/.4);double local=t-pulse*.4,value=0;if(pulse<4&&local<.34){double phase=2*Math.PI*(pulse%2==0?880:1175)*local,envelope=Math.Min(1,Math.Min(local/.015,(.34-local)/.025));value=.28*envelope*(Math.Sin(phase)+.25*Math.Sin(phase*3));}w.Write((short)(value*32767));}
  }wave.Position=0;player=new SoundPlayer(wave);player.Load();}
  public void Start(){player.PlayLooping();Playing=true;}
  public void Stop(){player.Stop();Playing=false;}
  public void Dispose(){Stop();player.Dispose();wave.Dispose();}
 }
}
