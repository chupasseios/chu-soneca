using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
namespace FlightWatch {
 public sealed class TelemetryClient:IDisposable {
  readonly Func<double> now;readonly Control owner;readonly string path;Process child;int generation;
  readonly RecoverySchedule recovery=new RecoverySchedule();Task stopping=Task.CompletedTask;bool enabled,starting;double last,healthySince=-1;
  public Action<Telemetry> Received;public Action<string> Error;
  public TelemetryClient(Func<double> time,Control form,string executable){now=time;owner=form;path=executable;}
  void Post(int token,Action action){try{if(!owner.IsDisposed&&owner.IsHandleCreated)owner.BeginInvoke(new Action(()=>{if(enabled&&token==generation)action();}));}catch(InvalidOperationException){}}
  public void Open(){enabled=true;recovery.Reset();StopChild();Tick();}
  public void Tick(){if(!enabled||starting)return;if(child!=null){if(now()-last>20)Failed("Sem dados do leitor por 20 segundos.");return;}if(now()>=recovery.Next)StartChild();}
  async void StartChild(){starting=true;int token=generation;try{
   await stopping;if(!enabled||token!=generation)return;
   var p=new Process{StartInfo=new ProcessStartInfo(path){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,RedirectStandardInput=true}};
   try{p.Start();}catch{p.Dispose();throw;}child=p;last=now();healthySince=-1;
   _=Task.Run(async()=>{string message="Leitor encerrado.";var errors=p.StandardError.ReadToEndAsync();try{string line;var json=new JavaScriptSerializer();while((line=await p.StandardOutput.ReadLineAsync())!=null){var t=json.Deserialize<Telemetry>(line);if(t==null)throw new Exception("Amostra vazia.");Post(token,()=>{t.Received=now();last=now();if(healthySince<0)healthySince=last;if(last-healthySince>=60)recovery.Reset();Received?.Invoke(t);});}await errors;if(!string.IsNullOrWhiteSpace(errors.Result))message=errors.Result.Trim();}catch(Exception e){message=e.Message;}finally{Post(token,()=>Failed(message));}});
  }catch(Exception e){if(enabled&&token==generation)Failed("Leitor indisponivel: "+e.Message);}finally{starting=false;}}
  void Failed(string reason){StopChild();double delay=recovery.Fail(now());Error?.Invoke(reason+" Nova tentativa automatica em "+delay+" s.");}
  void StopChild(){generation++;var p=child;child=null;if(p==null)return;try{p.StandardInput.Close();}catch{}
   stopping=Task.Run(()=>{try{if(!p.WaitForExit(3000)){p.Kill();p.WaitForExit(3000);}}catch{}finally{p.Dispose();}});
  }
  public void Dispose(){enabled=false;StopChild();}
 }
}
