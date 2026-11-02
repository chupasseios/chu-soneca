using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using System.Windows.Forms;
namespace FlightWatch {
 internal static class TelemetryHost {
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern bool SetDllDirectory(string path);
  [STAThread]static void Main(){
   string root=Directory.GetParent(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)).Parent.FullName;
   AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>{var name=new AssemblyName(e.Name).Name;if(name!="Microsoft.FlightSimulator.SimConnect")return null;return Assembly.LoadFrom(Path.Combine(root,name+".dll"));};
   SetDllDirectory(root);
   Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
   try{Run();}catch(Exception e){Console.Error.WriteLine(e.ToString());Environment.ExitCode=1;}
  }
  [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
  static void Run(){using(var form=new ReaderForm())Application.Run(form);}
 }
 internal sealed class ReaderForm:Form {
  SimReader reader;readonly Timer timer=new Timer{Interval=1000};readonly Stopwatch clock=Stopwatch.StartNew();double last;bool failed;
  readonly JavaScriptSerializer json=new JavaScriptSerializer();
  public ReaderForm(){ShowInTaskbar=false;Opacity=0;WindowState=FormWindowState.Minimized;}
  protected override void OnShown(EventArgs e){base.OnShown(e);Hide();reader=new SimReader(()=>clock.Elapsed.TotalSeconds,Handle);
   reader.Received=t=>{last=clock.Elapsed.TotalSeconds;Console.WriteLine(json.Serialize(t));Console.Out.Flush();};
   reader.Error=message=>{Console.Error.WriteLine(message);failed=true;};
   reader.SessionChanged=()=>{Console.Error.WriteLine("Sessão do simulador alterada. O leitor será recuperado automaticamente.");failed=true;};
   // EOF from parent exits gracefully, without touching the simulator or vPilot.
   System.Threading.Tasks.Task.Run(()=>{Console.In.ReadLine();try{BeginInvoke(new Action(Close));}catch(InvalidOperationException){}});
   timer.Tick+=(s,x)=>{if(failed||clock.Elapsed.TotalSeconds-last>15){if(!failed)Console.Error.WriteLine("Sem telemetria por 15 segundos. Leitura encerrada.");Close();}};
   reader.Open();timer.Start();
  }
  protected override void WndProc(ref Message m){reader?.Dispatch(m.Msg);base.WndProc(ref m);}
  protected override void Dispose(bool disposing){if(disposing){timer.Dispose();reader?.Dispose();}base.Dispose(disposing);}
 }
}
