using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using System.Windows.Controls;

public sealed class SessionChoice {
 public string MonitorIdentity,MonitorDevice,ProfileName;
 public int PresetIndex=-1;
}
public static class SessionChoiceStore {
 public static SessionChoice Load(string path){try{if(path!=null&&File.Exists(path))using(var file=File.OpenRead(path))return (SessionChoice)new XmlSerializer(typeof(SessionChoice)).Deserialize(file)??new SessionChoice();}catch(IOException){}catch(UnauthorizedAccessException){}catch(InvalidOperationException){}return new SessionChoice();}
 public static void Save(string path,SessionChoice choice){if(path==null)return;Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+".tmp";try{using(var file=File.Create(temp))new XmlSerializer(typeof(SessionChoice)).Serialize(file,choice);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}}
 public static int MonitorIndex(SessionChoice choice,List<DisplayItem> displays){if(!String.IsNullOrEmpty(choice.MonitorIdentity))return displays.FindIndex(delegate(DisplayItem item){return String.Equals(item.Identity,choice.MonitorIdentity,StringComparison.OrdinalIgnoreCase);});return displays.FindIndex(delegate(DisplayItem item){return !String.IsNullOrEmpty(choice.MonitorDevice)&&String.Equals(item.Device,choice.MonitorDevice,StringComparison.OrdinalIgnoreCase);});}
}
public sealed partial class StudioWindow {
 SessionChoice sessionChoice;string sessionPath;bool restoringSession;
 void InitializeSession(string customPath){
  sessionPath=customPath??(testing?null:Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LumaDesk","session.xml"));
  sessionChoice=SessionChoiceStore.Load(sessionPath);RestoreSessionSelection();
 }
 internal void RestoreSessionSelection(){
  restoringSession=true;try{
   int monitor=SessionChoiceStore.MonitorIndex(sessionChoice,Engine.StudioDisplays);if(monitor>=0)Find<ComboBox>("MonitorPicker").SelectedIndex=monitor;
   if(!String.IsNullOrEmpty(sessionChoice.ProfileName)){
    var profile=Engine.StudioProfiles.Find(delegate(Profile p){return p.Name==sessionChoice.ProfileName;});
    if(profile!=null){Engine.StudioLoadProfilePreview(profile);ApplyHardwareProfile(profile);Find<TextBox>("ProfileName").Text=profile.Name;Find<TextBox>("ProfileProcess").Text=profile.Process;Find<TextBlock>("ProfileFeedback").Text="Загружен последний профиль. Коррекция выключена.";}
    else{sessionChoice.ProfileName=null;Find<TextBlock>("ProfileFeedback").Text="Последний профиль больше не существует. Коррекция выключена.";}
   }else if(sessionChoice.PresetIndex>=0&&sessionChoice.PresetIndex<presets.Length)Engine.StudioLoadProfilePreview(new Profile{Values=presets[sessionChoice.PresetIndex]});
   else sessionChoice.PresetIndex=-1;
   profileSignature="";
  }finally{restoringSession=false;}
 }
 void RememberMonitor(){if(sessionChoice==null||restoringSession)return;var displays=Engine.StudioDisplays;int index=Engine.StudioDisplay;if(index>=0&&index<displays.Count){sessionChoice.MonitorDevice=displays[index].Device;sessionChoice.MonitorIdentity=displays[index].Identity;}SaveSessionChoice();}
 void RememberProfile(string name,int preset){if(sessionChoice==null||restoringSession)return;sessionChoice.ProfileName=name;sessionChoice.PresetIndex=preset;RememberMonitor();profileSignature="";}
 void ForgetProfile(string name){if(sessionChoice!=null&&sessionChoice.ProfileName==name)RememberProfile(null,-1);}
 void SaveSessionChoice(){try{SessionChoiceStore.Save(sessionPath,sessionChoice);}catch(Exception ex){Find<TextBlock>("StatusText").Text="Не удалось запомнить выбор: "+ex.Message;}}
 bool IsSelectedProfile(Profile profile){return sessionChoice!=null&&sessionChoice.ProfileName==profile.Name;}
}
