param([ValidateSet('run','seed')][string]$Action='run',[int]$Port=5080)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$taskEnv=Join-Path $taskRoot 'database/.env'
if(-not (Test-Path -LiteralPath $taskEnv)) {throw 'Run database/setup.ps1 and database/db.ps1 -Action up first.'}
$taskSettings=@{}
foreach($taskLine in Get-Content -LiteralPath $taskEnv) {
    if($taskLine -match '^([A-Z_]+)=(.*)$') {$taskSettings[$Matches[1]]=$Matches[2]}
}
$taskDotnet=Join-Path $taskRoot 'tmp/dotnet/dotnet.exe'
if(-not (Test-Path -LiteralPath $taskDotnet)) {$taskDotnet=(Get-Command dotnet -ErrorAction Stop).Source}
$env:DOTNET_CLI_HOME=Join-Path $taskRoot 'tmp/dotnet-home'
$env:NUGET_PACKAGES=Join-Path $taskRoot 'tmp/nuget-packages'
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:ASPNETCORE_URLS="http://127.0.0.1:$Port"
$env:ConnectionStrings__WorkPulse="Host=127.0.0.1;Port=$($taskSettings['DB_PORT']);Database=workpulse;Username=workpulse_app;Password=$($taskSettings['DB_APP_PASSWORD']);Search Path=workpulse;Include Error Detail=false"
$taskSecretFile=Join-Path $taskRoot 'tmp/api-demo-password.txt'
if($Action -eq 'seed') {
    if(-not (Test-Path -LiteralPath $taskSecretFile)) {
        New-Item -ItemType Directory -Path (Split-Path $taskSecretFile -Parent) -Force | Out-Null
        [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(18)) | Set-Content -LiteralPath $taskSecretFile -NoNewline
    }
    $env:WORKPULSE_DEMO_PASSWORD=(Get-Content -LiteralPath $taskSecretFile -Raw).Trim()
}
Push-Location $PSScriptRoot
try {
    if($Action -eq 'seed') {& $taskDotnet run --project WorkPulse.Api -c Release --no-build -- --seed-users}
    else {& $taskDotnet run --project WorkPulse.Api -c Release --no-build}
    if($LASTEXITCODE -ne 0) {throw 'API command failed.'}
} finally {
    Pop-Location
    Remove-Item Env:ConnectionStrings__WorkPulse -ErrorAction SilentlyContinue
    Remove-Item Env:WORKPULSE_DEMO_PASSWORD -ErrorAction SilentlyContinue
}
