using System;
using System.IO;
using System.Windows.Forms;
namespace FlightWatch {
 public static class Program {
  [STAThread] public static void Main(string[] args){
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   try{using(var w=new FlightWindow(true)){
    if(args.Length==2&&args[0].StartsWith("--render",StringComparison.Ordinal)){w.StartPosition=FormStartPosition.Manual;w.Location=new System.Drawing.Point(-10000,-10000);w.Show();Application.DoEvents();w.RenderPreview(Path.GetFullPath(args[1]),args[0]);w.Shutdown();}
    else Application.Run(w);
   }}catch(Exception e){if(args.Length==2&&args[0].StartsWith("--render",StringComparison.Ordinal)){File.WriteAllText(args[1]+".error.txt",e.ToString());Environment.ExitCode=1;return;}MessageBox.Show(e.Message,"Chu Soneca — erro tratado");}
  }
 }
}
