using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

public sealed partial class StudioWindow {
 StartupController startup;CheckBox startupToggle;bool startupChanging;
 SharpnessService sharpness;readonly Dictionary<string,SharpnessState> sharpStates=new Dictionary<string,SharpnessState>();readonly HashSet<string> probing=new HashSet<string>();readonly Dictionary<string,bool> sharpActive=new Dictionary<string,bool>();
 Slider sharpSlider;TextBox sharpField;TextBlock sharpStatus;bool syncingSharp;DispatcherTimer sharpDelay;Profile lastSharpProfile;readonly Dictionary<string,Profile> pendingSharpProfiles=new Dictionary<string,Profile>();readonly Dictionary<string,uint> beforeAutoSharpness=new Dictionary<string,uint>();
 string SelectedDevice {get{var monitors=Engine.StudioDisplays;int index=Engine.StudioDisplay;return index>=0&&index<monitors.Count?monitors[index].Device:null;}}
 void InitializeExtras(){
  startup=new StartupController(testing?(IStartupRegistration)new MemoryStartupRegistration():new UserStartupRegistration(),Assembly.GetExecutingAssembly().Location);
  startupToggle=Find<CheckBox>("StartupToggle");startupChanging=true;try{startupToggle.IsChecked=startup.Enabled;Find<TextBlock>("StartupStatus").Text=startup.Enabled?"Будет запускаться в трее при входе в Windows.":"Автозагрузка выключена.";}catch(Exception ex){Find<TextBlock>("StartupStatus").Text=ex.Message;}finally{startupChanging=false;}
  RoutedEventHandler change=delegate{if(startupChanging)return;string error;if(!startup.Set(startupToggle.IsChecked==true,out error)){startupChanging=true;try{startupToggle.IsChecked=startup.Enabled;}catch{startupToggle.IsChecked=false;}startupChanging=false;Find<TextBlock>("StartupStatus").Text=error;}else Find<TextBlock>("StartupStatus").Text=startupToggle.IsChecked==true?"Будет запускаться в трее при входе в Windows.":"Автозагрузка выключена.";};startupToggle.Checked+=change;startupToggle.Unchecked+=change;
  Find<Button>("SettingsButton").Click+=delegate{CloseThemePicker();CloseHotkey();Find<Border>("SettingsOverlay").Visibility=Visibility.Visible;};Find<Button>("CloseSettings").Click+=delegate{CloseSettings();};
  sharpness=new SharpnessService(testing?(ISharpnessDevice)new UnavailableSharpnessDevice():new DdcSharpnessDevice());
  sharpDelay=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(300)};sharpDelay.Tick+=delegate{sharpDelay.Stop();SharpnessState state;if(SelectedDevice!=null&&sharpStates.TryGetValue(SelectedDevice,out state)&&state.Supported)QueueSharpness(state,Engine.StudioEnabled);};
  var group=new StackPanel{Margin=new Thickness(0,22,0,12)};var heading=Text("РЕЗКОСТЬ МОНИТОРА",9,"#7F9270");heading.FontWeight=FontWeights.SemiBold;heading.Margin=new Thickness(0,0,0,12);group.Children.Add(heading);
  string warning="Резкость применяется на всём выбранном мониторе, включая игры, через DDC/CI. Команда может выполняться с задержкой. Обработка изображения самим монитором тоже может влиять на задержку. Поддержка зависит от монитора и подключения. До / После сравнивает цвет: аппаратная резкость действует на обе половины.";
  group.ToolTip=warning;ToolTipService.SetShowOnDisabled(group,true);ToolTipService.SetInitialShowDelay(group,250);ToolTipService.SetShowDuration(group,20000);
  var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(78)});var label=Text("Резкость  ⓘ",12,"#C4CDBD");label.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(label);sharpField=new TextBox{Text="—",IsEnabled=false,TextAlignment=TextAlignment.Center,Padding=new Thickness(6,5,6,5),FontSize=12};Grid.SetColumn(sharpField,1);row.Children.Add(sharpField);group.Children.Add(row);
  sharpSlider=new Slider{Minimum=0,Maximum=100,IsEnabled=false,SmallChange=1,LargeChange=5,TickFrequency=1,IsSnapToTickEnabled=true,Margin=new Thickness(0,2,0,0)};group.Children.Add(sharpSlider);sharpStatus=Text("Проверка поддержки DDC/CI…",10,"#8E9C84");sharpStatus.Margin=new Thickness(0,8,0,0);group.Children.Add(sharpStatus);Find<StackPanel>("ParameterList").Children.Add(group);
  sharpSlider.ValueChanged+=delegate{if(syncingSharp)return;SharpnessState state;if(SelectedDevice!=null&&sharpStates.TryGetValue(SelectedDevice,out state)&&state.Supported){state.Desired=(uint)Math.Round(sharpSlider.Value);sharpField.Text=state.Desired.ToString();sharpDelay.Stop();sharpDelay.Start();}};
  sharpField.KeyDown+=delegate(object sender,KeyEventArgs e){if(e.Key==Key.Enter){CommitSharpness();Keyboard.ClearFocus();e.Handled=true;}};sharpField.LostKeyboardFocus+=delegate{CommitSharpness();};
  SynchronizeExtras();
 }
 void CloseSettings(){Find<Border>("SettingsOverlay").Visibility=Visibility.Collapsed;}
 void CommitSharpness(){if(syncingSharp)return;SharpnessState state;uint value;if(SelectedDevice==null||!sharpStates.TryGetValue(SelectedDevice,out state)||!state.Supported)return;if(!UInt32.TryParse(sharpField.Text,out value)){sharpField.Text=state.Desired.ToString();return;}state.Desired=Math.Min(value,state.Maximum);syncingSharp=true;sharpSlider.Value=state.Desired;sharpField.Text=state.Desired.ToString();syncingSharp=false;sharpDelay.Stop();sharpDelay.Start();}
 void SynchronizeExtras(){
  if(sharpness==null||closing)return;string device=SelectedDevice;if(device==null)return;
  SharpnessState state;if(!sharpStates.TryGetValue(device,out state)){
   sharpField.IsEnabled=sharpSlider.IsEnabled=false;sharpField.Text="—";sharpStatus.Text="Проверка поддержки DDC/CI…";
   if(probing.Add(device))sharpness.ReadAsync(device).ContinueWith(delegate(System.Threading.Tasks.Task<SharpnessState> task){if(closing)return;Dispatcher.BeginInvoke(new Action(delegate{if(closing)return;probing.Remove(device);sharpStates[device]=task.IsFaulted?new SharpnessState{Device=device,Loaded=true,Message="Ошибка связи с монитором DDC/CI."}:task.Result;SynchronizeExtras();}));});return;
  }
  syncingSharp=true;try{sharpSlider.IsEnabled=sharpField.IsEnabled=state.Supported;if(state.Supported){sharpSlider.Maximum=state.Maximum;sharpSlider.Value=state.Desired;if(!sharpField.IsKeyboardFocusWithin)sharpField.Text=state.Desired.ToString();sharpStatus.Text=state.Message??("На всём экране · диапазон 0–"+state.Maximum);}else{sharpField.Text="—";sharpStatus.Text=state.Message;}}finally{syncingSharp=false;}
  Profile pending;if(pendingSharpProfiles.TryGetValue(device,out pending)){pendingSharpProfiles.Remove(device);ApplyHardwareProfile(pending);}bool wasActive;sharpActive.TryGetValue(device,out wasActive);if(state.Supported&&wasActive!=Engine.StudioEnabled){sharpActive[device]=Engine.StudioEnabled;QueueSharpness(state,Engine.StudioEnabled);}
  Profile active=Engine.StudioAutomaticProfile;if(active!=lastSharpProfile){if(active!=null){if(state.Supported&&!beforeAutoSharpness.ContainsKey(device))beforeAutoSharpness[device]=state.Desired;ApplyHardwareProfile(active);}else{foreach(var saved in beforeAutoSharpness){SharpnessState previous;if(sharpStates.TryGetValue(saved.Key,out previous)){previous.Desired=saved.Value;bool enabled;sharpActive.TryGetValue(saved.Key,out enabled);QueueSharpness(previous,enabled);}}beforeAutoSharpness.Clear();}lastSharpProfile=active;}
 }
 void QueueSharpness(SharpnessState state,bool active){int version=++state.Version;uint target=active?state.Desired:state.Original;sharpness.ApplyAsync(state,target,version).ContinueWith(delegate(System.Threading.Tasks.Task<SharpnessResult> task){if(closing)return;Dispatcher.BeginInvoke(new Action(delegate{if(closing||state.Version!=version)return;if(task.IsFaulted){state.Message="Ошибка применения аппаратной резкости.";}else if(!task.Result.Skipped){if(!task.Result.Confirmed){state.Desired=task.Result.Value;state.Message=task.Result.Message;}else state.Message=null;}SynchronizeExtras();}));});}
 void FlushHardwareSharpness(){if(sharpDelay==null||!sharpDelay.IsEnabled)return;sharpDelay.Stop();SharpnessState state;if(SelectedDevice!=null&&sharpStates.TryGetValue(SelectedDevice,out state)&&state.Supported)QueueSharpness(state,Engine.StudioEnabled);}
 void ResetHardwareSharpness(){SharpnessState state;if(SelectedDevice!=null&&sharpStates.TryGetValue(SelectedDevice,out state)&&state.Supported){state.Desired=state.Original;sharpDelay.Stop();QueueSharpness(state,false);}}
 void SaveHardwareProfile(string name){SharpnessState state;double value=SelectedDevice!=null&&sharpStates.TryGetValue(SelectedDevice,out state)&&state.Supported?(double)state.Desired:-1;Engine.StudioSaveSharpness(name,value);}
 void ApplyHardwareProfile(Profile profile){SharpnessState state;if(profile.Sharpness<0||Double.IsNaN(profile.Sharpness)||Double.IsInfinity(profile.Sharpness)||SelectedDevice==null)return;if(!sharpStates.TryGetValue(SelectedDevice,out state)){pendingSharpProfiles[SelectedDevice]=profile;return;}if(state.Supported){state.Desired=(uint)Math.Min(profile.Sharpness,state.Maximum);QueueSharpness(state,Engine.StudioEnabled);}}
 void StopExtras(){if(sharpDelay!=null)sharpDelay.Stop();if(sharpness!=null){string error=sharpness.Stop();if(error!=null&&!testing)MessageBox.Show(error,"Lumos",MessageBoxButton.OK,MessageBoxImage.Warning);}}
}
public sealed class MemoryStartupRegistration:IStartupRegistration {
 public string Command;public string Read(){return Command;}public void Write(string command){Command=command;}public void Remove(){Command=null;}
}
