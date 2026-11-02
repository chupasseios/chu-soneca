using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace ChuSoneca.UI {
 public static class NightTheme {
  public static readonly Color Background=Color.FromArgb(19,23,29),Surface=Color.FromArgb(28,34,42),Raised=Color.FromArgb(39,47,57),Ink=Color.FromArgb(232,237,243),Muted=Color.FromArgb(167,181,198),Accent=Color.FromArgb(141,183,247),AccentInk=Color.FromArgb(17,35,60),Green=Color.FromArgb(132,217,174);
  public static readonly Color Red=Color.FromArgb(255,151,158),Yellow=Color.FromArgb(245,204,112);
  public static void Action(Button b,Color color){b.BackColor=color;b.ForeColor=Background;b.FlatAppearance.MouseOverBackColor=ControlPaint.Light(color,.12f);b.FlatAppearance.MouseDownBackColor=ControlPaint.Dark(color,.08f);}
  [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
  public static void Window(Form f){f.HandleCreated+=(s,e)=>{try{int value=1;DwmSetWindowAttribute(f.Handle,20,ref value,4);}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}};}
  public static void Round(Control c,int radius=12){Action update=()=>{if(c.Width<2||c.Height<2)return;int d=Math.Min(radius*2,Math.Min(c.Width,c.Height));using(var p=new GraphicsPath()){p.AddArc(0,0,d,d,180,90);p.AddArc(c.Width-d,0,d,d,270,90);p.AddArc(c.Width-d,c.Height-d,d,d,0,90);p.AddArc(0,c.Height-d,d,d,90,90);p.CloseFigure();var old=c.Region;c.Region=new Region(p);old?.Dispose();}};c.SizeChanged+=(s,e)=>update();update();}
  public static void Button(Button b,bool primary=false){b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderSize=0;b.BackColor=primary?Accent:Raised;b.ForeColor=primary?AccentInk:Ink;b.FlatAppearance.MouseOverBackColor=primary?Color.FromArgb(171,204,252):Color.FromArgb(53,64,78);b.FlatAppearance.MouseDownBackColor=primary?Color.FromArgb(116,163,232):Color.FromArgb(62,75,91);b.UseVisualStyleBackColor=false;b.Padding=new Padding(12,7,12,7);b.Cursor=Cursors.Hand;if(b.Tag as string!="night-button"){b.Tag="night-button";Round(b,9);}}
  public static void List(ListView list){list.BackColor=Surface;list.ForeColor=Ink;list.OwnerDraw=true;list.HideSelection=false;list.DrawColumnHeader+=(s,e)=>{using(var brush=new SolidBrush(Raised))e.Graphics.FillRectangle(brush,e.Bounds);TextRenderer.DrawText(e.Graphics,e.Header.Text,list.Font,Rectangle.Inflate(e.Bounds,-8,0),Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);};list.DrawItem+=(s,e)=>{if(list.View!=View.Details)e.DrawDefault=true;};list.DrawSubItem+=(s,e)=>{var bg=e.Item.Selected?Color.FromArgb(47,67,91):Surface;using(var brush=new SolidBrush(bg))e.Graphics.FillRectangle(brush,e.Bounds);TextRenderer.DrawText(e.Graphics,e.SubItem.Text,list.Font,Rectangle.Inflate(e.Bounds,-8,0),Ink,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);if(e.Item.Focused&&list.Focused)ControlPaint.DrawFocusRectangle(e.Graphics,e.Bounds,Ink,bg);};}
 }
 public sealed class SoftButton:Button {
  bool hover,pressed;
  public SoftButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
  protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
  protected override void OnMouseLeave(EventArgs e){hover=pressed=false;Invalidate();base.OnMouseLeave(e);}
  protected override void OnMouseDown(MouseEventArgs e){pressed=true;Invalidate();base.OnMouseDown(e);}
  protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
  protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Parent?.BackColor??NightTheme.Background);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var path=SoftFlow.Shape(Width-1,Height-1,9)){using(var brush=new SolidBrush(!Enabled?NightTheme.Raised:pressed?FlatAppearance.MouseDownBackColor:hover?FlatAppearance.MouseOverBackColor:BackColor))e.Graphics.FillPath(brush,path);if(Focused&&ShowFocusCues)using(var pen=new Pen(NightTheme.Accent,2))e.Graphics.DrawPath(pen,path);}TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Enabled?ForeColor:NightTheme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);}
 }
 public sealed class SoftFlow:FlowLayoutPanel {
  public SoftFlow(){DoubleBuffered=true;}
  internal static GraphicsPath Shape(int w,int h,int r){int d=Math.Max(1,Math.Min(r*2,Math.Min(w,h)));var p=new GraphicsPath();p.AddArc(0,0,d,d,180,90);p.AddArc(w-d,0,d,d,270,90);p.AddArc(w-d,h-d,d,d,0,90);p.AddArc(0,h-d,d,d,90,90);p.CloseFigure();return p;}
  protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent?.BackColor??NightTheme.Background);if(Width<2||Height<2)return;e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var p=Shape(Width-1,Height-1,12))using(var b=new SolidBrush(BackColor))e.Graphics.FillPath(b,p);}
 }
 public sealed class NightTabs:UserControl {
  readonly FlowLayoutPanel navigation=new FlowLayoutPanel{Dock=DockStyle.Top,Height=48,WrapContents=false};
  readonly Panel body=new Panel{Dock=DockStyle.Fill};
  Panel[] pages=new Panel[0];int selected;
  public NightTabs(){BackColor=NightTheme.Background;Controls.Add(body);Controls.Add(navigation);}
  public int SelectedIndex{get=>selected;set{if(value<0||value>=pages.Length)return;selected=value;for(int i=0;i<pages.Length;i++){pages[i].Visible=i==value;NightTheme.Button((Button)navigation.Controls[i],i==value);}pages[value].BringToFront();}}
  public void AddPages(Panel[] items){pages=items;for(int i=0;i<items.Length;i++){int index=i;var b=new SoftButton{Text=items[i].Text,AutoSize=true,Margin=new Padding(0,0,8,0),AccessibleName=items[i].Text};NightTheme.Button(b);b.Click+=(s,e)=>SelectedIndex=index;navigation.Controls.Add(b);items[i].Dock=DockStyle.Fill;body.Controls.Add(items[i]);}SelectedIndex=0;}
  protected override bool ProcessCmdKey(ref Message msg,Keys key){if(key==(Keys.Control|Keys.Tab)||key==(Keys.Control|Keys.Shift|Keys.Tab)){SelectedIndex=(selected+(key.HasFlag(Keys.Shift)?pages.Length-1:1))%pages.Length;navigation.Controls[selected].Focus();return true;}return base.ProcessCmdKey(ref msg,key);}
 }
}
