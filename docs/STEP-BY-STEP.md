# Install and use AdminBridge

Company: **WizeMinds** · Author: **NGL/GPT**

## 1. Review the package

Use a Windows x64 computer with Windows PowerShell 5.1 and .NET Framework 4.8.
Extract the source ZIP into a folder you control. Read README.md and the security
model. This preview is unsigned; check the source and its origin before elevation.
Do not copy another computer's grant, credential or runtime-state directory.

## 2. Build

Open ordinary Windows PowerShell in the extracted repository folder and run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Build.ps1
```

This creates `dist`. It does not install or start anything. Build refuses an existing
`dist` directory; review and move or remove your generated output before rebuilding.
The process-scoped ExecutionPolicy option does not change machine execution policy.

## 3. Install

Open Windows PowerShell **as administrator**, using your normal account via UAC.
Change into the same extracted folder, then run:

```powershell
.\dist\Install-Broker.ps1 -WhatIf
.\dist\Install-Broker.ps1
```

Expected: installed files in `%ProgramFiles%\WizeMindsAdminBridge`. No grant or
running broker yet. If an installation already exists, stop and inspect it;
this preview deliberately does not overwrite or upgrade an existing installation.

## 4. Grant your project

In the same administrator window, choose the project you trust with administrator
execution. The example creates a new directory under your profile:

```powershell
$install = Join-Path $env:ProgramFiles 'WizeMindsAdminBridge'
$project = Join-Path $env:USERPROFILE 'Projects\AdminBridgeExample'
$private = Join-Path $env:LOCALAPPDATA 'WizeMindsAdminBridge'
New-Item -ItemType Directory -Path $project,$private -Force | Out-Null
$credential = Join-Path $private 'example.credential'
& "$install\Grant-Project.ps1" -ProjectRoot $project -CredentialPath $credential
```

Save the **state directory** and **start command** printed by this command in your
private project notes. Do not upload credentials or grant.json. Existing credential
files are not overwritten, and revoked grants are not automatically renewed.

## 5. Start the broker

Run the exact start command printed in step 4. It runs the installed
`Start-Broker.ps1 -StateDirectory ...` script. Starting requires administrator
elevation, but later client commands do not each require UAC. Authorization remains
until revoked; there is no automatic startup after sign-out or reboot.

## 6. Set the chat permission

For the WizeMinds assistant workflow, select **Unrestricted Access** in the chat
permission control. Some app versions call the setting **Full Access**. Use it
only in the trusted local project chat intended to operate this broker.

This broadens the assistant's host permissions; it does **not** grant a Windows
administrator token or replace the broker's authenticated project grant. A cloud
chat still needs a real local Windows command connection. The label and availability
depend on the host application. If organizational policy blocks the setting, follow
the application's supported approval process; do not bypass managed restrictions.
AdminBridge itself does not change any chat/app permission setting.

## 7. Confirm the connection from the assistant

Have the assistant run the following through its actual local Windows command tool:

```powershell
$client = Join-Path $env:ProgramFiles 'WizeMindsAdminBridge\Invoke-CommandBroker.ps1'
$credential = Join-Path $env:LOCALAPPDATA 'WizeMindsAdminBridge\example.credential'
& $client -CredentialPath $credential -Action status
```

Expected: `administrator=True`, `authorized=True`, `development=False`. The
`administrator` field describes the broker. A successful administrator installer
alone is not proof that the assistant can connect. If access is denied, compare
ordinary PowerShell and the assistant tool context using the troubleshooting guide.

## 8. Submit a harmless first command

```powershell
$job = & $client -CredentialPath $credential -Action submit -Shell powershell `
  -Command 'Get-Date; Get-Location' -TimeoutSeconds 60
$job.id
& $client -CredentialPath $credential -Action read -JobId $job.id
& $client -CredentialPath $credential -Action read -JobId $job.id -Stream stderr
```

Repeat `read` while state is `Running`. For long output use `-Offset` with the
returned `nextOffset` until you reach `availableBytes`. Preserve base64 bytes for
exact output. Confirm the exit code as well as the job state.

## 9. Run CMD or PowerShell background jobs

```powershell
& $client -CredentialPath $credential -Action submit -Shell cmd `
  -Command 'echo Hello from CMD' -TimeoutSeconds 60
& $client -CredentialPath $credential -Action submit -Shell powershell `
  -Command '$j=Start-Job {Get-Date}; try {$j|Wait-Job|Receive-Job} finally {$j|Remove-Job}' `
  -TimeoutSeconds 60
```

Record each returned job ID and read its output. Jobs use separate shell sessions;
working variables do not persist between submissions. Commands must not require
interactive input. Do not retry an uncertain submission automatically.

## 10. Stop, revoke or close

```powershell
& $client -CredentialPath $credential -Action stop -JobId $job.id
& $client -CredentialPath $credential -Action revoke
& $client -CredentialPath $credential -Action shutdown
```

Use the appropriate action; these are not a mandatory sequence. Stop affects one
job; revoke permanently disables its grant and stops project jobs; shutdown stops
the broker while retaining authorization. Revocation cannot undo completed changes.
For emergency revocation, use the installed `Emergency-Revoke.ps1` in administrator
PowerShell with your recorded `-StateDirectory`. Keep audit records for inspection.

## 11. Return later or uninstall

Restart an authorized broker with its saved start command. Revoked access requires
an intentional new grant. Do not delete revocation records to resume access.
To remove the product, revoke and shut down every grant first, archive private
records securely, then review and remove only this product's installation and
the state/credential paths you created. No service or scheduled-task cleanup is
needed. There is no automatic cleanup script that could erase your audit history.
