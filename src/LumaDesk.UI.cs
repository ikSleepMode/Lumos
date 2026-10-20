using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Diagnostics;
using System.Windows.Forms;

public class SoftCard : Panel {
 public int Radius=20;
 public SoftCard(){DoubleBuffered=true;BackColor=Color.FromArgb(25,31,43);Padding=new Padding(24);}
 public static GraphicsPath Shape(Rectangle r,int radius){var p=new GraphicsPath();int d=radius*2;p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
 protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent==null?BackColor:Parent.BackColor);if(Width<2||Height<2)return;e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var p=Shape(new Rectangle(0,0,Width-1,Height-1),Radius))using(var b=new SolidBrush(BackColor))using(var pen=new Pen(Color.FromArgb(43,52,69))){e.Graphics.FillPath(b,p);e.Graphics.DrawPath(pen,p);}}
}
public class SilkButton : Button {
 public bool Selected;public bool Primary; bool hover;
 public SilkButton(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);}
 protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
 protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
 protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;Color fill=Primary?Color.FromArgb(124,231,199):Selected?Color.FromArgb(43,67,65):hover?Color.FromArgb(47,57,76):Color.FromArgb(32,40,55);using(var path=SoftCard.Shape(new Rectangle(0,0,Width-1,Height-1),12))using(var brush=new SolidBrush(fill))e.Graphics.FillPath(brush,path);TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Primary?Color.FromArgb(12,37,32):Selected?Color.FromArgb(145,242,214):ForeColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);if(Focused)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(5,5,Width-10,Height-10));}
}
public class SilkSlider : Control {
 int value;public int Minimum=0,Maximum=100,SmallChange=1,LargeChange=10;
 public event EventHandler ValueChanged;
 public int Value{get{return value;}set{int n=Math.Max(Minimum,Math.Min(Maximum,value));if(this.value==n)return;this.value=n;Invalidate();if(ValueChanged!=null)ValueChanged(this,EventArgs.Empty);}}
 public SilkSlider(){Height=32;Cursor=Cursors.Hand;TabStop=true;SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.Selectable,true);AccessibleRole=AccessibleRole.Slider;}
 void SetFromX(int x){Value=Minimum+(int)Math.Round(Math.Max(0,Math.Min(1,(x-10.0)/Math.Max(1,Width-20)))*(Maximum-Minimum));}
 protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;Focus();Capture=true;SetFromX(e.X);}
 protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(Capture)SetFromX(e.X);}
 protected override void OnMouseUp(MouseEventArgs e){Capture=false;base.OnMouseUp(e);}
 protected override bool IsInputKey(Keys key){return key==Keys.Left||key==Keys.Right||key==Keys.Up||key==Keys.Down||base.IsInputKey(key);}
 protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);switch(e.KeyCode){case Keys.Left:case Keys.Down:Value-=SmallChange;break;case Keys.Right:case Keys.Up:Value+=SmallChange;break;case Keys.Home:Value=Minimum;break;case Keys.End:Value=Maximum;break;case Keys.PageUp:Value+=LargeChange;break;case Keys.PageDown:Value-=LargeChange;break;default:return;}e.Handled=true;}
 protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}protected override void OnLostFocus(EventArgs e){Invalidate();base.OnLostFocus(e);}
 protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;float x=10+(Width-20)*(Value-Minimum)/(float)Math.Max(1,Maximum-Minimum);using(var track=new Pen(Color.FromArgb(53,64,83),4))using(var active=new Pen(Color.FromArgb(125,231,200),4))using(var thumb=new SolidBrush(Color.FromArgb(207,255,239))){track.StartCap=track.EndCap=LineCap.Round;active.StartCap=active.EndCap=LineCap.Round;e.Graphics.DrawLine(track,10,Height/2,Width-10,Height/2);e.Graphics.DrawLine(active,10,Height/2,x,Height/2);e.Graphics.FillEllipse(thumb,x-6,Height/2-6,12,12);}if(Focused)using(var p=new Pen(Color.FromArgb(125,231,200)))e.Graphics.DrawEllipse(p,x-10,Height/2-10,20,20);}
}
public class TonePreview : Control {
 public double[] Settings=new double[]{1,1,1,1,1,1,6500,0,0};
 public TonePreview(){DoubleBuffered=true;Height=155;}
 protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(BackColor);int w=Math.Max(1,Width),h=Height-27;for(int x=0;x<w;x++){double t=x/(double)w;int r=(int)(255*LumaDesk.Curve(t,0,Settings)),g=(int)(255*LumaDesk.Curve(t,1,Settings)),b=(int)(255*LumaDesk.Curve(t,2,Settings));using(var pen=new Pen(Color.FromArgb(r,g,b)))e.Graphics.DrawLine(pen,x,0,x,32);}e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var grid=new Pen(Color.FromArgb(47,57,74)))for(int i=0;i<=4;i++){int y=48+(h-48)*i/4;e.Graphics.DrawLine(grid,0,y,w,y);}Color[] colors={Color.FromArgb(247,150,157),Color.FromArgb(127,229,195),Color.FromArgb(144,174,255)};for(int c=0;c<3;c++)using(var pen=new Pen(colors[c],2)){PointF[] pts=new PointF[128];for(int i=0;i<128;i++)pts[i]=new PointF(i*(w-1)/127f,h-(float)LumaDesk.Curve(i/127.0,c,Settings)*(h-48));e.Graphics.DrawLines(pen,pts);}TextRenderer.DrawText(e.Graphics,"ТЕНИ                                         СВЕТ",Font,new Rectangle(0,Height-22,w,22),Color.FromArgb(143,156,179),TextFormatFlags.Left);}
}
public partial class LumaDesk {
 Panel pageHost;Control[] pages;SilkButton[] navigation;int selectedPage;System.Windows.Forms.Timer transition;Stopwatch transitionClock;Control incoming,outgoing;int direction;TonePreview tonePreview;
 readonly Color muted=Color.FromArgb(149,162,184);
 Label Copy(string text,int size,Color color){return new Label{Text=text,AutoSize=true,MaximumSize=new Size(750,0),Font=new Font("Segoe UI",size),ForeColor=color,Margin=new Padding(0,0,0,14)};}
 SilkButton ActionButton(string text,EventHandler click,bool primary){var b=new SilkButton{Text=text,Width=245,Height=44,ForeColor=Color.FromArgb(229,236,246),Font=new Font("Segoe UI",10,FontStyle.Bold),Primary=primary,Margin=new Padding(0,6,12,8)};b.Click+=click;return b;}
 FlowLayoutPanel Stack(){return new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(2,0,16,16),BackColor=bg};}
 SoftCard Card(FlowLayoutPanel parent,int height){var card=new SoftCard{Width=800,Height=height,Margin=new Padding(0,0,0,16)};parent.Controls.Add(card);parent.ClientSizeChanged+=delegate{card.Width=Math.Max(200,parent.ClientSize.Width-24);};return card;}
 FlowLayoutPanel CardContent(SoftCard card){var flow=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,BackColor=card.BackColor};card.Controls.Add(flow);return flow;}
 void BuildInterface(){
  Text="LumaDesk — свет под вашим контролем";ClientSize=new Size(1420,860);MinimumSize=new Size(1200,760);StartPosition=FormStartPosition.CenterScreen;BackColor=bg;ForeColor=Color.FromArgb(232,238,248);Font=new Font("Segoe UI",10);AutoScaleMode=AutoScaleMode.Dpi;DoubleBuffered=true;
  var shell=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,BackColor=bg};shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,222));shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));Controls.Add(shell);
  var sidebar=new Panel{Dock=DockStyle.Fill,BackColor=Color.FromArgb(19,24,34),Padding=new Padding(20,30,20,24)};shell.Controls.Add(sidebar,0,0);
  var sideStack=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false};sidebar.Controls.Add(sideStack);
  sideStack.Controls.Add(Copy("◒  LUMA",24,accent));sideStack.Controls.Add(Copy("D E S K   /   DISPLAY STUDIO",8,muted));
  sideStack.Controls.Add(new Panel{Height=38,Width=160});
  string[] titles={"Обзор","Настройка","Профили","Экспорт"};string[] nav={"01    Обзор","02    Настройка","03    Профили","04    Экспорт"};
  navigation=new SilkButton[4];for(int i=0;i<4;i++){int index=i;navigation[i]=ActionButton(nav[i],delegate{SwitchPage(index);},false);navigation[i].Width=178;navigation[i].Height=49;navigation[i].Selected=i==0;sideStack.Controls.Add(navigation[i]);}
  var sideBottom=new Panel{Dock=DockStyle.Bottom,Height=130};hotkeyCaption=new Label{Dock=DockStyle.Fill,ForeColor=muted,Font=new Font("Segoe UI",9)};sideBottom.Controls.Add(hotkeyCaption);var editHotkey=ActionButton("Изменить хот-кей",delegate{ShowHotkeyEditor();},false);editHotkey.Dock=DockStyle.Bottom;editHotkey.Height=42;sideBottom.Controls.Add(editHotkey);sidebar.Controls.Add(sideBottom);RefreshHotkeyCaption();
  var main=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(28,24,28,12),ColumnCount=1,RowCount=3};main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));main.RowStyles.Add(new RowStyle(SizeType.Absolute,90));main.RowStyles.Add(new RowStyle(SizeType.Percent,100));main.RowStyles.Add(new RowStyle(SizeType.Absolute,48));shell.Controls.Add(main,1,0);
  var top=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=2};top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,255));top.RowStyles.Add(new RowStyle(SizeType.Absolute,24));top.RowStyles.Add(new RowStyle(SizeType.Absolute,48));main.Controls.Add(top,0,0);
  top.Controls.Add(Copy("РАБОЧИЙ ЭКРАН",8,muted),0,0);top.Controls.Add(Copy("КОРРЕКЦИЯ ИЗОБРАЖЕНИЯ",8,muted),1,0);
  monitors=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,FlatStyle=FlatStyle.Flat,BackColor=panel,ForeColor=ForeColor,Margin=new Padding(0,9,20,0),Font=new Font("Segoe UI",11)};top.Controls.Add(monitors,0,1);
  toggle=ActionButton("Включить коррекцию",delegate{enabled=!enabled;Apply();},true);top.Controls.Add(toggle,1,1);
  pageHost=new Panel{Dock=DockStyle.Fill,BackColor=bg};main.Controls.Add(pageHost,0,1);pages=new Control[4];
  for(int i=0;i<4;i++){var page=Stack();page.Dock=DockStyle.None;page.Bounds=pageHost.ClientRectangle;page.Visible=i==0;pages[i]=page;pageHost.Controls.Add(page);page.Controls.Add(Copy(titles[i],27,ForeColor));}
  var overview=(FlowLayoutPanel)pages[0];overview.Controls.Add(Copy("Ваш экран. Ваш ритм. Ваш свет.",11,muted));
  var hero=Card(overview,295);var heroFlow=CardContent(hero);heroFlow.Controls.Add(Copy("Свет, который подходит вам",20,ForeColor));heroFlow.Controls.Add(Copy("Выберите настроение или создайте свою настройку цвета.",10,muted));tonePreview=new TonePreview{Width=730,Height=132,BackColor=hero.BackColor,Font=new Font("Segoe UI",8)};heroFlow.Controls.Add(tonePreview);heroFlow.ClientSizeChanged+=delegate{tonePreview.Width=Math.Max(100,heroFlow.ClientSize.Width);};
  var presetCard=Card(overview,220);var presetFlow=CardContent(presetCard);presetFlow.Controls.Add(Copy("Быстрые сценарии",14,ForeColor));
  var grid=new TableLayoutPanel{Width=730,Height=126,ColumnCount=2,RowCount=2};grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));grid.RowStyles.Add(new RowStyle(SizeType.Percent,50));grid.RowStyles.Add(new RowStyle(SizeType.Percent,50));presetFlow.Controls.Add(grid);presetFlow.ClientSizeChanged+=delegate{grid.Width=Math.Max(100,presetFlow.ClientSize.Width);};
  string[] presetNames={"Естественный · нейтральный цвет","Тени · больше деталей","Ночной · тёплый свет","Мягкий · спокойный контраст"};double[][] presetValues={defaults,new double[]{1.45,1,1.04,1,1,1,6500,0,0},new double[]{1,0.8,0.95,1,1,1,4200,0,0},new double[]{1.08,0.95,0.88,1,1,1,5700,0.02,-0.04}};
  for(int i=0;i<4;i++){int n=i;var b=ActionButton(presetNames[i],delegate{SetValues(presetValues[n]);enabled=true;Apply();},false);b.Dock=DockStyle.Fill;grid.Controls.Add(b,i%2,i/2);}
  overview.Controls.Add(ActionButton("Свернуть в трей",delegate{Hide();tray.ShowBalloonTip(2000,"LumaDesk","Двойной щелчок по значку откроет окно.",ToolTipIcon.Info);},false));
  var tuning=(FlowLayoutPanel)pages[1];tuning.Controls.Add(Copy("Точная настройка без лишних отвлечений.",11,muted));
  string[] names={"Гамма","Яркость","Контраст","Красный","Зелёный","Синий","Температура · K","Уровень чёрного","Светлые участки"};int[] mins={50,30,50,20,20,20,2500,0,-30},maxs={250,150,180,150,150,150,9000,20,30};
  for(int group=0;group<3;group++){var card=Card(tuning,196);var flow=CardContent(card);flow.Controls.Add(Copy(new string[]{"Свет и контраст","Баланс каналов","Тон и температура"}[group],12,ForeColor));
   for(int j=0;j<3;j++){int i=group*3+j;var row=new TableLayoutPanel{Width=730,Height=34,ColumnCount=3,RowCount=1,Margin=Padding.Empty};row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,180));row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,106));row.Controls.Add(new Label{Text=names[i],Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=muted},0,0);
    sliders[i]=new SilkSlider{Minimum=mins[i],Maximum=maxs[i],Value=(int)(defaults[i]*scales[i]),Dock=DockStyle.Fill,SmallChange=i==6?100:1,LargeChange=i==6?500:10,BackColor=card.BackColor,AccessibleName=names[i]};row.Controls.Add(sliders[i],1,0);row.Controls.Add(CreateNumberInput(i,names[i],card),2,0);flow.Controls.Add(row);var localRow=row;flow.ClientSizeChanged+=delegate{localRow.Width=Math.Max(100,flow.ClientSize.Width);};sliders[i].ValueChanged+=delegate{UpdateLabels();if(!loading&&debounce!=null){debounce.Stop();debounce.Start();}};
   }
  }
  tuning.Controls.Add(ActionButton("Сбросить настройки",delegate{SetValues(defaults);enabled=false;Apply();},false));
  AddScreenshotPreview(tuning);
  var profilePage=(FlowLayoutPanel)pages[2];profilePage.Controls.Add(Copy("Сохраните любимый цвет для каждой игры.",11,muted));
  var listCard=Card(profilePage,200);var listFlow=CardContent(listCard);listFlow.Controls.Add(Copy("Библиотека профилей",14,ForeColor));saved=StyledCombo();listFlow.Controls.Add(saved);
  var actions=new FlowLayoutPanel{Width=730,Height=60};listFlow.Controls.Add(actions);actions.Controls.Add(ActionButton("Применить профиль",delegate{var p=saved.SelectedItem as Profile;if(p!=null){SetValues(p.Values);enabled=true;Apply();profileName.Text=p.Name;processName.Text=p.Process;}},true));actions.Controls.Add(ActionButton("Удалить",delegate{var p=saved.SelectedItem as Profile;if(p!=null){profiles.Remove(p);Persist();RefreshProfiles();}},false));
  var editCard=Card(profilePage,340);var editFlow=CardContent(editCard);editFlow.Controls.Add(Copy("Сохранить текущую настройку",14,ForeColor));editFlow.Controls.Add(Copy("Название профиля",9,muted));profileName=StyledText("Мой профиль");editFlow.Controls.Add(profileName);editFlow.Controls.Add(Copy("Процесс игры · например game.exe",9,muted));processName=StyledText("");editFlow.Controls.Add(processName);editFlow.Controls.Add(ActionButton("Сохранить настройки",delegate{SaveProfile();},true));auto=new CheckBox{Text="Автоматически применять при запуске игры",Width=470,Height=34,ForeColor=ForeColor};editFlow.Controls.Add(auto);
  saved.SelectedIndexChanged+=delegate{var p=saved.SelectedItem as Profile;if(p!=null){profileName.Text=p.Name;processName.Text=p.Process;}};
  var export=(FlowLayoutPanel)pages[3];export.Controls.Add(Copy("Перенесите свой цвет в видео и трансляции.",11,muted));var exportCard=Card(export,260);var exportFlow=CardContent(exportCard);exportFlow.Controls.Add(Copy("Один стиль. На разных экранах.",20,ForeColor));exportFlow.Controls.Add(Copy("Сохраните текущую настройку в LUT .cube и подключите файл\nв OBS или совместимом видеоредакторе.",11,muted));exportFlow.Controls.Add(Copy("CUBE  /  33 × 33 × 33  /  RGB",9,accent));exportFlow.Controls.Add(ActionButton("Экспортировать LUT",delegate{ExportLut();},true));
  var info=Card(export,205);var infoFlow=CardContent(info);infoFlow.Controls.Add(Copy("О работе коррекции",14,ForeColor));infoFlow.Controls.Add(Copy("HDR, ночной свет Windows и драйвер могут мешать применению.\nНасыщенность не изменяется. LUT подключается вручную.\nПри обычном выходе восстанавливается исходная гамма.",11,muted));
  status=new Label{Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=accent,AutoEllipsis=true,Font=new Font("Segoe UI",9)};main.Controls.Add(status,0,2);
  transitionClock=new Stopwatch();transition=new System.Windows.Forms.Timer{Interval=15};transition.Tick+=delegate{AnimatePage();};pageHost.SizeChanged+=delegate{FinishTransition();foreach(var p in pages)p.Bounds=pageHost.ClientRectangle;};
  FormClosed+=delegate{transition.Dispose();debounce.Dispose();watcher.Dispose();};
 }
 ComboBox StyledCombo(){return new ComboBox{Width=470,DropDownStyle=ComboBoxStyle.DropDownList,FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(35,44,60),ForeColor=ForeColor,Margin=new Padding(0,0,0,10)};}
 TextBox StyledText(string text){return new TextBox{Text=text,Width=470,BackColor=Color.FromArgb(35,44,60),ForeColor=ForeColor,BorderStyle=BorderStyle.FixedSingle,Margin=new Padding(0,0,0,14)};}
 void FinishTransition(){transition.Stop();if(outgoing!=null)outgoing.Visible=false;if(incoming!=null){incoming.Bounds=pageHost.ClientRectangle;incoming.Visible=true;}outgoing=null;incoming=null;}
 void SwitchPage(int index){if(index==selectedPage)return;FinishTransition();direction=index>selectedPage?1:-1;outgoing=pages[selectedPage];incoming=pages[index];selectedPage=index;for(int i=0;i<navigation.Length;i++){navigation[i].Selected=i==index;navigation[i].Invalidate();}incoming.Bounds=new Rectangle(direction*pageHost.Width,0,pageHost.Width,pageHost.Height);incoming.Visible=true;incoming.BringToFront();transitionClock.Restart();transition.Start();}
 void AnimatePage(){double t=Math.Min(1,transitionClock.Elapsed.TotalMilliseconds/260.0);double ease=1-Math.Pow(1-t,3);int offset=(int)Math.Round(pageHost.Width*(1-ease))*direction;pageHost.SuspendLayout();incoming.Left=offset;outgoing.Left=offset-direction*pageHost.Width;pageHost.ResumeLayout(false);if(t>=1)FinishTransition();}
}
