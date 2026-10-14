param([int]$Port=55433)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$database=Join-Path $root 'database'
$project='workpulse-lr11-'+(Get-Date -Format 'yyyyMMddHHmmss')
$results=Join-Path $root 'tmp/lr11'
New-Item -ItemType Directory -Path $results -Force | Out-Null
$checks=[System.Collections.Generic.List[object]]::new()
function Compose([string[]]$Arguments) {
    $output=& docker compose --project-directory $database -p $project @Arguments 2>&1
    if($LASTEXITCODE -ne 0){throw ($output -join "`n")}
    return ($output -join "`n")
}
$previousPort=$env:DB_PORT
$env:DB_PORT=[string]$Port
$timer=[System.Diagnostics.Stopwatch]::StartNew()
try {
    Compose @('up','-d','--wait','postgres') | Out-Null
    Compose @('run','--rm','migrate','migrate') | Out-Null
    Compose @('run','--rm','migrate','validate') | Out-Null
    $checks.Add(@{name='clean installation and migration validation';passed=$true;seconds=$timer.Elapsed.TotalSeconds})
    Compose @('exec','-T','postgres','psql','-U','workpulse_owner','-d','workpulse','-v','ON_ERROR_STOP=1','-f','/seed/demo.sql') | Out-Null
    $checks.Add(@{name='demo data provisioning';passed=$true})
    Compose @('exec','-T','postgres','pg_dump','-U','workpulse_owner','-d','workpulse','-Fc','-f','/tmp/lr11.dump') | Out-Null
    Compose @('exec','-T','postgres','createdb','-U','workpulse_owner','workpulse_restore') | Out-Null
    $restore=[System.Diagnostics.Stopwatch]::StartNew()
    Compose @('exec','-T','postgres','pg_restore','-U','workpulse_owner','-d','workpulse_restore','--exit-on-error','/tmp/lr11.dump') | Out-Null
    $restore.Stop()
    $sql="SELECT table_name FROM information_schema.tables WHERE table_schema='workpulse' AND table_type='BASE TABLE' ORDER BY table_name;"
    $tables=(Compose @('exec','-T','postgres','psql','-U','workpulse_owner','-d','workpulse','-At','-c',$sql)) -split "`n" | Where-Object {$_ -match '^[a-z_]+$'}
    foreach($table in $tables){
        $countSql='SELECT count(*) FROM workpulse."'+$table+'";'
        $original=Compose @('exec','-T','postgres','psql','-U','workpulse_owner','-d','workpulse','-At','-c',$countSql)
        $restored=Compose @('exec','-T','postgres','psql','-U','workpulse_owner','-d','workpulse_restore','-At','-c',$countSql)
        if($original.Trim() -ne $restored.Trim()){throw "Restored table count mismatch: $table"}
    }
    $checks.Add(@{name='backup restored and row counts equal in every table';passed=$true;tables=$tables.Count;seconds=$restore.Elapsed.TotalSeconds})
    Compose @('exec','-T','postgres','psql','-U','workpulse_owner','-d','workpulse_restore','-v','ON_ERROR_STOP=1','-f','/tests/integrity.sql') | Out-Null
    Compose @('exec','-T','postgres','sh','-c','PGPASSWORD="$APP_DB_PASSWORD" psql -h 127.0.0.1 -U workpulse_app -d workpulse_restore -v ON_ERROR_STOP=1 -f /tests/isolation.sql') | Out-Null
    $checks.Add(@{name='restored constraints and RLS';passed=$true})
    Compose @('down') | Out-Null
    Compose @('up','-d','--wait','postgres') | Out-Null
    $count=Compose @('exec','-T','postgres','psql','-U','workpulse_owner','-d','workpulse_restore','-At','-c','SELECT count(*) FROM workpulse.organizations;')
    if($count.Trim() -ne '2'){throw 'Restart persistence check failed'}
    $checks.Add(@{name='restart preserves restored data';passed=$true})
    @{date=(Get-Date -Format 'yyyy-MM-ddTHH:mm:ssK');project=$project;port=$Port;checks=$checks;scope='Isolated PostgreSQL installation; no working volume modified';retainedVolume=($project+'_pgdata')} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $results 'deployment-results.json') -Encoding UTF8
    Write-Host "PASS $($checks.Count)/$($checks.Count) deployment checks; tables: $($tables.Count); restore: $($restore.Elapsed.TotalSeconds) s"
} finally {
    Compose @('down') | Out-Null
    if($null -eq $previousPort){Remove-Item Env:DB_PORT -ErrorAction SilentlyContinue}else{$env:DB_PORT=$previousPort}
}
