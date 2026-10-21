using System;
using System.IO;
using System.Management;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

public sealed class LicenseManager {
 public readonly string Fingerprint,PublicKey,StatePath;public DateTime Expiry;public string Error;DateTime lastSeen;string token;
 public LicenseManager(string fingerprint,string publicKey,string path){Fingerprint=fingerprint;PublicKey=publicKey;StatePath=path;}
 public static LicenseManager Create(){string key;using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Lumos.LicensePublicKey.xml")){if(stream==null)throw new InvalidOperationException("Нет ключа проверки активации.");using(var reader=new StreamReader(stream))key=reader.ReadToEnd();}return new LicenseManager(DeviceFingerprint(),key,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LumaDesk","activation.bin"));}
 static string DeviceFingerprint(){
  string identity=null;using(var search=new ManagementObjectSearcher("SELECT UUID FROM Win32_ComputerSystemProduct"))foreach(ManagementObject item in search.Get()){using(item){string value=Convert.ToString(item["UUID"]).Trim().ToUpperInvariant();Guid guid;if(Guid.TryParse(value,out guid)&&guid!=Guid.Empty&&value!="FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF")identity="SMBIOS/"+value;}}
  if(identity==null){using(var hive=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64))using(var key=hive.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography")){string value=key==null?null:Convert.ToString(key.GetValue("MachineGuid"));if(String.IsNullOrWhiteSpace(value))throw new InvalidOperationException("Не удалось получить идентификатор устройства.");identity="WINDOWS/"+value.ToUpperInvariant();}}
  return LicenseCrypto.Hash("Lumos/device/1/"+identity);
 }
 public bool Check(DateTime now){
  try{if(token==null){if(!File.Exists(StatePath)){Error="Введите код, полученный от владельца Lumos.";return false;}string plain=Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(StatePath),null,DataProtectionScope.CurrentUser));var p=plain.Split('\n');if(p.Length!=2)throw new InvalidDataException();token=p[0];lastSeen=new DateTime(Int64.Parse(p[1]),DateTimeKind.Utc);}
   if(now.Ticks+TimeSpan.FromMinutes(5).Ticks<lastSeen.Ticks){Error="Обнаружен перевод часов назад. Верните правильное время Windows.";return false;}
   if(!LicenseCrypto.Verify(token,Fingerprint,now,PublicKey,out Expiry,out Error))return false;Save(now);return true;
  }catch(Exception ex){if(!(ex is IOException||ex is UnauthorizedAccessException||ex is CryptographicException||ex is FormatException||ex is ArgumentException))throw;token=null;Error="Не удалось прочитать или сохранить активацию. Повторите ввод кода.";return false;}
 }
 public bool Activate(string candidate,DateTime now){DateTime expiry;string error;if(!LicenseCrypto.Verify(candidate,Fingerprint,now,PublicKey,out expiry,out error)){Error=error;return false;}if(now.Ticks+TimeSpan.FromMinutes(5).Ticks<lastSeen.Ticks){Error="Верните правильное время Windows перед активацией.";return false;}string previous=token;token=candidate.Trim();try{Save(now);Expiry=expiry;Error=null;return true;}catch(Exception ex){token=previous;Error="Не удалось сохранить активацию: "+ex.Message;return false;}}
 void Save(DateTime now){DateTime high=now>lastSeen?now:lastSeen;Directory.CreateDirectory(Path.GetDirectoryName(StatePath));byte[] data=ProtectedData.Protect(Encoding.UTF8.GetBytes(token+"\n"+high.Ticks),null,DataProtectionScope.CurrentUser);string temp=StatePath+".tmp";try{File.WriteAllBytes(temp,data);if(File.Exists(StatePath))File.Replace(temp,StatePath,null);else File.Move(temp,StatePath);lastSeen=high;}finally{if(File.Exists(temp))File.Delete(temp);}}
}

public static class LicensedStartup {
 public static void Start(Application app){
  LicenseManager license;try{license=LicenseManager.Create();}catch(Exception ex){MessageBox.Show("Не удалось получить код устройства: "+ex.Message,"Lumos",MessageBoxButton.OK,MessageBoxImage.Error);app.Shutdown();return;}
  Action open=null;open=delegate{try{
   if(!license.Check(DateTime.UtcNow)&&new ActivationWindow(license).ShowDialog()!=true){app.Shutdown();return;}
   var studio=new StudioWindow(false);studio.GuardLicense(license);studio.Closed+=delegate{if(license.Check(DateTime.UtcNow))app.Shutdown();else app.Dispatcher.BeginInvoke(open);};app.MainWindow=studio;studio.Show();
  }catch(Exception ex){MessageBox.Show("Не удалось запустить Lumos: "+ex.Message,"Lumos",MessageBoxButton.OK,MessageBoxImage.Error);app.Shutdown();}};open();
 }
}
public sealed class ActivationWindow:Window {
 readonly LicenseManager manager;readonly TextBox code;readonly TextBlock status;
 public ActivationWindow(LicenseManager value){manager=value;Title="Активация Lumos";Width=620;Height=540;MinHeight=480;WindowStartupLocation=WindowStartupLocation.CenterScreen;Background=new SolidColorBrush(Color.FromRgb(24,27,29));Foreground=Brushes.White;FontFamily=new FontFamily("Segoe UI");
  var panel=new StackPanel{Margin=new Thickness(28)};Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};panel.Children.Add(new TextBlock{Text="Доступ к Lumos",FontSize=26,Margin=new Thickness(0,0,0,16)});panel.Children.Add(new TextBlock{Text="Отправьте этот код владельцу приложения. Он выдаст код активации на согласованный срок. Код содержит отпечаток устройства, без исходных серийных номеров.",TextWrapping=TextWrapping.Wrap});
  var request=new TextBox{Text=LicenseCrypto.Request(manager.Fingerprint),IsReadOnly=true,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,8),Padding=new Thickness(8)};panel.Children.Add(request);var copy=new Button{Content="Скопировать код устройства",Padding=new Thickness(10)};copy.Click+=delegate{try{Clipboard.SetText(request.Text);}catch(Exception){status.Text="Не удалось скопировать. Выделите код и нажмите Ctrl+C.";}};panel.Children.Add(copy);
  panel.Children.Add(new TextBlock{Text="Код активации",Margin=new Thickness(0,20,0,8)});code=new TextBox{AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,Height=100,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};panel.Children.Add(code);status=new TextBlock{Text=manager.Error??(manager.Expiry==DateTime.MinValue?"Введите код активации.":"Активировано до "+manager.Expiry.ToLocalTime().ToString("dd.MM.yyyy HH:mm")),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,12)};panel.Children.Add(status);
  var activate=new Button{Content="Активировать",Padding=new Thickness(12)};activate.Click+=delegate{if(manager.Activate(code.Text,DateTime.UtcNow))DialogResult=true;else status.Text=manager.Error;};panel.Children.Add(activate);
 }
}
public sealed partial class StudioWindow {
 DispatcherTimer licenseTimer;
 internal void GuardLicense(LicenseManager manager){
  var parent=(StackPanel)Find<TextBlock>("StartupStatus").Parent;var label=new TextBlock{Text="Доступ до "+manager.Expiry.ToLocalTime().ToString("dd.MM.yyyy HH:mm"),Margin=new Thickness(0,8,0,8)};parent.Children.Insert(parent.Children.Count-1,label);var renew=new Button{Content="Продлить активацию",Margin=new Thickness(0,0,0,8)};renew.Click+=delegate{new ActivationWindow(manager){Owner=this}.ShowDialog();label.Text="Доступ до "+manager.Expiry.ToLocalTime().ToString("dd.MM.yyyy HH:mm");};parent.Children.Insert(parent.Children.Count-1,renew);
  licenseTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(15)};licenseTimer.Tick+=delegate{CheckLicense(manager,DateTime.UtcNow);};Loaded+=delegate{licenseTimer.Start();};Closed+=delegate{licenseTimer.Stop();};
 }
 internal bool CheckLicense(LicenseManager manager,DateTime now){if(manager.Check(now))return true;if(licenseTimer!=null)licenseTimer.Stop();exitRequested=true;Close();return false;}
}
