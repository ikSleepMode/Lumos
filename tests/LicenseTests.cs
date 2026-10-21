using System;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
class LicenseTests {
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 [STAThread]static int Main(){try{
  string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"license-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);string path=Path.Combine(folder,"activation.bin");
  using(var rsa=LicenseCrypto.Rsa()){
   string pub=rsa.ToXmlString(false),priv=rsa.ToXmlString(true),fingerprint=LicenseCrypto.Hash("synthetic-device-A"),other=LicenseCrypto.Hash("synthetic-device-B");string request=LicenseCrypto.Request(fingerprint);DateTime now=new DateTime(2026,10,21,12,0,0,DateTimeKind.Utc),expiry;string error;
   Check(LicenseCrypto.Fingerprint(request)==fingerprint,"Request roundtrip");bool rejected=false;try{LicenseCrypto.Fingerprint(request+"A");}catch(FormatException){rejected=true;}Check(rejected,"Request checksum missing");
   string token=LicenseCrypto.Issue(request,now.AddDays(1),now,priv);
   Check(LicenseCrypto.Verify(token,fingerprint,now,pub,out expiry,out error)&&expiry==now.AddDays(1),"Valid signature rejected");
   Check(!LicenseCrypto.Verify(token,other,now,pub,out expiry,out error),"Other device accepted");
   Check(!LicenseCrypto.Verify(token,fingerprint,now.AddDays(1),pub,out expiry,out error),"Exact expiry accepted");
   Check(!LicenseCrypto.Verify(token,fingerprint,now.AddMinutes(-6),pub,out expiry,out error),"Before issuance accepted");
   var parts=token.Split('.');byte[] signature=LicenseCrypto.Decode(parts[1]);signature[0]^=1;Check(!LicenseCrypto.Verify(parts[0]+"."+LicenseCrypto.Encode(signature),fingerprint,now,pub,out expiry,out error),"Tampered signature accepted");
   string payload=System.Text.Encoding.UTF8.GetString(LicenseCrypto.Decode(parts[0])).Replace(fingerprint,other);Check(!LicenseCrypto.Verify(LicenseCrypto.Encode(System.Text.Encoding.UTF8.GetBytes(payload))+"."+parts[1],other,now,pub,out expiry,out error),"Tampered payload accepted");
   using(var unknown=LicenseCrypto.Rsa()){Check(!LicenseCrypto.Verify(token,fingerprint,now,unknown.ToXmlString(false),out expiry,out error),"Unknown issuer accepted");}
   foreach(string bad in new[]{"","garbage","a.b",new string('X',5000)})Check(!LicenseCrypto.Verify(bad,fingerprint,now,pub,out expiry,out error),"Malformed token accepted");
   var manager=new LicenseManager(fingerprint,pub,path);Check(!manager.Check(now),"Missing license accepted");Check(manager.Activate(token,now),"Activation save failed");Check(manager.Check(now.AddHours(2)),"License check failed");
   Check(!File.ReadAllText(path).Contains(token),"Activation stored in plain text");
   manager=new LicenseManager(fingerprint,pub,path);Check(!manager.Check(now.AddHours(1)),"Clock rollback after restart accepted");Check(!manager.Activate(token,now.AddHours(1)),"Reactivation bypassed remembered clock rollback");Check(manager.Check(now.AddHours(3)),"Clock recovery failed");
   manager=new LicenseManager(other,pub,path);Check(!manager.Check(now.AddHours(3)),"Copied activation accepted on other HWID");
   manager=new LicenseManager(fingerprint,pub,path);Check(!manager.Check(now.AddDays(2)),"Saved expired activation accepted");
   string renewal=LicenseCrypto.Issue(request,now.AddDays(30),now.AddDays(2),priv);Check(manager.Activate(renewal,now.AddDays(2)),"Renewal rejected");Check(new LicenseManager(fingerprint,pub,path).Check(now.AddDays(3)),"Renewal not persisted");
   File.WriteAllText(path,"corrupt");Check(!new LicenseManager(fingerprint,pub,path).Check(now),"Corrupt activation accepted");
   var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};var dialog=new ActivationWindow(new LicenseManager(fingerprint,pub,Path.Combine(folder,"ui.bin")));dialog.Show();dialog.UpdateLayout();Check(dialog.Title.Contains("Lumos"),"Activation UI missing");var image=new RenderTargetBitmap((int)dialog.ActualWidth,(int)dialog.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(dialog);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using(var output=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"activation-preview.png")))png.Save(output);dialog.Close();
   var studio=new StudioWindow(true,null);int writes=0;studio.Engine.gammaWriter=delegate(string device,ushort[] ramp){writes++;return new GammaResult{Confirmed=true};};studio.GuardLicense(manager);studio.Show();studio.Engine.StudioToggle();Check(studio.Engine.StudioEnabled,"Test correction not enabled");int before=writes;Check(!studio.CheckLicense(manager,now.AddDays(31)),"Expired session remained allowed");Check(!studio.IsVisible&&writes>before,"Expiry must close studio and restore gamma");app.Shutdown();
  }
  Console.WriteLine("PASS: RSA signatures, device binding, expiry, tampering, unknown issuer, corrupt state, DPAPI persistence, clock rollback across restarts, renewal, activation UI");return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
