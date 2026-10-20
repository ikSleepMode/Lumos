$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$testDir=Join-Path $root 'work'
New-Item -ItemType Directory -Force $testDir | Out-Null
foreach($name in @('UpdateTests','SessionTests')) {
 $exe=Join-Path $testDir "$name.exe"
 & (Join-Path $root 'src/build.ps1') -EntryPoint $name -TargetType exe -OutputPath $exe -ExtraSources @((Join-Path $root "tests/$name.cs"))
 & $exe
 if($LASTEXITCODE -ne 0){throw "$name failed"}
}
