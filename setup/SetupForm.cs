using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using ChuSoneca.UI;
namespace FlightWatchSetup {
 public sealed class SetupForm:Form {
  readonly Button install=new SoftButton{Text="Instalar",AutoSize=true,Enabled=false},update=new SoftButton{Text="Atualizar",AutoSize=true,Enabled=false},uninstall=new SoftButton{Text="Desinstalar",AutoSize=true,Enabled=false};
  readonly CheckBox accept=new CheckBox{Text="Li os termos de uso não comercial.",AutoSize=true};
  readonly TextBox folder=new TextBox{Dock=DockStyle.Fill,BackColor=NightTheme.Raised,ForeColor=NightTheme.Ink,BorderStyle=BorderStyle.FixedSingle};
  readonly LinkLabel browse=new LinkLabel{Text="Alterar pasta",AutoSize=true,LinkColor=NightTheme.Accent};
  readonly Label title=new Label{Text="Vamos preparar seu voo",AutoSize=true,Font=new Font("Segoe UI",21,FontStyle.Bold)};
  readonly Label stateTitle=new Label{AutoSize=true,Font=new Font("Segoe UI",13,FontStyle.Bold)};
  readonly Label message=new Label{AutoSize=true,MaximumSize=new Size(425,0),Margin=new Padding(0,10,0,0)};
  readonly FlowLayoutPanel state=new SoftFlow{Dock=DockStyle.Fill,Padding=new Padding(18),FlowDirection=FlowDirection.TopDown,WrapContents=false};
  readonly ProgressBar progress=new ProgressBar{Dock=DockStyle.Fill,Minimum=0,Maximum=100,Value=0,Style=ProgressBarStyle.Continuous};
  readonly Label percentage=new Label{Text="0%",AutoSize=true,ForeColor=NightTheme.Muted};
  readonly Label version=new Label{AutoSize=true,ForeColor=NightTheme.Muted,MaximumSize=new Size(470,0)};
  bool busy,preview,checking,installed,verified;string installedVersion;
  public static bool Newer(string current,string package){System.Version a,b;return System.Version.TryParse(current,out a)&&System.Version.TryParse(package,out b)&&b>a;}
  public SetupForm(){Text="Chu Soneca · Instalação "+Install.Version;ClientSize=new Size(820,640);MinimumSize=new Size(836,679);Font=new Font("Segoe UI",10);ForeColor=NightTheme.Ink;BackColor=NightTheme.Background;StartPosition=FormStartPosition.CenterScreen;AutoScaleMode=AutoScaleMode.Dpi;NightTheme.Window(this);
   var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,240));root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));Controls.Add(root);
   var rail=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(24,32,20,24),Margin=Padding.Empty,BackColor=NightTheme.Surface};root.Controls.Add(rail);
   rail.Controls.Add(new Label{Text="Chu\nSoneca",AutoSize=true,Font=new Font("Segoe UI",30,FontStyle.Bold),Margin=new Padding(0,0,0,18)});rail.Controls.Add(new Label{Text="Mais atenção ao seu voo.",AutoSize=true,ForeColor=NightTheme.Muted,MaximumSize=new Size(190,0)});
   rail.Controls.Add(new Label{Text="Instalar\n\nAtualizar\n\nDesinstalar",AutoSize=true,Font=new Font("Segoe UI",12),Margin=new Padding(0,56,0,0)});rail.Controls.Add(new Label{Text="Pacote "+Install.Version+"\nPara este usuário",AutoSize=true,ForeColor=NightTheme.Muted,Margin=new Padding(0,90,0,0)});
   var main=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(30,30,26,24),RowCount=10,ColumnCount=1};foreach(int h in new[]{66,52,25,38,30,66,145,50,55})main.RowStyles.Add(new RowStyle(SizeType.Absolute,h));main.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.Controls.Add(main,1,0);
   main.Controls.Add(title,0,0);main.Controls.Add(version,0,1);main.Controls.Add(new Label{Text="Pasta do vPilot",AutoSize=true},0,2);folder.Text=Install.Detect();main.Controls.Add(folder,0,3);main.Controls.Add(browse,0,4);
   var terms=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false};terms.Controls.Add(accept);var link=new LinkLabel{Text="Ler termos e licenças",AutoSize=true,LinkColor=NightTheme.Accent};terms.Controls.Add(link);main.Controls.Add(terms,0,5);
   state.Controls.Add(stateTitle);state.Controls.Add(message);main.Controls.Add(state,0,6);
   var meter=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2,ColumnCount=1,Padding=new Padding(0,5,0,8)};meter.RowStyles.Add(new RowStyle(SizeType.Absolute,20));meter.RowStyles.Add(new RowStyle(SizeType.Percent,100));meter.Controls.Add(percentage,0,0);meter.Controls.Add(progress,0,1);main.Controls.Add(meter,0,7);
   var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};foreach(var b in new[]{install,update,uninstall}){NightTheme.Button(b,b!=uninstall);b.Margin=new Padding(0,0,8,0);actions.Controls.Add(b);}main.Controls.Add(actions,0,8);
   main.Controls.Add(new Label{Text="Feche o vPilot antes de instalar, atualizar ou remover.\nO simulador pode continuar aberto.",AutoSize=true,ForeColor=NightTheme.Muted,Font=new Font("Segoe UI",9)},0,9);
   accept.CheckedChanged+=(s,e)=>Buttons();
   browse.LinkClicked+=(s,e)=>{using(var d=new FolderBrowserDialog{SelectedPath=folder.Text,Description="Pasta que contém vPilot.exe"})if(d.ShowDialog(this)==DialogResult.OK)folder.Text=d.SelectedPath;};
   link.LinkClicked+=(s,e)=>MessageBox.Show(this,"Chu Soneca é uma versão de testes. Inclui código do VATSIM Radar sob CC BY-NC 4.0: uso não comercial com atribuição. Licenças e código-fonte acompanham a instalação.","Termos e licenças");
   Shown+=async(s,e)=>{if(!preview)await RefreshInstallation();};
   install.Click+=async(s,e)=>await Run(false);update.Click+=async(s,e)=>await Run(false);
   uninstall.Click+=async(s,e)=>{if(MessageBox.Show(this,"Desinstalar o Chu Soneca? Preferências e histórico serão preservados.","Chu Soneca",MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes)await Run(true);};
   FormClosing+=(s,e)=>{if(busy)e.Cancel=true;};Ready();Buttons();
  }
  void Buttons(){bool available=!busy&&!checking;install.Enabled=available&&accept.Checked&&(!installed||(!verified&&!Newer(installedVersion,Install.Version)&&installedVersion==Install.Version));update.Enabled=available&&accept.Checked&&installed&&Newer(installedVersion,Install.Version);uninstall.Enabled=available&&installed;accept.Enabled=folder.Enabled=browse.Enabled=available;}
  async Task RefreshInstallation(){checking=true;Buttons();installed=File.Exists(Install.ReceiptPath);using(var key=Registry.CurrentUser.OpenSubKey(Install.RegPath))installedVersion=key?.GetValue("DisplayVersion") as string;string path=folder.Text;verified=installed&&await Task.Run(()=>Install.Verified(path));if(IsDisposed)return;checking=false;Ready();Buttons();}
  void Ready(){version.Text="Pacote disponível: "+Install.Version+"\nInstalada: "+(installed?installedVersion??"não identificada":"nenhuma");string heading=installed?(Newer(installedVersion,Install.Version)?"Atualização disponível":verified?"Você está na versão atual":"Instalação encontrada"):"Pronto para instalar";SetState(heading,installed?(Newer(installedVersion,Install.Version)?"Aceite os termos para atualizar com este pacote.":"Você pode desinstalar por aqui. Atualizar será habilitado quando o pacote for mais recente."):"Aceite os termos para habilitar a instalação.",false);}
  void SetState(string heading,string text,bool success){state.BackColor=success?Color.FromArgb(29,51,43):Color.FromArgb(32,45,63);stateTitle.ForeColor=success?NightTheme.Green:NightTheme.Accent;stateTitle.Text=heading;message.Text=text;}
  void Report(int value,string text){if(IsDisposed)return;if(InvokeRequired){Invoke(new Action(()=>Report(value,text)));return;}progress.Value=Math.Max(progress.Value,Math.Min(100,Math.Max(0,value)));percentage.Text=progress.Value+"%";message.Text=text;}
  async Task Run(bool remove){if(busy||checking||(!remove&&!accept.Checked))return;busy=true;Buttons();progress.Value=0;percentage.Text="0%";title.Text=remove?"Removendo Chu Soneca":"Preparando Chu Soneca";SetState(remove?"Desinstalando…":"Instalando…","Verificando requisitos…",false);string path=folder.Text.Trim();
   try{await Task.Run(()=>{if(remove)Install.Remove(Report);else Install.Execute(path,Report);});busy=false;await RefreshInstallation();title.Text=remove?"Chu Soneca removido":"Chu Soneca instalado";SetState(remove?"Desinstalação concluída":"Instalação concluída",remove?"Preferências e histórico foram preservados.":"Os componentes foram instalados e verificados. Abra o vPilot normalmente.",true);Report(100,message.Text);
   }catch(Exception e){title.Text="Não foi possível concluir";state.BackColor=Color.FromArgb(62,35,38);stateTitle.ForeColor=Color.FromArgb(255,166,158);stateTitle.Text="Operação interrompida";message.Text=e.Message;}finally{busy=false;Buttons();}
  }
  public void Preview(bool success){preview=true;installed=verified=success;installedVersion=success?Install.Version:null;Ready();Buttons();if(success){title.Text="Chu Soneca instalado";SetState("Instalação concluída","Prévia visual. Nenhum arquivo foi instalado nesta demonstração.",true);Report(100,message.Text);}}
  internal static void TestControls(){using(var f=new SetupForm()){f.Preview(false);if(f.install.Enabled)throw new Exception("Install enabled without consent");f.accept.Checked=true;if(!f.install.Enabled)throw new Exception("Consent did not enable install");f.accept.Checked=false;if(f.install.Enabled)throw new Exception("Consent removal ignored");f.installed=true;f.installedVersion="0.5.0";f.accept.Checked=true;f.Buttons();if(!f.update.Enabled||f.install.Enabled||!f.uninstall.Enabled)throw new Exception("Update state invalid");f.accept.Checked=false;if(f.update.Enabled||!f.uninstall.Enabled)throw new Exception("Update consent gate invalid");f.Preview(true);if(f.progress.Value!=100||f.update.Enabled)throw new Exception("Completion state invalid");if(Newer("9.0.0",Install.Version)||Newer(Install.Version,Install.Version))throw new Exception("Downgrade offered");}}
 }
}
