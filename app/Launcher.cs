using System;
using System.Threading;
using System.Windows.Forms;
namespace FlightWatch {
 public static class Launcher {
  [STAThread]public static void Main(){try{using(var signal=EventWaitHandle.OpenExisting(ManualLaunch.EventName)){signal.Set();}}catch(WaitHandleCannotBeOpenedException){MessageBox.Show("Abra o vPilot com o plugin Chu Soneca instalado e tente novamente.","Chu Soneca");}catch(Exception e){MessageBox.Show(e.Message,"Chu Soneca");}}
 }
}
