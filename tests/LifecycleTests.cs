using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using FlightWatch;
class LifecycleTests {
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS "+text);}
 static void Pump(Func<bool> condition,int ms){var c=Stopwatch.StartNew();while(!condition()&&c.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(20);}Check(condition(),"bounded lifecycle wait");}
 static bool Exited(int pid){try{using(var p=Process.GetProcessById(pid))return p.HasExited;}catch(ArgumentException){return true;}}
 [STAThread]static int Main(){string temp=Path.Combine(Path.GetTempPath(),"ChuLifecycle-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);string marker=Path.Combine(temp,"starts.txt");Environment.SetEnvironmentVariable("CHU_TEST_MARKER",marker);
  try{Application.EnableVisualStyles();var json=new JavaScriptSerializer();var old=json.Deserialize<Preferences>("{\"Cid\":\"7654321\",\"CruiseFL\":350}");Check(old.OpenWithVpilot&&!old.CloseToTray,"legacy preferences migrate to automatic opening and full close");var saved=json.Deserialize<Preferences>(json.Serialize(new Preferences{OpenWithVpilot=false,CloseToTray=true}));Check(!saved.OpenWithVpilot&&saved.CloseToTray,"startup and close options round trip");
   bool created;using(var signal=new EventWaitHandle(false,EventResetMode.AutoReset,ManualLaunch.EventName,out created)){if(!created)throw new Exception("Manual launch test requires no active Chu Soneca signal");string launcher=Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,@"..\..\..\..\app\bin\Release\net48\Chu-Soneca.exe"));using(var process=Process.Start(new ProcessStartInfo(launcher){UseShellExecute=false,CreateNoWindow=true})){Check(signal.WaitOne(5000),"manual launcher signals the plugin opening event");Check(process.WaitForExit(5000),"manual launcher exits after opening request");}}
   using(var form=new Form()){var handle=form.Handle;var clock=Stopwatch.StartNew();int errors=0,received=0;using(var client=new TelemetryClient(()=>clock.Elapsed.TotalSeconds*4,form,Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"FakeReader.exe"))){client.Error=s=>errors++;client.Received=t=>received++;client.Open();Pump(()=>{client.Tick();return received>=3;},10000);Check(errors==1&&File.ReadAllLines(marker).Length==2,"reader retries after exit and receives fresh data");client.Dispose();Pump(()=>File.ReadAllLines(marker).Select(int.Parse).All(Exited),5000);int runs=File.ReadAllLines(marker).Length;for(int i=0;i<30;i++){client.Tick();Application.DoEvents();Thread.Sleep(20);}Check(File.ReadAllLines(marker).Length==runs,"dispose prevents reader restart");}}
   File.Copy(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"FakeReader.exe"),Path.Combine(temp,"node.exe"));Environment.SetEnvironmentVariable("CHU_TEST_BLOCK","1");
   using(var window=new FlightWindow(true)){typeof(FlightWindow).GetField("assets",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,temp);var method=typeof(FlightWindow).GetMethod("RunEngine",BindingFlags.Instance|BindingFlags.NonPublic);var task=Task.Run(()=>{try{method.Invoke(window,new object[]{"{}"});}catch(TargetInvocationException){}});Pump(()=>File.ReadAllLines(marker).Length==3,5000);window.Shutdown();Pump(()=>task.IsCompleted&&File.ReadAllLines(marker).Select(int.Parse).All(Exited),5000);Check(task.IsCompleted,"full close terminates owned query process");}
   Console.WriteLine("PASS lifecycle integration: no vPilot or simulator commands");return 0;
  }catch(Exception e){Console.Error.WriteLine(e);return 1;}finally{Environment.SetEnvironmentVariable("CHU_TEST_MARKER",null);Environment.SetEnvironmentVariable("CHU_TEST_BLOCK",null);if(File.Exists(marker))foreach(var line in File.ReadAllLines(marker)){int pid;if(int.TryParse(line,out pid)&&!Exited(pid))try{Process.GetProcessById(pid).Kill();}catch{}}Directory.Delete(temp,true);}
 }
}
