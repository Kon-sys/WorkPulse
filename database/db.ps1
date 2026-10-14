param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('up','migrate','validate','info','seed','test','down','backup')]
    [string]$Action
)
$ErrorActionPreference='Stop'
function Invoke-Compose {
    param([string[]]$Arguments)
    & docker compose --project-directory $PSScriptRoot @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Docker Compose failed (exit $LASTEXITCODE)." }
}
if (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot '.env'))) { & (Join-Path $PSScriptRoot 'setup.ps1') }
switch ($Action) {
    'up' { Invoke-Compose @('up','-d','--wait','postgres'); Invoke-Compose @('run','--rm','migrate','migrate') }
    'migrate' { Invoke-Compose @('run','--rm','migrate','migrate') }
    'validate' { Invoke-Compose @('run','--rm','migrate','validate') }
    'info' { Invoke-Compose @('run','--rm','migrate','info') }
    'seed' { Invoke-Compose @('exec','-T','postgres','psql','-U','workpulse_owner','-d','workpulse','-v','ON_ERROR_STOP=1','-f','/seed/demo.sql') }
    'test' {
        Invoke-Compose @('exec','-T','postgres','psql','-U','workpulse_owner','-d','workpulse','-v','ON_ERROR_STOP=1','-f','/tests/integrity.sql')
        Invoke-Compose @('exec','-T','postgres','sh','-c','PGPASSWORD="$APP_DB_PASSWORD" psql -h 127.0.0.1 -U workpulse_app -d workpulse -v ON_ERROR_STOP=1 -f /tests/isolation.sql')
    }
    'down' { Invoke-Compose @('down') }
    'backup' {
        $directory=Join-Path $PSScriptRoot 'backups'
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
        $destination=Join-Path $directory ('workpulse-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'.dump')
        Invoke-Compose @('exec','-T','postgres','pg_dump','-U','workpulse_owner','-d','workpulse','-Fc','-f','/tmp/workpulse-backup.dump')
        Invoke-Compose @('cp','postgres:/tmp/workpulse-backup.dump',$destination)
        Write-Host "Backup: $destination"
    }
}
