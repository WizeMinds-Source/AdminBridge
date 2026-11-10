param([string]$BrokerExe=(Join-Path $PSScriptRoot '..\dist\Broker.exe'),[Parameter(Mandatory)][string]$TestRoot,[switch]$Production)
$ErrorActionPreference='Stop'
$OutputEncoding=[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$BrokerExe=(Resolve-Path -LiteralPath $BrokerExe).Path
if(Test-Path -LiteralPath $TestRoot){throw 'TestRoot must be a new directory.'}
$null=New-Item -ItemType Directory -Path $TestRoot
$TestRoot=(Resolve-Path -LiteralPath $TestRoot).Path
$project=Join-Path $TestRoot 'project with spaces'
$null=New-Item -ItemType Directory -Path $project
$state=Join-Path $TestRoot 'state';$credential=Join-Path $TestRoot 'client.credential'
if($Production){$state=Join-Path (Join-Path $env:ProgramData 'WizeMindsAdminBridge') ('test-'+[guid]::NewGuid().ToString('N'))}
$results=[Collections.Generic.List[object]]::new();$server=$null
function Assert($Condition,[string]$Name) {if(!$Condition){throw "FAILED: $Name"};$results.Add([pscustomobject]@{test=$Name;passed=$true});Write-Host "PASS $Name"}
function Call($Request) {
 $file=Join-Path $TestRoot 'request.json'
 [IO.File]::WriteAllText($file,($Request|ConvertTo-Json -Compress -Depth 10),[Text.UTF8Encoding]::new($false))
 $raw=& $BrokerExe call --credential $credential --request $file
 if($LASTEXITCODE -ne 0){throw "Client failed: $raw"}
 return ($raw|ConvertFrom-Json)
}
function Start-Server {
 $args=@('serve','--state',('"'+$state+'"'));if(!$Production){$args+='--development'}
 $script:server=Start-Process -FilePath $BrokerExe -ArgumentList $args -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $TestRoot 'server.stdout') -RedirectStandardError (Join-Path $TestRoot 'server.stderr')
 $deadline=[DateTime]::UtcNow.AddSeconds(10)
 do {Start-Sleep -Milliseconds 100;if($server.HasExited){throw (Get-Content (Join-Path $TestRoot 'server.stderr') -Raw)}}while(!(Select-String -LiteralPath (Join-Path $TestRoot 'server.stdout') -SimpleMatch '"ready":true' -Quiet) -and [DateTime]::UtcNow -lt $deadline)
}
function Wait-JobResult([string]$Id) {
 $deadline=[DateTime]::UtcNow.AddSeconds(20)
 do {$r=Call @{action='read';id=$Id;stream='stdout'};if(!$r.ok){throw $r.error};if($r.result.job.state -ne 'Running'){return $r.result};Start-Sleep -Milliseconds 100}while([DateTime]::UtcNow -lt $deadline)
 throw "Job $Id did not finish."
}
function Submit([string]$Shell,[string]$Command,[int]$Seconds=30){$r=Call @{action='submit';shell=$Shell;command=$Command;timeoutSeconds=$Seconds};if(!$r.ok){throw $r.error};return $r.result.id}
try {
 $args=@('init','--root',$project,'--state',$state,'--credential',$credential);if(!$Production){$args+='--development'}
 $null=& $BrokerExe @args;if($LASTEXITCODE -ne 0){throw 'Grant creation failed.'}
 Start-Server
 $status=Call @{action='status'}
 Assert ($status.ok -and $status.result.authorized) 'Authenticated project connection'
 Assert ($status.result.administrator -eq [bool]$Production) 'Expected execution privilege level'
 $probe=Join-Path $TestRoot 'ProbeTests.exe'
 & (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe') /nologo /target:exe /platform:x64 /main:WizeMindsAdminBridge.ProbeTests ('/out:'+$probe) /r:System.Web.Extensions.dll /r:System.Security.dll (Join-Path $PSScriptRoot '..\src\Broker.cs') (Join-Path $PSScriptRoot '..\src\Native.cs') (Join-Path $PSScriptRoot 'ProbeTests.cs')
 if($LASTEXITCODE -ne 0){throw 'Probe compilation failed.'}
 & $probe $credential | Tee-Object -FilePath (Join-Path $TestRoot 'authentication-tests.txt') | Write-Host
 Assert ($LASTEXITCODE -eq 0) 'Eight adversarial authentication checks'
 $j=Submit 'cmd' 'echo CMD_OK & echo CMD_ERROR 1>&2 & exit /b 7'
 $r=Wait-JobResult $j
 Assert ($r.text.Contains('CMD_OK') -and $r.job.exitCode -eq 7) 'CMD output and exit code'
 $r=Call @{action='read';id=$j;stream='stderr'}
 Assert ($r.result.text.Contains('CMD_ERROR')) 'Separate stderr capture'
 $j=Submit 'powershell' 'Write-Output "PS_OK café 漢字"; [Console]::Error.WriteLine("PS_ERROR"); exit 9'
 $r=Wait-JobResult $j
 Assert ($r.text.Contains('PS_OK café 漢字') -and $r.job.exitCode -eq 9) 'PowerShell Unicode output and exit code'
 $r=Call @{action='read';id=$j;stream='stderr'}
 Assert ($r.result.text.Contains('PS_ERROR')) 'PowerShell stderr'
 $background=Submit 'powershell' '$j=Start-Job { "BACKGROUND_OK" }; try {$j|Wait-Job -Timeout 10|Receive-Job} finally {$j|Remove-Job -Force}'
 $backgroundResult=Wait-JobResult $background
 Assert ($backgroundResult.job.exitCode -eq 0 -and $backgroundResult.text.Contains('BACKGROUND_OK')) 'PowerShell background job output captured'
 $r=Call @{action='read';id=$j;stream='stdout';offset=0;count=3};Assert ($r.result.nextOffset -eq 3 -and [Convert]::FromBase64String($r.result.base64).Length -eq 3) 'Bounded output pagination'
 $j=Submit 'powershell' 'throw "EXPECTED_FAILURE"';$r=Wait-JobResult $j;Assert ($r.job.exitCode -ne 0) 'PowerShell failure returns nonzero exit code'
 $parallel=@();1..4|ForEach-Object{$parallel+=Submit 'powershell' 'Start-Sleep -Seconds 120' 180}
 $r=Call @{action='submit';shell='cmd';command='echo excess'};Assert (!$r.ok) 'Concurrent job limit enforced'
 foreach($parallelId in $parallel){$null=Call @{action='stop';id=$parallelId}}
 $r=Call @{action='status'};Assert ($r.result.active -eq 0) 'Controls remain responsive with four jobs'
 $j=Submit 'powershell' '$chunk="X"*1048576; 1..24 | ForEach-Object { [Console]::Write($chunk) }; Start-Sleep -Seconds 30' 60
 $r=Wait-JobResult $j;Assert ($r.job.state -eq 'OutputLimit' -and $r.availableBytes -le 16777216) 'Output quota terminates job and caps retained bytes'
 $j=Submit 'powershell' '$p=[Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent(); Write-Output ("ADMIN="+$p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)); Write-Output (Get-Location).Path'
 $r=Wait-JobResult $j
 Assert ($r.text.Contains(('ADMIN='+([bool]$Production).ToString())) -and $r.text.Contains($project)) 'Child administrator token and project working directory'
 $r=Call @{action='submit';shell='bash';command='x'};Assert (!$r.ok) 'Unsupported shell rejected'
 $r=Call @{action='read';id='../grant.json';stream='stdout'};Assert (!$r.ok) 'Cross-job path traversal rejected'
 $r=Call @{action='grant'};Assert (!$r.ok) 'Remote grant creation rejected'
 $r=Call @{action='submit';shell='cmd';command='echo x';timeoutSeconds=0};Assert (!$r.ok) 'Invalid timeout rejected'
 $j=Submit 'powershell' 'Start-Sleep -Seconds 30' 1
 $r=Wait-JobResult $j;Assert ($r.job.state -eq 'TimedOut') 'Timeout terminates job'
 $pidFile=Join-Path $project 'child.pid'
 $childCommand='$p=Start-Process -FilePath "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -ArgumentList "-NoProfile -NonInteractive -Command Start-Sleep -Seconds 120" -WindowStyle Hidden -PassThru; Set-Content -LiteralPath '''+$pidFile.Replace("'","''")+''' -Value $p.Id; Start-Sleep -Seconds 120'
 $j=Submit 'powershell' $childCommand 180
 $deadline=[DateTime]::UtcNow.AddSeconds(10);while(!(Test-Path -LiteralPath $pidFile) -and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 100}
 $childId=[int](Get-Content -LiteralPath $pidFile)
 Assert ($null -ne (Get-Process -Id $childId -ErrorAction SilentlyContinue)) 'Descendant process started'
 $r=Call @{action='stop';id=$j};Start-Sleep -Milliseconds 250
 Assert ($r.result.state -eq 'Stopped' -and !(Get-Process -Id $childId -ErrorAction SilentlyContinue)) 'Stop kills the descendant process'
 $r=Call @{action='stop';id=$j};Assert ($r.ok -and $r.result.state -eq 'Stopped') 'Stop is idempotent'
 # A crash closes the job handle and kills descendants; restart records interruption.
 Remove-Item -LiteralPath $pidFile
 $j=Submit 'powershell' $childCommand 180
 $deadline=[DateTime]::UtcNow.AddSeconds(10);while(!(Test-Path -LiteralPath $pidFile) -and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 100}
 $childId=[int](Get-Content -LiteralPath $pidFile)
 Stop-Process -Id $server.Id -Force;$server.WaitForExit();Start-Sleep -Milliseconds 250
 Assert (!(Get-Process -Id $childId -ErrorAction SilentlyContinue)) 'Broker crash kills descendant process'
 Start-Server
 $r=Call @{action='read';id=$j;stream='stdout'};Assert ($r.result.job.state -eq 'Interrupted') 'Restart marks interrupted job'
 $j=Submit 'powershell' 'Start-Sleep -Seconds 120' 180
 $r=Call @{action='revoke'};Assert ($r.ok -and $r.result.revoked) 'Revoke succeeds'
 $r=Call @{action='read';id=$j;stream='stdout'};Assert ($r.result.job.state -eq 'Revoked') 'Revoke stops running job'
 $r=Call @{action='submit';shell='cmd';command='echo FORBIDDEN'};Assert (!$r.ok) 'Revoked grant blocks new commands'
 $r=Call @{action='shutdown'};$server.WaitForExit(10000)|Out-Null
 Start-Server
 $r=Call @{action='status'};Assert (!$r.result.authorized) 'Revocation persists after restart'
 $r=Call @{action='submit';shell='cmd';command='echo FORBIDDEN'};Assert (!$r.ok) 'Restart cannot restore revoked authorization'
 $r=Call @{action='shutdown'};$server.WaitForExit(10000)|Out-Null
 Assert ($server.HasExited) 'Authenticated shutdown'
 Assert (@(Get-ChildItem -LiteralPath $state -Filter 'audit-*.jsonl').Count -ge 2) 'Persistent audit files'
 $auditResult=& $BrokerExe verify-audit --state $state|ConvertFrom-Json
 Assert ($LASTEXITCODE -eq 0 -and $auditResult.ok -and $auditResult.records -gt 20) 'Audit authentication and chain verification'
 [IO.File]::WriteAllText((Join-Path $TestRoot 'state-path.txt'),$state)
 $results|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $TestRoot 'results.json') -Encoding UTF8
 Write-Host "All $($results.Count) checks passed."
} catch {
 $results.Add([pscustomobject]@{test=$_.Exception.Message;passed=$false});$results|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $TestRoot 'results.json') -Encoding UTF8
 throw
} finally {if($server -and !$server.HasExited){Stop-Process -Id $server.Id -Force}}
