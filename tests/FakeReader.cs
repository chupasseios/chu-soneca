using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
class FakeReader {
 static void Main(){string marker=Environment.GetEnvironmentVariable("CHU_TEST_MARKER");if(string.IsNullOrEmpty(marker))return;bool first=!File.Exists(marker);File.AppendAllText(marker,Process.GetCurrentProcess().Id+Environment.NewLine);if(first){Console.Error.WriteLine("Injected reader failure");return;}
  bool stop=false;new Thread(()=>{Console.In.ReadLine();stop=true;}){IsBackground=true}.Start();
  if(Environment.GetEnvironmentVariable("CHU_TEST_BLOCK")=="1"){Thread.Sleep(30000);return;}
  while(!stop){Console.WriteLine("{\"Altitude\":5000,\"GroundSpeed\":130,\"Rate\":1}");Console.Out.Flush();Thread.Sleep(100);}
 }
}
