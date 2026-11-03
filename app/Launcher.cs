using System;
using System.Threading;
using System.Windows.Forms;
namespace FlightWatch {
 public static class Launcher {
  [STAThread]public static void Main(string[] args){try{using(var signal=EventWaitHandle.OpenExisting(args.Length==1&&args[0]=="--xpilot"?ManualLaunch.XpilotEventName:ManualLaunch.EventName)){signal.Set();}}catch(WaitHandleCannotBeOpenedException){MessageBox.Show("Abra o cliente selecionado (vPilot ou xPilot) com o plugin Chu Soneca instalado e tente novamente.","Chu Soneca");}catch(Exception e){MessageBox.Show(e.Message,"Chu Soneca");}}
 }
}
