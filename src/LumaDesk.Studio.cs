using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Shell;
using System.Windows.Threading;

public static class ModernMain {
 [STAThread] public static void Main(){
  bool first;using(var mutex=new Mutex(true,"Local\\LumaDesk.Unique",out first)){
   if(!first){MessageBox.Show("Lumos уже запущен. Откройте окно через значок в трее.","Lumos");return;}
   System.Windows.Forms.Application.EnableVisualStyles();System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
   var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
   app.Startup+=delegate{LicensedStartup.Start(app);};app.Run();
  }
 }
}

public sealed class FrameComparison : Grid {
 readonly Image after=new Image{Stretch=Stretch.Uniform},before=new Image{Stretch=Stretch.Uniform};
 readonly Grid beforeLayer=new Grid();readonly Canvas overlay=new Canvas{IsHitTestVisible=false};readonly Border divider,handle,leftBadge,rightBadge;
 double split=0.5;bool comparison=true;
 public bool Comparison{get{return comparison;}set{comparison=value;beforeLayer.Visibility=value?Visibility.Visible:Visibility.Collapsed;Cursor=value?Cursors.SizeWE:Cursors.Arrow;ArrangeOverlay();}}
 public FrameComparison(){
  Background=(Brush)new BrushConverter().ConvertFromString("#090C09");ClipToBounds=true;Focusable=true;Cursor=Cursors.SizeWE;
  Children.Add(after);beforeLayer.Children.Add(before);Children.Add(beforeLayer);Children.Add(overlay);
  divider=new Border{Width=1,Background=Brushes.White,Opacity=.8};handle=new Border{Width=30,Height=30,CornerRadius=new CornerRadius(15),Background=(Brush)new BrushConverter().ConvertFromString("#C5F778"),Child=new TextBlock{Text="↔",FontSize=15,Foreground=Brushes.Black,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}};
  leftBadge=Badge("ДО");rightBadge=Badge("ПОСЛЕ");overlay.Children.Add(divider);overlay.Children.Add(handle);overlay.Children.Add(leftBadge);overlay.Children.Add(rightBadge);
  SizeChanged+=delegate{Clip=new RectangleGeometry(new Rect(0,0,Math.Max(0,ActualWidth),Math.Max(0,ActualHeight)),16,16);ArrangeOverlay();};MouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e){if(!comparison)return;Focus();CaptureMouse();SetSplit(e.GetPosition(this).X);e.Handled=true;};MouseMove+=delegate(object sender,MouseEventArgs e){if(IsMouseCaptured)SetSplit(e.GetPosition(this).X);};MouseLeftButtonUp+=delegate{ReleaseMouseCapture();};KeyDown+=delegate(object sender,KeyEventArgs e){if(e.Key==Key.Left||e.Key==Key.Right){split=Math.Max(0,Math.Min(1,split+(e.Key==Key.Left?-.02:.02)));ArrangeOverlay();e.Handled=true;}};
  System.Windows.Automation.AutomationProperties.SetName(this,"Предпросмотр До / После. Перетащите границу или используйте стрелки.");
 }
 static Border Badge(string text){return new Border{Background=new SolidColorBrush(Color.FromArgb(220,19,25,17)),CornerRadius=new CornerRadius(6),Padding=new Thickness(12,7,12,7),Child=new TextBlock{Text=text,FontSize=9,Foreground=new SolidColorBrush(Color.FromRgb(211,226,197)),FontWeight=FontWeights.SemiBold}};}
 Rect PictureRect(){var source=after.Source as BitmapSource;if(source==null||ActualWidth==0||ActualHeight==0)return new Rect(0,0,Math.Max(0,ActualWidth),Math.Max(0,ActualHeight));double scale=Math.Min(ActualWidth/source.PixelWidth,ActualHeight/source.PixelHeight);double w=source.PixelWidth*scale,h=source.PixelHeight*scale;return new Rect((ActualWidth-w)/2,(ActualHeight-h)/2,w,h);}
 void SetSplit(double x){Rect r=PictureRect();if(r.Width>0){split=Math.Max(0,Math.Min(1,(x-r.Left)/r.Width));ArrangeOverlay();}}
 void ArrangeOverlay(){Rect r=PictureRect();double x=r.Left+r.Width*split;beforeLayer.Clip=new RectangleGeometry(new Rect(0,0,Math.Max(0,x),Math.Max(0,ActualHeight)));divider.Height=r.Height;Canvas.SetLeft(divider,x);Canvas.SetTop(divider,r.Top);Canvas.SetLeft(handle,x-15);Canvas.SetTop(handle,r.Top+r.Height/2-15);Canvas.SetLeft(leftBadge,r.Left+16);Canvas.SetTop(leftBadge,r.Top+16);Canvas.SetLeft(rightBadge,Math.Max(0,r.Right-77));Canvas.SetTop(rightBadge,r.Top+16);divider.Visibility=handle.Visibility=leftBadge.Visibility=comparison?Visibility.Visible:Visibility.Collapsed;((TextBlock)rightBadge.Child).Text=comparison?"ПОСЛЕ":"РЕЗУЛЬТАТ";if(!comparison)Canvas.SetLeft(rightBadge,Math.Max(0,r.Right-105));}
 public void SetImages(BitmapSource original,BitmapSource modified){before.Source=original;after.Source=modified;ArrangeOverlay();}
 public void ApplyTheme(StudioPalette palette){
  Color accent=(Color)ColorConverter.ConvertFromString(palette.Accent),ink=(Color)ColorConverter.ConvertFromString(palette.AccentInk),surface=(Color)ColorConverter.ConvertFromString(palette.Card),text=(Color)ColorConverter.ConvertFromString(palette.Text);surface.A=238;
  divider.Background=new SolidColorBrush(accent);handle.Background=new SolidColorBrush(accent);((TextBlock)handle.Child).Foreground=new SolidColorBrush(ink);
  leftBadge.Background=rightBadge.Background=new SolidColorBrush(surface);((TextBlock)leftBadge.Child).Foreground=((TextBlock)rightBadge.Child).Foreground=new SolidColorBrush(text);
 }
}

public sealed partial class StudioWindow : Window {
 internal readonly LumaDesk Engine;readonly Grid root;readonly bool testing;
 readonly Grid[] pages=new Grid[4];readonly Button[] tabs=new Button[4];
 readonly Slider[] sliders=new Slider[9];readonly TextBox[] fields=new TextBox[9];
 readonly List<FrameComparison> viewers=new List<FrameComparison>();readonly List<Button> presetButtons=new List<Button>();
 readonly DispatcherTimer refresh;System.Windows.Forms.NotifyIcon tray;System.Drawing.Icon trayIcon;bool synchronizing,closing,exitRequested;int selectedPage;uint pendingKey,pendingModifiers;
 long renderedRevision=-1;string lastStatus="",profileSignature="";double[] lastValues;BitmapSource currentBefore,currentAfter;Window expanded;
 readonly string[] parameterNames={"Гамма","Яркость","Контраст","Красный","Зелёный","Синий","Температура","Уровень чёрного","Светлые участки"};
 readonly double[] minimum={.5,.3,.5,.2,.2,.2,2500,0,-.3},maximum={2.5,1.5,1.8,1.5,1.5,1.5,9000,.2,.3};
 readonly double[][] presets={new double[]{1,1,1,1,1,1,6500,0,0},new double[]{1.45,1,1.04,1,1,1,6500,0,0},new double[]{1,.8,.95,1,1,1,4200,0,0},new double[]{1.08,.95,.88,1,1,1,5700,.02,-.04}};
 [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr handle,int attribute,ref int value,int size);
 T Find<T>(string name)where T:FrameworkElement{return (T)root.FindName(name);}
 Brush ColorBrush(string hex){return ThemeBrush(hex);}
 Style StyleFor(string key){return (Style)root.FindResource(key);}
 Button MakeButton(string text,Action action,bool primary){var b=new Button{Content=text,HorizontalContentAlignment=HorizontalAlignment.Center};if(primary)b.Style=StyleFor("Primary");b.Click+=delegate{action();};return b;}
 TextBlock Text(string value,double size,string color){return new TextBlock{Text=value,FontSize=size,Foreground=ColorBrush(color),TextWrapping=TextWrapping.Wrap};}
 internal StudioWindow(bool test):this(test,null){}
 internal StudioWindow(bool test,string appearanceFile):this(test,appearanceFile,null,null){}
 internal StudioWindow(bool test,string appearanceFile,string sessionFile,string profilesFile){
  testing=test;Title="Lumos";Width=1440;Height=950;MinWidth=1180;MinHeight=760;WindowStartupLocation=WindowStartupLocation.CenterScreen;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.CanResize;Background=ColorBrush("#111214");Foreground=ColorBrush("#F2F2ED");FontFamily=new FontFamily("Segoe UI");
  if(!test){Rect area=SystemParameters.WorkArea;MinWidth=Math.Min(MinWidth,area.Width-20);MinHeight=Math.Min(MinHeight,area.Height-20);Width=Math.Max(MinWidth,Math.Min(Width,area.Width-40));Height=Math.Max(MinHeight,Math.Min(Height,area.Height-40));}
  WindowChrome.SetWindowChrome(this,new WindowChrome{CaptionHeight=0,ResizeBorderThickness=new Thickness(6),GlassFrameThickness=new Thickness(0),CornerRadius=new CornerRadius(12)});
  using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("LumaDesk.Studio.xaml")){if(stream==null)throw new FileNotFoundException("Не найден интерфейс Lumos.");root=(Grid)XamlReader.Load(stream);}Content=root;InitializeTheme(appearanceFile);
  using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Lumos.Brand.png")){if(stream!=null){var image=BitmapFrame.Create(stream,BitmapCreateOptions.None,BitmapCacheOption.OnLoad);image.Freeze();Icon=image;Find<Image>("BrandIcon").Source=image;}}
  Engine=new LumaDesk(test?System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-settings-unused.xml"):null,true,profilesFile);
  for(int i=0;i<4;i++){int index=i;pages[i]=Find<Grid>("Page"+i);tabs[i]=Find<Button>("Nav"+i);tabs[i].Click+=delegate{Navigate(index,true);};}
  Find<Border>("TitleBar").MouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e){if(e.ClickCount==2)ToggleMaximize();else if(e.ButtonState==MouseButtonState.Pressed)DragMove();};
  Find<Button>("Minimize").Click+=delegate{WindowState=WindowState.Minimized;};Find<Button>("Maximize").Click+=delegate{ToggleMaximize();};Find<Button>("CloseApp").Click+=delegate{HideToTray();};
  Find<Button>("PowerButton").Click+=delegate{Engine.StudioToggle();Synchronize();};Find<Button>("ResetValues").Click+=delegate{Engine.StudioReset();ResetHardwareSharpness();RememberProfile(null,-1);Synchronize();};Find<Button>("GoTuning").Click+=delegate{Navigate(1,true);};
  var monitor=Find<ComboBox>("MonitorPicker");monitor.ItemsSource=Engine.StudioDisplays;monitor.SelectedIndex=Engine.StudioDisplay;monitor.SelectionChanged+=delegate{FlushHardwareSharpness();Engine.StudioDisplay=monitor.SelectedIndex;monitor.ToolTip=monitor.SelectedItem;RememberMonitor();Synchronize();};
  CreateParameters();CreatePresets();CreateViewers();
  Find<Button>("OverviewCompare").Click+=delegate{SetComparison(!viewers[0].Comparison);};Find<Button>("TuningCompare").Click+=delegate{SetComparison(!viewers[0].Comparison);};Find<Button>("ExpandPreview").Click+=delegate{OpenExpanded();};
  Find<Button>("SaveProfile").Click+=delegate{string name=Find<TextBox>("ProfileName").Text.Trim();if(name.Length==0){Find<TextBlock>("ProfileFeedback").Text="Введите название профиля.";return;}Engine.StudioSaveProfile(name,Find<TextBox>("ProfileProcess").Text.Trim());SaveHardwareProfile(name);RememberProfile(name,-1);profileSignature="";RefreshProfiles();Find<TextBlock>("ProfileFeedback").Text="Профиль «"+name+"» сохранён.";};
  var auto=Find<CheckBox>("AutoProfiles");auto.IsChecked=Engine.StudioAuto;auto.Checked+=delegate{Engine.StudioAuto=true;};auto.Unchecked+=delegate{Engine.StudioAuto=false;};
  Find<Button>("ExportLut").Click+=delegate{Export();};WireHotkey();WireThemePicker();InitializeExtras();InitializeSession(sessionFile);InitializeUpdates();bool startupLaunch=!testing&&StartupController.IsStartup(Environment.GetCommandLineArgs());if(startupLaunch){ShowActivated=false;WindowState=WindowState.Minimized;}
  Engine.StudioPreview.Changed+=PreviewChanged;Find<Canvas>("CurveCanvas").SizeChanged+=delegate{DrawCurve();};
  refresh=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};refresh.Tick+=delegate{Synchronize();};
  SourceInitialized+=delegate{int corner=2,dark=1;try{IntPtr hwnd=new WindowInteropHelper(this).Handle;DwmSetWindowAttribute(hwnd,33,ref corner,4);DwmSetWindowAttribute(hwnd,20,ref dark,4);}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}};
  Loaded+=delegate{UpdatePreviewMonitor();if(!testing){Engine.StudioStart();CreateTray();refresh.Start();if(startupLaunch)Hide();}else Engine.StudioPreviewValues(Engine.StudioValues);Synchronize();};
  Closing+=delegate(object sender,System.ComponentModel.CancelEventArgs e){if(!testing&&!exitRequested&&!Dispatcher.HasShutdownStarted){e.Cancel=true;HideToTray();}};if(!testing&&Application.Current!=null)Application.Current.SessionEnding+=delegate{exitRequested=true;};LocationChanged+=delegate{UpdatePreviewMonitor();};Closed+=delegate{closing=true;refresh.Stop();Engine.StudioPreview.Changed-=PreviewChanged;if(expanded!=null)expanded.Close();if(tray!=null)tray.Dispose();if(trayIcon!=null)trayIcon.Dispose();try{StopExtras();}finally{Engine.StudioStop();}};
  PreviewKeyDown+=delegate(object sender,KeyEventArgs e){if(e.Key==Key.Escape&&Find<Border>("SettingsOverlay").Visibility==Visibility.Visible){CloseSettings();e.Handled=true;}else if(e.Key==Key.Escape&&Find<Border>("ThemeOverlay").Visibility==Visibility.Visible){CloseThemePicker();e.Handled=true;}else if(e.Key==Key.Escape&&Find<Border>("HotkeyOverlay").Visibility==Visibility.Visible){CloseHotkey();e.Handled=true;}else if(Find<Border>("SettingsOverlay").Visibility!=Visibility.Visible&&Find<Border>("ThemeOverlay").Visibility!=Visibility.Visible&&Find<Border>("HotkeyOverlay").Visibility!=Visibility.Visible&&e.Key==Key.Tab&&(Keyboard.Modifiers&ModifierKeys.Control)!=0){Navigate((selectedPage+((Keyboard.Modifiers&ModifierKeys.Shift)!=0?3:1))%4,true);e.Handled=true;}};
  Synchronize();RefreshProfiles();
 }
 void HideToTray(){CloseThemePicker();CloseHotkey();CloseSettings();if(expanded!=null)expanded.Hide();Hide();}
 void ToggleMaximize(){WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;}
 void CreateTray(){using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("LumaDesk.Icon.ico")){if(stream!=null)using(var image=new System.Drawing.Icon(stream))trayIcon=(System.Drawing.Icon)image.Clone();}tray=new System.Windows.Forms.NotifyIcon{Visible=true,Icon=trayIcon??System.Drawing.SystemIcons.Application,Text="Lumos"};Action open=delegate{Dispatcher.Invoke(delegate{Show();if(expanded!=null)expanded.Show();if(WindowState==WindowState.Minimized)WindowState=WindowState.Normal;Activate();});};tray.DoubleClick+=delegate{open();};var menu=new System.Windows.Forms.ContextMenuStrip();menu.Items.Add("Открыть Lumos",null,delegate{open();});menu.Items.Add("Переключить коррекцию",null,delegate{Engine.StudioToggle();});menu.Items.Add("Свернуть в трей",null,delegate{HideToTray();});menu.Items.Add("Выйти и восстановить экран",null,delegate{exitRequested=true;Close();});tray.ContextMenuStrip=menu;}
 void UpdatePreviewMonitor(){if(closing||Engine==null)return;IntPtr handle=new WindowInteropHelper(this).Handle;if(handle!=IntPtr.Zero)Engine.StudioSelectPreviewDisplay(System.Windows.Forms.Screen.FromHandle(handle).DeviceName);}
 internal void Navigate(int index,bool animate){
  if(index==selectedPage)return;pages[selectedPage].Visibility=Visibility.Collapsed;selectedPage=index;
  for(int i=0;i<4;i++)tabs[i].Tag=i==index?"active":null;
  var page=pages[index];page.Visibility=Visibility.Visible;page.BeginAnimation(UIElement.OpacityProperty,null);page.RenderTransform=new TranslateTransform();
  if(animate&&SystemParameters.ClientAreaAnimation){var ease=new CubicEase{EasingMode=EasingMode.EaseOut};page.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(240)));((TranslateTransform)page.RenderTransform).BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(14,0,TimeSpan.FromMilliseconds(260)){EasingFunction=ease});}else page.Opacity=1;
  Find<TextBlock>("PageHeading").Text=new string[]{"Всё начинается со света.","Каждый оттенок — под контролем.","Хороший свет стоит сохранить.","Ваш стиль в каждом кадре."}[index];if(index==1)DrawCurve();
 }
 void CreateParameters(){var list=Find<StackPanel>("ParameterList");
  for(int i=0;i<9;i++){int index=i;if(i%3==0){var heading=Text(new string[]{"СВЕТ И КОНТРАСТ","БАЛАНС КАНАЛОВ","ТОН И ТЕМПЕРАТУРА"}[i/3],9,"#7F9270");heading.FontWeight=FontWeights.SemiBold;heading.Margin=new Thickness(0,i==0?0:18,0,11);list.Children.Add(heading);}
   var row=new Grid{Margin=new Thickness(0,0,0,10)};row.RowDefinitions.Add(new RowDefinition{Height=new GridLength(34)});row.RowDefinitions.Add(new RowDefinition{Height=new GridLength(24)});row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(78)});
   var label=Text(parameterNames[i],12,"#C4CDBD");label.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(label);
   var input=new TextBox{TextAlignment=TextAlignment.Center,Padding=new Thickness(6,5,6,5),FontSize=12,VerticalContentAlignment=VerticalAlignment.Center};Grid.SetColumn(input,1);row.Children.Add(input);fields[i]=input;System.Windows.Automation.AutomationProperties.SetName(input,parameterNames[i]+" — точное значение");input.ToolTip=minimum[i].ToString(CultureInfo.CurrentCulture)+" — "+maximum[i].ToString(CultureInfo.CurrentCulture)+(i==6?" K":"");
   input.KeyDown+=delegate(object sender,KeyEventArgs e){if(e.Key==Key.Enter){CommitValue(index);Keyboard.ClearFocus();e.Handled=true;}else if(e.Key==Key.Escape){input.Text=Format(index,Engine.StudioValues[index]);input.Tag=null;e.Handled=true;}};input.LostKeyboardFocus+=delegate{CommitValue(index);};
   var slider=new Slider{Minimum=minimum[i],Maximum=maximum[i],SmallChange=i==6?100:.01,LargeChange=i==6?500:.1,TickFrequency=i==6?1:.01,IsSnapToTickEnabled=true,Margin=new Thickness(0,0,0,0)};sliders[i]=slider;Grid.SetRow(slider,1);Grid.SetColumnSpan(slider,2);row.Children.Add(slider);System.Windows.Automation.AutomationProperties.SetName(slider,parameterNames[i]);slider.ValueChanged+=delegate{if(!synchronizing){Engine.StudioValue(index,slider.Value);Synchronize();}};list.Children.Add(row);
  }
 }
 string Format(int index,double value){return value.ToString(index==6?"0":"0.00",CultureInfo.CurrentCulture);}
 internal void CommitValue(int index){if(synchronizing)return;double value;var box=fields[index];if(!Double.TryParse(box.Text.Trim().Replace(',','.'),NumberStyles.AllowDecimalPoint|NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out value)||Double.IsNaN(value)||Double.IsInfinity(value)){box.Tag="invalid";Find<TextBlock>("StatusText").Text="Введите число от "+minimum[index]+" до "+maximum[index]+".";return;}box.Tag=null;Engine.StudioValue(index,Math.Max(minimum[index],Math.Min(maximum[index],value)));box.Text=Format(index,Engine.StudioValues[index]);Find<TextBlock>("StatusText").Text=Engine.StudioStatus.Length>0?Engine.StudioStatus:"Предпросмотр обновлён · коррекция экрана выключена";Synchronize();}
 void CreatePresets(){string[] names={"Естественный","Детали в тенях","Ночной сеанс","Мягкий свет"};string[] descriptions={"Чистый цвет. Ничего лишнего.","Видеть больше в тёмных сценах.","Теплее и спокойнее для глаз.","Бережный свет, мягкий контраст."};string[] colors={"#ACBBA0","#C5F778","#A3B6E0","#E3C79B"};var parent=Find<StackPanel>("PresetCards");
  for(int i=0;i<4;i++){int index=i;var content=new Grid();content.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(42)});content.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});content.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(22)});var icon=new Grid{Width=30,Height=30,VerticalAlignment=VerticalAlignment.Center};icon.Children.Add(new Ellipse{Stroke=ColorBrush(colors[i]),StrokeThickness=1,Margin=new Thickness(2)});icon.Children.Add(new Ellipse{Fill=ColorBrush(colors[i]),Width=i==1?17:10,Height=i==1?17:10,Opacity=.7});content.Children.Add(icon);var words=new StackPanel();words.Children.Add(Text(names[i],15,"#E9EEE4"));var description=Text(descriptions[i],10,"#8E9C84");description.Margin=new Thickness(0,7,0,0);words.Children.Add(description);Grid.SetColumn(words,1);content.Children.Add(words);var arrow=Text("↗",17,colors[i]);Grid.SetColumn(arrow,2);arrow.VerticalAlignment=VerticalAlignment.Center;content.Children.Add(arrow);
   var button=MakeButton("",delegate{Engine.StudioPreset(presets[index]);RememberProfile(null,index);Synchronize();},false);button.Content=content;button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.Padding=new Thickness(14,18,12,18);button.Margin=new Thickness(0,0,0,12);button.Background=ColorBrush(i==1?"#293420":"#20251E");button.BorderBrush=ColorBrush(i==1?"#526A3D":"#343D2E");parent.Children.Add(button);presetButtons.Add(button);
  }
 }
 void CreateViewers(){foreach(string name in new string[]{"OverviewPreview","TuningPreview","ExportPreview"}){var viewer=new FrameComparison();viewer.ApplyTheme(activeTheme);if(name=="ExportPreview")viewer.Comparison=false;Find<ContentControl>(name).Content=viewer;viewers.Add(viewer);}}
 void SetComparison(bool value){viewers[0].Comparison=value;viewers[1].Comparison=value;Find<Button>("TuningCompare").Content=value?"До / После":"Результат";Find<Button>("OverviewCompare").Content=value?"До / После  ↔":"Показать сравнение  ↔";}
 internal void Synchronize(){
  if(closing)return;synchronizing=true;try{bool forceValues=renderedRevision!=Engine.StudioRevision;renderedRevision=Engine.StudioRevision;double[] values=Engine.StudioValues;bool changed=lastValues==null;for(int i=0;i<9;i++){if(lastValues!=null&&values[i]!=lastValues[i]){changed=true;if(!fields[i].IsKeyboardFocusWithin)fields[i].Tag=null;}sliders[i].Value=values[i];if(forceValues){fields[i].Tag=null;fields[i].Text=Format(i,values[i]);}else if(!fields[i].IsKeyboardFocusWithin&&fields[i].Tag==null)fields[i].Text=Format(i,values[i]);}lastValues=values;if(changed){DrawCurve();for(int p=0;p<presetButtons.Count;p++){bool match=true;for(int j=0;j<9;j++)if(Math.Abs(values[j]-presets[p][j])>0.001)match=false;presetButtons[p].Background=ColorBrush(match?"#293420":"#20251E");presetButtons[p].BorderBrush=ColorBrush(match?"#769350":"#343D2E");}}
   Find<Button>("PowerButton").Content=Engine.StudioUncertain?"Повторить выключение   ↻":Engine.StudioEnabled?"Коррекция включена   ●":"Включить коррекцию   ◉";
   Find<Button>("HotkeyButton").Content="Горячая клавиша: "+Engine.StudioHotkey+"  →";Find<Button>("HotkeyButton").ToolTip="Изменить горячую клавишу";Find<TextBlock>("HotkeyTile").Text=Engine.StudioHotkey;
   if(lastStatus!=Engine.StudioStatus){lastStatus=Engine.StudioStatus;if(lastStatus.Length>0)Find<TextBlock>("StatusText").Text=lastStatus;}
   RefreshProfiles();
  }finally{synchronizing=false;}SynchronizeExtras();
 }
 void PreviewChanged(object sender,EventArgs e){if(closing)return;var model=Engine.StudioPreview;currentBefore=Bitmap(model.Before??model.Original);currentAfter=Bitmap(model.After??model.Original);foreach(var viewer in viewers)viewer.SetImages(currentBefore,currentAfter);}
 internal static BitmapSource Bitmap(System.Drawing.Bitmap source){if(source==null)return null;var rect=new System.Drawing.Rectangle(0,0,source.Width,source.Height);var bits=source.LockBits(rect,System.Drawing.Imaging.ImageLockMode.ReadOnly,System.Drawing.Imaging.PixelFormat.Format24bppRgb);try{var image=BitmapSource.Create(source.Width,source.Height,96,96,PixelFormats.Bgr24,null,bits.Scan0,Math.Abs(bits.Stride)*source.Height,bits.Stride);image.Freeze();return image;}finally{source.UnlockBits(bits);}}
 void DrawCurve(){var canvas=Find<Canvas>("CurveCanvas");if(canvas==null||canvas.ActualWidth<1||canvas.ActualHeight<1||Engine==null)return;canvas.Children.Clear();double w=canvas.ActualWidth,h=canvas.ActualHeight;for(int i=0;i<=4;i++){canvas.Children.Add(new Line{X1=0,X2=w,Y1=h*i/4,Y2=h*i/4,Stroke=ColorBrush("#30382A"),StrokeThickness=.6});}canvas.Children.Add(new Line{X1=0,Y1=h,X2=w,Y2=0,Stroke=ColorBrush("#46523D"),StrokeDashArray=new DoubleCollection{3,4},StrokeThickness=.7});double[] values=Engine.StudioValues;string[] colors=activeTheme!=null&&activeTheme.Id=="white"?new string[]{"#B45D48","#2B8750","#4F79A6"}:new string[]{"#DDA693","#C5F778","#92ADCB"};for(int c=0;c<3;c++){var line=new Polyline{Stroke=new SolidColorBrush(ParseColor(colors[c])),StrokeThickness=1.6,Opacity=.9};for(int i=0;i<128;i++)line.Points.Add(new Point(i*w/127,h-LumaDesk.Curve(i/127.0,c,values)*h));canvas.Children.Add(line);}}
 void RefreshProfiles(){var profiles=Engine.StudioProfiles;string signature=sessionChoice==null?"":sessionChoice.ProfileName+"|";foreach(var p in profiles)signature+=p.Name+"|"+p.Process+"|"+p.Sharpness.ToString(CultureInfo.InvariantCulture)+"|"+String.Join(",",Array.ConvertAll(p.Values,delegate(double x){return x.ToString(CultureInfo.InvariantCulture); }))+";";if(signature==profileSignature&&Find<StackPanel>("ProfileCards").Children.Count>0)return;profileSignature=signature;var host=Find<StackPanel>("ProfileCards");host.Children.Clear();
  if(profiles.Count==0){var empty=new StackPanel{Margin=new Thickness(25,55,25,20)};empty.Children.Add(Text("Пока чистый лист.",27,"#D5E2C9"));var hint=Text("Настройте изображение и сохраните свой первый профиль. Он появится здесь.",14,"#87977D");hint.Margin=new Thickness(0,15,0,0);empty.Children.Add(hint);host.Children.Add(empty);return;}
  foreach(var profile in profiles){var p=profile;var card=new Border{Style=StyleFor("Card"),Margin=new Thickness(0,0,8,12),Padding=new Thickness(20),Background=ColorBrush(IsSelectedProfile(p)?"#293420":"#22291E")};var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});var words=new StackPanel();words.Children.Add(Text(p.Name,19,"#E6EDDF"));var meta=Text((String.IsNullOrEmpty(p.Process)?"Ручное применение":p.Process+".exe")+"  ·  γ "+p.Values[0].ToString("0.00",CultureInfo.CurrentCulture),11,"#91A082");meta.Margin=new Thickness(0,9,0,0);words.Children.Add(meta);grid.Children.Add(words);var actions=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};var apply=MakeButton("Применить",delegate{Engine.StudioApplyProfile(p);ApplyHardwareProfile(p);RememberProfile(p.Name,-1);Find<TextBox>("ProfileName").Text=p.Name;Find<TextBox>("ProfileProcess").Text=p.Process;Synchronize();},false);actions.Children.Add(apply);var delete=MakeButton("✕",delegate{Engine.StudioDeleteProfile(p);ForgetProfile(p.Name);profileSignature="";RefreshProfiles();},false);delete.Margin=new Thickness(8,0,0,0);delete.Padding=new Thickness(12);delete.ToolTip="Удалить профиль";actions.Children.Add(delete);Grid.SetColumn(actions,1);grid.Children.Add(actions);card.Child=grid;host.Children.Add(card);}
 }
 void WireHotkey(){Find<Button>("HotkeyButton").Click+=delegate{Find<Border>("SettingsOverlay").Visibility=Visibility.Collapsed;CloseThemePicker();pendingKey=Engine.StudioKey;pendingModifiers=Engine.StudioModifiers;Find<TextBox>("HotkeyCapture").Text=Engine.StudioHotkey;Find<TextBlock>("HotkeyError").Text="";Engine.StudioCaptureHotkey(true);Find<Border>("HotkeyOverlay").Visibility=Visibility.Visible;Find<TextBox>("HotkeyCapture").Focus();};Find<Button>("CancelHotkey").Click+=delegate{CloseHotkey();};
  Find<TextBox>("HotkeyCapture").PreviewKeyDown+=delegate(object sender,KeyEventArgs e){e.Handled=true;Key key=e.Key==Key.System?e.SystemKey:e.Key;if(key==Key.Escape){CloseHotkey();return;}if(key==Key.LeftCtrl||key==Key.RightCtrl||key==Key.LeftAlt||key==Key.RightAlt||key==Key.LeftShift||key==Key.RightShift||key==Key.LWin||key==Key.RWin)return;ModifierKeys mods=Keyboard.Modifiers;uint m=((mods&ModifierKeys.Control)!=0?2u:0)|((mods&ModifierKeys.Alt)!=0?1u:0)|((mods&ModifierKeys.Shift)!=0?4u:0),k=(uint)KeyInterop.VirtualKeyFromKey(key);if(!LumaDesk.ValidHotkey(m,k)){Find<TextBlock>("HotkeyError").Text="Добавьте Ctrl или Alt, либо используйте F1–F24.";return;}pendingKey=k;pendingModifiers=m;Find<TextBox>("HotkeyCapture").Text=LumaDesk.HotkeyText(m,k);Find<TextBlock>("HotkeyError").Text="";};
  Find<Button>("SaveHotkey").Click+=delegate{string error;if(Engine.ChangeHotkey(pendingModifiers,pendingKey,out error)){CloseHotkey();Synchronize();}else Find<TextBlock>("HotkeyError").Text=error;};
 }
 void CloseHotkey(){bool wasOpen=Find<Border>("HotkeyOverlay").Visibility==Visibility.Visible;Find<Border>("HotkeyOverlay").Visibility=Visibility.Collapsed;if(wasOpen&&!closing)Find<Border>("SettingsOverlay").Visibility=Visibility.Visible;Engine.StudioCaptureHotkey(false);Keyboard.ClearFocus();}
 void Export(){var dialog=new Microsoft.Win32.SaveFileDialog{Filter="Cube LUT (*.cube)|*.cube",FileName="Lumos.cube"};if(dialog.ShowDialog(this)!=true)return;try{Engine.StudioExport(dialog.FileName);Synchronize();}catch(Exception ex){MessageBox.Show(this,"Не удалось сохранить LUT: "+ex.Message,"Lumos",MessageBoxButton.OK,MessageBoxImage.Warning);}}
 void OpenExpanded(){
  if(expanded!=null){expanded.Activate();return;}
  var window=new Window{Title="Lumos · предпросмотр",Width=1200,Height=760,MinWidth=640,MinHeight=400,Owner=this,WindowStartupLocation=WindowStartupLocation.CenterOwner,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.CanResize,Background=ColorBrush("#10150D"),Foreground=Foreground,FontFamily=FontFamily,Resources=root.Resources,Icon=Icon};expanded=window;
  WindowChrome.SetWindowChrome(window,new WindowChrome{CaptionHeight=0,ResizeBorderThickness=new Thickness(6),GlassFrameThickness=new Thickness(0)});
  var grid=new Grid();grid.RowDefinitions.Add(new RowDefinition{Height=new GridLength(50)});grid.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});grid.RowDefinitions.Add(new RowDefinition{Height=new GridLength(44)});
  var bar=new Border{Background=ColorBrush("#1B2117"),Padding=new Thickness(22,0,10,0)};var barGrid=new Grid();bar.Child=barGrid;var title=Text("LUMOS   /   ЖИВОЙ ПРЕДПРОСМОТР",11,"#BBCCA8");title.VerticalAlignment=VerticalAlignment.Center;barGrid.Children.Add(title);var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};foreach(string symbol in new string[]{"─","□","✕"}){string action=symbol;var b=MakeButton(symbol,delegate{if(action=="✕")window.Close();else if(action=="─")window.WindowState=WindowState.Minimized;else window.WindowState=window.WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;},false);b.Style=StyleFor("Chrome");buttons.Children.Add(b);}barGrid.Children.Add(buttons);bar.MouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e){if(e.ClickCount==2)window.WindowState=window.WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;else if(e.ButtonState==MouseButtonState.Pressed)window.DragMove();};grid.Children.Add(bar);
  var viewer=new FrameComparison();viewer.ApplyTheme(activeTheme);expandedViewer=viewer;viewer.SetImages(currentBefore,currentAfter);Grid.SetRow(viewer,1);grid.Children.Add(viewer);var hint=Text("Перетащите границу До / После. Настройки остаются доступны в основном окне.",12,"#9CA98F");hint.HorizontalAlignment=HorizontalAlignment.Center;hint.VerticalAlignment=VerticalAlignment.Center;Grid.SetRow(hint,2);grid.Children.Add(hint);window.Content=grid;
  var model=new PreviewModel();Action render=delegate{IntPtr hwnd=new WindowInteropHelper(window).Handle;string device=System.Windows.Forms.Screen.FromHandle(hwnd).DeviceName;Engine.StudioFillPreview(model,device);viewer.SetImages(Bitmap(model.Before??model.Original),Bitmap(model.After??model.Original));};EventHandler handler=delegate{render();};Engine.StudioPreview.Changed+=handler;window.Loaded+=delegate{render();};window.LocationChanged+=delegate{if(window.IsLoaded)render();};window.Closed+=delegate{Engine.StudioPreview.Changed-=handler;model.Dispose();expanded=null;expandedViewer=null;};window.Show();
 }
 internal void RenderTestPreview(){Engine.StudioRefreshPreview();PreviewChanged(null,EventArgs.Empty);UpdateLayout();}
}
