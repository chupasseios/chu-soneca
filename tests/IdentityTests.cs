using System;
using System.Reflection;
using System.Windows.Forms;
using FlightWatch;
class IdentityTests {
 static object Field(FlightWindow w,string name)=>typeof(FlightWindow).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(w);
 static void Check(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS "+message);}
 [STAThread]static int Main(){try{using(var w=new FlightWindow(true)){
 var cid=(Label)Field(w,"cid");var start=(Button)Field(w,"start");var m=(FlightWatch.Monitor)Field(w,"monitor");
 Check(cid.Text=="Aguardando conexão vPilot"&&w.SelectedCid==""&&!start.Enabled,"no editable or saved identity before vPilot connection");
 w.NetworkConnected("7654321","TEST1","A320","",false,"session1");
 Check(cid.Text=="CID 7654321"&&w.SelectedCid=="7654321"&&start.Enabled,"CID comes from vPilot session");
 m.Start();m.AwaitingReconnect=true;m.OwnedDisconnect=true;w.NetworkConnected("7654321","TEST1","A320","",false,"session2");
 Check(m.Armed&&w.SelectedCid=="7654321","same-account owned reconnect preserves monitoring");
 m.AwaitingReconnect=true;m.OwnedDisconnect=true;w.NetworkConnected("1234567","TEST2","B738","",false,"session3");
 Check(cid.Text=="CID 1234567"&&w.SelectedCid=="1234567"&&!m.Armed&&!m.OwnedDisconnect,"new account replaces CID and cancels old actions");
 w.NetworkDisconnected();Check(!start.Enabled&&w.SelectedCid=="1234567","disconnected identity retained but cannot start");
 w.NetworkConnected("","TEST3","A320","",false,"session4");Check(!start.Enabled,"invalid session CID cannot start");
 Check(((Label)Field(w,"status")).ForeColor==ChuSoneca.UI.NightTheme.Yellow,"connected session awaiting data is yellow");
 w.NetworkDisconnected();Check(((Label)Field(w,"status")).ForeColor==ChuSoneca.UI.NightTheme.Red,"disconnected status is red");
 w.NetworkConnected("7654321","TEST5","A320","",false,"session5");Check(start.BackColor==ChuSoneca.UI.NightTheme.Green,"start action is green");
 m.Start();((CheckBox)Field(w,"disableAutoReconnect")).Checked=!((CheckBox)Field(w,"disableAutoReconnect")).Checked;Check(w.SelectedCid=="7654321"&&m.Armed&&m.DisableAutoReconnect==((CheckBox)Field(w,"disableAutoReconnect")).Checked,"preference changes cannot replace session CID with display text");
 Check(PluginUpdates.NewerVersion("{\"tag_name\":\"v0.7.3\"}",new Version(0,7,2,0))==new Version(0,7,3),"new stable release detected");
 Check(PluginUpdates.NewerVersion("{\"tag_name\":\"v0.7.2\"}",new Version(0,7,2,0))==null,"equal assembly revision does not trigger update");
 Check(PluginUpdates.NewerVersion("{\"tag_name\":\"v0.7.1\"}",new Version(0,7,2))==null,"older release ignored");
 Check(PluginUpdates.NewerVersion("{\"tag_name\":\"v0.10.0\"}",new Version(0,9,9))!=null,"version comparison is numeric");
 foreach(var body in new[]{"{\"tag_name\":\"v1.0.0\",\"draft\":true}","{\"tag_name\":\"v1.0.0\",\"prerelease\":true}","{\"tag_name\":\"v1.0.0-beta\"}","{}"})Check(PluginUpdates.NewerVersion(body,new Version(0,7,2))==null,"non stable or invalid release ignored");
 using(var cancelled=new System.Threading.CancellationTokenSource()){cancelled.Cancel();Check(PluginUpdates.CheckAsync(new Version(0,7,2),cancelled.Token).GetAwaiter().GetResult()==null,"cancelled update check finishes without network or exception");}
 m.Start();var armed=m.Armed;var identity=w.SelectedCid;
 typeof(FlightWindow).GetMethod("ShowUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(w,new object[]{new Version(0,7,3)});
 Check(((LinkLabel)Field(w,"updateNotice")).Text.Contains("v0.7.3")&&m.Armed==armed&&w.SelectedCid==identity,"update banner preserves monitoring and identity");
 var serializer=new System.Web.Script.Serialization.JavaScriptSerializer();
 var legacy=serializer.Deserialize<Preferences>("{\"ExitMarginNm\":10}");Check(!legacy.DisableAutoReconnect&&m.ExitMarginNm==15,"legacy 10 NM preference cannot change fixed 15 NM margin");
 Check(serializer.Deserialize<Preferences>(serializer.Serialize(new Preferences{DisableAutoReconnect=true})).DisableAutoReconnect,"manual reconnect preference persists");
 m.Start();var sessionBeforeSave=m.SessionId;
 typeof(FlightWindow).GetMethod("SaveRouteSource",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(w,null);
 Check(m.Armed&&m.SessionId==sessionBeforeSave,"saving secondary source preserves active flight monitoring");
 XPlaneTests.Run();
 w.Shutdown();}return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
