using System;
using System.IO;
using System.Runtime.InteropServices;
namespace FlightWatchSetup {
 public static class Shortcuts {
  static string PathName=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"Chu Soneca.lnk");
  static string Target=>Path.Combine(Install.AppRoot,"Chu-Soneca.exe");
  public static void Create(){Write(PathName,Target,false);}
  public static void Remove(){Write(PathName,Target,true);}
  static string XpilotPath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"Chu Soneca - xPilot.lnk");
  public static void CreateXpilot(){Write(XpilotPath,Target,false,"--xpilot");}
  public static void RemoveXpilot(){Write(XpilotPath,Target,true,"--xpilot");}
  public static void Write(string path,string target,bool remove,string arguments=""){
   if(remove&&!File.Exists(path))return;
   object shell=null,link=null;
   try{shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));link=((dynamic)shell).CreateShortcut(path);dynamic shortcut=link;
    if(File.Exists(path)&&!string.Equals((string)shortcut.TargetPath,target,StringComparison.OrdinalIgnoreCase)){if(remove)return;throw new IOException("Já existe um atalho Chu Soneca para outro programa.");}
    if(remove){File.Delete(path);return;}
    Directory.CreateDirectory(Path.GetDirectoryName(path));shortcut.TargetPath=target;shortcut.WorkingDirectory=Path.GetDirectoryName(target);shortcut.Arguments=arguments;shortcut.Description="Abrir Chu Soneca com o cliente em execução";shortcut.Save();
   }finally{if(link!=null)Marshal.FinalReleaseComObject(link);if(shell!=null)Marshal.FinalReleaseComObject(shell);}
  }
 }
}
