using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

public sealed class SharpnessResult {
 public bool Supported,Confirmed,Skipped;public uint Value,Maximum;public string Message;
}
public interface ISharpnessDevice {SharpnessResult Read(string display);SharpnessResult Write(string display,uint value);}
public sealed class DdcSharpnessDevice:ISharpnessDevice {
 [StructLayout(LayoutKind.Sequential)]struct Rect{public int Left,Top,Right,Bottom;}
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct MonitorInfo{public int Size;public Rect Monitor,Work;public uint Flags;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string Device;}
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct Physical{public IntPtr Handle;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Description;}
 delegate bool EnumProc(IntPtr monitor,IntPtr dc,ref Rect rectangle,IntPtr parameter);
 [DllImport("user32.dll")]static extern bool EnumDisplayMonitors(IntPtr dc,IntPtr clip,EnumProc callback,IntPtr data);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
 [DllImport("dxva2.dll",SetLastError=true)]static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr monitor,out uint count);
 [DllImport("dxva2.dll",SetLastError=true)]static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr monitor,uint count,[Out]Physical[] physical);
 [DllImport("dxva2.dll")]static extern bool DestroyPhysicalMonitors(uint count,Physical[] monitors);
 [DllImport("dxva2.dll",SetLastError=true)]static extern bool GetVCPFeatureAndVCPFeatureReply(IntPtr monitor,byte code,out uint type,out uint current,out uint maximum);
 [DllImport("dxva2.dll",SetLastError=true)]static extern bool SetVCPFeature(IntPtr monitor,byte code,uint value);
 static SharpnessResult Access(string display,Func<IntPtr,SharpnessResult> action){IntPtr found=IntPtr.Zero;EnumProc callback=delegate(IntPtr monitor,IntPtr dc,ref Rect rect,IntPtr data){var info=new MonitorInfo{Size=Marshal.SizeOf(typeof(MonitorInfo))};if(GetMonitorInfo(monitor,ref info)&&String.Equals(info.Device,display,StringComparison.OrdinalIgnoreCase)){found=monitor;return false;}return true;};EnumDisplayMonitors(IntPtr.Zero,IntPtr.Zero,callback,IntPtr.Zero);GC.KeepAlive(callback);
  uint count;if(found==IntPtr.Zero||!GetNumberOfPhysicalMonitorsFromHMONITOR(found,out count)||count!=1)return new SharpnessResult{Message="Аппаратная резкость недоступна для этого подключения или режима дублирования."};
  var monitors=new Physical[count];if(!GetPhysicalMonitorsFromHMONITOR(found,count,monitors))return new SharpnessResult{Message="Не удалось открыть управление монитором DDC/CI."};try{return action(monitors[0].Handle);}finally{DestroyPhysicalMonitors(count,monitors);}
 }
 static SharpnessResult ReadHandle(IntPtr handle){uint type,current,maximum;if(!GetVCPFeatureAndVCPFeatureReply(handle,0x87,out type,out current,out maximum)||maximum==0||maximum>65535||current>maximum)return new SharpnessResult{Message="Монитор не сообщает поддержку резкости DDC/CI (VCP 87). Проверьте DDC/CI в его меню."};return new SharpnessResult{Supported=true,Confirmed=true,Value=current,Maximum=maximum};}
 public SharpnessResult Read(string display){return Access(display,ReadHandle);}
 public SharpnessResult Write(string display,uint value){return Access(display,delegate(IntPtr handle){var before=ReadHandle(handle);if(!before.Supported)return before;if(value>before.Maximum)return new SharpnessResult{Supported=true,Value=before.Value,Maximum=before.Maximum,Message="Значение резкости вне диапазона монитора."};if(before.Value==value)return before;if(!SetVCPFeature(handle,0x87,value))return new SharpnessResult{Supported=true,Value=before.Value,Maximum=before.Maximum,Message="Монитор отклонил изменение резкости."};var after=ReadHandle(handle);after.Confirmed=after.Supported&&after.Value==value;if(!after.Confirmed)after.Message="Монитор не подтвердил заданную резкость.";return after;});}
}
public sealed class UnavailableSharpnessDevice:ISharpnessDevice {
 public SharpnessResult Read(string display){return new SharpnessResult{Message="Резкость недоступна в тестовом подключении."};}
 public SharpnessResult Write(string display,uint value){throw new InvalidOperationException("Test must not change monitor sharpness");}
}
public sealed class SharpnessState {
 public string Device;public uint Original,Maximum,Desired;public bool Supported,Loaded;public string Message;public int Version;
 internal uint Last;
}
public sealed class SharpnessService {
 readonly ISharpnessDevice device;readonly object gate=new object();readonly Dictionary<string,uint> modified=new Dictionary<string,uint>();volatile bool stopped;
 public SharpnessService(ISharpnessDevice device){this.device=device;}
 SharpnessResult Write(string display,uint value){try{return device.Write(display,value);}catch(Exception ex){return new SharpnessResult{Message="Ошибка связи DDC/CI: "+ex.Message};}}
 public Task<SharpnessState> ReadAsync(string display){return Task.Run(delegate{lock(gate){if(stopped)return new SharpnessState{Device=display,Loaded=true};SharpnessResult value=device.Read(display);return new SharpnessState{Device=display,Loaded=true,Supported=value.Supported,Original=value.Value,Desired=value.Value,Last=value.Value,Maximum=value.Maximum,Message=value.Message};}});}
 public Task<SharpnessResult> ApplyAsync(SharpnessState state,uint target,int version){return Task.Run(delegate{lock(gate){if(stopped||version!=state.Version)return new SharpnessResult{Skipped=true};if(!state.Supported)return new SharpnessResult{Message=state.Message};if(target==state.Last)return new SharpnessResult{Supported=true,Confirmed=true,Value=target,Maximum=state.Maximum};uint previous=state.Last;modified[state.Device]=state.Original;
   SharpnessResult result=Write(state.Device,target);if(result.Confirmed){state.Last=target;if(target==state.Original)modified.Remove(state.Device);return result;}
   SharpnessResult recovery=Write(state.Device,previous);if(!recovery.Confirmed)recovery=Write(state.Device,state.Original);
   if(recovery.Confirmed){state.Last=recovery.Value;if(recovery.Value==state.Original)modified.Remove(state.Device);return new SharpnessResult{Supported=true,Value=recovery.Value,Maximum=state.Maximum,Message=(result.Message??"Резкость отклонена.")+" Возвращено подтверждённое значение."};}
   return new SharpnessResult{Supported=true,Value=previous,Maximum=state.Maximum,Message="Не удалось подтвердить резкость. Проверьте меню монитора; при выходе будет повторено восстановление."};
  } });}
 public string Stop(){stopped=true;lock(gate){string error=null;foreach(var entry in modified){var result=Write(entry.Key,entry.Value);if(!result.Confirmed)error="Не удалось восстановить исходную резкость «"+entry.Key+"». Проверьте меню монитора.";}modified.Clear();return error;}}
}
