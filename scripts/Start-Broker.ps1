param([Parameter(Mandatory)][string]$StateDirectory)
$ErrorActionPreference='Stop'
$broker=Join-Path $PSScriptRoot 'Broker.exe'
$args=@('serve','--state',('"'+[IO.Path]::GetFullPath($StateDirectory)+'"'))
$principal=[Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
if($principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){$process=Start-Process -FilePath $broker -ArgumentList $args -WindowStyle Hidden -PassThru}
else {$process=Start-Process -FilePath $broker -ArgumentList $args -WindowStyle Hidden -Verb RunAs -PassThru}
Start-Sleep -Milliseconds 500
if($process.HasExited){throw 'Broker did not stay running. Run Broker.exe serve --state PATH in an administrator window to see the error.'}
Write-Output "Broker process $($process.Id) started. Use status to confirm readiness."
