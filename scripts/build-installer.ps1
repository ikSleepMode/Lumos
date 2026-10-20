$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$packageDir=Join-Path $PSScriptRoot 'installer'
New-Item -ItemType Directory -Path $packageDir -Force | Out-Null
$exe=Join-Path $root 'src\Lumos.exe'
$msi=Join-Path $root 'dist\Lumos-Setup.msi'
$version=(Get-Item -LiteralPath $exe).VersionInfo.FileVersion
$msiVersion=([version]$version).ToString(3)
New-Item -ItemType Directory -Force (Join-Path $root 'dist') | Out-Null
$ddf=Join-Path $packageDir 'payload.ddf'
@"
.OPTION EXPLICIT
.Set CabinetNameTemplate=LumaPayload.cab
.Set DiskDirectoryTemplate=$packageDir
.Set CompressionType=LZX
.Set MaxDiskSize=0
.Set MaxCabinetSize=0
.Set InfFileName=$packageDir\setup.inf
.Set RptFileName=$packageDir\setup.rpt
.Set Cabinet=on
.Set Compress=on
"$exe" LumaExe
"@ | Set-Content -LiteralPath $ddf -Encoding ascii
& "$env:WINDIR\System32\makecab.exe" /F $ddf | Out-Null
if($LASTEXITCODE -ne 0){throw 'Cabinet creation failed'}
if(Test-Path -LiteralPath $msi){Remove-Item -LiteralPath $msi}
$installer=New-Object -ComObject WindowsInstaller.Installer
function ComCall($obj,[string]$name,[object[]]$arguments=@()){$nativeArgs=New-Object object[] $arguments.Count;for($n=0;$n -lt $arguments.Count;$n++){if($null -ne $arguments[$n]){$nativeArgs[$n]=$arguments[$n].PSObject.BaseObject}};$obj.GetType().InvokeMember($name,[Reflection.BindingFlags]::InvokeMethod,$null,$obj,$nativeArgs)}
function ComSet($obj,[string]$name,[object[]]$arguments){$nativeArgs=New-Object object[] $arguments.Count;for($n=0;$n -lt $arguments.Count;$n++){if($null -ne $arguments[$n]){$nativeArgs[$n]=$arguments[$n].PSObject.BaseObject}};$obj.GetType().InvokeMember($name,[Reflection.BindingFlags]::SetProperty,$null,$obj,$nativeArgs) | Out-Null}
$db=ComCall $installer 'OpenDatabase' @($msi,3)
[IO.File]::WriteAllText((Join-Path $packageDir '_ForceCodepage.idt'),"`r`n`r`n1251`t_ForceCodepage`r`n",[Text.Encoding]::ASCII)
ComCall $db 'Import' @($packageDir,'_ForceCodepage.idt') | Out-Null
$schemas=@{}
function Sql([string]$query){$v=ComCall $db 'OpenView' @($query);try{ComCall $v 'Execute' | Out-Null}finally{ComCall $v 'Close' | Out-Null}}
function Table([string]$name,[string[]]$columns,[string[]]$definitions,[string[]]$keys){
 $parts=for($i=0;$i -lt $columns.Count;$i++){'`'+$columns[$i]+'` '+$definitions[$i]}
 $pk=($keys|ForEach-Object{'`'+$_+'`'}) -join ', '
 Sql ('CREATE TABLE `'+$name+'` ('+($parts -join ', ')+' PRIMARY KEY '+$pk+')')
 $schemas[$name]=$columns
}
function Row([string]$table,[object[]]$values){
 $cols=$schemas[$table];if($cols.Count -ne $values.Count){throw "Invalid row length: $table ($($values.Count) vs $($cols.Count))"}
 $query='INSERT INTO `'+$table+'` ('+(($cols|ForEach-Object{'`'+$_+'`'}) -join ', ')+') VALUES ('+((@('?')*$cols.Count) -join ', ')+')'
 $v=ComCall $db 'OpenView' @($query);$record=ComCall $installer 'CreateRecord' @($cols.Count)
 try{for($i=0;$i -lt $values.Count;$i++){if($null -eq $values[$i]){continue};if($values[$i] -is [int]){ComSet $record 'IntegerData' @(($i+1),$values[$i])}else{ComSet $record 'StringData' @(($i+1),[string]$values[$i])}};ComCall $v 'Execute' @($record) | Out-Null}finally{ComCall $v 'Close' | Out-Null}
}
function Stream([string]$table,[string]$name,[string]$path){$v=ComCall $db 'OpenView' @('SELECT * FROM `'+$table+'`');$r=ComCall $installer 'CreateRecord' @(2);try{ComSet $r 'StringData' @(1,$name);ComCall $r 'SetStream' @(2,$path) | Out-Null;ComCall $v 'Modify' @(3,$r) | Out-Null}finally{ComCall $v 'Close' | Out-Null}}
Table 'Property' @('Property','Value') @('CHAR(72) NOT NULL','CHAR(0) LOCALIZABLE') @('Property')
$product='{'+[Guid]::NewGuid().ToString().ToUpperInvariant()+'}'
$upgrade='{621333F5-9290-4625-8A7F-AC043FE46324}'
$properties=[ordered]@{
 ProductCode=$product;UpgradeCode=$upgrade;ProductName="Lumos Alpha $msiVersion";ProductVersion=$msiVersion;ProductLanguage='1049';Manufacturer='Lumos';
 ARPCOMMENTS='Управление гаммой экрана с предпросмотром и цветовыми темами';ARPPRODUCTICON='LumaIcon';ARPNOMODIFY='1';ARPINSTALLLOCATION='[INSTALLDIR]';
 INSTALLLEVEL='1';DefaultUIFont='UiNormal';SecureCustomProperties='PREVIOUSPRODUCTS;LEGACYPRODUCTS;NEWERPRODUCTS';REBOOT='ReallySuppress'
}
foreach($entry in $properties.GetEnumerator()){Row 'Property' @($entry.Key,$entry.Value)}
Table 'Directory' @('Directory','Directory_Parent','DefaultDir') @('CHAR(72) NOT NULL','CHAR(72)','CHAR(255) NOT NULL LOCALIZABLE') @('Directory')
Row 'Directory' @('TARGETDIR',$null,'SourceDir')
Row 'Directory' @('LocalAppDataFolder','TARGETDIR','.')
Row 'Directory' @('ProgramsDir','LocalAppDataFolder','Programs')
Row 'Directory' @('INSTALLDIR','ProgramsDir','LumaStudio')
Row 'Directory' @('ProgramMenuFolder','TARGETDIR','.')
Row 'Directory' @('LumaMenu','ProgramMenuFolder','Lumos')
Row 'Directory' @('DesktopFolder','TARGETDIR','.')
Row 'Directory' @('StartupFolder','TARGETDIR','.')
Table 'Component' @('Component','ComponentId','Directory_','Attributes','Condition','KeyPath') @('CHAR(72) NOT NULL','CHAR(38)','CHAR(72) NOT NULL','SHORT NOT NULL','CHAR(255)','CHAR(72)') @('Component')
Row 'Component' @('MainComponent','{D7A494A0-568C-4344-AB53-27E2B6A67885}','INSTALLDIR',260,$null,'InstallMarker')
Table 'Registry' @('Registry','Root','Key','Name','Value','Component_') @('CHAR(72) NOT NULL','SHORT NOT NULL','CHAR(255) NOT NULL LOCALIZABLE','CHAR(255) LOCALIZABLE','CHAR(0) LOCALIZABLE','CHAR(72) NOT NULL') @('Registry')
Row 'Registry' @('InstallMarker',1,'Software\LumaStudio\Installer','InstallDir','[INSTALLDIR]','MainComponent')
Table 'Feature' @('Feature','Feature_Parent','Title','Description','Display','Level','Directory_','Attributes') @('CHAR(38) NOT NULL','CHAR(38)','CHAR(64) LOCALIZABLE','CHAR(255) LOCALIZABLE','SHORT','SHORT NOT NULL','CHAR(72)','SHORT NOT NULL') @('Feature')
Row 'Feature' @('MainFeature',$null,'Lumos','Приложение и ярлыки',1,1,'INSTALLDIR',0)
Table 'FeatureComponents' @('Feature_','Component_') @('CHAR(38) NOT NULL','CHAR(72) NOT NULL') @('Feature_','Component_')
Row 'FeatureComponents' @('MainFeature','MainComponent')
Table 'File' @('File','Component_','FileName','FileSize','Version','Language','Attributes','Sequence') @('CHAR(72) NOT NULL','CHAR(72) NOT NULL','CHAR(255) NOT NULL LOCALIZABLE','LONG NOT NULL','CHAR(72)','CHAR(20)','SHORT','SHORT NOT NULL') @('File')
Row 'File' @('LumaExe','MainComponent','Lumos.exe',[int](Get-Item -LiteralPath $exe).Length,$version,'1049',16384,1)
Table 'Media' @('DiskId','LastSequence','DiskPrompt','Cabinet','VolumeLabel','Source') @('SHORT NOT NULL','SHORT NOT NULL','CHAR(64) LOCALIZABLE','CHAR(255)','CHAR(32)','CHAR(72)') @('DiskId')
Row 'Media' @(1,1,$null,'#LumaPayload.cab',$null,$null)
Stream '_Streams' 'LumaPayload.cab' (Join-Path $packageDir 'LumaPayload.cab')
Table 'Icon' @('Name','Data') @('CHAR(72) NOT NULL','OBJECT NOT NULL') @('Name')
Stream 'Icon' 'LumaIcon' (Join-Path $root 'src\assets\luma.ico')
Table 'Shortcut' @('Shortcut','Directory_','Name','Component_','Target','Arguments','Description','Hotkey','Icon_','IconIndex','ShowCmd','WkDir') @('CHAR(72) NOT NULL','CHAR(72) NOT NULL','CHAR(128) NOT NULL LOCALIZABLE','CHAR(72) NOT NULL','CHAR(255) NOT NULL','CHAR(255)','CHAR(255) LOCALIZABLE','SHORT','CHAR(72)','SHORT','SHORT','CHAR(72)') @('Shortcut')
Row 'Shortcut' @('MenuShortcut','LumaMenu','Lumos','MainComponent','[INSTALLDIR]Lumos.exe',$null,'Управление цветом экрана',$null,'LumaIcon',0,1,'INSTALLDIR')
Row 'Shortcut' @('DesktopShortcut','DesktopFolder','Lumos','MainComponent','[INSTALLDIR]Lumos.exe',$null,'Управление цветом экрана',$null,'LumaIcon',0,1,'INSTALLDIR')
Table 'RemoveFile' @('FileKey','Component_','FileName','DirProperty','InstallMode') @('CHAR(72) NOT NULL','CHAR(72) NOT NULL','CHAR(255) LOCALIZABLE','CHAR(72) NOT NULL','SHORT NOT NULL') @('FileKey')
Row 'RemoveFile' @('RemoveStartup','MainComponent','Lumos.lnk','StartupFolder',2)
Row 'RemoveFile' @('RemoveLog','MainComponent','LumaDesk-diagnostics.log','INSTALLDIR',2)
Row 'RemoveFile' @('RemoveMenuFolder','MainComponent',$null,'LumaMenu',2)
Row 'RemoveFile' @('RemoveAppFolder','MainComponent',$null,'INSTALLDIR',2)
Table 'Upgrade' @('UpgradeCode','VersionMin','VersionMax','Language','Attributes','Remove','ActionProperty') @('CHAR(38) NOT NULL','CHAR(20)','CHAR(20)','CHAR(255)','LONG NOT NULL','CHAR(255)','CHAR(72) NOT NULL') @('UpgradeCode','VersionMin','VersionMax','Language','Attributes')
Row 'Upgrade' @($upgrade,$null,$msiVersion,$null,1,$null,'PREVIOUSPRODUCTS')
# Migrate the older product's historical 2.x versions after the alpha version reset.
Row 'Upgrade' @($upgrade,'2.1.0','2.3.0',$null,769,$null,'LEGACYPRODUCTS')
Row 'Upgrade' @($upgrade,$msiVersion,'2.1.0',$null,2,$null,'NEWERPRODUCTS')
Table 'LaunchCondition' @('Condition','Description') @('CHAR(255) NOT NULL','CHAR(255) LOCALIZABLE') @('Condition')
Row 'LaunchCondition' @('Installed OR NOT NEWERPRODUCTS','Уже установлена более новая версия Lumos.')
foreach($name in @('InstallExecuteSequence','InstallUISequence')){Table $name @('Action','Condition','Sequence') @('CHAR(72) NOT NULL','CHAR(255)','SHORT') @('Action')}
$execute=[ordered]@{FindRelatedProducts=25;LaunchConditions=100;ValidateProductID=700;CostInitialize=800;FileCost=900;CostFinalize=1000;MigrateFeatureStates=1200;InstallValidate=1400;InstallInitialize=1500;RemoveExistingProducts=1550;ProcessComponents=1600;UnpublishFeatures=1800;RemoveShortcuts=3200;RemoveFiles=3500;RemoveFolders=3600;InstallFiles=4000;CreateShortcuts=4500;WriteRegistryValues=5000;RegisterUser=6000;RegisterProduct=6100;PublishFeatures=6300;PublishProduct=6400;InstallFinalize=6600}
foreach($e in $execute.GetEnumerator()){Row 'InstallExecuteSequence' @($e.Key,($null),[int]$e.Value)}
# Registry removal must run before component registration is updated on uninstall.
Row 'InstallExecuteSequence' @('RemoveRegistryValues',$null,2600)
$ui=[ordered]@{FindRelatedProducts=25;LaunchConditions=100;CostInitialize=800;FileCost=900;CostFinalize=1000;Welcome=1200;Progress=1280;ExecuteAction=1300;Completed=-1;Failed=-3;Cancelled=-2}
foreach($e in $ui.GetEnumerator()){$condition=if($e.Key -eq 'Welcome'){'NOT Installed'}else{$null};Row 'InstallUISequence' @($e.Key,$condition,[int]$e.Value)}
Table 'TextStyle' @('TextStyle','FaceName','Size','Color','StyleBits') @('CHAR(72) NOT NULL','CHAR(32) NOT NULL','SHORT NOT NULL','LONG','SHORT') @('TextStyle')
Row 'TextStyle' @('UiNormal','Segoe UI',9,0,0)
Row 'TextStyle' @('UiTitle','Segoe UI',17,0,1)
Table 'Dialog' @('Dialog','HCentering','VCentering','Width','Height','Attributes','Title','Control_First','Control_Default','Control_Cancel') @('CHAR(72) NOT NULL','SHORT NOT NULL','SHORT NOT NULL','SHORT NOT NULL','SHORT NOT NULL','LONG NOT NULL','CHAR(128) LOCALIZABLE','CHAR(50)','CHAR(50)','CHAR(50)') @('Dialog')
Table 'Control' @('Dialog_','Control','Type','X','Y','Width','Height','Attributes','Property','Text','Control_Next','Help') @('CHAR(72) NOT NULL','CHAR(50) NOT NULL','CHAR(20) NOT NULL','SHORT NOT NULL','SHORT NOT NULL','SHORT NOT NULL','SHORT NOT NULL','LONG','CHAR(72)','CHAR(0) LOCALIZABLE','CHAR(50)','CHAR(50) LOCALIZABLE') @('Dialog_','Control')
Table 'ControlEvent' @('Dialog_','Control_','Event','Argument','Condition','Ordering') @('CHAR(72) NOT NULL','CHAR(50) NOT NULL','CHAR(50) NOT NULL','CHAR(255) NOT NULL','CHAR(255)','SHORT') @('Dialog_','Control_','Event','Argument','Condition')
Table 'EventMapping' @('Dialog_','Control_','Event','Attribute') @('CHAR(72) NOT NULL','CHAR(50) NOT NULL','CHAR(50) NOT NULL','CHAR(50) NOT NULL') @('Dialog_','Control_','Event')
function TextControl($dialog,$name,$text,$x,$y,$width,$height){Row 'Control' @($dialog,$name,'Text',[int]$x,[int]$y,[int]$width,[int]$height,3,$null,$text,$null,$null)}
function ButtonControl($dialog,$name,$text,$x,$next,$event,$argument){Row 'Control' @($dialog,$name,'PushButton',[int]$x,198,82,20,3,$null,$text,$next,$null);Row 'ControlEvent' @($dialog,$name,$event,$argument,'1',1)}
Row 'Dialog' @('Welcome',50,50,380,235,3,'Установка Lumos','Continue','Continue','Cancel')
TextControl 'Welcome' 'Title' '{\UiTitle}Lumos' 20 16 340 35
TextControl 'Welcome' 'Description' "Управление гаммой и цветом экрана.`r`nПредпросмотр, профили игр и шесть цветовых тем." 20 62 340 45
TextControl 'Welcome' 'InstallInfo' "Приложение будет установлено только для текущего пользователя. Будут созданы ярлыки на рабочем столе и в меню «Пуск».`r`n`r`nПапка: [INSTALLDIR]" 20 115 340 65
ButtonControl 'Welcome' 'Continue' 'Установить' 185 'Cancel' 'EndDialog' 'Return'
ButtonControl 'Welcome' 'Cancel' 'Отмена' 277 'Continue' 'EndDialog' 'Exit'
Row 'Dialog' @('Progress',50,50,380,235,1,'Lumos',$null,$null,$null)
TextControl 'Progress' 'Title' '{\UiTitle}Выполнение операции' 20 20 340 35
TextControl 'Progress' 'ActionText' 'Подождите, Windows обрабатывает пакет…' 20 90 340 40
Row 'Control' @('Progress','Bar','ProgressBar',20,145,340,14,1,$null,$null,$null,$null)
Row 'EventMapping' @('Progress','Bar','SetProgress','Progress')
Row 'EventMapping' @('Progress','ActionText','ActionText','Text')
foreach($dialog in @('Completed','Failed','Cancelled')){
 Row 'Dialog' @($dialog,50,50,380,235,3,'Lumos','Finish','Finish','Finish')
 $title=if($dialog -eq 'Completed'){'Готово'}elseif($dialog -eq 'Cancelled'){'Операция отменена'}else{'Операция не завершена'}
 $message=if($dialog -eq 'Completed'){'Операция успешно завершена. После установки Lumos можно открыть через ярлык на рабочем столе или в меню «Пуск».'}elseif($dialog -eq 'Cancelled'){'Изменения отменены. Вы можете повторить установку позже.'}else{'Windows не удалось завершить операцию. Проверьте сообщение об ошибке Windows Installer или повторите установку с записью журнала.'}
 TextControl $dialog 'Title' ('{\UiTitle}'+$title) 20 20 340 45
 TextControl $dialog 'Message' $message 20 85 340 80
 ButtonControl $dialog 'Finish' 'Закрыть' 277 'Finish' 'EndDialog' 'Return'
}
$summary=$db.GetType().InvokeMember('SummaryInformation',[Reflection.BindingFlags]::GetProperty,$null,$db,@(20))
$summaryValues=@{1=1251;2='Установка Lumos';3="Lumos Alpha $msiVersion";4='Lumos';5='Luma, gamma, display';6='Per-user installation. No user preferences included.';7='x64;1049';9=('{'+[Guid]::NewGuid().ToString().ToUpperInvariant()+'}');14=500;15=10;18='Lumos package builder';19=2}
foreach($entry in $summaryValues.GetEnumerator()){ComSet $summary 'Property' @([int]$entry.Key,$entry.Value)}
ComCall $summary 'Persist' | Out-Null
ComCall $db 'Commit' | Out-Null
[Runtime.InteropServices.Marshal]::FinalReleaseComObject($summary) | Out-Null
[Runtime.InteropServices.Marshal]::FinalReleaseComObject($db) | Out-Null
[Runtime.InteropServices.Marshal]::FinalReleaseComObject($installer) | Out-Null
Write-Output "Создан установщик: $msi"
