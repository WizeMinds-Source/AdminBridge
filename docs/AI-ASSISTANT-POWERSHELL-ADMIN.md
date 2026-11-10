# How to run administrator PowerShell commands from a local AI assistant with AdminBridge

Company: **WizeMinds** · Author: **NGL/GPT**

Can ChatGPT or a local AI assistant run PowerShell as administrator? It can use
AdminBridge when its host provides a permitted local Windows command runner.
A browser-only chat does not gain access to your computer by downloading this tool.

AdminBridge is an experimental Windows command broker. You start it elevated,
authorize a trusted project, and use an authenticated local client to submit
PowerShell or CMD jobs. The assistant can then collect output and exit codes,
stop a managed job, or revoke that project's authorization.

## Why ordinary PowerShell commands can return Access denied

Being signed in to an administrator account does not mean every process has an
elevated Windows token. A normal PowerShell window or assistant command runner
can run at medium integrity. Its Administrators group may be marked deny-only.
Changing a chat's permission setting does not itself elevate that Windows token.

AdminBridge separates the normal client from the elevated executor:

```text
Local AI assistant / Windows command runner
    -> authenticated local named pipe
    -> elevated AdminBridge process
    -> managed PowerShell or CMD job
    <- output, exit code and job status
```

The broker is local. It does not expose a public terminal, create a cloud-chat
relay, or bypass application approval policies.

## 1. Check that your assistant has a local Windows connection

Ask the assistant to report its execution environment and Windows identity using
its actual command tool. A Linux cloud container, a browser displaying a chat,
and a local Windows command runner are different environments. AdminBridge's
named-pipe client needs access to the Windows computer running the broker.

For the WizeMinds local-assistant workflow, select **Unrestricted Access** in the
trusted chat's permission control; some versions call it **Full Access**. This
broadens the host application's permissions, not the Windows token. Where managed
policy limits that setting, use the host's supported approval mechanism instead.
Do not disable protections or use another route to bypass a rejected approval.

## 2. Install and authorize a project

Download the [preview release](https://github.com/WizeMinds-Source/AdminBridge/releases/tag/v0.1.0-preview)
and follow the [step-by-step setup guide](STEP-BY-STEP.md). Review the source and
[security model](SECURITY-MODEL.md) first. The build is unsigned and experimental.

Use your normal Windows account elevated through UAC for installation and grant
creation. Start the broker with the state-directory command printed by setup.
Keep the credential private. Do not upload credentials, grant files or runtime
state to a chat, repository or issue tracker.

## 3. Verify access from the assistant itself

Run this through the assistant's permitted local Windows command runner, using
the credential path you created. The example assumes the setup guide's paths:

```powershell
$client = Join-Path $env:ProgramFiles 'WizeMindsAdminBridge\Invoke-CommandBroker.ps1'
$credential = Join-Path $env:LOCALAPPDATA 'WizeMindsAdminBridge\example.credential'
& $client -CredentialPath $credential -Action status
```

Expected broker fields are `administrator=True`, `authorized=True` and
`development=False`. These describe the broker, not an elevation of the assistant.
Administrator setup success alone does not prove the assistant can connect.
If status fails, see [access troubleshooting](TROUBLESHOOTING.md).

## 4. Submit a harmless administrator-token check

```powershell
$job = & $client -CredentialPath $credential -Action submit -Shell powershell `
  -Command '$identity=[Security.Principal.WindowsIdentity]::GetCurrent(); [pscustomobject]@{User=$identity.Name; Administrator=([Security.Principal.WindowsPrincipal]$identity).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)} | ConvertTo-Json' `
  -TimeoutSeconds 60
$job.id
& $client -CredentialPath $credential -Action read -JobId $job.id -Stream stdout
& $client -CredentialPath $credential -Action read -JobId $job.id -Stream stderr
```

Keep the returned job ID. Poll `read` until the state leaves `Running`, check the
exit code, and collect remaining output pages. `Completed` can still have a nonzero
exit code. For exact output, concatenate decoded base64 pages before text decoding.
This example checks identity only; it does not change system configuration.

## 5. Run CMD or wait for a PowerShell background job

```powershell
$cmdJob = & $client -CredentialPath $credential -Action submit -Shell cmd `
  -Command 'echo Hello from AdminBridge' -TimeoutSeconds 60

$psJob = & $client -CredentialPath $credential -Action submit -Shell powershell `
  -Command '$j=Start-Job {Get-Date}; try {$j|Wait-Job|Receive-Job} finally {$j|Remove-Job}' `
  -TimeoutSeconds 60
```

Read each job using its own ID. Receive background-job output inside the managed
command rather than leaving it running after the command returns. Jobs have separate
shell state and closed standard input; interactive prompts are unsupported.

## 6. Stop work or revoke access

```powershell
& $client -CredentialPath $credential -Action stop -JobId $job.id
# When this project should no longer be authorized:
& $client -CredentialPath $credential -Action revoke
# To close the broker:
& $client -CredentialPath $credential -Action shutdown
```

Stop targets a managed process tree. Revoke persists denial across restarts and
stops that project's jobs. Shutdown closes the broker; without revoke, the grant
remains valid for a later administrator-approved restart. These actions cannot
undo changes already made. Work delegated to external services may outlive a job.

## Common questions

### Does this remove every UAC or assistant permission prompt?

No. Installation, granting and startup require elevation. While the broker is
running, the project grant authorizes its command requests, but host application
approvals remain independent. It is not a universal UAC bypass.

### Can a normal ChatGPT website tab use it directly?

Not by itself. A permitted local Windows tool connection is required. AdminBridge
does not install a ChatGPT connector or expose a network API.

### Is project authorization a filesystem sandbox?

No. It groups a credential, working directory, audit and managed jobs. An authorized
administrator command can affect the rest of the machine. Only grant projects
and automation you trust with that level of access.

### Can I tell whether a command ran after a lost connection?

Inspect existing jobs and audit records before trying again. Submission is not
an exactly-once API; blindly resubmitting could perform a change twice.

### Is this production-ready?

No. Version 0.1.0-preview has passed local development integration and installer
refusal checks. Production validation of the newly branded build in disposable
Windows VMs and independent security review remain pending. Read the release notes.

## Further reading

- [Install and use AdminBridge](STEP-BY-STEP.md)
- [Security model](SECURITY-MODEL.md)
- [Troubleshooting](TROUBLESHOOTING.md)
- [Source and releases](https://github.com/WizeMinds-Source/AdminBridge)
- [Windows mandatory integrity control](https://learn.microsoft.com/en-us/windows/win32/secauthz/mandatory-integrity-control)
- [Windows restricted tokens](https://learn.microsoft.com/en-us/windows/win32/secauthz/restricted-tokens)
- [Codex Windows sandbox and permissions](https://learn.chatgpt.com/docs/windows/windows-sandbox)