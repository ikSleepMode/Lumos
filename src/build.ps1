param([string]$OutputPath,[string]$EntryPoint='ModernMain',[string[]]$ExtraSources=@(),[string]$TargetType='winexe')
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (!(Test-Path -LiteralPath $compiler)) { throw 'Не найден компилятор .NET Framework 4.x.' }
$target = if($OutputPath){[IO.Path]::GetFullPath($OutputPath)}else{Join-Path $PSScriptRoot 'Lumos.exe'}
$framework = Split-Path $compiler
$arguments = @('/nologo',"/target:$TargetType","/main:$EntryPoint","/out:$target",'/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Web.Extensions.dll',"/reference:$framework\System.Xaml.dll","/reference:$framework\WPF\WindowsBase.dll","/reference:$framework\WPF\PresentationCore.dll","/reference:$framework\WPF\PresentationFramework.dll","/resource:$(Join-Path $PSScriptRoot 'assets\preview.png'),LumaDesk.Preview.png","/resource:$(Join-Path $PSScriptRoot 'Studio.xaml'),LumaDesk.Studio.xaml","/resource:$(Join-Path $PSScriptRoot 'assets\luma.ico'),LumaDesk.Icon.ico","/resource:$(Join-Path $PSScriptRoot 'assets\lumos.png'),Lumos.Brand.png","/win32icon:$(Join-Path $PSScriptRoot 'assets\luma.ico')","/win32manifest:$(Join-Path $PSScriptRoot 'LumaDesk.manifest')")
$sources = Get-ChildItem -LiteralPath $PSScriptRoot -Filter 'LumaDesk*.cs' | Select-Object -ExpandProperty FullName
& $compiler @arguments @sources @ExtraSources
if ($LASTEXITCODE -ne 0) { throw 'Сборка завершилась с ошибкой.' }
Write-Output "Сборка завершена: $target"
