param([string]$OutputDirectory=(Join-Path $PSScriptRoot 'dist'))
$ErrorActionPreference='Stop'
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
$allowed=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'dist'))
if($OutputDirectory -ne $allowed){throw 'Build output must be the repository dist directory.'}
if(Test-Path $OutputDirectory){throw 'dist already exists. Review and remove or move that generated directory before rebuilding.'}
$null=New-Item -ItemType Directory -Path $OutputDirectory
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /platform:x64 /optimize+ ('/out:'+(Join-Path $OutputDirectory 'Broker.exe')) /r:System.Web.Extensions.dll /r:System.Security.dll (Join-Path $PSScriptRoot 'src\Broker.cs') (Join-Path $PSScriptRoot 'src\Native.cs') (Join-Path $PSScriptRoot 'src\AssemblyInfo.cs')
if($LASTEXITCODE){throw 'Compilation failed.'}
Get-ChildItem (Join-Path $PSScriptRoot 'scripts') -Filter '*.ps1'|Copy-Item -Destination $OutputDirectory
Copy-Item (Join-Path $PSScriptRoot 'LICENSE') $OutputDirectory
$files=@(Get-ChildItem $OutputDirectory -File|Sort-Object Name|ForEach-Object{[ordered]@{file=$_.Name;sha256=(Get-FileHash $_.FullName).Hash}})
$files|ConvertTo-Json|Set-Content (Join-Path $OutputDirectory 'SHA256SUMS.json') -Encoding UTF8
Write-Output 'Built unsigned x64 review package in dist. No installation or project grant was performed.'
