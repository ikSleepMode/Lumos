using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using System.Xml.Serialization;

public class HotkeyPreferences {
 public uint Modifiers=3;
 public uint Key=(uint)Keys.G;
}

public class ValueInput : NumericUpDown {
 public ValueInput(){TextAlign=HorizontalAlignment.Right;ThousandsSeparator=false;BorderStyle=BorderStyle.FixedSingle;}
 protected override void OnKeyPress(KeyPressEventArgs e){if(e.KeyChar=='.'||e.KeyChar==',')e.KeyChar=CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0];base.OnKeyPress(e);}
 protected override void ValidateEditText(){
  UserEdit=false;
  decimal number;
  if(Decimal.TryParse(Text.Trim().Replace(',','.'),NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out number))Value=Math.Max(Minimum,Math.Min(Maximum,number));
  UpdateEditText();
 }
}

public partial class LumaDesk {
 readonly ValueInput[] numberInputs=new ValueInput[9];bool syncingInputs;
 uint hotkeyModifiers=3,hotkeyKey=(uint)Keys.G;int activeHotkeyId=1;bool hotkeyRegistered,editingHotkey;
 Label hotkeyCaption;
 string settingsPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LumaDesk","settings.xml");
 internal Func<IntPtr,int,uint,uint,bool> hotkeyRegister=RegisterHotKey;
 internal Func<IntPtr,int,bool> hotkeyUnregister=UnregisterHotKey;
 internal static bool ValidHotkey(uint modifiers,uint key){
  if((modifiers&~7u)!=0)return false;
  if(key==0||key>254||key==(uint)Keys.ControlKey||key==(uint)Keys.ShiftKey||key==(uint)Keys.Menu||key==(uint)Keys.LWin||key==(uint)Keys.RWin||key==(uint)Keys.Escape)return false;
  return (modifiers&3)!=0||(key>=(uint)Keys.F1&&key<=(uint)Keys.F24);
 }
 internal static string HotkeyText(uint modifiers,uint key){return ((modifiers&2)!=0?"Ctrl + ":"")+((modifiers&1)!=0?"Alt + ":"")+((modifiers&4)!=0?"Shift + ":"")+new KeysConverter().ConvertToString((Keys)key);}
 void LoadHotkeyPreferences(){try{if(File.Exists(settingsPath))using(var f=File.OpenRead(settingsPath)){var p=(HotkeyPreferences)new XmlSerializer(typeof(HotkeyPreferences)).Deserialize(f);if(ValidHotkey(p.Modifiers,p.Key)){hotkeyModifiers=p.Modifiers;hotkeyKey=p.Key;}}}catch(IOException){}catch(InvalidOperationException){}catch(UnauthorizedAccessException){} }
 void SaveHotkeyPreferences(uint modifiers,uint key){
  Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));string temp=settingsPath+".tmp";
  try{using(var f=File.Create(temp))new XmlSerializer(typeof(HotkeyPreferences)).Serialize(f,new HotkeyPreferences{Modifiers=modifiers,Key=key});if(File.Exists(settingsPath))File.Replace(temp,settingsPath,null);else File.Move(temp,settingsPath);}
  finally{if(File.Exists(temp))File.Delete(temp);}
 }
 void StartHotkey(){hotkeyRegistered=hotkeyRegister(Handle,activeHotkeyId,0x4000|hotkeyModifiers,hotkeyKey);RefreshHotkeyCaption();status.Text=hotkeyRegistered?"Готово · "+HotkeyText(hotkeyModifiers,hotkeyKey):"Комбинация "+HotkeyText(hotkeyModifiers,hotkeyKey)+" занята или недоступна. Измените её в боковой панели.";}
 void RefreshHotkeyCaption(){if(hotkeyCaption!=null)hotkeyCaption.Text="БЫСТРОЕ ПЕРЕКЛЮЧЕНИЕ\n\n"+HotkeyText(hotkeyModifiers,hotkeyKey)+(hotkeyRegistered?"":" · неактивно");}
 internal bool ChangeHotkey(uint modifiers,uint key,out string error){
  error=null;
  if(!ValidHotkey(modifiers,key)){error="Используйте Ctrl или Alt с клавишей, либо F1–F24. Можно добавить Shift.";return false;}
  if(hotkeyRegistered&&modifiers==hotkeyModifiers&&key==hotkeyKey)return true;
  int candidate=hotkeyRegistered?(activeHotkeyId==1?2:1):activeHotkeyId;
  if(!hotkeyRegister(Handle,candidate,0x4000|modifiers,key)){error="Эта комбинация занята или зарезервирована Windows. Выберите другую. Прежняя настройка сохранена.";return false;}
  try{SaveHotkeyPreferences(modifiers,key);}catch(Exception ex){hotkeyUnregister(Handle,candidate);error="Не удалось сохранить настройку: "+ex.Message;return false;}
  if(hotkeyRegistered)hotkeyUnregister(Handle,activeHotkeyId);
  activeHotkeyId=candidate;hotkeyRegistered=true;hotkeyModifiers=modifiers;hotkeyKey=key;RefreshHotkeyCaption();status.Text="Горячая клавиша сохранена: "+HotkeyText(modifiers,key);return true;
 }
 void ShowHotkeyEditor(){
  using(var dialog=new Form{Text="Горячая клавиша",ClientSize=new Size(520,300),FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,StartPosition=FormStartPosition.CenterParent,BackColor=bg,ForeColor=ForeColor,Font=Font,ShowInTaskbar=false}){
   uint modifiers=hotkeyModifiers,key=hotkeyKey;
   var title=new Label{Text="Переключение коррекции",Location=new Point(24,22),Size=new Size(472,35),Font=new Font("Segoe UI",17,FontStyle.Bold)};dialog.Controls.Add(title);
   dialog.Controls.Add(new Label{Text="Щёлкните поле и нажмите новую комбинацию.\nCtrl или Alt + клавиша; можно добавить Shift. Либо F1–F24.",Location=new Point(24,66),Size=new Size(472,48),ForeColor=muted});
   var capture=new TextBox{ReadOnly=true,ShortcutsEnabled=false,Location=new Point(24,122),Size=new Size(472,36),Font=new Font("Segoe UI",16),BackColor=panel,ForeColor=accent,Text=HotkeyText(modifiers,key),AccessibleName="Новая горячая клавиша"};dialog.Controls.Add(capture);
   var feedback=new Label{Location=new Point(24,166),Size=new Size(472,59),ForeColor=Color.FromArgb(248,178,151)};dialog.Controls.Add(feedback);
   capture.KeyDown+=delegate(object sender,KeyEventArgs e){e.SuppressKeyPress=true;e.Handled=true;if(e.KeyCode==Keys.Escape){dialog.Close();return;}uint m=(e.Control?2u:0)|(e.Alt?1u:0)|(e.Shift?4u:0);if(e.KeyCode==Keys.ControlKey||e.KeyCode==Keys.Menu||e.KeyCode==Keys.ShiftKey)return;if(!ValidHotkey(m,(uint)e.KeyCode)){feedback.Text="Добавьте Ctrl или Alt, либо выберите F1–F24.";return;}modifiers=m;key=(uint)e.KeyCode;capture.Text=HotkeyText(modifiers,key);feedback.Text="";};
   var save=ActionButton("Сохранить",delegate{string error;if(ChangeHotkey(modifiers,key,out error)){dialog.DialogResult=DialogResult.OK;dialog.Close();}else feedback.Text=error;},true);save.SetBounds(24,238,228,42);dialog.Controls.Add(save);
   var cancel=ActionButton("Отмена",delegate{dialog.Close();},false);cancel.SetBounds(268,238,228,42);dialog.Controls.Add(cancel);dialog.CancelButton=cancel;
   dialog.Shown+=delegate{capture.Focus();capture.SelectAll();};editingHotkey=true;try{dialog.ShowDialog(this);}finally{editingHotkey=false;}
  }
 }
 ValueInput CreateNumberInput(int index,string name,Control parent){
  var slider=sliders[index];var input=new ValueInput{Dock=DockStyle.Fill,Minimum=(decimal)(slider.Minimum/scales[index]),Maximum=(decimal)(slider.Maximum/scales[index]),DecimalPlaces=index==6?0:2,Increment=index==6?100m:0.01m,BackColor=parent.BackColor,ForeColor=accent,Font=new Font("Segoe UI",10),Margin=new Padding(7,5,0,3),AccessibleName=name+" — точное значение",Value=(decimal)(slider.Value/scales[index])};
  input.ValueChanged+=delegate{if(syncingInputs)return;slider.Value=(int)Math.Round((double)input.Value*scales[index],MidpointRounding.AwayFromZero);};
  numberInputs[index]=input;return input;
 }
 void SyncNumberInputs(){syncingInputs=true;try{for(int i=0;i<9;i++)if(numberInputs[i]!=null){decimal value=(decimal)(sliders[i].Value/scales[i]);if(numberInputs[i].Value!=value)numberInputs[i].Value=value;}}finally{syncingInputs=false;}}
}
