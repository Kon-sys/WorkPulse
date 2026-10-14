param([string]$PythonPath='python')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$taskConfig=Join-Path $PSScriptRoot 'appsettings.Local.json'
$taskBackup=Join-Path $taskRoot ('tmp/lr10/agent-config-'+[guid]::NewGuid()+'.json')
$taskHadConfig=Test-Path -LiteralPath $taskConfig
New-Item -ItemType Directory -Path (Split-Path $taskBackup -Parent) -Force | Out-Null
if($taskHadConfig){Copy-Item -LiteralPath $taskConfig -Destination $taskBackup}
Push-Location $taskRoot
try {
    & $PythonPath server/tests/native-agent-check.py
    if($LASTEXITCODE -ne 0){throw 'Enrollment failed.'}
    & "$PSScriptRoot/run.ps1" -Action run -DurationSeconds 6
    & $PythonPath server/tests/native-agent-check.py --verify
    if($LASTEXITCODE -ne 0){throw 'Native integration check failed.'}
} finally {
    if($taskHadConfig){Copy-Item -LiteralPath $taskBackup -Destination $taskConfig;Remove-Item -LiteralPath $taskBackup}
    else {Remove-Item -LiteralPath $taskConfig -ErrorAction SilentlyContinue}
    Pop-Location
}
