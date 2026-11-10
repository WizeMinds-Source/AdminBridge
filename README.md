# AdminBridge: administrator PowerShell and CMD for local AI assistants

**Company: WizeMinds · Author: NGL/GPT · Version: 0.1.0-preview**

Looking for a way to run **PowerShell as administrator from ChatGPT or a local AI assistant**?
Read [How to run administrator PowerShell commands from a local AI assistant](docs/AI-ASSISTANT-POWERSHELL-ADMIN.md).
AdminBridge requires a permitted local Windows command connection; a browser-only chat cannot use it directly.

Start with the [step-by-step installation and use guide](docs/STEP-BY-STEP.md).

An experimental, local Windows administrator command broker for trusted automation.
Run PowerShell and CMD jobs through an authenticated named pipe, collect output,
and stop work or revoke a project's authorization. No network listener or cloud
service is included. This is an unsigned review candidate, not an independently
audited security product.

**A project grant permits general administrator commands. It is not a sandbox.**
Project scope associates authorization, working directory, audit and jobs; it does
not restrict commands to that directory. Only grant access to trusted automation.

## Requirements

- Windows x64 with Windows PowerShell 5.1 and .NET Framework 4.8.
- An administrator account for installation, grant creation and broker startup.
- The same Windows account for client use, normally without elevation.
- No NuGet packages, external modules or download steps are required to build.

Windows 10 x64 was used for local development testing. Validate your target Windows
version before use. Public Windows CI remains a release-review task.

## Build and install

Review the source first. In Windows PowerShell, from the repository directory:

```powershell
.\Build.ps1
.\dist\Install-Broker.ps1 -WhatIf
```

Run `dist\Install-Broker.ps1` in an administrator PowerShell window when ready.
It installs to `%ProgramFiles%\WizeMindsAdminBridge` and protects
`%ProgramData%\WizeMindsAdminBridge`. Existing installations are never overwritten.
Installation does not create a grant or start any process, service or scheduled
task. It adds no firewall rule. Hash manifests detect accidental corruption;
they do not authenticate the publisher. Builds and scripts are currently unsigned.

## Authorize a project once

Use your normal Windows account elevated through UAC, not alternate credentials.
Create a project directory and a private credential directory under your profile.
In the elevated window:

```powershell
$install = Join-Path $env:ProgramFiles 'WizeMindsAdminBridge'
$project = Join-Path $env:USERPROFILE 'Projects\Example'
$private = Join-Path $env:LOCALAPPDATA 'WizeMindsAdminBridge'
New-Item -ItemType Directory -Path $project,$private -Force | Out-Null
$credential = Join-Path $private 'example.credential'
& "$install\Grant-Project.ps1" -ProjectRoot $project -CredentialPath $credential
```

Grant creation prints its state directory and a start command. Run that start
command. A grant remains authorized across restarts until revoked. Credential and
state paths must be new; existing grants are not silently reset. Keep the credential
local and do not commit it or send it to a chat, issue tracker or public repository.

## Run and read jobs

From a normal PowerShell window of the authorized account:

```powershell
$client = Join-Path $env:ProgramFiles 'WizeMindsAdminBridge\Invoke-CommandBroker.ps1'
$credential = Join-Path $env:LOCALAPPDATA 'WizeMindsAdminBridge\example.credential'
& $client -CredentialPath $credential -Action status
$job = & $client -CredentialPath $credential -Action submit -Shell powershell `
    -Command 'Get-Date; Get-Location' -TimeoutSeconds 60
& $client -CredentialPath $credential -Action read -JobId $job.id -Stream stdout
& $client -CredentialPath $credential -Action read -JobId $job.id -Stream stderr
& $client -CredentialPath $credential -Action list
```

Use `-Shell cmd` for CMD. Commands can contain newlines, pipelines and redirection.
Every job starts in the project root with fresh shell state and closed stdin.
Interactive commands are unsupported. PowerShell uses Windows PowerShell 5.1;
explicitly invoke another installed executable when a particular bitness is needed.
Wait for PowerShell background jobs inside the managed command and receive their
output before exiting.

Poll `read` until the job leaves `Running`, then collect all remaining bytes.
Use `nextOffset` with `-Offset` until it reaches `availableBytes`. `base64` is
authoritative: concatenate decoded bytes before decoding text, because UTF-8
characters can cross page boundaries. `Completed` does not mean exit code zero.
Never automatically retry an uncertain submission: inspect `list` and audit first.
Request IDs authenticate individual exchanges; submission is not an exactly-once
or client-idempotent API.

## Stop, revoke and shut down

```powershell
& $client -CredentialPath $credential -Action stop -JobId $job.id
& $client -CredentialPath $credential -Action revoke
& $client -CredentialPath $credential -Action shutdown
```

`stop` terminates the managed job tree. `revoke` persists denial and terminates
the project's jobs. It survives restart; read/status/list/stop/shutdown remain
available. `shutdown` stops the broker and its jobs without revoking the grant.
There is no automatic startup. Starting again requires elevation.

If the broker is unresponsive, run the installed `Emergency-Revoke.ps1` elevated
with `-StateDirectory` set to the recorded state path. It persists revocation and
targets a process only when executable, PID and creation time match its record.
Stops cannot undo changes already made or guarantee cancellation of work delegated
to external services. See [security boundaries](docs/SECURITY-MODEL.md).

## Assistant and application access

This project does not install a ChatGPT/Codex tool or bypass application permissions.
An ordinary client of the granted Windows user is intended to work. A sandboxed
process may have a different account or restricting SIDs, even at medium integrity.
Use the host application's supported, approved execution mechanism when required.
Do not widen the pipe DACL or disable the application's sandbox to bypass a denial.
See [troubleshooting](docs/TROUBLESHOOTING.md).

## Tests, development and release status

```powershell
.\tests\Test-Source.ps1
.\tests\Test-Broker.ps1 -TestRoot (Join-Path $env:TEMP ([guid]::NewGuid().ToString('N')))
```

The integration test requires a **non-elevated** Windows account by default and
uses a distinct development grant. It tests real process execution, authentication,
limits, output, timeouts, stop/revoke, crash cleanup and audit verification.
It preserves generated test state outside the source tree. Do not publish it.
For production-mode tests, install this build into a disposable VM, then run the
test elevated with `-Production -BrokerExe` pointing at the installed executable.
Never use existing working credentials for tests.

See [testing](docs/TESTING.md), [review checklist](docs/RELEASE-CHECKLIST.md),
[contributing](CONTRIBUTING.md) and [vulnerability handling](SECURITY.md).
Licence: [MIT](LICENSE). Published by [WizeMinds-Source](https://github.com/WizeMinds-Source/AdminBridge).
This is an experimental preview; production validation and independent security review remain pending.
