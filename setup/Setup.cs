using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace FlightWatchSetup {
 public sealed class Receipt {public string Vpilot;public string Xpilot;public string[] Files;}
 public static class Install {
  public static readonly string Version=typeof(Install).Assembly.GetName().Version.ToString(3);
  public static readonly string AppRoot=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","FlightWatch");
  public static readonly string ReceiptPath=Path.Combine(AppRoot,"installed.json");
  public const string RegPath=@"Software\Microsoft\Windows\CurrentVersion\Uninstall\FlightWatch";
  public static bool ForXpilot;
  public static Receipt Existing(){try{return Json.Deserialize<Receipt>(File.ReadAllText(ReceiptPath))??new Receipt();}catch{return new Receipt();}}
  public static string XpilotDefault=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"org.vatsim.xpilot");
  public static readonly JavaScriptSerializer Json=new JavaScriptSerializer();
  public static bool FilesMatch(string vpilot,string app,string xpilot=null){try{Dictionary<string,string> hashes;using(var r=new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("hashes.json")))hashes=Json.Deserialize<Dictionary<string,string>>(r.ReadToEnd());foreach(var pair in hashes){string[] parts=pair.Key.Split(new[]{'/'},2);if(parts.Length!=2)return false;if(parts[0]=="plugin"&&string.IsNullOrEmpty(vpilot)||parts[0]=="xpilot"&&string.IsNullOrEmpty(xpilot))continue;string root=parts[0]=="plugin"?Path.Combine(vpilot,"Plugins"):parts[0]=="xpilot"?Path.Combine(xpilot,"Plugins"):parts[0]=="app"?app:null;if(root==null)return false;string file=Inside(root,parts[1]);if(!File.Exists(file)||Hash(file)!=pair.Value)return false;}return hashes.Count>0;}catch{return false;}}
  public static bool Verified(string path){try{var receipt=Json.Deserialize<Receipt>(File.ReadAllText(ReceiptPath));if(!string.Equals(Path.GetFullPath(ForXpilot?receipt.Xpilot:receipt.Vpilot),Path.GetFullPath(path),StringComparison.OrdinalIgnoreCase))return false;using(var key=Registry.CurrentUser.OpenSubKey(RegPath)){if((string)key?.GetValue("DisplayVersion")!=Version)return false;}return FilesMatch(receipt.Vpilot,AppRoot,receipt.Xpilot);}catch{return false;}}
  public static bool Running(){return Process.GetProcessesByName("vPilot").Any()||Process.GetProcessesByName("xPilot").Any();}
  public static string Detect(){if(ForXpilot)return Existing().Xpilot??XpilotDefault;foreach(var view in new[]{RegistryView.Registry64,RegistryView.Registry32})try{using(var root=RegistryKey.OpenBaseKey(RegistryHive.CurrentUser,view))using(var key=root.OpenSubKey(@"Software\vPilot")){string path=key?.GetValue("Install_Dir") as string;if(!string.IsNullOrWhiteSpace(path)&&File.Exists(Path.Combine(path,"vPilot.exe")))return path;}}catch{}return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"vPilot");}
  public static void Check(string path,bool checkProcess=true){
   if(!Environment.Is64BitOperatingSystem)throw new Exception("Esta versão inclui Node x64 e requer Windows de 64 bits.");
   using(var key=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full")){if(Convert.ToInt32(key?.GetValue("Release")??0)<528040)throw new Exception("Instale o .NET Framework 4.8 e execute novamente.");}
   if(checkProcess&&Running())throw new Exception("Feche o vPilot e o xPilot e clique em Instalar novamente. O instalador não encerra sua conexão.");
   if(ForXpilot){
    if(!Directory.Exists(path)||!string.Equals(Path.GetFileName(Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar)),"org.vatsim.xpilot",StringComparison.OrdinalIgnoreCase))throw new Exception("Selecione a pasta org.vatsim.xpilot criada pelo xPilot 4. Abra o xPilot uma vez antes de instalar.");
    var prior=Existing();if(!string.IsNullOrEmpty(prior.Xpilot)&&!string.Equals(Path.GetFullPath(prior.Xpilot),Path.GetFullPath(path),StringComparison.OrdinalIgnoreCase))throw new Exception("A integração xPilot já está instalada em outra pasta.");return;
   }
   foreach(string file in new[]{"vPilot.exe","RossCarlson.Vatsim.Vpilot.Plugins.dll","Microsoft.FlightSimulator.SimConnect.dll"})if(!File.Exists(Path.Combine(path,file)))throw new Exception("Pasta incompatível: falta "+file+". Selecione a pasta onde o vPilot está instalado.");
   using(var reader=new BinaryReader(File.OpenRead(Path.Combine(path,"vPilot.exe")))){reader.BaseStream.Position=60;int pe=reader.ReadInt32();reader.BaseStream.Position=pe+4;if(reader.ReadUInt16()!=0x14c)throw new Exception("Esta versão do Chu Soneca requer vPilot x86.");}
   if(File.Exists(ReceiptPath)){var previous=Json.Deserialize<Receipt>(File.ReadAllText(ReceiptPath));if(!string.IsNullOrEmpty(previous.Vpilot)&&!string.Equals(Path.GetFullPath(previous.Vpilot),Path.GetFullPath(path),StringComparison.OrdinalIgnoreCase))throw new Exception("Desinstale a versão anterior antes de mudar a pasta do vPilot.");}
  }
  public static string Inside(string root,string relative){var fullRoot=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;var result=Path.GetFullPath(Path.Combine(fullRoot,relative));if(!result.StartsWith(fullRoot,StringComparison.OrdinalIgnoreCase))throw new Exception("Caminho fora do destino permitido.");return result;}
  static void NoLinks(string path){if(File.Exists(path)&&(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new Exception("Arquivo de destino é um link: "+path);for(var d=new DirectoryInfo(Path.GetDirectoryName(path));d!=null;d=d.Parent)if(d.Exists&&(d.Attributes&FileAttributes.ReparsePoint)!=0)throw new Exception("Instalação por links de diretório não suportada: "+d.FullName);}
  public static string Hash(string file){using(var sha=SHA256.Create())using(var input=File.OpenRead(file))return BitConverter.ToString(sha.ComputeHash(input)).Replace("-","");}
  public static string Extract(Action<int,string> report=null){var stage=Path.Combine(Path.GetTempPath(),"FlightWatch-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);try{
   Dictionary<string,string> hashes;using(var r=new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("hashes.json")))hashes=Json.Deserialize<Dictionary<string,string>>(r.ReadToEnd());
   int extracted=0;using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))using(var zip=new ZipArchive(stream,ZipArchiveMode.Read))foreach(var entry in zip.Entries){if(entry.Name=="")continue;var key=entry.FullName.Replace('\\','/');if(!hashes.ContainsKey(key))throw new Exception("Arquivo não previsto no pacote: "+key);string file=Inside(stage,key);Directory.CreateDirectory(Path.GetDirectoryName(file));entry.ExtractToFile(file);if(Hash(file)!=hashes[key])throw new Exception("Falha de integridade: "+key);report?.Invoke(5+30*++extracted/hashes.Count,"Conferindo o pacote...");}
   foreach(var key in hashes.Keys)if(!File.Exists(Inside(stage,key)))throw new Exception("Pacote incompleto: "+key);return stage;
  }catch{CleanStage(stage);throw;}}
  public static void CleanStage(string stage){string full=Path.GetFullPath(stage),temp=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;if(!full.StartsWith(temp,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(full).StartsWith("FlightWatch-",StringComparison.Ordinal))throw new Exception("Limpeza fora da área temporária bloqueada.");if(Directory.Exists(full))Directory.Delete(full,true);}
  public static List<KeyValuePair<string,string>> Map(string stage,string vpilot,string app,string xpilot=null){
   var list=new List<KeyValuePair<string,string>>();foreach(var group in new[]{"plugin","xpilot","app"}){
    if(group=="plugin"&&string.IsNullOrEmpty(vpilot)||group=="xpilot"&&string.IsNullOrEmpty(xpilot))continue;
    var root=group=="plugin"?Path.Combine(vpilot,"Plugins"):group=="xpilot"?Path.Combine(xpilot,"Plugins"):app;
    foreach(var file in Directory.GetFiles(Inside(stage,group),"*",SearchOption.AllDirectories)){var relative=file.Substring(Inside(stage,group).Length+1);list.Add(new KeyValuePair<string,string>(file,Inside(root,relative)));}
   }return list;
  }
  public static void Deploy(string stage,string vpilot,string app,int failAfter=-1,Action<int,string> report=null,string xpilot=null){
   var files=Map(stage,vpilot,app,xpilot);string backup=Inside(stage,"rollback");Directory.CreateDirectory(backup);var changed=new List<KeyValuePair<string,string>>();
   try{foreach(var pair in files){NoLinks(pair.Value);Directory.CreateDirectory(Path.GetDirectoryName(pair.Value));string old=null;if(File.Exists(pair.Value)){old=Inside(backup,changed.Count.ToString());File.Copy(pair.Value,old);}changed.Add(new KeyValuePair<string,string>(pair.Value,old));File.Copy(pair.Key,pair.Value,true);if(Hash(pair.Key)!=Hash(pair.Value))throw new Exception("Falha ao verificar "+Path.GetFileName(pair.Value));report?.Invoke(35+50*changed.Count/files.Count,"Instalando componentes...");if(changed.Count==failAfter)throw new Exception("Falha de teste de rollback.");}}
   catch{foreach(var pair in changed.AsEnumerable().Reverse()){if(pair.Value==null){if(File.Exists(pair.Key))File.Delete(pair.Key);}else File.Copy(pair.Value,pair.Key,true);}throw;}
  }
  public static void Execute(string path,Action<int,string> report=null){report?.Invoke(0,"Verificando cliente e requisitos...");Check(path);report?.Invoke(5,"Conferindo integridade do pacote...");string stage=Extract(report);try{
   var previous=Existing();string vp=ForXpilot?previous.Vpilot:Path.GetFullPath(path),xp=ForXpilot?Path.GetFullPath(path):previous.Xpilot;
   Check(path);Deploy(stage,vp,AppRoot,report:report,xpilot:xp);report?.Invoke(90,"Registrando a instalação...");
   File.WriteAllText(ReceiptPath,Json.Serialize(new Receipt{Vpilot=vp,Xpilot=xp,Files=Map(stage,vp,AppRoot,xp).Select(p=>p.Value).ToArray()}));
   using(var key=Registry.CurrentUser.CreateSubKey(RegPath)){key.SetValue("DisplayName","Chu Soneca");key.SetValue("DisplayVersion",Version);key.SetValue("Publisher","Chu Soneca — testes não comerciais");key.SetValue("InstallLocation",AppRoot);key.SetValue("DisplayIcon",Path.Combine(AppRoot,"FlightWatch.Demo.exe"));key.SetValue("UninstallString","\""+Path.Combine(AppRoot,"FlightWatch-Uninstall.exe")+"\"");key.SetValue("NoModify",1);key.SetValue("NoRepair",1);}
   if(!string.IsNullOrEmpty(vp))Shortcuts.Create();if(!string.IsNullOrEmpty(xp))Shortcuts.CreateXpilot();
   report?.Invoke(95,"Verificando os arquivos instalados...");
   if(!Verified(path))throw new Exception("A verificacao final falhou. Execute novamente para reparar.");
  }finally{CleanStage(stage);}
   report?.Invoke(100,"Instalação concluída e verificada.");}
  public static void Remove(Action<int,string> report=null){report?.Invoke(0,"Verificando a instalação...");if(Running())throw new Exception("Feche o vPilot e o xPilot antes de remover o Chu Soneca.");var receipt=Json.Deserialize<Receipt>(File.ReadAllText(ReceiptPath));Shortcuts.Remove();Shortcuts.RemoveXpilot();RemoveFiles(receipt,AppRoot,report);File.Delete(ReceiptPath);Registry.CurrentUser.DeleteSubKeyTree(RegPath,false);if(Directory.Exists(AppRoot)&&!Directory.EnumerateFileSystemEntries(AppRoot).Any())Directory.Delete(AppRoot);report?.Invoke(100,"Desinstalação concluída.");}
  public static void RemoveFiles(Receipt receipt,string appRoot,Action<int,string> report=null){var pluginRoot=string.IsNullOrEmpty(receipt.Vpilot)?null:Path.Combine(receipt.Vpilot,"Plugins");var xpilotRoot=string.IsNullOrEmpty(receipt.Xpilot)?null:Path.Combine(receipt.Xpilot,"Plugins","ChuSoneca.Xpilot");
   // Restrict deletion to this product's exact DLL, owned asset directory, and application directory.
   foreach(var file in receipt.Files){var full=Path.GetFullPath(file);bool permitted=(pluginRoot!=null&&(string.Equals(full,Path.Combine(pluginRoot,"FlightWatch.dll"),StringComparison.OrdinalIgnoreCase)||full.StartsWith(Path.Combine(pluginRoot,"FlightWatch")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)))||(xpilotRoot!=null&&full.StartsWith(xpilotRoot+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))||full.StartsWith(appRoot+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);if(!permitted)throw new Exception("Manifesto de remoção inválido.");NoLinks(full);}
   int removed=0;foreach(var file in receipt.Files){if(File.Exists(file))File.Delete(file);report?.Invoke(10+80*++removed/Math.Max(1,receipt.Files.Length),"Removendo componentes...");}
   foreach(var root in new[]{pluginRoot==null?null:Path.Combine(pluginRoot,"FlightWatch"),xpilotRoot,appRoot}){if(root==null||!Directory.Exists(root))continue;foreach(var dir in Directory.GetDirectories(root,"*",SearchOption.AllDirectories).OrderByDescending(x=>x.Length))if(!Directory.EnumerateFileSystemEntries(dir).Any())Directory.Delete(dir);if(!Directory.EnumerateFileSystemEntries(root).Any())Directory.Delete(root);}
  }
 }
 public static class Program {
  [STAThread]public static void Main(string[] args){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);if(args.Length==2&&args[0]=="--self-test"){try{SelfTest.Run(args[1]);}catch(Exception e){File.WriteAllText(args[1],e.ToString());Environment.ExitCode=1;}return;}if(args.Length==2&&args[0].StartsWith("--render",StringComparison.Ordinal)){using(var form=new SetupForm()){form.Preview(args[0]=="--render-success");if(args[0]=="--render-xpilot")form.PreviewXpilot();form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-10000,-10000);form.Show();Application.DoEvents();using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(args[1]);}form.Close();}return;}Application.Run(new SetupForm());}
 }
 public static class RemoveProgram {
  [STAThread]public static void Main(string[] args){Application.EnableVisualStyles();try{
   if(args.Length==2&&args[0]=="--remove"){int pid=int.Parse(args[1]);try{Process.GetProcessById(pid).WaitForExit(10000);}catch(ArgumentException){}Install.Remove();MessageBox.Show("Chu Soneca removido. Preferências, cache e diagnóstico foram preservados.","Chu Soneca");return;}
   if(Install.Running())throw new Exception("Feche o vPilot e o xPilot antes de desinstalar.");if(MessageBox.Show("Remover o Chu Soneca? Suas preferências e outros plugins serão preservados.","Chu Soneca",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
   string copy=Path.Combine(Path.GetTempPath(),"FlightWatch-Remove-"+Guid.NewGuid().ToString("N")+".exe");File.Copy(Assembly.GetExecutingAssembly().Location,copy);Process.Start(new ProcessStartInfo(copy,"--remove "+Process.GetCurrentProcess().Id){UseShellExecute=true});
  }catch(Exception e){MessageBox.Show(e.Message,"Não foi possível remover o Chu Soneca");Environment.ExitCode=1;}}
 }
 public static class SelfTest {
  public static void Run(string output){SetupForm.TestControls();int last=0;Action<int,string> progress=(value,text)=>{if(value<last||value>100)throw new Exception("Invalid progress");last=value;};string stage=Install.Extract(progress);if(last!=35)throw new Exception("Extraction progress incomplete");try{string sandbox=Install.Inside(stage,"sandbox"),vp=Path.Combine(sandbox,"vPilot"),app=Path.Combine(sandbox,"app");Directory.CreateDirectory(Path.Combine(vp,"Plugins"));File.WriteAllText(Path.Combine(vp,"Plugins","unrelated.dll"),"preserve");Install.Deploy(stage,vp,app,report:progress);if(last!=85)throw new Exception("Deployment progress incomplete");foreach(var pair in Install.Map(stage,vp,app))if(Install.Hash(pair.Key)!=Install.Hash(pair.Value))throw new Exception("Installed file mismatch");
   if(!Install.FilesMatch(vp,app))throw new Exception("Valid installation not detected");string plugin=Path.Combine(vp,"Plugins","FlightWatch.dll");File.WriteAllText(plugin,"old version");if(Install.FilesMatch(vp,app))throw new Exception("Corrupt installation incorrectly marked installed");try{Install.Deploy(stage,vp,app,1);throw new Exception("Missing injected failure");}catch(Exception e){if(!e.Message.Contains("rollback"))throw;}if(File.ReadAllText(plugin)!="old version")throw new Exception("Rollback failed");if(File.ReadAllText(Path.Combine(vp,"Plugins","unrelated.dll"))!="preserve")throw new Exception("Foreign plugin modified");try{Install.Inside(sandbox,"../../escape");throw new Exception("Traversal allowed");}catch(Exception e){if(!e.Message.Contains("permitido"))throw;}
   Install.RemoveFiles(new Receipt{Vpilot=vp,Files=Install.Map(stage,vp,app).Select(p=>p.Value).ToArray()},app);if(File.Exists(plugin)||File.Exists(Path.Combine(vp,"Plugins","FlightWatch","FlightWatch.Telemetry.exe")))throw new Exception("Uninstall incomplete");if(!File.Exists(Path.Combine(vp,"Plugins","unrelated.dll")))throw new Exception("Uninstall affected other plugin");
   string xp=Path.Combine(sandbox,"org.vatsim.xpilot");Directory.CreateDirectory(Path.Combine(xp,"Plugins","Other"));File.WriteAllText(Path.Combine(xp,"Plugins","Other","Other.dll"),"preserve");
   Install.Deploy(stage,null,app,xpilot:xp);if(!Install.FilesMatch(null,app,xp))throw new Exception("xPilot-only files failed verification");
   if(File.Exists(Path.Combine(xp,"Plugins","ChuSoneca.Xpilot","xPilot.PluginSdk.dll")))throw new Exception("SDK runtime must not be distributed");
   string xpDll=Path.Combine(xp,"Plugins","ChuSoneca.Xpilot","ChuSoneca.Xpilot.dll");File.WriteAllText(xpDll,"old xPilot plugin");
   try{Install.Deploy(stage,null,app,1,xpilot:xp);throw new Exception("Missing xPilot rollback fault");}catch(Exception e){if(!e.Message.Contains("rollback"))throw;}
   if(File.ReadAllText(xpDll)!="old xPilot plugin")throw new Exception("xPilot rollback damaged previous plugin");
   Install.Deploy(stage,vp,app,xpilot:xp);if(!Install.FilesMatch(vp,app,xp))throw new Exception("Dual-client deployment failed");
   var dual=new Receipt{Vpilot=vp,Xpilot=xp,Files=Install.Map(stage,vp,app,xp).Select(p=>p.Value).ToArray()};Install.RemoveFiles(dual,app);
   if(File.Exists(xpDll)||File.Exists(plugin)||!File.Exists(Path.Combine(xp,"Plugins","Other","Other.dll")))throw new Exception("Dual-client uninstall isolation failed");
   string shortcut=Path.Combine(sandbox,"Chu Soneca.lnk"),launcher=Path.Combine(app,"Chu-Soneca.exe");Shortcuts.Write(shortcut,launcher,false);if(!File.Exists(shortcut))throw new Exception("Shortcut missing");Shortcuts.Write(shortcut,launcher,true);if(File.Exists(shortcut))throw new Exception("Shortcut not removed");
   File.WriteAllText(output,"PASS: xPilot-only and dual-client deployment, xPilot rollback, dual-client uninstall isolation; consent gating, update version comparison, 0/100 UI states and monotonic file progress; embedded payload integrity; all installed file hashes; rollback restores previous plugin; unrelated plugin preserved; path traversal blocked; uninstall removes helper and all owned files, preserving other plugins; manual launch shortcut creation/removal. No real vPilot files or registry modified.");
  }finally{Install.CleanStage(stage);}}
 }
}
