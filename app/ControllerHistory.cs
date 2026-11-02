using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
namespace FlightWatch {
 public sealed class ControllerVisit {
  public string Key,Callsign,Frequency,Flight,OnlineSinceUtc,FirstCoveredUtc,LastOnlineUtc,OfflineDetectedUtc,TrackingEndedUtc,State;
 }
 public sealed class ControllerHistory {
  public List<ControllerVisit> Entries=new List<ControllerVisit>();
  readonly Dictionary<string,ControllerVisit> tracked=new Dictionary<string,ControllerVisit>();
  static string Stamp(DateTime utc)=>utc.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss'Z'",CultureInfo.InvariantCulture);
  public void Observe(Coverage sample,DateTime now,string flight){
   if(sample==null||!sample.ok||sample.onlineControllers==null||sample.dataAgeSeconds>90)return;
   DateTime at=sample.timestamp>0?DateTimeOffset.FromUnixTimeMilliseconds(sample.timestamp).UtcDateTime:now.ToUniversalTime();
   var online=new HashSet<string>(sample.onlineControllers.Select(c=>c.Key));
   foreach(var c in sample.controllers){if(tracked.ContainsKey(c.Key))continue;DateTimeOffset logon;string started=DateTimeOffset.TryParse(c.logon_time,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out logon)?Stamp(logon.UtcDateTime):"Não informado";
    var row=new ControllerVisit{Key=c.Key,Callsign=c.callsign,Frequency=c.frequency,Flight=flight,OnlineSinceUtc=started,FirstCoveredUtc=Stamp(at),LastOnlineUtc=Stamp(at),State="Online"};Entries.Add(row);tracked.Add(c.Key,row);
   }
   var covering=new HashSet<string>(sample.controllers.Select(c=>c.Key));
   foreach(var pair in tracked.ToArray()){var row=pair.Value;if(online.Contains(pair.Key)){row.LastOnlineUtc=Stamp(at);row.State=covering.Contains(pair.Key)?"Cobrindo posição":"Online fora da cobertura";}else{row.OfflineDetectedUtc=Stamp(at);row.State="Offline detectado";tracked.Remove(pair.Key);}}
  }
  public void End(DateTime now){foreach(var row in tracked.Values){row.TrackingEndedUtc=Stamp(now);row.State="Observação encerrada";}tracked.Clear();}
  public void Restore(IEnumerable<ControllerVisit> saved){Entries=saved?.ToList()??new List<ControllerVisit>();foreach(var row in Entries.Where(r=>string.IsNullOrEmpty(r.OfflineDetectedUtc)&&string.IsNullOrEmpty(r.TrackingEndedUtc))){row.State="Sessão anterior; fim desconhecido";}}
 }
}
