using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Serialization;

public sealed class AppearancePreferences {public string Theme="black";}
public sealed class StudioPalette {
 public string Id,Name,Window,Header,Card,Surface,Input,Border,Track,Text,Muted,Accent,AccentInk,Selected;
 public StudioPalette(string id,string name,string window,string header,string card,string surface,string input,string border,string track,string text,string muted,string accent,string ink,string selected){Id=id;Name=name;Window=window;Header=header;Card=card;Surface=surface;Input=input;Border=border;Track=track;Text=text;Muted=muted;Accent=accent;AccentInk=ink;Selected=selected;}
 public static readonly StudioPalette[] All={
  new StudioPalette("white","Белая","#F4F6F9","#FFFFFF","#FFFFFF","#EBEFF5","#F8FAFC","#CBD3DE","#B8C4D3","#172333","#59697E","#2563EB","#FFFFFF","#E4EDFF"),
  new StudioPalette("gray","Серая","#26282C","#2B2E33","#32363C","#3B4047","#282C31","#525B66","#64707C","#F0F3F7","#B8C1CD","#D5DEE9","#202A36","#4B5664"),
  new StudioPalette("black","Чёрная","#101113","#141618","#1A1C1F","#24282A","#131517","#353C37","#424B43","#F0F3EB","#A1AC99","#C5F778","#17200F","#303C26"),
  new StudioPalette("cyan","Циан","#0A171D","#0D1E26","#102630","#18333E","#0D2029","#315564","#3F7180","#E6F7FC","#9CBEC9","#50DFEF","#07282F","#19434D"),
  new StudioPalette("purple","Фиолетовая","#16111F","#1C1528","#241B33","#30243F","#1B1428","#534166","#6C5285","#F3EBFF","#BFAFD1","#C6A4FF","#28133F","#45305E"),
  new StudioPalette("orange","Оранжевая","#1C1410","#241A13","#2D2118","#3A2A1E","#221810","#624833","#856040","#FFF1E5","#C9AE95","#FFB36F","#351A06","#51351F")
 };
 public static StudioPalette Find(string id){foreach(var p in All)if(p.Id==id)return p;return All[2];}
}

public sealed class AppearanceStore {
 public static string Load(string path){try{if(File.Exists(path))using(var f=File.OpenRead(path)){var value=(AppearancePreferences)new XmlSerializer(typeof(AppearancePreferences)).Deserialize(f);return StudioPalette.Find(value.Theme).Id;}}catch(IOException){}catch(UnauthorizedAccessException){}catch(InvalidOperationException){}return "black";}
 public static void Save(string path,string id){Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+".tmp";try{using(var f=File.Create(temp))new XmlSerializer(typeof(AppearancePreferences)).Serialize(f,new AppearancePreferences{Theme=id});if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}}
}

public sealed partial class StudioWindow {
 static readonly string[] ThemeTones=("090C09 0A0D0B 10150D 111214 131517 141618 17200F 1A1C1F 1A2017 1B2117 1C201C 20251D 20251E 222722 22291E 24272B 242A23 252B23 252C21 282B2C 293321 293420 293820 2A2D31 30382A 303C28 31382D 33372D 34383D 343A30 343C31 343D2E 363B3D 383D40 394831 3B4734 42483D 45513A 46523D 475340 4B5C3D 4D6540 525B4D 526A3D 555F50 738F54 769350 7D847B 7F9270 858991 87977D 898F97 89977F 8C9A83 8E9C84 8F988A 91A082 92ADCB 969BA3 97A48C 9CA98F 9FAE95 A0A48F A2B794 A3AF9B A3B6E0 A4AEA0 A6ADA1 A7B09E AAB5A3 ACBBA0 BBCCA8 BCCBB0 BDD4AB BFC6BD C4CDBD C5F778 C9090B09 CCD9C1 CDD4C6 D4F5AD D5E2C9 D7FBB0 DDA693 E0E7DB E3C79B E6AB92 E6EDDF E8EBE4 E9EEE4 ED151A13 ED977E EEF2E9 F2F2ED").Split(' ');
 StudioPalette activeTheme;string appearancePath;FrameComparison expandedViewer;
 readonly Dictionary<string,Button> themeChoices=new Dictionary<string,Button>();
 internal string CurrentTheme {get{return activeTheme.Id;}}
 static bool In(string list,string value){return (" "+list+" ").Contains(" "+value+" ");}
 static Color ParseColor(string color){return (Color)ColorConverter.ConvertFromString(color);}
 internal static Color ThemeColor(string tone,StudioPalette p){
  tone=tone.TrimStart('#').ToUpperInvariant();
  if(In("090C09 0A0D0B C9090B09 92ADCB A3B6E0 DDA693 E3C79B",tone))return ParseColor("#"+tone);
  if(tone=="ED151A13"){Color overlay=ParseColor(p.Header);overlay.A=240;return overlay;}
  if(In("C5F778 D7FBB0",tone))return ParseColor(p.Accent);
  if(tone=="17200F")return ParseColor(p.AccentInk);
  if(In("ED977E E6AB92",tone))return ParseColor(p.Id=="white"?"#AE3F25":"#F0AA92");
  if(In("10150D 111214",tone))return ParseColor(p.Window);
  if(In("141618 1B2117",tone))return ParseColor(p.Header);
  if(In("1A1C1F 1A2017",tone))return ParseColor(p.Card);
  if(tone=="131517")return ParseColor(p.Input);
  if(In("1C201C 20251D 20251E 222722 22291E 24272B 242A23 252C21",tone))return ParseColor(p.Surface);
  if(In("293321 293420 293820 303C28 33372D 3B4734 4D6540",tone))return ParseColor(p.Selected);
  if(In("526A3D 738F54 769350",tone))return ParseColor(p.Accent);
  if(In("30382A 383D40 42483D 46523D",tone))return ParseColor(p.Track);
  if(In("252B23 282B2C 2A2D31 31382D 34383D 343A30 343C31 343D2E 363B3D 394831 45513A 475340 4B5C3D 555F50",tone))return ParseColor(p.Border);
  Color source=ParseColor("#"+tone);return ParseColor(source.R>=187?p.Text:p.Muted);
 }
 Brush ThemeBrush(string hex){string key="Tone_"+hex.TrimStart('#').ToUpperInvariant();if(root!=null&&root.Resources.Contains(key))return (Brush)root.Resources[key];return new SolidColorBrush(ParseColor(hex));}
 void InitializeTheme(string customPath){
  appearancePath=customPath??(testing?Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-appearance-unused.xml"):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LumaDesk","appearance.xml"));
  activeTheme=StudioPalette.Find(AppearanceStore.Load(appearancePath));
  foreach(string tone in ThemeTones)root.Resources["Tone_"+tone]=new SolidColorBrush(ThemeColor(tone,activeTheme));
  Background=ThemeBrush("#111214");Foreground=ThemeBrush("#F2F2ED");
 }
 internal bool ApplyTheme(string id,bool save,out string error){
  error=null;StudioPalette palette=StudioPalette.Find(id);
  if(save)try{AppearanceStore.Save(appearancePath,palette.Id);}catch(Exception ex){error="Не удалось сохранить тему: "+ex.Message;return false;}
  activeTheme=palette;
  foreach(string tone in ThemeTones){var brush=(SolidColorBrush)root.Resources["Tone_"+tone];Color color=ThemeColor(tone,palette);if(brush.IsFrozen)root.Resources["Tone_"+tone]=new SolidColorBrush(color);else brush.Color=color;}
  foreach(var viewer in viewers)viewer.ApplyTheme(palette);if(expandedViewer!=null)expandedViewer.ApplyTheme(palette);
  DrawCurve();RefreshThemeChoices();return true;
 }
 void RefreshThemeChoices(){foreach(var pair in themeChoices){StudioPalette palette=StudioPalette.Find(pair.Key);pair.Value.BorderBrush=new SolidColorBrush(ParseColor(pair.Key==activeTheme.Id?palette.Accent:palette.Border));pair.Value.BorderThickness=new Thickness(pair.Key==activeTheme.Id?2:1);pair.Value.ToolTip=palette.Name+(pair.Key==activeTheme.Id?" · выбрана":"");}if(Find<Button>("ThemeButton")!=null){Find<Button>("ThemeButton").ToolTip="Цветовая тема: "+activeTheme.Name;Find<Button>("ThemeButton").Content="Цветовая тема: "+activeTheme.Name+"  →";}}
 void WireThemePicker(){
  Find<Button>("ThemeButton").Click+=delegate{CloseSettings();CloseHotkey();Find<TextBlock>("ThemeError").Text="";Find<Border>("ThemeOverlay").Visibility=Visibility.Visible;RefreshThemeChoices();};
  Find<Button>("CloseTheme").Click+=delegate{CloseThemePicker();};var host=Find<WrapPanel>("ThemeChoices");
  foreach(var palette in StudioPalette.All){var p=palette;var card=new Grid{Height=78};card.RowDefinitions.Add(new RowDefinition{Height=new GridLength(39)});card.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});
   var swatches=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};foreach(string hex in new string[]{p.Card,p.Surface,p.Accent})swatches.Children.Add(new Border{Width=24,Height=24,CornerRadius=new CornerRadius(12),Background=new SolidColorBrush(ParseColor(hex)),Margin=new Thickness(0,0,7,0),BorderBrush=new SolidColorBrush(ParseColor(p.Border)),BorderThickness=new Thickness(1)});card.Children.Add(swatches);
   var label=new TextBlock{Text=p.Name,FontSize=15,FontWeight=FontWeights.SemiBold,Foreground=new SolidColorBrush(ParseColor(p.Text)),VerticalAlignment=VerticalAlignment.Bottom};Grid.SetRow(label,1);card.Children.Add(label);
   var button=MakeButton("",delegate{string message;if(!ApplyTheme(p.Id,true,out message))Find<TextBlock>("ThemeError").Text=message;},false);button.Content=card;button.Width=192;button.Margin=new Thickness(0,0,12,12);button.Padding=new Thickness(17,13,17,13);button.Background=new SolidColorBrush(ParseColor(p.Window));button.Foreground=new SolidColorBrush(ParseColor(p.Text));button.HorizontalContentAlignment=HorizontalAlignment.Stretch;host.Children.Add(button);themeChoices[p.Id]=button;
  }
  foreach(var viewer in viewers)viewer.ApplyTheme(activeTheme);RefreshThemeChoices();
 }
 void CloseThemePicker(){bool wasOpen=Find<Border>("ThemeOverlay").Visibility==Visibility.Visible;Find<Border>("ThemeOverlay").Visibility=Visibility.Collapsed;if(wasOpen&&!closing)Find<Border>("SettingsOverlay").Visibility=Visibility.Visible;}
}
