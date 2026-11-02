using System;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace FlightWatch {
 public sealed class SupervisorProof {
  public bool ok {get;set;} public bool confirmed {get;set;}
  public string callsign {get;set;} public string cid {get;set;} public int rating {get;set;} public long timestamp {get;set;} public string error {get;set;}
  public bool Valid(string sender,long utcNow)=>ok&&confirmed&&rating==11&&!string.IsNullOrWhiteSpace(cid)&&string.Equals(callsign,sender,StringComparison.OrdinalIgnoreCase)&&utcNow-timestamp<=60000&&timestamp-utcNow<=5000;
 }
 public sealed partial class FlightWindow {
  readonly HashSet<string> supervisorChecks=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  public async Task CheckSupervisorMessage(string sender){
   sender=(sender??"").Trim().ToUpperInvariant();
   if(demo||closing||!monitor.CanVerifySupervisor(Now,AccountUnchanged()))return;
   if(!System.Text.RegularExpressions.Regex.IsMatch(sender,"^[A-Z0-9_-]{1,32}$")||supervisorChecks.Count>=4||!supervisorChecks.Add(sender))return;
   int requestEpoch=epoch;string expectedSession=HostSessionId;double started=Now;long messageTime=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
   try{
    Log("Mensagem privada de "+sender+": verificando rating no feed VATSIM.");
    string request=json.Serialize(new{mode="supervisor",sender,messageTimeUtc=messageTime});
    string result=await Task.Run(()=>RunEngine(request));
    if(closing||IsDisposed||requestEpoch!=epoch||expectedSession!=HostSessionId||Now-started>30)return;
    var proof=json.Deserialize<SupervisorProof>(result);
    if(proof==null||!proof.Valid(sender,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())){Log("Remetente "+sender+" não confirmado como Supervisor em dados atuais; nenhuma desconexão por mensagem.");return;}
    double lifetime=(60000-(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()-proof.timestamp))/1000.0;
    var effect=monitor.RequestSupervisorDisconnect(Now,true,AccountUnchanged(),lifetime);
    if(effect==Effect.None){Log("Supervisor confirmado, mas solo/telemetria/identidade/estado bloqueou a ação.");return;}
    Log("Supervisor confirmado no feed: "+sender+" · CID "+proof.cid+" · rating 11. Retorno automático desativado.");
    DispatchEffect(effect);testAlarmUntil=Now+10;Notify("Mensagem de Supervisor confirmado","Desconexão solicitada. Reconexão automática desativada.");
    Render();
   }catch(Exception e){if(!closing&&!IsDisposed)Log("Verificação de Supervisor indisponível: "+e.Message+". Nenhuma ação adicional.");}
   finally{supervisorChecks.Remove(sender);}
  }
 }
}
