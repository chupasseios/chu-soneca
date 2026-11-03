using System;
using System.IO;
using System.Web.Script.Serialization;
namespace FlightWatch {
 public sealed class Preferences {
  public bool DisableAutoReconnect {get;set;}=false;
  public string Simbrief {get;set;}="";
  public string Cid {get;set;}="";
  public bool OpenWithVpilot {get;set;}=true;
  public bool CloseToTray {get;set;}=false;
  public static string DataDirectory=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FlightWatch",IsXpilot?"xPilot":"");
  public static bool IsXpilot;
  public static Preferences Read(){try{return new JavaScriptSerializer().Deserialize<Preferences>(File.ReadAllText(Path.Combine(DataDirectory,"preferences.json")))??new Preferences();}catch{return new Preferences();}}
 }
}
