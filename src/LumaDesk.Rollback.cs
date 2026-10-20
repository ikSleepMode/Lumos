using System;
using System.Collections.Generic;

public sealed class ConfirmedGammaState {
 public double[] Values;
 public ushort[] Ramp;
 public bool Enabled;
}

public partial class LumaDesk {
 readonly Dictionary<string,ConfirmedGammaState> confirmedStates=new Dictionary<string,ConfirmedGammaState>();
 readonly Dictionary<string,double[]> displayDrafts=new Dictionary<string,double[]>();
 readonly HashSet<string> uncertainDisplays=new HashSet<string>();
 string selectedDisplayDevice;long stateRevision;
 ConfirmedGammaState ConfirmedState(string device){
  ConfirmedGammaState state;
  if(!confirmedStates.TryGetValue(device,out state)){state=new ConfirmedGammaState{Values=(double[])defaults.Clone(),Ramp=(ushort[])originals[device].Clone(),Enabled=false};confirmedStates[device]=state;}
  return state;
 }
 void RememberConfirmed(string device,double[] values,ushort[] ramp,bool active){
  confirmedStates[device]=new ConfirmedGammaState{Values=(double[])(active?values:defaults).Clone(),Ramp=(ushort[])ramp.Clone(),Enabled=active};
  uncertainDisplays.Remove(device);if(active)changedDisplays.Add(device);else changedDisplays.Remove(device);
 }
 void SwitchDisplay(){
  debounce.Stop();
  if(selectedDisplayDevice!=null)displayDrafts[selectedDisplayDevice]=Current();
  var display=monitors.SelectedItem as DisplayItem;selectedDisplayDevice=display==null?null:display.Device;
  if(display==null){enabled=false;UpdateToggle();return;}
  ConfirmedGammaState state=ConfirmedState(display.Device);double[] draft;
  enabled=state.Enabled||uncertainDisplays.Contains(display.Device);
  SetValues(state.Enabled?state.Values:displayDrafts.TryGetValue(display.Device,out draft)?draft:defaults);
  stateRevision++;UpdateToggle();SchedulePreview();
  status.Text=uncertainDisplays.Contains(display.Device)?"Состояние гаммы этого экрана не подтверждено. Повторите выключение.":enabled?"Показаны применённые настройки выбранного экрана.":"Предпросмотр активен. Коррекция выбранного экрана выключена.";
 }
 void RecoverRejected(string device,bool requested,ConfirmedGammaState previous){
  GammaResult rollback=gammaWriter(device,previous.Ramp);
  RecordGammaResult(device,previous.Enabled,new GammaResult{Confirmed=rollback.Confirmed,WriteAccepted=rollback.WriteAccepted,Detail="rollback-to-confirmed: "+rollback.Detail},previous.Values);
  ConfirmedGammaState effective=previous;bool recovered=rollback.Confirmed;
  if(!recovered&&previous.Enabled){
   GammaResult reset=gammaWriter(device,originals[device]);
   RecordGammaResult(device,false,new GammaResult{Confirmed=reset.Confirmed,WriteAccepted=reset.WriteAccepted,Detail="rollback-to-original: "+reset.Detail},defaults);
   if(reset.Confirmed){effective=new ConfirmedGammaState{Values=(double[])defaults.Clone(),Ramp=(ushort[])originals[device].Clone(),Enabled=false};recovered=true;}
  }
  if(recovered){
   RememberConfirmed(device,effective.Values,effective.Ramp,effective.Enabled);enabled=effective.Enabled;
   status.Text=effective.Enabled?(requested?"Настройка отклонена. Возвращены последние успешно применённые значения.":"Не удалось выключить коррекцию. Восстановлены её предыдущие рабочие значения."):"Настройка отклонена. Исходная гамма восстановлена; коррекция выключена.";
  }else{
   uncertainDisplays.Add(device);changedDisplays.Add(device);enabled=true;
   status.Text="Не удалось подтвердить гамму экрана. Предпросмотр расчётный; повторите выключение коррекции.";
  }
  SetValues(effective.Values);displayDrafts[device]=(double[])effective.Values.Clone();stateRevision++;
  // Cancel the pending rejected preview and render only the recovered parameters.
  previewDelay.Stop();RefreshScreenshot();
 }
}
