using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;

public sealed class ReleaseAsset {public string name,browser_download_url,digest;public long size;}
public sealed class AppRelease {public string tag_name,html_url;public bool draft;public ReleaseAsset[] assets;}
public static class LumosUpdates {
 public const string Repository="ikSleepMode/Lumos";
 public static Version ParseVersion(string tag){Version v;return Version.TryParse((tag??"").TrimStart('v').Split('-')[0],out v)?v:null;}
 public static AppRelease Select(string json,Version current){
  var releases=new JavaScriptSerializer{MaxJsonLength=2097152}.Deserialize<AppRelease[]>(json);AppRelease best=null;Version newest=current;
  foreach(var r in releases??new AppRelease[0]){var v=ParseVersion(r.tag_name);if(r.draft||v==null||v<=newest||Installer(r)==null)continue;best=r;newest=v;}return best;
 }
 public static ReleaseAsset Installer(AppRelease release){foreach(var a in release.assets??new ReleaseAsset[0])if(a.name=="Lumos-Setup.msi"&&a.size>0&&a.size<104857600&&ValidUrl(a.browser_download_url)&&a.digest!=null&&System.Text.RegularExpressions.Regex.IsMatch(a.digest,"^sha256:[a-fA-F0-9]{64}$"))return a;return null;}
 public static bool ValidUrl(string value){Uri u;return Uri.TryCreate(value,UriKind.Absolute,out u)&&u.Scheme=="https"&&u.Host=="github.com"&&u.AbsolutePath.StartsWith("/"+Repository+"/releases/download/",StringComparison.Ordinal)&&String.IsNullOrEmpty(u.UserInfo);}
 public static bool Verify(string file,ReleaseAsset asset){if(new FileInfo(file).Length!=asset.size)return false;using(var sha=SHA256.Create())using(var stream=File.OpenRead(file))return String.Equals("sha256:"+BitConverter.ToString(sha.ComputeHash(stream)).Replace("-",""),asset.digest,StringComparison.OrdinalIgnoreCase);}
 public static WebClient Client(){ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;var c=new UpdateClient();c.Headers[HttpRequestHeader.UserAgent]="Lumos-Updater";c.Headers[HttpRequestHeader.Accept]="application/vnd.github+json";return c;}
 sealed class UpdateClient:WebClient {protected override WebRequest GetWebRequest(Uri address){var r=base.GetWebRequest(address);r.Timeout=30000;return r;}}
}
public sealed partial class StudioWindow {
 bool updateBusy;AppRelease availableUpdate;
 void InitializeUpdates(){
  Find<Button>("CheckUpdates").Click+=async delegate{await CheckUpdates(true);};
  Find<Button>("InstallUpdate").Click+=async delegate{await InstallUpdate();};
  Loaded+=async delegate{if(!testing)await CheckUpdates(false);};
 }
 async Task CheckUpdates(bool manual){
  if(updateBusy)return;updateBusy=true;Find<Button>("CheckUpdates").IsEnabled=false;
  try{Find<TextBlock>("UpdateStatus").Text="Проверка обновлений…";string json;using(var client=LumosUpdates.Client())json=await client.DownloadStringTaskAsync("https://api.github.com/repos/"+LumosUpdates.Repository+"/releases?per_page=100");if(closing)return;
   availableUpdate=LumosUpdates.Select(json,Assembly.GetExecutingAssembly().GetName().Version);
   Find<Button>("InstallUpdate").Visibility=availableUpdate==null?Visibility.Collapsed:Visibility.Visible;
   Find<TextBlock>("UpdateStatus").Text=availableUpdate==null?"Установлена актуальная версия.":"Доступна версия "+availableUpdate.tag_name+". Можно скачать и установить.";
   if(availableUpdate!=null)Find<TextBlock>("StatusText").Text="Доступно обновление Lumos "+availableUpdate.tag_name+" — откройте настройки ⚙.";
  }catch(Exception){if(!closing)Find<TextBlock>("UpdateStatus").Text="Не удалось проверить обновления. Проверьте подключение и повторите позже.";}
  finally{updateBusy=false;if(!closing)Find<Button>("CheckUpdates").IsEnabled=true;}
 }
 async Task InstallUpdate(){
  if(updateBusy||availableUpdate==null)return;var asset=LumosUpdates.Installer(availableUpdate);if(asset==null)return;
  updateBusy=true;Find<Button>("InstallUpdate").IsEnabled=false;string file=null;
  try{
   string folder=Path.Combine(Path.GetTempPath(),"LumosUpdates",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);file=Path.Combine(folder,"Lumos-Setup.msi");
   using(var client=LumosUpdates.Client()){client.DownloadProgressChanged+=delegate(object s,DownloadProgressChangedEventArgs e){if(!closing)Find<TextBlock>("UpdateStatus").Text="Скачивание: "+e.ProgressPercentage+"%";};await client.DownloadFileTaskAsync(asset.browser_download_url,file);}if(closing)return;
   if(!LumosUpdates.Verify(file,asset))throw new InvalidDataException("Проверка целостности установщика не пройдена.");
   if(MessageBox.Show(this,"Обновление скачано и проверено. Закрыть Lumos, восстановить экран и открыть установщик? После установки запустите приложение через ярлык.","Обновление Lumos",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes){File.Delete(file);return;}
   System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"msiexec.exe"),"/i \""+file+"\""){UseShellExecute=true});exitRequested=true;Close();
  }catch(Exception ex){if(file!=null&&File.Exists(file))File.Delete(file);if(!closing)Find<TextBlock>("UpdateStatus").Text="Обновление не установлено: "+ex.Message;}
  finally{updateBusy=false;if(!closing)Find<Button>("InstallUpdate").IsEnabled=true;}
 }
}
