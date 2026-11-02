using System;
using System.IO;
using System.Runtime.InteropServices;
namespace FlightWatchSetup {
 public static class Shortcuts {
  static string PathName=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"Chu Soneca.lnk");
  static string Target=>Path.Combine(Install.AppRoot,"Chu-Soneca.exe");
  public static void Create(){Write(PathName,Target,false);}
  public static void Remove(){Write(PathName,Target,true);}
  public static void Write(string path,string target,bool remove){
   if(remove&&!File.Exists(path))return;
   object shell=null,link=null;
   try{shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));link=((dynamic)shell).CreateShortcut(path);dynamic shortcut=link;
    if(File.Exists(path)&&!string.Equals((string)shortcut.TargetPath,target,StringComparison.OrdinalIgnoreCase)){if(remove)return;throw new IOException("Já existe um atalho Chu Soneca para outro programa.");}
    if(remove){File.Delete(path);return;}
    Directory.CreateDirectory(Path.GetDirectoryName(path));shortcut.TargetPath=target;shortcut.WorkingDirectory=Path.GetDirectoryName(target);shortcut.Description="Abrir Chu Soneca com o vPilot em execução";shortcut.Save();
   }finally{if(link!=null)Marshal.FinalReleaseComObject(link);if(shell!=null)Marshal.FinalReleaseComObject(shell);}
  }
 }
}
