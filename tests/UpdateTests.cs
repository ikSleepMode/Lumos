using System;
using System.IO;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
class UpdateTests {
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static int Main(){try{
  string file=Path.GetTempFileName();try{
   File.WriteAllText(file,"installer-test");string hash;using(var sha=SHA256.Create())hash="sha256:"+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(file))).Replace("-","").ToLowerInvariant();
   var asset=new ReleaseAsset{name="Lumos-Setup.msi",size=new FileInfo(file).Length,digest=hash,browser_download_url="https://github.com/ikSleepMode/Lumos/releases/download/v0.0.9-alpha/Lumos-Setup.msi"};
   Check(LumosUpdates.Verify(file,asset),"Valid digest rejected");File.AppendAllText(file,"tampered");Check(!LumosUpdates.Verify(file,asset),"Tampered installer accepted");
   var release=new AppRelease{tag_name="v0.0.9-alpha",assets=new[]{asset}};var serializer=new JavaScriptSerializer();string json=serializer.Serialize(new[]{release});
   Check(LumosUpdates.Select(json,new Version(0,0,8))!=null,"Alpha update ignored");Check(LumosUpdates.Select(json,new Version(0,0,9))==null,"Same version accepted");Check(LumosUpdates.Select(json,new Version(0,0,10))==null,"Downgrade accepted");
   release.draft=true;Check(LumosUpdates.Select(serializer.Serialize(new[]{release}),new Version(0,0,8))==null,"Draft accepted");release.draft=false;
   asset.browser_download_url="https://github.com/attacker/other/releases/download/v1/Lumos-Setup.msi";Check(LumosUpdates.Installer(release)==null,"Other repository accepted");
   Check(!LumosUpdates.ValidUrl("http://github.com/ikSleepMode/Lumos/releases/download/v1/a"),"HTTP accepted");Check(LumosUpdates.ParseVersion("broken")==null,"Malformed version accepted");
  }finally{File.Delete(file);}Console.WriteLine("PASS: release versions, alpha channel, no downgrades, draft exclusion, repository/HTTPS restrictions and installer integrity");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
