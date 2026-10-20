using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

public partial class LumaDesk {
 string modernPreviewDevice;bool modernStopped;
 internal double[] StudioValues {get{return Current();}}
 internal string StudioStatus {get{return status.Text;}}
 internal bool StudioEnabled {get{return enabled;}}
 internal long StudioRevision {get{return stateRevision;}}
 internal bool StudioUncertain {get{var display=monitors.SelectedItem as DisplayItem;return display!=null&&uncertainDisplays.Contains(display.Device);}}
 internal bool StudioHotkeyActive {get{return hotkeyRegistered;}}
 internal string StudioHotkey {get{return HotkeyText(hotkeyModifiers,hotkeyKey);}}
 internal uint StudioKey {get{return hotkeyKey;}}
 internal uint StudioModifiers {get{return hotkeyModifiers;}}
 internal bool StudioAuto {get{return auto.Checked;}set{auto.Checked=value;}}
 internal Profile StudioAutomaticProfile {get{return activeAuto;}}
 internal void StudioSaveSharpness(string name,double value){var profile=profiles.Find(delegate(Profile p){return p.Name==name;});if(profile!=null){profile.Sharpness=value;Persist();}}
 internal PreviewModel StudioPreview {get{return previewModel;}}
 internal List<DisplayItem> StudioDisplays {get{var list=new List<DisplayItem>();foreach(DisplayItem d in monitors.Items)list.Add(d);return list;}}
 internal List<Profile> StudioProfiles {get{return new List<Profile>(profiles);}}
 internal int StudioDisplay {get{return monitors.SelectedIndex;}set{if(value>=0&&value<monitors.Items.Count)monitors.SelectedIndex=value;}}
 internal void StudioStart(){StartHotkey();RefreshScreenshot();}
 internal void StudioToggle(){enabled=!enabled;Apply();}
 internal void StudioReset(){SetValues(defaults);enabled=false;Apply();}
 internal void StudioValue(int index,double value){numberInputs[index].Value=Math.Max(numberInputs[index].Minimum,Math.Min(numberInputs[index].Maximum,(decimal)value));}
 internal void StudioPreset(double[] values){SetValues(values);enabled=true;Apply();}
 internal void StudioPreviewValues(double[] values){SetValues(values);}
 internal void StudioLoadProfilePreview(Profile profile){if(profile==null)return;debounce.Stop();enabled=false;SetValues(profile.Values);stateRevision++;UpdateToggle();}
 internal void StudioRefreshPreview(){RefreshScreenshot();}
 internal void StudioFillPreview(PreviewModel model,string device){ushort[] current=null,baseline=null;IntPtr dc=CreateDC("DISPLAY",device,null,IntPtr.Zero);if(dc!=IntPtr.Zero){try{var ramp=new ushort[768];if(GetDeviceGammaRamp(dc,ramp))current=ramp;}finally{DeleteDC(dc);}}originals.TryGetValue(device,out baseline);model.Update(Current(),baseline,current);}
 internal void StudioSelectPreviewDisplay(string device){if(modernPreviewDevice==device)return;modernPreviewDevice=device;SchedulePreview();}
 internal void StudioSaveProfile(string name,string process){profileName.Text=name;processName.Text=process;SaveProfile();}
 internal void StudioApplyProfile(Profile profile){if(profile==null)return;SetValues(profile.Values);enabled=true;Apply();}
 internal void StudioDeleteProfile(Profile profile){if(profile==null)return;profiles.Remove(profile);Persist();RefreshProfiles();}
 internal void StudioCaptureHotkey(bool capture){editingHotkey=capture;}
 internal void StudioExport(string path){double[] v=Current();using(var w=new StreamWriter(path)){w.WriteLine("TITLE \"LumaDesk\"\nLUT_3D_SIZE 33\nDOMAIN_MIN 0 0 0\nDOMAIN_MAX 1 1 1");for(int b=0;b<33;b++)for(int g=0;g<33;g++)for(int r=0;r<33;r++)w.WriteLine(String.Format(CultureInfo.InvariantCulture,"{0:F6} {1:F6} {2:F6}",Curve(r/32.0,0,v),Curve(g/32.0,1,v),Curve(b/32.0,2,v)));}status.Text="LUT сохранён: "+path;}
 internal void StudioStop(){if(modernStopped)return;modernStopped=true;debounce.Stop();watcher.Stop();previewClosing=true;previewDelay.Stop();RestoreAll();if(hotkeyRegistered){hotkeyUnregister(Handle,activeHotkeyId);hotkeyRegistered=false;}tray.Dispose();transition.Dispose();debounce.Dispose();watcher.Dispose();previewDelay.Dispose();previewModel.Dispose();Dispose();}
}
