using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ChuSoneca.UI;

namespace FlightWatch {
 public sealed partial class FlightWindow {
  readonly CancellationTokenSource updateCancellation=new CancellationTokenSource();
  readonly System.Windows.Forms.Timer updateTimer=new System.Windows.Forms.Timer{Interval=6*60*60*1000};
  readonly LinkLabel updateNotice=new LinkLabel{AutoSize=true,MaximumSize=new Size(1000,0),Margin=new Padding(8,6,8,10),Visible=false,LinkColor=NightTheme.Accent,ActiveLinkColor=NightTheme.Green,VisitedLinkColor=NightTheme.Accent};
  bool updateBusy;
  void InitializeUpdates(){
   updateNotice.LinkClicked+=(s,e)=>{try{Process.Start(new ProcessStartInfo(PluginUpdates.ReleasesUrl){UseShellExecute=true});}catch{Log("Não foi possível abrir o navegador. Acesse "+PluginUpdates.ReleasesUrl);}};
   Shown+=async(s,e)=>{if(demo)return;updateTimer.Start();await CheckForUpdate();};
   updateTimer.Tick+=async(s,e)=>await CheckForUpdate();
  }
  async Task CheckForUpdate(){
   if(closing||updateBusy)return;updateBusy=true;
   try{
    var version=await PluginUpdates.CheckAsync(typeof(FlightWindow).Assembly.GetName().Version,updateCancellation.Token);
    if(!closing&&!IsDisposed&&version!=null)ShowUpdate(version);
   }finally{updateBusy=false;}
  }
  void ShowUpdate(Version version){
   updateNotice.Text="Atualização do plugin disponível: v"+version.ToString(3)+".  Ver atualização e baixar instalador";
   updateNotice.Visible=true;
  }
  void StopUpdates(){updateTimer.Stop();updateTimer.Dispose();updateCancellation.Cancel();updateCancellation.Dispose();}
 }
}
