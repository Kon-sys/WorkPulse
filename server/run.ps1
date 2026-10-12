param([ValidateSet('build','test','demo')][string]$Action = 'test')
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskDotnet = Join-Path $taskRoot 'tmp/dotnet/dotnet.exe'
if (-not (Test-Path -LiteralPath $taskDotnet)) {
    $taskCommand = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $taskCommand) { throw 'Install .NET SDK 10.0.401 or use server/setup.ps1.' }
    $taskDotnet = $taskCommand.Source
}
$env:DOTNET_CLI_HOME = Join-Path $taskRoot 'tmp/dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
$env:NUGET_PACKAGES = Join-Path $taskRoot 'tmp/nuget-packages'
Push-Location $PSScriptRoot
try {
    & $taskDotnet restore WorkPulse.slnx --configfile NuGet.Config --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
    switch ($Action) {
        'build' { & $taskDotnet build WorkPulse.slnx --configuration Release --nologo --no-restore }
        'test' { & $taskDotnet run --project tests/WorkPulse.Tests --configuration Release --no-restore }
        'demo' { & $taskDotnet run --project WorkPulse.Demo --configuration Release --no-restore }
    }
    if ($LASTEXITCODE -ne 0) { throw "dotnet $Action failed (exit $LASTEXITCODE)." }
} finally { Pop-Location }
