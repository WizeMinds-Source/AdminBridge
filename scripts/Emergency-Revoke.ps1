param([Parameter(Mandatory)][string]$StateDirectory)
$ErrorActionPreference='Stop'
$broker=Join-Path $PSScriptRoot 'Broker.exe'
& $broker local-revoke --state $StateDirectory
if($LASTEXITCODE -ne 0){throw 'Revocation failed.'}
$file=Join-Path $StateDirectory 'broker-process.json'
if(Test-Path -LiteralPath $file){
 $record=Get-Content -LiteralPath $file -Raw|ConvertFrom-Json
 $process=Get-Process -Id $record.pid -ErrorAction SilentlyContinue
 if($process -and $process.Path -eq $record.executable -and $process.Path -eq $broker -and $process.StartTime.ToUniversalTime().Ticks.ToString() -eq $record.startTicks){Stop-Process -Id $process.Id -Force;Write-Output 'Matching broker stopped; Windows closes its job objects and terminates their process trees.'}
}
