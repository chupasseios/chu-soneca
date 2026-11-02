using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
namespace FlightWatch {
 public sealed partial class FlightWindow {
  readonly ControllerHistory history=new ControllerHistory();
  string HistoryFile=>Path.Combine(dataDir,"controller-history.json");
  void LoadHistory(){if(demo)return;try{history.Restore(json.Deserialize<ControllerVisit[]>(File.ReadAllText(HistoryFile)));}catch(Exception e){if(File.Exists(HistoryFile))Log("Não foi possível ler histórico: "+e.Message);}RenderHistory();}
  void SaveHistory(){RenderHistory();if(demo)return;try{Directory.CreateDirectory(dataDir);string tmp=HistoryFile+".tmp";File.WriteAllText(tmp,json.Serialize(history.Entries));if(File.Exists(HistoryFile))File.Replace(tmp,HistoryFile,null);else File.Move(tmp,HistoryFile);}catch(Exception e){Log("Não foi possível salvar histórico: "+e.Message);}}
  void EndHistory(){history.End(DateTime.UtcNow);SaveHistory();}
  void RenderHistory(){historyRows.BeginUpdate();historyRows.Items.Clear();foreach(var row in history.Entries.AsEnumerable().Reverse().Take(500))historyRows.Items.Add(new ListViewItem(new[]{row.Callsign,ShortUtc(row.OnlineSinceUtc),ShortUtc(row.LastOnlineUtc),ShortUtc(row.OfflineDetectedUtc),Duration(row),row.State}));historyRows.EndUpdate();}
  static string ShortUtc(string value){DateTime parsed;return DateTime.TryParse(value,out parsed)?parsed.ToUniversalTime().ToString("dd/MM HH:mm:ss")+"Z":value??"—";}
  static string Duration(ControllerVisit row){DateTime a,b;if(!DateTime.TryParse(row.OnlineSinceUtc,out a)||!DateTime.TryParse(row.LastOnlineUtc,out b)||b<a)return "—";var d=b-a;return ((int)d.TotalHours).ToString("00")+":"+d.Minutes.ToString("00")+":"+d.Seconds.ToString("00");}
  static string Csv(string value){value=value??"";if(value.Length>0&&"=+-@\t\r\n".Contains(value[0]))value="'"+value;return "\""+value.Replace("\"","\"\"")+"\"";}
  void ExportHistory(){using(var dialog=new SaveFileDialog{Filter="CSV (*.csv)|*.csv",FileName="Chu-Soneca-historico-"+DateTime.UtcNow.ToString("yyyyMMdd")+".csv"}){if(dialog.ShowDialog(this)!=DialogResult.OK)return;try{var lines=history.Entries.Select(r=>string.Join(",",new[]{r.Callsign,r.Frequency,r.Flight,r.OnlineSinceUtc,r.FirstCoveredUtc,r.LastOnlineUtc,r.OfflineDetectedUtc,r.TrackingEndedUtc,r.State}.Select(Csv)));File.WriteAllLines(dialog.FileName,new[]{"Controlador,Frequencia,Voo,Online_desde_UTC,Cobertura_detectada_UTC,Ultima_confirmacao_UTC,Offline_detectado_UTC,Observacao_encerrada_UTC,Estado"}.Concat(lines),new UTF8Encoding(true));Log("Histórico exportado em Zulu.");}catch(Exception e){MessageBox.Show(this,e.Message,"Não foi possível exportar");}}}
 }
}
