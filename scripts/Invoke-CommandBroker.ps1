param([Parameter(Mandatory)][string]$CredentialPath,[Parameter(Mandatory)][ValidateSet('status','list','submit','read','stop','revoke','shutdown')][string]$Action,[ValidateSet('powershell','cmd')][string]$Shell='powershell',[string]$Command,[string]$JobId,[ValidateSet('stdout','stderr')][string]$Stream='stdout',[int]$Offset=0,[int]$Count=32768,[int]$TimeoutSeconds=3600)
$ErrorActionPreference='Stop'
$previousEncoding=[Console]::OutputEncoding
[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$request=@{action=$Action}
if($Action -eq 'submit'){$request.shell=$Shell;$request.command=$Command;$request.timeoutSeconds=$TimeoutSeconds}
if($Action -in @('read','stop')){$request.id=$JobId}
if($Action -eq 'read'){$request.stream=$Stream;$request.offset=$Offset;$request.count=$Count}
$temp=[IO.Path]::GetTempFileName()
try {
 [IO.File]::WriteAllText($temp,($request|ConvertTo-Json -Depth 8 -Compress),[Text.UTF8Encoding]::new($false))
 $raw=& (Join-Path $PSScriptRoot 'Broker.exe') call --credential $CredentialPath --request $temp
 if($LASTEXITCODE -ne 0){throw 'Broker connection failed.'}
 $result=$raw|ConvertFrom-Json
 if(!$result.ok){throw $result.error}
 $result.result
}finally{[Console]::OutputEncoding=$previousEncoding;Remove-Item -LiteralPath $temp -ErrorAction SilentlyContinue}
