param([ValidateSet('run','build','test')][string]$Action='run',[int]$DurationSeconds=0)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$taskDotnet=Join-Path $taskRoot 'tmp/dotnet/dotnet.exe'
if(-not (Test-Path -LiteralPath $taskDotnet)) {$taskDotnet=(Get-Command dotnet -ErrorAction Stop).Source}
$env:DOTNET_CLI_HOME=Join-Path $taskRoot 'tmp/dotnet-home'
$env:NUGET_PACKAGES=Join-Path $taskRoot 'tmp/nuget-packages'
Push-Location $PSScriptRoot
try {
    & $taskDotnet restore WorkPulse.Agent/WorkPulse.Agent.csproj --configfile ../server/NuGet.Config --nologo
    if($LASTEXITCODE -ne 0) {throw 'Agent restore failed.'}
    if($Action -eq 'build') {& $taskDotnet build WorkPulse.Agent -c Release --no-restore}
    elseif($Action -eq 'test') {& $taskDotnet run --project WorkPulse.Agent -c Release --no-restore -- --self-test}
    elseif($DurationSeconds -gt 0) {& $taskDotnet run --project WorkPulse.Agent -c Release --no-restore -- --seconds $DurationSeconds}
    else {& $taskDotnet run --project WorkPulse.Agent -c Release --no-restore}
    if($LASTEXITCODE -ne 0) {throw 'Agent command failed.'}
} finally {Pop-Location}
