using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.Xml;

namespace FlightWatch {
 public sealed partial class FlightWindow:Form {
  readonly System.Collections.Generic.HashSet<Process> workers=new System.Collections.Generic.HashSet<Process>();readonly object workerGate=new object();
  readonly CheckBox openWithVpilot=new CheckBox{Text="Abrir com o vPilot",AutoSize=true};
  readonly CheckBox disableAutoReconnect=new CheckBox{Text="Não reconectar automaticamente, mesmo após sair 15 NM",AutoSize=true};
  readonly CheckBox closeToTray=new CheckBox{Text="Ao fechar, manter na bandeja",AutoSize=true};
  readonly RecoverySchedule feedRecovery=new RecoverySchedule();
  readonly TextBox simbrief=new TextBox{Width=220};
  readonly Label routeLabel=new Label{AutoSize=true,MaximumSize=new Size(630,0),Text="Importe o plano do SimBrief para validar os controles na rota."};
  object importedRoute;bool routeValidated;
  sealed class RouteImport {public bool ok {get;set;} public string error {get;set;} public object route {get;set;}}
  async Task ImportRoute(){
   if(busy)return;monitor.Stop();epoch++;int generation=epoch;busy=true;routeValidated=false;importedRoute=null;routeLabel.Text="Importando rota do SimBrief…";SaveSettings();Render();string user=simbrief.Text.Trim();
   try{var data=await Task.Run(()=>RunEngine(json.Serialize(new {mode="importRoute",simbrief=user})));if(closing||generation!=epoch)return;var response=json.Deserialize<RouteImport>(data);if(!response.ok)throw new Exception(response.error);importedRoute=response.route;routeLabel.Text="Rota importada. Será validada com o voo conectado antes das ações.";Log(routeLabel.Text);nextPoll=0;}
   catch(Exception ex){if(!closing&&generation==epoch){routeLabel.Text="Importação falhou: "+ex.Message;Log(routeLabel.Text);}}
   finally{busy=false;if(!closing)Render();}
  }
  readonly Monitor monitor=new Monitor();readonly Stopwatch clock=Stopwatch.StartNew();readonly bool demo;
  readonly JavaScriptSerializer json=new JavaScriptSerializer{MaxJsonLength=8000000};
  readonly string dataDir=Preferences.DataDirectory;
  readonly string assets=Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"FlightWatch");
  readonly Label cid=new Label{AutoSize=true,MaximumSize=new Size(190,0),Text="",Font=new Font("Segoe UI",11,FontStyle.Bold)};
  readonly Label planLevel=new Label{AutoSize=true,MaximumSize=new Size(190,0),ForeColor=ChuSoneca.UI.NightTheme.Accent};
  readonly Label status=new Label{AutoSize=true,MaximumSize=new Size(730,0),Font=new Font("Segoe UI",17,FontStyle.Bold)};
  readonly Label details=new Label{AutoSize=true,MaximumSize=new Size(730,0)};
  readonly Label timing=new Label{AutoSize=true,ForeColor=Color.FromArgb(60,70,80)};
  readonly Label session=new Label{AutoSize=true,MaximumSize=new Size(730,0)};
  readonly Button start=new ChuSoneca.UI.SoftButton{Text="Iniciar monitoramento",AutoSize=true};readonly Button stop=new ChuSoneca.UI.SoftButton{Text="Parar",AutoSize=true};
  readonly Button ack=new ChuSoneca.UI.SoftButton{Text="Estou presente",AutoSize=true,Enabled=false,BackColor=Color.FromArgb(17,97,77),ForeColor=Color.White,FlatStyle=FlatStyle.Flat};
  readonly ListView controllers=new ListView{View=View.Details,FullRowSelect=true,Height=110,Dock=DockStyle.Fill};
  readonly ListBox events=new ListBox{Dock=DockStyle.Fill,HorizontalScrollbar=true};
  readonly NotifyIcon tray=new NotifyIcon{Icon=SystemIcons.Information,Text="Chu Soneca",Visible=true};
  readonly Timer timer=new Timer{Interval=1000};
  ITelemetrySource sim;readonly Alarm alarm=new Alarm();double testAlarmUntil=-1;string telemetryError="";readonly Button mute=new ChuSoneca.UI.SoftButton{Text="Silenciar alarme",AutoSize=true,Enabled=false};string loginFingerprint="",simTitle="";bool closing,busy,wasMonitoring,loading=true;double nextPoll;int epoch;
  public string HostSessionId {get;private set;}="";
  public Action<Action<bool,string>> DisconnectRequested,ReconnectRequested;
  public string SelectedCid=>monitor.SelectedCid;
  public double Now=>1000+clock.Elapsed.TotalSeconds;
  readonly bool xpilot;
  public string ClientName=>xpilot?"xPilot":"vPilot";
  public Func<bool> ExternalSessionValid;
  public FlightWindow(bool isDemo,bool useXpilot=false){
   xpilot=useXpilot;
   demo=isDemo;Text="Chu Soneca"+(demo?" · demonstração":" · "+ClientName);Font=new Font("Segoe UI",10);BackColor=Color.FromArgb(246,248,249);ForeColor=Color.FromArgb(26,37,42);
   Button test,retry;BuildLayout(out test,out retry);AdaptClientLabels(this);InitializeUpdates();
   retry.Click+=(s,e)=>{if(!demo){monitor.InvalidateCoverage();monitor.Telemetry=null;telemetryError="";nextPoll=0;sim?.Open();Log("Nova tentativa manual de leitura do simulador.");Render();}};
   mute.Click+=(s,e)=>{alarm.Stop();testAlarmUntil=-1;Render();};
   LoadHistory();
   LoadSettings();loading=false;
   openWithVpilot.CheckedChanged+=(s,e)=>SaveSettings();closeToTray.CheckedChanged+=(s,e)=>SaveSettings();disableAutoReconnect.CheckedChanged+=(s,e)=>{if(loading)return;monitor.DisableAutoReconnect=disableAutoReconnect.Checked;SaveSettings();Log(disableAutoReconnect.Checked?"Reconexão automática desativada. Reconecte manualmente.":"Reconexão automática habilitada após sair 15 NM e confirmar área livre.");Render();};
   start.Click+=(s,e)=>{if(!monitor.Connected||!System.Text.RegularExpressions.Regex.IsMatch(monitor.SessionCid??"","^[1-9][0-9]{3,9}$")){MessageBox.Show(this,"Aguarde a conexão do vPilot para identificar o CID automaticamente.");return;}
    monitor.SelectedCid=monitor.SessionCid;monitor.Start();epoch++;nextPoll=0;Log(monitor.Status);SaveSettings();};
   stop.Click+=(s,e)=>{EndHistory();monitor.Stop();epoch++;Log("Monitoramento parado pelo usuário.");Render();};
   ack.Click+=(s,e)=>{monitor.Acknowledge(Now);Log(monitor.Status);Render();};
   test.Click+=(s,e)=>{testAlarmUntil=Now+10;Notify("Teste de alarme", "Teste por 10 segundos. Nenhuma acao no vPilot.");};
   tray.DoubleClick+=(s,e)=>Restore();tray.BalloonTipClicked+=(s,e)=>Restore();
   var menu=new ContextMenuStrip();menu.Items.Add("Abrir Chu Soneca",null,(s,e)=>Restore());menu.Items.Add("Parar monitoramento",null,(s,e)=>{monitor.Stop();epoch++;Render();});menu.Items.Add("Encerrar Chu Soneca",null,(s,e)=>Shutdown());tray.ContextMenuStrip=menu;
   FormClosing+=(s,e)=>{if(!closing&&!demo&&closeToTray.Checked&&e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();tray.ShowBalloonTip(4000,"Chu Soneca","Monitoramento continua na bandeja. Use Encerrar para sair.",ToolTipIcon.Info);}else Cleanup();};
   Shown+=(s,e)=>{if(!demo){sim=xpilot?(ITelemetrySource)new XPlaneTelemetry(()=>Now,this):new TelemetryClient(()=>Now,this,Path.Combine(assets,"FlightWatch.Telemetry.exe"));sim.Received=t=>{var previous=monitor.Telemetry;if(simTitle!=""&&simTitle!=t.Aircraft){monitor.Stop();epoch++;Log("Aeronave alterada. Automação cancelada.");}if(previous!=null&&t.Received-previous.Received<10){double lat=(t.Latitude-previous.Latitude)*60,lon=(t.Longitude-previous.Longitude)*60*Math.Cos(t.Latitude*Math.PI/180);if(Math.Sqrt(lat*lat+lon*lon)>5){monitor.Stop();epoch++;Log("Salto de posição detectado. Automação cancelada.");}}bool recovered=monitor.Telemetry==null||!monitor.Telemetry.Fresh(Now);simTitle=t.Aircraft;monitor.Telemetry=t;telemetryError="";if(recovered){nextPoll=0;Log("Telemetria recebida. Avaliação retomada automaticamente.");}};sim.Error=m=>{monitor.InvalidateCoverage();monitor.Telemetry=null;telemetryError=m;Log("Telemetria interrompida: "+m);Render();};sim.Open();}else DemoSetup();timer.Start();};
   timer.Tick+=async(s,e)=>{try{await Tick();}catch(Exception ex){monitor.Stop();epoch++;alarm.Stop();Log("Falha tratada: "+ex.Message);Render();}};Render();
  }
  void AdaptClientLabels(Control parent){if(!xpilot)return;foreach(Control c in parent.Controls){c.Text=c.Text.Replace("vPilot","xPilot");AdaptClientLabels(c);}}
  void Changed(){if(loading)return;EndHistory();monitor.Stop();epoch++;monitor.ApplyFlightPlan(null);nextPoll=0;Log("Configuração alterada. Reinicie o monitoramento.");SaveSettings();Render();}
  void LoadSettings(){try{var p=Preferences.Read();simbrief.Text=p.Simbrief;openWithVpilot.Checked=p.OpenWithVpilot;closeToTray.Checked=p.CloseToTray;disableAutoReconnect.Checked=p.DisableAutoReconnect;monitor.DisableAutoReconnect=p.DisableAutoReconnect;}catch{}}
  void SaveSettings(){if(loading||demo)return;try{Directory.CreateDirectory(dataDir);File.WriteAllText(Path.Combine(dataDir,"preferences.json"),json.Serialize(new Preferences{Simbrief=simbrief.Text.Trim(),Cid="",OpenWithVpilot=openWithVpilot.Checked,CloseToTray=closeToTray.Checked,DisableAutoReconnect=monitor.DisableAutoReconnect}));}catch(Exception e){Log("Não foi possível salvar: "+e.Message);}}
  string Fingerprint(){var config=Path.Combine(Directory.GetParent(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)).FullName,"vPilotConfig.xml");
   using(var reader=XmlReader.Create(config,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null})){
    while(reader.Read()){if(reader.NodeType!=XmlNodeType.Element||reader.Name!="NetworkLogin")continue;string login=reader.ReadElementContentAsString();if(string.IsNullOrWhiteSpace(login))return "";using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(login)));}return "";
   }}
  public bool AccountUnchanged(){if(demo)return true;if(xpilot)return ExternalSessionValid?.Invoke()==true;try{return loginFingerprint!=""&&Fingerprint()==loginFingerprint;}catch{return false;}}
  public void NetworkConnected(string id,string call,string type,string selcal,bool observer,string hostSession="demo"){if(!monitor.AwaitingReconnect)EndHistory();epoch++;HostSessionId=hostSession;routeValidated=false;if(!xpilot)try{loginFingerprint=Fingerprint();}catch{loginFingerprint="";}monitor.SelectedCid=id??"";cid.Text=monitor.SelectedCid;monitor.Connect(monitor.SelectedCid,call,type,Now);if(observer){monitor.Stop();monitor.Status="Modo observador: automação bloqueada.";}Log("vPilot conectado: "+call+" · CID "+id);nextPoll=0;Render();}
  public Telemetry CurrentTelemetry=>monitor.Telemetry;
  public bool CanRequest(bool reconnect)=>monitor.CanCommand(reconnect,Now,AccountUnchanged());
  public void NetworkDisconnected(){epoch++;monitor.Disconnected(Now);Log(monitor.Status);nextPoll=Now;Render();}
  async Task Tick(){
   if(closing)return;
   sim?.Tick();
   if(demo){monitor.Telemetry.Received=Now;}

   var effect=monitor.Tick(Now,AccountUnchanged());
   if(effect==Effect.DescentAlert){Log("Descida detectada: prazo de 300 segundos iniciado.");Notify("Descida detectada", "Confirme presenca em 5 minutos. Sem resposta: desconexao sem retorno automatico.");}
   if(effect==Effect.Alert){Log("Alerta ATC: prazo de 120 segundos iniciado.");Notify("Controlador na área ou aproximação","Confirme presença em até 2 minutos. Entrada monitorada a 15 NM.");}

   DispatchEffect(effect);
   Render();
   if((monitor.Armed||(monitor.Connected&&monitor.SelectedCid==monitor.SessionCid))&&!busy&&Now>=nextPoll){nextPoll=Now+60;await Poll();}
  }
  void DispatchEffect(Effect effect){
   if(effect==Effect.Disconnect||effect==Effect.Reconnect){
    Log(monitor.Status);
    var callback=effect==Effect.Disconnect?DisconnectRequested:ReconnectRequested;
    if(demo){if(effect==Effect.Disconnect)NetworkDisconnected();else NetworkConnected(monitor.SelectedCid,"DEMO123","A320","",false);}
    else if(callback==null){monitor.Stop();Log("Plugin sem conexão com o broker.");}
    else callback((ok,error)=>{if(!ok&&!IsDisposed)BeginInvoke(new Action(()=>{monitor.Stop();Log("Ação bloqueada: "+error);Render();}));});
   }
  }
  async Task Poll(){
   if(demo){monitor.Coverage=new Coverage{ok=true,Received=Now,Sampled=Now,controllers=new[]{new Controller{cid="123456",callsign="DEMO_CTR",frequency="125.000",logon_time="demo"}}};monitor.Coverage.boundary=new Boundary{ready=true,exitNm=monitor.ExitMarginNm,inside=true,controllers=monitor.Coverage.controllers};return;}
   var t=monitor.Telemetry;if(t==null||!t.Fresh(Now))return;
   busy=true;int requestEpoch=epoch;double sampled=Now;
   try{
    var request=json.Serialize(new {cid=monitor.SelectedCid,callsign=monitor.Callsign,sessionId=monitor.SessionId,route=importedRoute,routeValidated=routeValidated,previousBoundary=monitor.BoundaryState,exitNm=monitor.ExitMarginNm,sampleTimeUtc=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),longitude=t.Longitude,latitude=t.Latitude,flightLevel=t.Altitude/100,cache=Path.Combine(dataDir,"cache")});
    var result=await Task.Run(()=>RunEngine(request));
    if(closing||IsDisposed||requestEpoch!=epoch||(!monitor.Armed&&!monitor.Connected))return;
    var response=json.Deserialize<Coverage>(result);response.Received=Now;response.Sampled=sampled;
    monitor.Coverage=response;if(response.ok){routeValidated=true;routeLabel.Text=response.routeStatus??"Rota validada";monitor.BoundaryState=response.boundary?.state;var previousPlan=monitor.PlanStatus;monitor.ApplyFlightPlan(response.flightPlan);if(previousPlan!=monitor.PlanStatus)Log("Nível automático: "+monitor.PlanStatus);}if(monitor.Armed){history.Observe(response,DateTime.UtcNow,monitor.Callsign);SaveHistory();}
    controllers.Items.Clear();foreach(var c in response.boundary?.controllers??new Controller[0])controllers.Items.Add(new ListViewItem(new[]{c.callsign,c.frequency,c.cid}));
    if(!response.ok){nextPoll=Now+feedRecovery.Fail(Now);routeLabel.Text=response.error;Log("Consulta falhou: "+response.error);}
    else {feedRecovery.Reset();nextPoll=Math.Max(Now,sampled+60);Log("Consulta concluída: "+response.controllers.Length+" controlador(es) na posição.");if(response.unmapped.Length>0)Log("Retorno bloqueado enquanto houver posições sem geometria: "+string.Join(", ",response.unmapped.Take(8)));}
   }catch(Exception e){if(!closing&&!IsDisposed&&requestEpoch==epoch){monitor.InvalidateCoverage();nextPoll=Now+feedRecovery.Fail(Now);Log("Consulta indisponível: "+e.Message);}}
   finally{busy=false;if(!closing&&!IsDisposed)Render();}
  }
  string RunEngine(string request){
   var startInfo=new ProcessStartInfo(Path.Combine(assets,"node.exe"),"\""+Path.Combine(assets,"coverage.cjs")+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
   using(var p=new Process{StartInfo=startInfo}){
    lock(workerGate){if(closing)throw new OperationCanceledException();p.Start();workers.Add(p);}
    try{
    var output=p.StandardOutput.ReadToEndAsync();var errors=p.StandardError.ReadToEndAsync();p.StandardInput.Write(request);p.StandardInput.Close();
    if(!p.WaitForExit(70000)){try{p.Kill();}catch{}throw new TimeoutException("Fontes não responderam em 70 s.");}
    Task.WaitAll(output,errors);return output.Result;
    }finally{lock(workerGate)workers.Remove(p);}
   }
  }
  void Notify(string title,string text){try{alarm.Start();}catch(Exception e){Log("Audio indisponivel: "+e.Message);}tray.ShowBalloonTip(10000,title,text,ToolTipIcon.Warning);Restore();}
  public void Restore(){Show();WindowState=FormWindowState.Normal;Activate();}
  void Log(string text){if(xpilot)text=text.Replace("vPilot","xPilot");events.Items.Insert(0,DateTime.UtcNow.ToString("HH:mm:ss")+"Z"+"  "+text);while(events.Items.Count>120)events.Items.RemoveAt(events.Items.Count-1);if(!demo)try{Directory.CreateDirectory(dataDir);var file=Path.Combine(dataDir,"diagnostic.log");if(File.Exists(file)&&new FileInfo(file).Length>1000000)File.WriteAllText(file,"");File.AppendAllText(file,DateTime.UtcNow.ToString("s")+"Z"+" "+text+Environment.NewLine);}catch{}}
  void Render(){planLevel.Text=monitor.PlanStatus;if(wasMonitoring&&!monitor.Armed)EndHistory();wasMonitoring=monitor.Armed;zulu.Text=DateTime.UtcNow.ToString("yyyy-MM-dd   HH:mm:ss")+" Z";countdown.Text=monitor.Deadline>Now?TimeSpan.FromSeconds(Math.Ceiling(monitor.Deadline-Now)).ToString(@"mm\:ss"):(monitor.Telemetry!=null&&monitor.Telemetry.Fresh(Now)&&monitor.Telemetry.OnGround?"No solo":"");if(monitor.Deadline<=Now&&testAlarmUntil<=Now)alarm.Stop();mute.Enabled=alarm.Playing;status.Text=monitor.Status;var boundary=monitor.Coverage?.boundary;if(boundary!=null)details.AccessibleDescription="Limite horizontal: "+(boundary.inside?"dentro da área":boundary.distance?.ToString("0.0")+" NM");session.Text=monitor.SessionCid==""?"Aguardando conexão vPilot":"vPilot: "+(monitor.Connected?"conectado":"desconectado")+" · "+monitor.Callsign+" · CID "+monitor.SessionCid;
   var t=monitor.Telemetry;details.Text=t==null?(telemetryError==""?"Simulador: aguardando telemetria local":"Recuperando telemetria: "+telemetryError):$"FL atual {t.Altitude/100:0.0}  ·  V/S {t.VerticalSpeed:0} pés/min  ·  {t.Latitude:0.000}, {t.Longitude:0.000}";
   if(boundary?.ready==true)details.Text+=" · "+(boundary.inside?"Dentro da área ATC":boundary.distance.HasValue?boundary.distance.Value.ToString("0.0")+" NM do limite ATC":"Sem área ATC mapeada");
   timing.Text=busy?"Consultando VATSIM, VATSpy, SimAware e VATGlasses…":monitor.Armed?"Próxima consulta em "+Math.Max(0,Math.Ceiling(nextPoll-Now))+" s":"CID identificado automaticamente ao conectar o vPilot.";
   ack.Enabled=monitor.Deadline>Now;start.Enabled=!monitor.Armed&&monitor.Connected&&System.Text.RegularExpressions.Regex.IsMatch(monitor.SessionCid??"","^[1-9][0-9]{3,9}$");stop.Enabled=monitor.Armed;ApplyStatusColors();
   var rows=monitor.Coverage?.boundary?.controllers??new Controller[0];
   var signature=string.Join("|",rows.Select(c=>c.Key+c.frequency));
   if((string)controllers.Tag!=signature){controllers.BeginUpdate();controllers.Items.Clear();foreach(var c in rows)controllers.Items.Add(new ListViewItem(new[]{c.callsign,c.frequency,c.cid}));controllers.Tag=signature;controllers.EndUpdate();}
  }
  void ApplyStatusColors(){
   var c=monitor.Coverage;var t=monitor.Telemetry;
   bool fresh=c!=null&&c.ok&&c.boundary?.ready==true&&Now-c.Received<=75&&Now-c.Sampled<=85&&c.dataAgeSeconds+Now-c.Received<=90;
   bool telemetry=t!=null&&t.Fresh(Now);bool alert=monitor.Deadline>Now;
   var connected=monitor.Connected;
   cid.Text=connected?"CID "+monitor.SessionCid:"Aguardando conexão vPilot";
   cid.ForeColor=session.ForeColor=connected?ChuSoneca.UI.NightTheme.Green:ChuSoneca.UI.NightTheme.Red;
   planLevel.ForeColor=!connected?ChuSoneca.UI.NightTheme.Red:monitor.CruiseFL>0?ChuSoneca.UI.NightTheme.Green:ChuSoneca.UI.NightTheme.Yellow;
   details.ForeColor=!connected?ChuSoneca.UI.NightTheme.Red:telemetry?ChuSoneca.UI.NightTheme.Green:ChuSoneca.UI.NightTheme.Yellow;
   routeLabel.ForeColor=routeValidated?ChuSoneca.UI.NightTheme.Green:ChuSoneca.UI.NightTheme.Yellow;
   if(connected&&monitor.Armed&&importedRoute==null&&!demo)status.Text="Importe a rota do SimBrief em Preferências";
   bool ready=connected&&monitor.Armed&&monitor.CruiseFL>0&&fresh&&telemetry&&monitor.AirborneSafe(Now)&&!alert;
   status.ForeColor=!connected?ChuSoneca.UI.NightTheme.Red:ready?ChuSoneca.UI.NightTheme.Green:ChuSoneca.UI.NightTheme.Yellow;
   if(!connected&&!monitor.Armed)status.Text="Aguardando conexão vPilot";
   countdown.ForeColor=ChuSoneca.UI.NightTheme.Yellow;
   timing.ForeColor=!connected?ChuSoneca.UI.NightTheme.Red:busy||!ready?ChuSoneca.UI.NightTheme.Yellow:ChuSoneca.UI.NightTheme.Green;
   if(!connected&&!monitor.Armed)timing.Text="Conecte o vPilot. O CID será identificado automaticamente.";
   else if(connected&&!monitor.Armed)timing.Text="Conexão identificada. Inicie o monitoramento quando estiver pronto.";
   ChuSoneca.UI.NightTheme.Action(start,ChuSoneca.UI.NightTheme.Green);
   ChuSoneca.UI.NightTheme.Action(stop,ChuSoneca.UI.NightTheme.Red);
   ChuSoneca.UI.NightTheme.Action(ack,ChuSoneca.UI.NightTheme.Accent);
   ChuSoneca.UI.NightTheme.Action(mute,ChuSoneca.UI.NightTheme.Yellow);
   if(xpilot){status.Text=status.Text.Replace("vPilot","xPilot");session.Text=session.Text.Replace("vPilot","xPilot");timing.Text=timing.Text.Replace("vPilot","xPilot");cid.Text=cid.Text.Replace("vPilot","xPilot");}
  }
  void DemoSetup(){cid.Text="1234567";NetworkConnected("1234567","DEMO123","A320","",false);monitor.Telemetry=new Telemetry{Altitude=35000,GroundSpeed=450,Received=Now,Latitude=-23,Longitude=-47};monitor.ApplyFlightPlan(new Plan{cid="1234567",callsign="DEMO123",altitude="35000"});monitor.Start();monitor.StableSince=Now-121;monitor.Coverage=new Coverage{ok=true,Received=Now,Sampled=Now,controllers=new[]{new Controller{cid="123456",callsign="DEMO_CTR",frequency="125.000",logon_time="demo"}}};monitor.Coverage.boundary=new Boundary{ready=true,exitNm=monitor.ExitMarginNm,inside=true,controllers=monitor.Coverage.controllers};nextPoll=Now+60;}
  public void RenderPreview(string file,string mode="--render"){
   if(mode=="--render-update")ShowUpdate(new Version(0,7,3));
   if(!monitor.Connected)DemoSetup();monitor.Tick(Now,true);monitor.Tick(Now,true);
   var atc=new Controller{cid="123456",callsign="DEMO_CTR",frequency="125.000",logon_time=DateTime.UtcNow.AddHours(-1).ToString("o")};
   history.Observe(new Coverage{ok=true,controllers=new[]{atc},onlineControllers=new[]{atc}},DateTime.UtcNow.AddMinutes(-20),"DEMO123");
   history.Observe(new Coverage{ok=true,controllers=new Controller[0],onlineControllers=new[]{atc}},DateTime.UtcNow,"DEMO123");RenderHistory();
   if(mode=="--render-ground"){monitor.Telemetry.OnGround=true;monitor.Telemetry.Altitude=60;monitor.Telemetry.GroundSpeed=0;monitor.Tick(Now,true);}
   if(mode=="--render-descent"){monitor.Telemetry.Altitude=34000;monitor.Telemetry.VerticalSpeed=-800;monitor.DescentTriggered=true;monitor.Deadline=Now+300;monitor.Status="Descida detectada. Confirme em 5 minutos.";}
   if(mode=="--render-disconnected"){monitor.Stop();monitor.Connected=false;}
   if(mode=="--render-waiting"){monitor.InvalidateCoverage();monitor.Status="Aguardando dados da VATSIM…";}
   if(mode=="--render-ready"){monitor.Acknowledge(Now);monitor.Tick(Now,true);}
   if(mode=="--render-start"){monitor.Stop();}
   if(mode=="--render-history")tabs.SelectedIndex=1;
   if(mode=="--render-settings")tabs.SelectedIndex=3;
   Render();using(var bitmap=new Bitmap(Width,Height)){DrawToBitmap(bitmap,new Rectangle(Point.Empty,Size));bitmap.Save(file,System.Drawing.Imaging.ImageFormat.Png);}
  }
  void Cleanup(){if(closing)return;closing=true;epoch++;StopUpdates();timer.Stop();monitor.Stop();EndHistory();alarm.Stop();sim?.Dispose();lock(workerGate){foreach(var p in workers)try{if(!p.HasExited)p.Kill();}catch{}}tray.Visible=false;tray.Dispose();}
  public void Shutdown(){Cleanup();Close();}
  protected override void Dispose(bool disposing){if(disposing){Cleanup();timer.Dispose();alarm.Dispose();sim?.Dispose();tray.Dispose();}base.Dispose(disposing);}
 }
}
