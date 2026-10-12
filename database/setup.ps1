$ErrorActionPreference = 'Stop'
$target = Join-Path $PSScriptRoot '.env'
if (Test-Path -LiteralPath $target) { Write-Host '.env already exists; kept unchanged.'; exit 0 }
function New-Password {
    $bytes = New-Object byte[] 32
    $generator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $generator.GetBytes($bytes) } finally { $generator.Dispose() }
    return [Convert]::ToBase64String($bytes)
}
$content = "DB_PORT=55432`nDB_OWNER_PASSWORD=$(New-Password)`nDB_APP_PASSWORD=$(New-Password)`n"
[System.IO.File]::WriteAllText($target, $content, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'Created database/.env with two random passwords. Do not commit or share it.'
