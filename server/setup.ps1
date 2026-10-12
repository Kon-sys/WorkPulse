$ErrorActionPreference = 'Stop'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskInstallerDir = Join-Path $taskRoot 'tmp/lr8'
New-Item -ItemType Directory -Path $taskInstallerDir -Force | Out-Null
$taskInstaller = Join-Path $taskInstallerDir 'dotnet-install.ps1'
Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $taskInstaller
& $taskInstaller -Version 10.0.401 -InstallDir (Join-Path $taskRoot 'tmp/dotnet') -NoPath
if (-not (Test-Path -LiteralPath (Join-Path $taskRoot 'tmp/dotnet/dotnet.exe'))) { throw 'SDK installation failed.' }
