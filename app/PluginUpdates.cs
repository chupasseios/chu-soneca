using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace FlightWatch {
 internal static class PluginUpdates {
  internal const string ReleasesUrl="https://github.com/chupasseios/chu-soneca/releases";
  sealed class Release {public string tag_name {get;set;} public bool draft {get;set;} public bool prerelease {get;set;}}
  internal static Version NewerVersion(string body,Version installed){
   var release=new JavaScriptSerializer{MaxJsonLength=1024*1024}.Deserialize<Release>(body);
   if(release==null||release.draft||release.prerelease||!Regex.IsMatch(release.tag_name??"","^v[0-9]+\\.[0-9]+\\.[0-9]+$"))return null;
   Version candidate;
   if(!Version.TryParse(release.tag_name.Substring(1),out candidate))return null;
   var current=new Version(installed.Major,installed.Minor,Math.Max(0,installed.Build));
   return candidate>current?candidate:null;
  }
  internal static Task<Version> CheckAsync(Version installed,CancellationToken cancellation)=>Task.Run(()=>{
   try {
    cancellation.ThrowIfCancellationRequested();
    var request=(HttpWebRequest)WebRequest.Create("https://api.github.com/repos/chupasseios/chu-soneca/releases/latest");
    request.UserAgent="Chu-Soneca/"+installed.ToString(3);request.Accept="application/vnd.github+json";
    request.Timeout=10000;request.ReadWriteTimeout=10000;
    using(cancellation.Register(request.Abort))
    using(var response=request.GetResponse())
    using(var reader=new StreamReader(response.GetResponseStream())){
     var buffer=new char[1024*1024+1];int length=0,count;
     while(length<buffer.Length&&(count=reader.Read(buffer,length,buffer.Length-length))>0)length+=count;
     if(length==buffer.Length)return null;
     return NewerVersion(new string(buffer,0,length),installed);
    }
   } catch {return null;} // Offline/rate limiting must never affect flight monitoring.
  });
 }
}
