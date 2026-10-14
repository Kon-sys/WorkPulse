param([string]$CoverageDirectory='')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
if(-not $CoverageDirectory){$CoverageDirectory=Join-Path $taskRoot 'tmp/lr10/coverage'}
$taskCoverage=Get-ChildItem -LiteralPath $CoverageDirectory -Recurse -Filter coverage.cobertura.xml | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
if(-not $taskCoverage){throw 'Run server/run.ps1 -Action coverage first.'}
[xml]$taskXml=Get-Content -LiteralPath $taskCoverage.FullName
$taskRows=@()
foreach($taskPackage in $taskXml.coverage.packages.package){
 $taskLines=@($taskPackage.classes.class.lines.line)
 $taskUnique=$taskLines | Group-Object { $_.number.ToString()+'|'+$_.ParentNode.ParentNode.filename }
 $taskCovered=@($taskUnique | Where-Object { @($_.Group | Where-Object { [int]$_.hits -gt 0 }).Count -gt 0 }).Count
 $taskRows+= [pscustomobject]@{Assembly=$taskPackage.name;LinePercent=[math]::Round([double]$taskPackage.'line-rate'*100,2);BranchPercent=[math]::Round([double]$taskPackage.'branch-rate'*100,2);CoveredLines=$taskCovered;Lines=$taskUnique.Count}
}
$taskText=@('# Покрытие модульными тестами ЛР10','','Снимок реального Cobertura-прогона. Это покрытие только модульными тестами: HTTP/браузерные сценарии не включены. Высокое покрытие Domain не означает такое же покрытие всего сервера.','','| Сборка | Строки, % | Ветви, % |','|---|---|---|')
foreach($taskRow in $taskRows){$taskText+='| '+$taskRow.Assembly+' | '+$taskRow.LinePercent+' | '+$taskRow.BranchPercent+' |'}
$taskText+='';$taskText+='Общее покрытие строк: '+[math]::Round([double]$taskXml.coverage.'line-rate'*100,2)+'%. Порог методичкой не задан; непокрытые участки требуют отдельного анализа.'
[IO.File]::WriteAllLines((Join-Path $PSScriptRoot 'testing/coverage-summary.md'),$taskText,(New-Object Text.UTF8Encoding($false)))
$taskRows | Format-Table -AutoSize
