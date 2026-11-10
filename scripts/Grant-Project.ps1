param([Parameter(Mandatory)][string]$ProjectRoot,[Parameter(Mandatory)][string]$CredentialPath,[string]$StateDirectory)
$ErrorActionPreference='Stop'
if(!$StateDirectory){$StateDirectory=Join-Path (Join-Path $env:ProgramData 'WizeMindsAdminBridge') ([guid]::NewGuid().ToString('N'))}
& (Join-Path $PSScriptRoot 'Broker.exe') init --root $ProjectRoot --state $StateDirectory --credential $CredentialPath
if($LASTEXITCODE -ne 0){throw 'Grant creation failed.'}
Write-Output ('Start with: & "'+(Join-Path $PSScriptRoot 'Start-Broker.ps1')+'" -StateDirectory "'+$StateDirectory+'"')
