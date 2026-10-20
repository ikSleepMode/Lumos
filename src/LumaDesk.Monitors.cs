using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public static class MonitorNames {
 [StructLayout(LayoutKind.Sequential)]struct Luid{public uint Low;public int High;}
 [StructLayout(LayoutKind.Sequential)]struct Header{public uint Type,Size;public Luid Adapter;public uint Id;}
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct SourceName{public Header Header;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string Name;}
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct TargetName{public Header Header;public uint Flags,Technology;public ushort Manufacturer,Product;public uint Connector;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=64)]public string Friendly;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Path;}
 [DllImport("user32.dll")]static extern int GetDisplayConfigBufferSizes(uint flags,out uint paths,out uint modes);
 [DllImport("user32.dll")]static extern int QueryDisplayConfig(uint flags,ref uint paths,IntPtr pathArray,ref uint modes,IntPtr modeArray,IntPtr topology);
 [DllImport("user32.dll",EntryPoint="DisplayConfigGetDeviceInfo")]static extern int SourceInfo(ref SourceName source);
 [DllImport("user32.dll",EntryPoint="DisplayConfigGetDeviceInfo")]static extern int TargetInfo(ref TargetName target);
 public static Dictionary<string,string> Read(){return Read(false);} public static Dictionary<string,string> ReadIdentities(){return Read(true);} static Dictionary<string,string> Read(bool identity){var result=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
  for(int attempt=0;attempt<3;attempt++){uint paths,modes;if(GetDisplayConfigBufferSizes(2,out paths,out modes)!=0)return result;IntPtr pathBuffer=Marshal.AllocHGlobal(checked((int)paths*72)),modeBuffer=Marshal.AllocHGlobal(checked((int)modes*64));
   try{int code=QueryDisplayConfig(2,ref paths,pathBuffer,ref modes,modeBuffer,IntPtr.Zero);if(code==122)continue;if(code!=0)return result;
    for(int i=0;i<paths;i++){IntPtr p=IntPtr.Add(pathBuffer,i*72);var source=new SourceName();source.Header=new Header{Type=1,Size=(uint)Marshal.SizeOf(typeof(SourceName)),Adapter=(Luid)Marshal.PtrToStructure(p,typeof(Luid)),Id=(uint)Marshal.ReadInt32(p,8)};var target=new TargetName();target.Header=new Header{Type=2,Size=(uint)Marshal.SizeOf(typeof(TargetName)),Adapter=(Luid)Marshal.PtrToStructure(IntPtr.Add(p,20),typeof(Luid)),Id=(uint)Marshal.ReadInt32(p,28)};
     if(SourceInfo(ref source)!=0||TargetInfo(ref target)!=0||String.IsNullOrWhiteSpace(identity?target.Path:target.Friendly))continue;string name=(identity?target.Path:target.Friendly).Trim();if(result.ContainsKey(source.Name)){if(result[source.Name]!=name)result[source.Name]+=" / "+name;}else result[source.Name]=name;
    }return result;
   }finally{Marshal.FreeHGlobal(pathBuffer);Marshal.FreeHGlobal(modeBuffer);}
  }return result;
 }
}
