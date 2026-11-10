[CmdletBinding(SupportsShouldProcess=$true)]
param()
$ErrorActionPreference='Stop'
$destination=Join-Path $env:ProgramFiles 'WizeMindsAdminBridge'
$stateBase=Join-Path $env:ProgramData 'WizeMindsAdminBridge'
$admin=([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if(!$admin -and !$WhatIfPreference){throw 'Run installation in an administrator Windows PowerShell window.'}
function NoLinks([string]$Path){$cursor=[IO.Path]::GetFullPath($Path);while($cursor){if(Test-Path -LiteralPath $cursor){if((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Reparse-point installation paths are not allowed.'}};$cursor=Split-Path -Parent $cursor}}
NoLinks $destination;NoLinks $stateBase
if(Test-Path -LiteralPath $destination){throw 'An installation already exists. This preview never overwrites installed brokers. Review an upgrade separately.'}
$manifest=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'SHA256SUMS.json') -Raw|ConvertFrom-Json
$expected=@('Broker.exe','Emergency-Revoke.ps1','Grant-Project.ps1','Install-Broker.ps1','Invoke-CommandBroker.ps1','LICENSE','Start-Broker.ps1')
if(@(Compare-Object ($expected|Sort-Object) (@($manifest.file)|Sort-Object)).Count){throw 'Unexpected or incomplete package manifest.'}
foreach($entry in $manifest){if([IO.Path]::GetFileName($entry.file) -ne $entry.file -or (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot $entry.file)).Hash -ne $entry.sha256){throw 'Package verification failed.'}}
if(Test-Path -LiteralPath $stateBase){& (Join-Path $PSScriptRoot 'Broker.exe') check-protected --path $stateBase;if($LASTEXITCODE){throw 'Existing state base permissions are unsafe; nothing changed.'}}
function Protect([string]$Path,[bool]$ReadUsers){
 $acl=[Security.AccessControl.DirectorySecurity]::new();$acl.SetAccessRuleProtection($true,$false);$acl.SetOwner([Security.Principal.SecurityIdentifier]::new('S-1-5-32-544'))
 foreach($sid in @('S-1-5-18','S-1-5-32-544')){$acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.SecurityIdentifier]::new($sid),[Security.AccessControl.FileSystemRights]::FullControl,[Security.AccessControl.InheritanceFlags]'ContainerInherit,ObjectInherit',[Security.AccessControl.PropagationFlags]::None,[Security.AccessControl.AccessControlType]::Allow))}
 if($ReadUsers){$acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.SecurityIdentifier]::new('S-1-5-32-545'),[Security.AccessControl.FileSystemRights]::ReadAndExecute,[Security.AccessControl.InheritanceFlags]'ContainerInherit,ObjectInherit',[Security.AccessControl.PropagationFlags]::None,[Security.AccessControl.AccessControlType]::Allow))}
 Set-Acl -LiteralPath $Path -AclObject $acl
}
if(!$PSCmdlet.ShouldProcess($destination,'Install reviewed broker files and protect the product state base')){return}
$null=New-Item -ItemType Directory -Path $destination;Protect $destination $true
foreach($entry in $manifest){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $entry.file) -Destination $destination}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'SHA256SUMS.json') -Destination $destination
if(!(Test-Path -LiteralPath $stateBase)){$null=New-Item -ItemType Directory -Path $stateBase;Protect $stateBase $false}
Write-Output 'Installed WizeMindsAdminBridge. No broker, grant, service, task, firewall rule or network listener was created.'
