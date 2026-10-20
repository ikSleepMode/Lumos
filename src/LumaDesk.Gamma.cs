using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Collections.Generic;

public class GammaResult {
 public bool Confirmed,WriteAccepted;
 public string Detail;
}
public static class GammaVerifier {
 public static bool Matches(ushort[] a,ushort[] b){if(a==null||b==null||a.Length!=768||b.Length!=768)return false;for(int i=0;i<768;i++)if(Math.Abs((int)a[i]-b[i])>512)return false;return true;}
 public static GammaResult Apply(ushort[] ramp,Func<ushort[]> read,Func<ushort[],bool> write){
  if(ramp==null||ramp.Length!=768)return new GammaResult{Detail="invalid-ramp"};
  if(Matches(read(),ramp))return new GammaResult{Confirmed=true,Detail="already-applied"};
  if(!write(ramp))return new GammaResult{Detail="write-rejected"};
  ushort[] actual=read();
  return new GammaResult{WriteAccepted=true,Confirmed=Matches(actual,ramp),Detail=actual==null?"readback-unavailable":Matches(actual,ramp)?"verified":"readback-mismatch"};
 }
}
public partial class LumaDesk {
 [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr window);
 [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr window,IntPtr dc);
 readonly HashSet<string> changedDisplays=new HashSet<string>();
 internal Func<string,ushort[],GammaResult> gammaWriter;
 GammaResult WriteUsingContext(IntPtr dc,ushort[] ramp){
  if(dc==IntPtr.Zero)return new GammaResult{Detail="no-display-context"};
  return GammaVerifier.Apply(ramp,delegate{ushort[] actual=new ushort[768];return GetDeviceGammaRamp(dc,actual)?actual:null;},delegate(ushort[] values){return SetDeviceGammaRamp(dc,values);});
 }
 GammaResult WriteGamma(string device,ushort[] ramp){
  IntPtr dc=CreateDC("DISPLAY",device,null,IntPtr.Zero);GammaResult result;
  try{result=WriteUsingContext(dc,ramp);}finally{if(dc!=IntPtr.Zero)DeleteDC(dc);}
  // Some drivers expose gamma only on the desktop DC. This fallback is restricted
  // to a single-screen desktop so a different monitor can never be affected.
  if(!result.Confirmed&&!result.WriteAccepted&&Screen.AllScreens.Length==1&&Screen.PrimaryScreen.DeviceName==device){
   dc=GetDC(IntPtr.Zero);GammaResult alternate;
   try{alternate=WriteUsingContext(dc,ramp);}finally{if(dc!=IntPtr.Zero)ReleaseDC(IntPtr.Zero,dc);}
   alternate.Detail=result.Detail+"; desktop="+alternate.Detail;result=alternate;
  }
  return result;
 }
 void RecordGammaResult(string device,bool requested,GammaResult result){
  RecordGammaResult(device,requested,result,Current());
 }
 void RecordGammaResult(string device,bool requested,GammaResult result,double[] values){
  try{string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"LumaDesk-diagnostics.log");if(File.Exists(path)&&new FileInfo(path).Length>512000)File.WriteAllText(path,"");File.AppendAllText(path,DateTime.Now.ToString("s")+" | "+device+" | enabled="+requested+" | "+result.Detail+" | confirmed="+result.Confirmed+" | values="+String.Join(",",Array.ConvertAll(values,delegate(double x){return x.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture); }))+Environment.NewLine);}catch(IOException){}catch(UnauthorizedAccessException){}
 }
 void UpdateToggle(){var display=monitors.SelectedItem as DisplayItem;toggle.Text=display!=null&&uncertainDisplays.Contains(display.Device)?"Повторить выключение":enabled?"Выключить коррекцию":"Включить коррекцию";}
}
