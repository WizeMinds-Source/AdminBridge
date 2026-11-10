param([Parameter(Mandatory)][string]$TestRoot)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $TestRoot){throw 'TestRoot must be a fresh directory outside the repository.'}
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..')).TrimEnd('\')+'\'
if([IO.Path]::GetFullPath($TestRoot).StartsWith($repo,[StringComparison]::OrdinalIgnoreCase)){throw 'Keep test artifacts outside the source tree.'}
$dist=Join-Path $repo 'dist'
$target=Join-Path $env:ProgramFiles 'WizeMindsAdminBridge'
if(Test-Path -LiteralPath $target){throw 'Use a clean environment without this product installed for installer refusal tests.'}
& (Join-Path $dist 'Install-Broker.ps1') -WhatIf
if(Test-Path -LiteralPath $target){throw 'WhatIf created an installation.'}
Write-Output 'PASS: installer WhatIf does not install'
$null=New-Item -ItemType Directory -Path $TestRoot
Copy-Item -LiteralPath $dist -Destination (Join-Path $TestRoot 'corrupt') -Recurse
Add-Content -LiteralPath (Join-Path $TestRoot 'corrupt\LICENSE') -Value 'tamper test'
$rejected=$false
try{& (Join-Path $TestRoot 'corrupt\Install-Broker.ps1') -WhatIf}catch{if($_.Exception.Message -notmatch 'verification failed'){throw};$rejected=$true}
if(!$rejected){throw 'Tampered package was accepted.'}
Write-Output 'PASS: installer rejects tampered package'
Copy-Item -LiteralPath $dist -Destination (Join-Path $TestRoot 'missing') -Recurse
$m=Get-Content (Join-Path $TestRoot 'missing\SHA256SUMS.json') -Raw|ConvertFrom-Json
@($m|Where-Object file -ne 'Broker.exe')|ConvertTo-Json|Set-Content (Join-Path $TestRoot 'missing\SHA256SUMS.json') -Encoding UTF8
$rejected=$false
try{& (Join-Path $TestRoot 'missing\Install-Broker.ps1') -WhatIf}catch{if($_.Exception.Message -notmatch 'incomplete package manifest'){throw};$rejected=$true}
if(!$rejected -or (Test-Path -LiteralPath $target)){throw 'Manifest refusal or no-install check failed.'}
Write-Output 'PASS: installer rejects incomplete manifest without installation'
