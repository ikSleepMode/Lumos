using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using System.Diagnostics;
using System.Globalization;

public class Profile {
 public double Sharpness=-1;
 public string Name = "Мой профиль";
 public string Process = "";
 public double[] Values = new double[]{1,1,1,1,1,1,6500,0,0};
 public override string ToString(){return Name;}
}
public class DisplayItem {
 public string Device; public string Label;public string Identity;
 public override string ToString(){return Label;}
}
public partial class LumaDesk : Form {
 [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] struct DISPLAY_DEVICE {
  public int cb; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string DeviceName;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string DeviceString;
  public int StateFlags;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string DeviceID;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string DeviceKey;
 }
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool EnumDisplayDevices(string device,uint number,ref DISPLAY_DEVICE data,uint flags);
 [DllImport("gdi32.dll",CharSet=CharSet.Unicode)] static extern IntPtr CreateDC(string driver,string device,string output,IntPtr init);
 [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
 [DllImport("gdi32.dll")] static extern bool GetDeviceGammaRamp(IntPtr dc,[Out] ushort[] ramp);
 [DllImport("gdi32.dll")] static extern bool SetDeviceGammaRamp(IntPtr dc,ushort[] ramp);
 [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hwnd,int id,uint modifiers,uint key);
 [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hwnd,int id);
 readonly Color bg=Color.FromArgb(17,21,30), panel=Color.FromArgb(27,33,45), accent=Color.FromArgb(112,224,193);
 readonly Dictionary<string,ushort[]> originals=new Dictionary<string,ushort[]>();
 readonly List<Profile> profiles=new List<Profile>();
 readonly SilkSlider[] sliders=new SilkSlider[9];
 readonly double[] defaults={1,1,1,1,1,1,6500,0,0};
 readonly double[] scales={100,100,100,100,100,100,1,100,100};
 ComboBox monitors, saved; TextBox profileName, processName; Label status; Button toggle; CheckBox auto;
 NotifyIcon tray; System.Windows.Forms.Timer debounce, watcher; bool enabled=false,loading=false; Profile activeAuto; double[] beforeAuto;
 string dataPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LumaDesk","profiles.xml");
 public LumaDesk():this(null){}
 internal LumaDesk(string preferencesFile):this(preferencesFile,false){}
 internal LumaDesk(string preferencesFile,bool modern):this(preferencesFile,modern,null){}
 internal LumaDesk(string preferencesFile,bool modern,string profilesFile){
  if(profilesFile!=null)dataPath=profilesFile;
  if(preferencesFile!=null)settingsPath=preferencesFile;
  LoadHotkeyPreferences();
  gammaWriter=WriteGamma;
  BuildInterface();
  debounce=new System.Windows.Forms.Timer{Interval=90};debounce.Tick+=delegate{debounce.Stop();if(enabled)Apply();};
  watcher=new System.Windows.Forms.Timer{Interval=2000};watcher.Tick+=delegate{WatchProcesses();};watcher.Start();
  tray=new NotifyIcon{Text="LumaDesk",Icon=SystemIcons.Application,Visible=!modern};tray.DoubleClick+=delegate{Show();WindowState=FormWindowState.Normal;Activate();};
  var menu=new ContextMenuStrip();menu.Items.Add("Открыть",null,delegate{Show();Activate();});menu.Items.Add("Вкл / выкл",null,delegate{enabled=!enabled;Apply();});menu.Items.Add("Выйти и восстановить экран",null,delegate{Close();});tray.ContextMenuStrip=menu;
  LoadProfiles();EnumerateMonitors();var initialDisplay=monitors.SelectedItem as DisplayItem;selectedDisplayDevice=initialDisplay==null?null:initialDisplay.Device;monitors.SelectedIndexChanged+=delegate{SwitchDisplay();};UpdateLabels();
  Shown+=delegate{StartHotkey();};
  FormClosing+=delegate{RestoreAll();if(hotkeyRegistered)hotkeyUnregister(Handle,activeHotkeyId);tray.Dispose();};
 }
 Label Title(string text,int size){return new Label{Text=text,AutoSize=true,Font=new Font("Segoe UI",size,FontStyle.Bold),Margin=new Padding(3,5,0,12)};}
 Button Button(string text,EventHandler click){var b=new Button{Text=text,AutoSize=true,MinimumSize=new Size(120,36),FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(39,49,65),ForeColor=Color.White,Margin=new Padding(3,5,6,5),Padding=new Padding(8,2,8,2)};b.FlatAppearance.BorderSize=0;b.Click+=click;return b;}
 void AddPreset(FlowLayoutPanel p,string name,double[] v){p.Controls.Add(Button(name,delegate{SetValues(v);enabled=true;Apply();}));}
 double[] Current(){double[] v=new double[9];for(int i=0;i<9;i++)v[i]=sliders[i].Value/scales[i];return v;}
 void SetValues(double[] v){if(v==null||v.Length!=9)return;loading=true;for(int i=0;i<9;i++)sliders[i].Value=Math.Max(sliders[i].Minimum,Math.Min(sliders[i].Maximum,(int)Math.Round(v[i]*scales[i])));loading=false;UpdateLabels();}
 void UpdateLabels(){SyncNumberInputs();if(tonePreview!=null){tonePreview.Settings=Current();tonePreview.Invalidate();}SchedulePreview();}
 void EnumerateMonitors(){
  var names=MonitorNames.Read();var identities=MonitorNames.ReadIdentities();
  for(uint i=0;;i++){DISPLAY_DEVICE d=new DISPLAY_DEVICE();d.cb=Marshal.SizeOf(d);if(!EnumDisplayDevices(null,i,ref d,0))break;if((d.StateFlags&1)==0||(d.StateFlags&8)!=0)continue;
   IntPtr dc=CreateDC("DISPLAY",d.DeviceName,null,IntPtr.Zero);if(dc==IntPtr.Zero)continue;var ramp=new ushort[768];bool ok=GetDeviceGammaRamp(dc,ramp);DeleteDC(dc);if(!ok)continue;
   string friendly;if(!names.TryGetValue(d.DeviceName,out friendly)){DISPLAY_DEVICE child=new DISPLAY_DEVICE();child.cb=Marshal.SizeOf(child);friendly=EnumDisplayDevices(d.DeviceName,0,ref child,0)&&!String.IsNullOrWhiteSpace(child.DeviceString)?child.DeviceString:"Дисплей «"+d.DeviceName+"»";}originals[d.DeviceName]=ramp;string identity;identities.TryGetValue(d.DeviceName,out identity);monitors.Items.Add(new DisplayItem{Device=d.DeviceName,Label=friendly,Identity=identity});
  }if(monitors.Items.Count>0)monitors.SelectedIndex=0;
 }
 public static double Curve(double x,int channel,double[] v){
  double temp=v[6];double warmth=(6500-temp)/4000.0;
  double factor=channel==0?1+Math.Min(0,warmth)*0.18:channel==1?1-Math.Max(0,warmth)*0.12:1-Math.Max(0,warmth)*0.42;
  double y=Math.Pow(x,1/v[0]);y=(y-0.5)*v[2]+0.5;y=y*v[1]*v[3+channel]*factor+v[7]*(1-x)+v[8]*x*x;
  return Math.Max(0,Math.Min(1,y));
 }
 public static ushort[] MakeRamp(double[] v){var r=new ushort[768];for(int c=0;c<3;c++)for(int i=0;i<256;i++)r[c*256+i]=(ushort)Math.Round(Curve(i/255.0,c,v)*65535);return r;}
 void Apply(){
  debounce.Stop();SchedulePreview();
  var d=monitors.SelectedItem as DisplayItem;if(d==null){enabled=false;UpdateToggle();status.Text="Доступен предпросмотр. Windows не предоставила доступ к гамме экрана.";return;}
  bool requested=enabled;double[] attempted=Current();ConfirmedGammaState previous=ConfirmedState(d.Device);
  if(!requested&&!changedDisplays.Contains(d.Device)){UpdateToggle();status.Text="Коррекция экрана выключена. Предпросмотр продолжает работать.";return;}
  ushort[] ramp=requested?MakeRamp(attempted):originals[d.Device];
  GammaResult result=gammaWriter(d.Device,ramp);RecordGammaResult(d.Device,requested,result,attempted);
  if(result.Confirmed){RememberConfirmed(d.Device,attempted,ramp,requested);displayDrafts[d.Device]=(double[])attempted.Clone();status.Text=requested?"Коррекция применена и проверена · "+d.Label:"Исходная гамма восстановлена · "+d.Label;}
  else RecoverRejected(d.Device,requested,previous);
  UpdateToggle();
 }
 void RestoreAll(){foreach(var device in new List<string>(changedDisplays)){var result=gammaWriter(device,originals[device]);RecordGammaResult(device,false,result);if(result.Confirmed)changedDisplays.Remove(device);}}
 void RefreshProfiles(){saved.DataSource=null;saved.DataSource=new List<Profile>(profiles);}
 void LoadProfiles(){try{if(File.Exists(dataPath))using(var f=File.OpenRead(dataPath)){var list=(List<Profile>)new XmlSerializer(typeof(List<Profile>)).Deserialize(f);foreach(var p in list)if(p.Values!=null&&p.Values.Length==9)profiles.Add(p);}}catch(Exception){ }RefreshProfiles();}
 void Persist(){try{Directory.CreateDirectory(Path.GetDirectoryName(dataPath));string tmp=dataPath+".tmp";using(var f=File.Create(tmp))new XmlSerializer(typeof(List<Profile>)).Serialize(f,profiles);File.Copy(tmp,dataPath,true);File.Delete(tmp);}catch(Exception e){MessageBox.Show("Не удалось сохранить профили: "+e.Message);}}
 void SaveProfile(){string n=profileName.Text.Trim();if(n.Length==0)return;Profile p=profiles.Find(delegate(Profile x){return x.Name==n;});if(p==null){p=new Profile();profiles.Add(p);}p.Name=n;p.Process=Path.GetFileNameWithoutExtension(processName.Text.Trim());p.Values=Current();Persist();RefreshProfiles();saved.SelectedItem=p;}
 void WatchProcesses(){
  Profile found=null;if(auto.Checked){var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(var p in Process.GetProcesses()){try{names.Add(p.ProcessName);}catch{}finally{p.Dispose();}}foreach(var p in profiles)if(!String.IsNullOrEmpty(p.Process)&&names.Contains(p.Process)){found=p;break;}}
  if(found==activeAuto)return;
  if(found!=null){if(activeAuto==null){beforeAuto=Current();autoWasEnabled=enabled;}SetValues(found.Values);enabled=true;activeAuto=found;Apply();}
  else if(activeAuto!=null){activeAuto=null;if(beforeAuto!=null)SetValues(beforeAuto);enabled=autoWasEnabled;Apply();}
 }
 bool autoWasEnabled;
 void ExportLut(){using(var dialog=new SaveFileDialog{Filter="Cube LUT (*.cube)|*.cube",FileName="LumaDesk.cube"}){if(dialog.ShowDialog()!=DialogResult.OK)return;try{double[] v=Current();using(var w=new StreamWriter(dialog.FileName)){w.WriteLine("TITLE \"Lumos\"\nLUT_3D_SIZE 33\nDOMAIN_MIN 0 0 0\nDOMAIN_MAX 1 1 1");for(int b=0;b<33;b++)for(int g=0;g<33;g++)for(int r=0;r<33;r++)w.WriteLine(String.Format(CultureInfo.InvariantCulture,"{0:F6} {1:F6} {2:F6}",Curve(r/32.0,0,v),Curve(g/32.0,1,v),Curve(b/32.0,2,v)));}status.Text="LUT сохранён: "+dialog.FileName;}catch(Exception e){MessageBox.Show(e.Message);}}}
 protected override void WndProc(ref Message m){if(m.Msg==0x0312&&hotkeyRegistered&&!editingHotkey&&m.WParam.ToInt32()==activeHotkeyId){enabled=!enabled;Apply();}base.WndProc(ref m);}
 [STAThread] public static void Main(){bool first;using(var mutex=new System.Threading.Mutex(true,"Local\\LumaDesk.Unique",out first)){if(!first){MessageBox.Show("LumaDesk уже запущен. Откройте окно через значок в трее.");return;}Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new LumaDesk());}}
}
