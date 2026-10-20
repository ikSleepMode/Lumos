using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using System.Windows;
using System.Windows.Controls;
class SessionTests {
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static T Get<T>(StudioWindow w,string name)where T:FrameworkElement{return (T)((FrameworkElement)w.Content).FindName(name);}
 static void Profiles(string file,params Profile[] values){using(var stream=File.Create(file))new XmlSerializer(typeof(List<Profile>)).Serialize(stream,new List<Profile>(values));}
 [STAThread]static int Main(){try{
  var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"session-tests");Directory.CreateDirectory(folder);string stateFile=Path.Combine(folder,"session.xml"),profileFile=Path.Combine(folder,"profiles.xml");
  if(File.Exists(stateFile))File.Delete(stateFile);
  var p1=new Profile{Name="Evening",Values=new double[]{1.42,.9,1.02,1,1,1,5000,0,0},Sharpness=3};
  var p2=new Profile{Name="Game",Values=new double[]{1.71,1,1.05,1,1,1,6500,0,0}};
  Profiles(profileFile,p1,p2);
  var initial=new StudioWindow(true,null,stateFile,profileFile);var displays=initial.Engine.StudioDisplays;Check(displays.Count>0,"No display for UI test");int chosen=displays.Count-1;var monitor=displays[chosen];initial.Close();
  SessionChoiceStore.Save(stateFile,new SessionChoice{MonitorIdentity=monitor.Identity,MonitorDevice=monitor.Device,ProfileName=p1.Name});
  var window=new StudioWindow(true,null,stateFile,profileFile);int writes=0;window.Engine.gammaWriter=delegate(string d,ushort[] r){writes++;return new GammaResult{Confirmed=true};};window.Show();
  Check(window.Engine.StudioDisplay==chosen&&window.Engine.StudioValues[0]==1.42&&!window.Engine.StudioEnabled,"Startup must restore monitor/profile with correction OFF");
  Check(Get<TextBox>(window,"ProfileName").Text==p1.Name&&Get<TextBox>(window,"ProfileProcess").Text==p1.Process,"Selected profile fields");
  window.RestoreSessionSelection();Check(writes==0&&!window.Engine.StudioEnabled,"Restoring preview must never write gamma");
  window.Navigate(2,false);window.UpdateLayout();
  var card=(Border)Get<StackPanel>(window,"ProfileCards").Children[1];var actions=(StackPanel)((Grid)card.Child).Children[1];((Button)actions.Children[0]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  Check(SessionChoiceStore.Load(stateFile).ProfileName==p2.Name,"Profile choice not saved");
  Get<ComboBox>(window,"MonitorPicker").SelectedIndex=0;Check(SessionChoiceStore.Load(stateFile).MonitorDevice==displays[0].Device,"Monitor choice not saved");window.Close();
  window=new StudioWindow(true,null,stateFile,profileFile);window.Engine.gammaWriter=delegate(string d,ushort[] r){throw new Exception("Startup gamma write");};window.Show();
  Check(window.Engine.StudioDisplay==0&&window.Engine.StudioValues[0]==1.71&&!window.Engine.StudioEnabled,"Restart profile recovery");
  var presets=(List<Button>)typeof(StudioWindow).GetField("presetButtons",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(window);
  window.Engine.gammaWriter=delegate(string d,ushort[] r){return new GammaResult{Confirmed=true};};presets[1].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(SessionChoiceStore.Load(stateFile).PresetIndex==1,"Preset choice not saved");window.Close();
  window=new StudioWindow(true,null,stateFile,profileFile);Check(window.Engine.StudioValues[0]==1.45&&!window.Engine.StudioEnabled,"Preset restore must be off");window.Close();
  SessionChoiceStore.Save(stateFile,new SessionChoice{MonitorIdentity="missing-physical-monitor",MonitorDevice=displays[0].Device,ProfileName="deleted"});
  window=new StudioWindow(true,null,stateFile,profileFile);Check(!window.Engine.StudioEnabled&&window.Engine.StudioValues[0]==1,"Missing monitor/profile fallback");window.Close();
  var synthetic=new List<DisplayItem>{new DisplayItem{Device="DISPLAY1",Identity="B"},new DisplayItem{Device="DISPLAY2",Identity="A"}};
  Check(SessionChoiceStore.MonitorIndex(new SessionChoice{MonitorIdentity="A",MonitorDevice="DISPLAY1"},synthetic)==1,"Physical monitor identity must beat changing index");
  Check(SessionChoiceStore.MonitorIndex(new SessionChoice{MonitorIdentity="removed",MonitorDevice="DISPLAY1"},synthetic)==-1,"Must not match another physical monitor by reused name");
  Check(SessionChoiceStore.MonitorIndex(new SessionChoice{MonitorDevice="DISPLAY2"},synthetic)==1,"GDI fallback");
  File.WriteAllText(stateFile,"broken xml");Check(SessionChoiceStore.Load(stateFile).PresetIndex==-1,"Corrupt state fallback");
  app.Shutdown();Console.WriteLine("PASS: monitor/profile persistence across restarts, presets, disabled correction, zero gamma writes during restore, profile form, missing monitor/profile and reordered physical IDs, corrupt XML.");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
