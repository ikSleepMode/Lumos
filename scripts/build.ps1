$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
New-Item -ItemType Directory -Force (Join-Path $root 'dist') | Out-Null
& (Join-Path $root 'src/build.ps1')
& (Join-Path $PSScriptRoot 'build-installer.ps1')
Copy-Item (Join-Path $root 'src/Lumos.exe') (Join-Path $root 'dist/Lumos.exe') -Force
