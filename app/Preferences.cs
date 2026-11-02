using System;
using System.IO;
using System.Web.Script.Serialization;
namespace FlightWatch {
 public sealed class Preferences {
  public int ExitMarginNm {get;set;}=15;
  public string Simbrief {get;set;}="";
  public string Cid {get;set;}="";
  public bool OpenWithVpilot {get;set;}=true;
  public bool CloseToTray {get;set;}=false;
  public static Preferences Read(){try{return new JavaScriptSerializer().Deserialize<Preferences>(File.ReadAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FlightWatch","preferences.json")))??new Preferences();}catch{return new Preferences();}}
 }
}
