using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

 // The screenshot is embedded so the executable does not depend on loose assets.
public sealed class PreviewModel : IDisposable {
 public Bitmap Original {get;private set;}
 public Bitmap Before {get;private set;}
 public Bitmap After {get;private set;}
 public string Error {get;private set;}
 public event EventHandler Changed;
 public PreviewModel(){
  try{using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("LumaDesk.Preview.png")){
   if(stream==null)throw new IOException("Картинка предпросмотра не включена в сборку.");
   using(var image=Image.FromStream(stream)){int width=Math.Min(1280,image.Width),height=(int)Math.Round(image.Height*width/(double)image.Width);Original=new Bitmap(width,height,PixelFormat.Format24bppRgb);using(var g=Graphics.FromImage(Original)){g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(image,0,0,width,height);}}
  }}catch(Exception ex){Error=ex.Message;}
 }
 // Map desired baseline output back through the current display ramp, avoiding
 // applying the same gamma twice to the preview when desktop correction is on.
 public static byte[][] BuildTables(double[] settings,ushort[] baseline,ushort[] current,bool transformed){
  var tables=new byte[3][];
  for(int c=0;c<3;c++){tables[c]=new byte[256];for(int i=0;i<256;i++){
   int input=transformed?(int)Math.Round(LumaDesk.Curve(i/255.0,c,settings)*255):i;
   if(baseline==null||current==null){tables[c][i]=(byte)input;continue;}
   int target=baseline[c*256+input],best=0,distance=Int32.MaxValue;
   for(int j=0;j<256;j++){int delta=Math.Abs(current[c*256+j]-target);if(delta<distance){distance=delta;best=j;}}
   tables[c][i]=(byte)best;
  }}return tables;
 }
 public static Bitmap Transform(Bitmap source,byte[][] tables){
  Bitmap result=source.Clone(new Rectangle(0,0,source.Width,source.Height),PixelFormat.Format24bppRgb);
  BitmapData bits=result.LockBits(new Rectangle(0,0,result.Width,result.Height),ImageLockMode.ReadWrite,PixelFormat.Format24bppRgb);
  try{byte[] row=new byte[Math.Abs(bits.Stride)];for(int y=0;y<bits.Height;y++){IntPtr ptr=IntPtr.Add(bits.Scan0,y*bits.Stride);Marshal.Copy(ptr,row,0,row.Length);for(int x=0;x<bits.Width;x++){int offset=x*3;row[offset]=tables[2][row[offset]];row[offset+1]=tables[1][row[offset+1]];row[offset+2]=tables[0][row[offset+2]];}Marshal.Copy(row,0,ptr,row.Length);}}
  finally{result.UnlockBits(bits);}return result;
 }
 public void Update(double[] settings,ushort[] baseline,ushort[] current){
  if(Original==null)return;
  Bitmap before=Transform(Original,BuildTables(settings,baseline,current,false));Bitmap after=null;
  try{after=Transform(Original,BuildTables(settings,baseline,current,true));}catch{before.Dispose();throw;}
  if(Before!=null)Before.Dispose();if(After!=null)After.Dispose();Before=before;After=after;if(Changed!=null)Changed(this,EventArgs.Empty);
 }
 public void Dispose(){if(Original!=null)Original.Dispose();if(Before!=null)Before.Dispose();if(After!=null)After.Dispose();}
}

public sealed class ScreenshotPreview : Control {
 readonly PreviewModel model;double split=0.5;public bool Comparison=true;
 public ScreenshotPreview(PreviewModel model){this.model=model;DoubleBuffered=true;TabStop=true;AccessibleName="Предпросмотр гаммы. Стрелки меняют границу сравнения.";AccessibleRole=AccessibleRole.Graphic;BackColor=Color.FromArgb(12,16,23);Cursor=Cursors.VSplit;model.Changed+=ModelChanged;}
 void ModelChanged(object sender,EventArgs e){Invalidate();}
 Rectangle ImageRect(){if(model.Original==null)return Rectangle.Empty;double scale=Math.Min(Width/(double)model.Original.Width,Height/(double)model.Original.Height);int w=(int)(model.Original.Width*scale),h=(int)(model.Original.Height*scale);return new Rectangle((Width-w)/2,(Height-h)/2,w,h);}
 protected override void OnPaint(PaintEventArgs e){
  base.OnPaint(e);Rectangle r=ImageRect();if(r.Width<1||r.Height<1){TextRenderer.DrawText(e.Graphics,model.Error??"Загрузка предпросмотра…",Font,ClientRectangle,Color.Silver,TextFormatFlags.WordBreak|TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);return;}
  e.Graphics.InterpolationMode=InterpolationMode.HighQualityBilinear;
  e.Graphics.DrawImage(model.After??model.Original,r);
  if(Comparison){int boundary=r.Left+(int)(r.Width*split);var state=e.Graphics.Save();e.Graphics.SetClip(new Rectangle(r.Left,r.Top,boundary-r.Left,r.Height));e.Graphics.DrawImage(model.Before??model.Original,r);e.Graphics.Restore(state);using(var pen=new Pen(Color.FromArgb(133,240,208),2))e.Graphics.DrawLine(pen,boundary,r.Top,boundary,r.Bottom);using(var brush=new SolidBrush(Color.FromArgb(133,240,208)))e.Graphics.FillEllipse(brush,boundary-12,r.Top+r.Height/2-12,24,24);TextRenderer.DrawText(e.Graphics,"↔",Font,new Rectangle(boundary-12,r.Top+r.Height/2-12,24,24),Color.FromArgb(15,35,30),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);Badge(e.Graphics,"ДО",r.Left+10,r.Top+10);Badge(e.Graphics,"ПОСЛЕ",r.Right-78,r.Top+10);}
  else Badge(e.Graphics,"РЕЗУЛЬТАТ",r.Left+10,r.Top+10);
  if(Focused)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(2,2,Width-4,Height-4));
 }
 void Badge(Graphics g,string text,int x,int y){var r=new Rectangle(x,y,text.Length>6?104:68,27);using(var b=new SolidBrush(Color.FromArgb(210,18,25,34)))g.FillRectangle(b,r);TextRenderer.DrawText(g,text,Font,r,Color.White,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);}
 void SetSplit(int x){Rectangle r=ImageRect();if(r.Width>0){split=Math.Max(0,Math.Min(1,(x-r.Left)/(double)r.Width));Invalidate();}}
 protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left&&Comparison){Focus();Capture=true;SetSplit(e.X);}}
 protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(Capture)SetSplit(e.X);}
 protected override void OnMouseUp(MouseEventArgs e){Capture=false;base.OnMouseUp(e);}
 protected override bool IsInputKey(Keys key){return key==Keys.Left||key==Keys.Right||base.IsInputKey(key);}
 protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(e.KeyCode==Keys.Left||e.KeyCode==Keys.Right){split=Math.Max(0,Math.Min(1,split+(e.KeyCode==Keys.Left?-0.02:0.02)));Invalidate();e.Handled=true;}}
 protected override void OnResize(EventArgs e){base.OnResize(e);Invalidate();}
 protected override void Dispose(bool disposing){if(disposing)model.Changed-=ModelChanged;base.Dispose(disposing);}
}

public partial class LumaDesk {
 PreviewModel previewModel,expandedModel;System.Windows.Forms.Timer previewDelay;Form expandedPreview;bool previewClosing;
 void SchedulePreview(){if(!previewClosing&&previewDelay!=null){previewDelay.Stop();previewDelay.Start();}}
 void RefreshScreenshot(){
  if(previewModel!=null)RefreshScreenshotModel(previewModel,this);
  if(expandedModel!=null&&expandedPreview!=null)RefreshScreenshotModel(expandedModel,expandedPreview);
 }
 void RefreshScreenshotModel(PreviewModel model,Control viewer){
  // Each preview can be on a different monitor than the selected display.
  string device=modernPreviewDevice??Screen.FromControl(viewer).DeviceName;
  ushort[] current=null,baseline=null;IntPtr dc=CreateDC("DISPLAY",device,null,IntPtr.Zero);
  if(dc!=IntPtr.Zero){try{var ramp=new ushort[768];if(GetDeviceGammaRamp(dc,ramp))current=ramp;}finally{DeleteDC(dc);}}
  originals.TryGetValue(device,out baseline);
  model.Update(Current(),baseline,current);
 }
 void AddScreenshotPreview(FlowLayoutPanel tuning){
  previewModel=new PreviewModel();previewDelay=new System.Windows.Forms.Timer{Interval=60};previewDelay.Tick+=delegate{previewDelay.Stop();RefreshScreenshot();};
  var wrapper=new TableLayoutPanel{Bounds=tuning.Bounds,ColumnCount=2,RowCount=1,BackColor=bg,Margin=Padding.Empty};wrapper.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,58));wrapper.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,42));wrapper.RowStyles.Add(new RowStyle(SizeType.Percent,100));
  pageHost.Controls.Remove(tuning);pageHost.Controls.Add(wrapper);pages[1]=wrapper;wrapper.Visible=false;tuning.Dock=DockStyle.Fill;wrapper.Controls.Add(tuning,0,0);tuning.Visible=true;
  var card=new SoftCard{Dock=DockStyle.Fill,Padding=new Padding(16),Margin=new Padding(10,0,0,16)};wrapper.Controls.Add(card,1,0);
  var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=6,BackColor=card.BackColor};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
  layout.RowStyles.Add(new RowStyle(SizeType.Absolute,42));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,52));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,62));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,58));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,84));card.Controls.Add(layout);
  layout.Controls.Add(Copy("Предпросмотр",16,ForeColor),0,0);
  layout.Controls.Add(new Label{Text="Ваш скриншот · изменения видны\nдаже при выключенной коррекции",Dock=DockStyle.Fill,ForeColor=muted,Font=new Font("Segoe UI",9)},0,1);
  var image=new ScreenshotPreview(previewModel){Dock=DockStyle.Fill,Margin=Padding.Empty};layout.Controls.Add(image,0,2);
  var compare=new CheckBox{Checked=true,Text="Сравнение До / После\nПотяните границу на изображении",Dock=DockStyle.Fill,ForeColor=ForeColor,Font=new Font("Segoe UI",9)};compare.CheckedChanged+=delegate{image.Comparison=compare.Checked;image.Cursor=compare.Checked?Cursors.VSplit:Cursors.Default;image.Invalidate();};layout.Controls.Add(compare,0,3);
  var enlarge=ActionButton("Открыть крупно  ↗",delegate{OpenExpandedPreview();},false);enlarge.Dock=DockStyle.Fill;enlarge.Margin=new Padding(0,5,0,9);layout.Controls.Add(enlarge,0,4);
  layout.Controls.Add(new Label{Text="Программная симуляция. HDR, драйвер и обрезанные оттенки могут влиять на точность сравнения.",Dock=DockStyle.Fill,ForeColor=muted,Font=new Font("Segoe UI",9)},0,5);
  Move+=delegate{SchedulePreview();};Shown+=delegate{SchedulePreview();};FormClosed+=delegate{previewClosing=true;previewDelay.Stop();if(expandedPreview!=null)expandedPreview.Close();previewDelay.Dispose();previewModel.Dispose();};
 }
 void OpenExpandedPreview(){
  if(expandedPreview!=null){expandedPreview.Activate();return;}
  var window=new Form{Text="LumaDesk · предпросмотр До / После",ClientSize=new Size(1120,680),MinimumSize=new Size(640,420),BackColor=bg,ForeColor=ForeColor,Font=Font,StartPosition=FormStartPosition.CenterParent};expandedPreview=window;
  expandedModel=new PreviewModel();var image=new ScreenshotPreview(expandedModel){Dock=DockStyle.Fill};window.Controls.Add(image);
  var hint=new Label{Text="ДО  ←  Перетащите границу для сравнения  →  ПОСЛЕ     •     Изменяйте ползунки в основном окне",Dock=DockStyle.Bottom,Height=44,TextAlign=ContentAlignment.MiddleCenter,ForeColor=accent};window.Controls.Add(hint);
  window.Move+=delegate{SchedulePreview();};window.Activated+=delegate{SchedulePreview();};window.FormClosed+=delegate{expandedPreview=null;expandedModel.Dispose();expandedModel=null;SchedulePreview();};window.Show(this);
 }
}
