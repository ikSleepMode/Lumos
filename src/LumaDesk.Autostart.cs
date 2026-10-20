using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

public interface IStartupRegistration {string Read();void Write(string command);void Remove();}
public sealed class UserStartupRegistration:IStartupRegistration {
 readonly string path;
 public UserStartupRegistration():this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup),"Lumos.lnk")){}
 public UserStartupRegistration(string shortcutPath){path=Path.GetFullPath(shortcutPath);}
 object Link(out object shell){shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell",true));return shell.GetType().InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{path});}
 static object Get(object link,string name){return link.GetType().InvokeMember(name,BindingFlags.GetProperty,null,link,null);}
 static void Put(object link,string name,string value){link.GetType().InvokeMember(name,BindingFlags.SetProperty,null,link,new object[]{value});}
 public string Read(){if(!File.Exists(path))return null;object shell=null,link=null;try{link=Link(out shell);return "\""+(string)Get(link,"TargetPath")+"\" "+(string)Get(link,"Arguments");}finally{if(link!=null)Marshal.FinalReleaseComObject(link);if(shell!=null)Marshal.FinalReleaseComObject(shell);}}
 public void Write(string command){int end=command.IndexOf('"',1);if(end<2)throw new ArgumentException("Invalid startup command");string target=command.Substring(1,end-1);object shell=null,link=null;try{Directory.CreateDirectory(Path.GetDirectoryName(path));link=Link(out shell);Put(link,"TargetPath",target);Put(link,"Arguments",command.Substring(end+1).Trim());Put(link,"WorkingDirectory",Path.GetDirectoryName(target));Put(link,"IconLocation",target+",0");Put(link,"Description","Lumos");link.GetType().InvokeMember("Save",BindingFlags.InvokeMethod,null,link,null);}finally{if(link!=null)Marshal.FinalReleaseComObject(link);if(shell!=null)Marshal.FinalReleaseComObject(shell);}}
 public void Remove(){if(File.Exists(path))File.Delete(path);}
}
public sealed class StartupController {
 readonly IStartupRegistration store;readonly string executable;
 public StartupController(IStartupRegistration store,string executable){this.store=store;this.executable=Path.GetFullPath(executable);}
 public string Command {get{return "\""+executable+"\" --startup";}}
 public bool Enabled {get{return String.Equals(store.Read(),Command,StringComparison.OrdinalIgnoreCase);}}
 public bool Set(bool enabled,out string error){error=null;try{if(enabled)store.Write(Command);else store.Remove();return true;}catch(Exception ex){error="Не удалось изменить автозагрузку: "+ex.Message;return false;}}
 public static bool IsStartup(string[] args){foreach(var arg in args)if(String.Equals(arg,"--startup",StringComparison.OrdinalIgnoreCase))return true;return false;}
}
