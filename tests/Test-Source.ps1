$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$files=@(Get-ChildItem -LiteralPath $root -Recurse -File -Force|Where-Object {$_.FullName -notmatch '[\\/](dist|\.git|artifacts|test-results)[\\/]'})
foreach($file in $files){
 if($file.Extension -in @('.credential','.pfx','.p12','.key','.exe','.dll','.log','.bin') -or $file.Name -in @('grant.json','broker-process.json')){throw ('Private/generated artifact in source: '+$file.Name)}
 $text=[IO.File]::ReadAllText($file.FullName)
 if($text -match '-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----'){throw ('Private key material detected: '+$file.Name)}
 if($text -match 'S-1-5-21-\d+-\d+-\d+-\d+' -or $text -match '[A-Za-z]:\\Users\\[A-Za-z0-9_.-]+\\'){throw ('Machine-specific identity/path detected: '+$file.Name)}
 if($file.Extension -eq '.ps1'){$tokens=$null;$errors=$null;$null=[Management.Automation.Language.Parser]::ParseFile($file.FullName,[ref]$tokens,[ref]$errors);if($errors){throw ($errors|Out-String)}}
}
$broker=[IO.File]::ReadAllText((Join-Path $root 'src\Broker.cs'))
$native=[IO.File]::ReadAllText((Join-Path $root 'src\Native.cs'))
if(!$broker.Contains('WizeMindsAdminBridge/v1') -or !$native.Contains('namespace WizeMindsAdminBridge')){throw 'Product isolation identifiers differ.'}
$dist=Join-Path $root 'dist'
if(Test-Path $dist){foreach($entry in (Get-Content (Join-Path $dist 'SHA256SUMS.json') -Raw|ConvertFrom-Json)){if([IO.Path]::GetFileName($entry.file) -ne $entry.file -or (Get-FileHash (Join-Path $dist $entry.file)).Hash -ne $entry.sha256){throw 'Built manifest mismatch.'}}}
Write-Output ('PASS: '+$files.Count+' source files checked; PowerShell syntax, product identifiers, artifact exclusions and available build hashes verified.')
