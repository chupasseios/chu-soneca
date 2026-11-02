using System;
using System.Collections.Generic;
using System.Linq;

namespace FlightWatch {
 public sealed class Telemetry {
  public double Latitude, Longitude, Altitude, VerticalSpeed, GroundSpeed, Rate=1, Received;
  public bool OnGround, Paused;
  public string Aircraft="";
  static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
  public bool Fresh(double now) => now-Received>=0 && now-Received<=5 && Finite(Altitude) && Finite(VerticalSpeed) && Finite(GroundSpeed) && Finite(Rate) && Finite(Latitude) && Finite(Longitude) && Math.Abs(Latitude)<=90 && Math.Abs(Longitude)<=180;
 }
 public sealed class Controller {
  public string cid {get;set;} public string callsign {get;set;} public string frequency {get;set;} public string logon_time {get;set;}
  public string Key => cid+":"+callsign+":"+logon_time;
 }
 public sealed class Boundary {public bool ready {get;set;}public bool exitClear {get;set;}public bool inside {get;set;}public double? distance {get;set;}public int exitNm {get;set;}public object state {get;set;}public Controller[] controllers {get;set;}=new Controller[0];}
 public sealed class Coverage {
  public Boundary boundary {get;set;}
  public bool ok {get;set;} public bool clearConfirmed {get;set;} public string error {get;set;}
  public Controller[] controllers {get;set;}=new Controller[0]; public string[] unmapped {get;set;}=new string[0];
  public Controller[] onlineControllers {get;set;}
  public double Received, Sampled; public long timestamp {get;set;} public double dataAgeSeconds {get;set;}
  public string routeStatus {get;set;}
  public Plan flightPlan {get;set;}
 }
 public sealed class Plan {public string departure {get;set;} public string arrival {get;set;} public string altitude {get;set;} public string cid {get;set;} public string callsign {get;set;}}
 public enum Effect { None, Alert, Disconnect, Reconnect, DescentAlert }
 public sealed class Monitor {
  public object BoundaryState;
  public int ExitMarginNm=15;
  static Controller[] Relevant(Coverage c)=>c.boundary?.controllers??new Controller[0];
  bool BoundaryValid(Coverage c)=>c.boundary!=null&&c.boundary.ready&&c.boundary.exitNm==ExitMarginNm;
  public bool Armed, Connected, OwnedDisconnect, AwaitingDisconnect, AwaitingReconnect;
  public string SelectedCid="", SessionCid="", Callsign="", SessionId="", Aircraft="";
  public double CruiseFL {get;private set;}
  public string PlanStatus {get;private set;}="Aguardando plano da VATSIM";
  public void ApplyFlightPlan(Plan plan){
   double feet=0;bool valid=plan!=null&&SelectedCid==SessionCid&&plan.cid==SessionCid&&string.Equals(plan.callsign,Callsign,StringComparison.OrdinalIgnoreCase)&&TryPlanAltitude(plan.altitude,out feet);
   double next=valid?feet/100:0;
   if(next!=CruiseFL){if(DescentPending)Deadline=-1;if(DescentDisconnect)AwaitingDisconnect=false;CruiseFL=next;StableSince=-1;InCruise=CruiseSeen=DescentTriggered=DescentAcknowledged=DescentDisconnect=false;DescentSince=-1;}
   PlanStatus=valid?"FL"+CruiseFL.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+" · plano VATSIM":"Plano ausente ou altitude inválida";
  }
  public static bool TryPlanAltitude(string value,out double feet){feet=0;string raw=(value??"").Trim().ToUpperInvariant();bool fl=raw.StartsWith("FL")||raw.StartsWith("F");if(fl)raw=raw.Substring(raw.StartsWith("FL")?2:1);int n;if(!int.TryParse(raw,System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out n)||n<=0)return false;feet=fl||n<1000?n*100.0:n;return feet>=1000&&feet<=60000;}
  public Telemetry Telemetry;
  public Coverage Coverage;
  public string Status="Informe seu CID. O nível vem do plano VATSIM.";
  public double Deadline=-1, StableSince=-1, DisconnectRequested=-1, ReconnectRequested=-1;
  public bool InCruise;
  public bool CruiseSeen,DescentTriggered,DescentAcknowledged,DescentDisconnect;
  public bool SupervisorDisconnect;
  double supervisorValidUntil=-1;
  public double DescentSince=-1;
  double lastDescentSample=-1;
  public bool DescentPending=>DescentTriggered&&!DescentAcknowledged;
  public bool AirborneSafe(double now)=>Telemetry!=null&&Telemetry.Fresh(now)&&!Telemetry.OnGround&&!Telemetry.Paused&&Math.Abs(Telemetry.Rate-1)<0.01&&Telemetry.GroundSpeed>=0;
  public bool CanVerifySupervisor(double now,bool accountUnchanged)=>Armed&&Connected&&accountUnchanged&&SelectedCid==SessionCid&&SessionId==boundSession&&AirborneSafe(now)&&!AwaitingDisconnect&&!AwaitingReconnect;
  public Effect RequestSupervisorDisconnect(double now,bool confirmed,bool accountUnchanged,double confirmationLifetime=15){
   if(!confirmed||confirmationLifetime<=0||double.IsNaN(confirmationLifetime)||!CanVerifySupervisor(now,accountUnchanged))return Effect.None;
   supervisorValidUntil=now+Math.Min(15,confirmationLifetime);
   SupervisorDisconnect=true;DescentDisconnect=false;AwaitingDisconnect=true;OwnedDisconnect=false;AwaitingReconnect=false;DisconnectRequested=now;Deadline=-1;pending.Clear();
   Status="Supervisor confirmado: solicitando desconexão, sem retorno automático…";return Effect.Disconnect;
  }
  public bool CanCommand(bool reconnect,double now,bool accountUnchanged){
   if(!Armed||!accountUnchanged||SessionCid!=SelectedCid||SessionId!=boundSession||!AirborneSafe(now))return false;
   if(SupervisorDisconnect)return !reconnect&&Connected&&AwaitingDisconnect&&now-DisconnectRequested>=0&&now<=supervisorValidUntil;
   if(!reconnect&&DescentDisconnect)return Connected&&AwaitingDisconnect&&DescentPending&&now-DisconnectRequested<=15;
   if(DescentPending)return false;
   var c=Coverage;if(c==null||!c.ok||!BoundaryValid(c)||now-c.Received<0||now-c.Received>75||now-c.Sampled>85||c.dataAgeSeconds+now-c.Received>90)return false;
   return reconnect?!Connected&&OwnedDisconnect&&AwaitingReconnect&&c.clearConfirmed&&c.boundary.exitClear&&Relevant(c).Length==0:Connected&&AwaitingDisconnect&&Relevant(c).Length>0;
  }
  private HashSet<string> acknowledged=new HashSet<string>(), pending=new HashSet<string>();
  private double clearSince=-1;
  private string boundSession="";
  public void Connect(string cid,string callsign,string aircraft,double now) {
   BoundaryState=null;ApplyFlightPlan(null);SupervisorDisconnect=false;
   bool restore=AwaitingReconnect && cid==SessionCid && callsign==Callsign && aircraft==Aircraft;
   Connected=true;SessionCid=cid;Callsign=callsign;Aircraft=aircraft;SessionId=Guid.NewGuid().ToString("N");
   OwnedDisconnect=false;AwaitingDisconnect=false;AwaitingReconnect=false;clearSince=-1;
   Deadline=-1;acknowledged.Clear();pending.Clear();Coverage=null;
   if(restore)boundSession=SessionId;
   else {Armed=false;StableSince=-1;CruiseSeen=DescentTriggered=DescentAcknowledged=DescentDisconnect=false;DescentSince=-1;Status="Sessão identificada. Inicie o monitoramento.";}
   if(cid!=SelectedCid){Armed=false;Status="CID diferente do vPilot. Automação bloqueada.";}
  }
  public void Disconnected(double now) {
   if(SupervisorDisconnect){Connected=false;Stop();Status="Desconectado após mensagem de Supervisor confirmado. Reconexão automática desativada.";return;}
   if(DescentDisconnect){Connected=false;Stop();Status="Desconectado após aviso de descida. Reconexão automática desativada.";return;}
   Connected=false;OwnedDisconnect=AwaitingDisconnect && now-DisconnectRequested<=15;
   AwaitingDisconnect=false;AwaitingReconnect=false;Deadline=-1;pending.Clear();Coverage=null;clearSince=-1;
   if(!OwnedDisconnect){Stop();Status="Desconexão externa: reconexão automática desativada.";}
   else Status="Desconectado pelo app. Acompanhando cobertura em voo.";
  }
  public void Start() {
   Stop();
   if(!Connected || SelectedCid!=SessionCid){Status="Conecte no vPilot com o mesmo CID antes de iniciar.";return;}
   Armed=true;boundSession=SessionId;Status="Monitoramento ativo. Aguardando dados atuais.";
  }
  public void Stop(){BoundaryState=null;SupervisorDisconnect=false;Armed=false;OwnedDisconnect=false;AwaitingDisconnect=false;AwaitingReconnect=false;Deadline=-1;StableSince=-1;InCruise=false;Coverage=null;acknowledged.Clear();pending.Clear();clearSince=-1;CruiseSeen=DescentTriggered=DescentAcknowledged=DescentDisconnect=false;DescentSince=-1;Status="Monitoramento parado.";}
  public void Acknowledge(double now){if(Deadline<0||now>=Deadline)return;if(DescentPending){DescentAcknowledged=true;Deadline=-1;Status="Descida confirmada. Continue acompanhando o voo.";return;}acknowledged.UnionWith(pending);pending.Clear();Deadline=-1;Status="Presença confirmada. Contate o controlador pelo rádio.";}
  public void InvalidateCoverage(){Coverage=null;if(!DescentPending)Deadline=-1;pending.Clear();clearSince=-1;}
  public Effect Tick(double now,bool accountUnchanged) {
   if(!Armed)return Effect.None;
   if(!accountUnchanged || SessionCid!=SelectedCid || SessionId!=boundSession){Stop();Status="Identidade da sessão mudou. Automação cancelada.";return Effect.None;}
   var t=Telemetry;
   if(!AirborneSafe(now)){
    if(AwaitingDisconnect||AwaitingReconnect){AwaitingDisconnect=AwaitingReconnect=OwnedDisconnect=false;SupervisorDisconnect=DescentDisconnect=false;}
    if(t!=null&&t.Fresh(now)&&t.OnGround){OwnedDisconnect=false;BoundaryState=null;}
    Coverage=null;
    Deadline=-1;DescentSince=-1;StableSince=-1;InCruise=false;pending.Clear();clearSince=-1;
    if(!DescentAcknowledged)DescentTriggered=false;
    if(t!=null&&t.Fresh(now)&&t.OnGround){CruiseSeen=DescentTriggered=DescentAcknowledged=false;Status="No solo · conexão e desconexão automáticas bloqueadas.";}
    else Status="Telemetria indisponível, pausa ou velocidade inválida. Ações bloqueadas.";
    return Effect.None;
   }
   if(SupervisorDisconnect){if(now-DisconnectRequested>15){Stop();Status="vPilot não confirmou a desconexão por Supervisor. Reconexão automática desativada.";}return Effect.None;}
   if(DescentPending&&Connected){
    InCruise=false;
    if(AwaitingDisconnect){if(now-DisconnectRequested>15){Stop();Status="vPilot não confirmou a desconexão na descida. Verifique o cliente.";}return Effect.None;}
    Status="Descida: confirme presença · "+Math.Max(0,Math.Ceiling(Deadline-now))+" s";
    if(now>=Deadline){DescentDisconnect=true;AwaitingDisconnect=true;DisconnectRequested=now;Deadline=-1;Status="Descida sem resposta: solicitando desconexão, sem retorno automático…";return Effect.Disconnect;}
    return Effect.None;
   }
   if(CruiseFL>0&&!DescentTriggered&&CruiseSeen&&Connected&&!AwaitingDisconnect&&t.Altitude<CruiseFL*100&&t.VerticalSpeed<=-300){
    if(DescentSince<0||now-lastDescentSample>5)DescentSince=now;
    lastDescentSample=now;
    if(now-DescentSince>=60){DescentTriggered=true;InCruise=false;Deadline=now+300;pending.Clear();Status="Descida detectada. Confirme presença em 5 minutos.";return Effect.DescentAlert;}
   }else DescentSince=-1;
   bool valid=CruiseFL>0&&t!=null&&t.Fresh(now)&&!t.OnGround&&!t.Paused&&Math.Abs(t.Rate-1)<0.01&&t.GroundSpeed>80&&Math.Abs(t.Altitude-CruiseFL*100)<=200&&Math.Abs(t.VerticalSpeed)<=200;
   if(valid){if(StableSince<0)StableSince=now;InCruise=now-StableSince>=120;if(InCruise)CruiseSeen=true;}
   else {StableSince=-1;InCruise=false;}
   if(AwaitingDisconnect){if(now-DisconnectRequested>15){Stop();Status="vPilot não confirmou a desconexão. Verifique o cliente.";}return Effect.None;}
   if(AwaitingReconnect){if(now-ReconnectRequested>20){Stop();Status="Reconexão não confirmada. Reconecte manualmente.";}return Effect.None;}
   var c=Coverage;
   if(c==null||!c.ok||!BoundaryValid(c)||now-c.Received>75||now-c.Sampled>85||c.dataAgeSeconds+now-c.Received>90){InvalidateCoverage();Status="Dados indisponíveis ou desatualizados. Ações bloqueadas.";return Effect.None;}
   var keys=new HashSet<string>(Relevant(c).Select(x=>x.Key));acknowledged.IntersectWith(keys);
   if(Connected){
    if(Deadline>=0){pending.IntersectWith(keys);if(pending.Count==0){Deadline=-1;Status="Cobertura anterior encerrada.";}}
    var newKeys=new HashSet<string>(keys.Where(k=>!acknowledged.Contains(k)));
    if(newKeys.Count>0&&Deadline<0){pending=newKeys;Deadline=now+120;Status="Controlador na área ou aproximação a 15 NM. Confirme presença.";return Effect.Alert;}
    if(Deadline>=0){pending.UnionWith(newKeys);Status="Confirme presença · "+Math.Max(0,Math.Ceiling(Deadline-now))+" s";
     // Require a new spatial evaluation after expiry before taking action.
     if(now>=Deadline&&c.Sampled>=Deadline){AwaitingDisconnect=true;DisconnectRequested=now;Deadline=-1;Status="Solicitando desconexão ao vPilot…";return Effect.Disconnect;}
    }else Status=keys.Count>0?"Em voo · presença confirmada":"Em voo · sem cobertura detectada";
   } else if(OwnedDisconnect){
    if(!c.clearConfirmed||!c.boundary.exitClear||keys.Count>0){clearSince=-1;Status=keys.Count>0?"Desconectado · ainda há cobertura ATC":"Desconectado · cobertura inconclusiva; retorno bloqueado";return Effect.None;}
    if(clearSince<0)clearSince=c.Sampled;
    Status="Fora da margem ATC · confirmando na próxima consulta";
    if(c.Sampled-clearSince>=55){AwaitingReconnect=true;ReconnectRequested=now;Status="Solicitando reconexão ao vPilot…";return Effect.Reconnect;}
   }
   return Effect.None;
  }
 }
}
