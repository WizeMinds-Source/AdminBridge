# Testing

Run `Build.ps1`, then `tests\Test-Source.ps1`. Run integration tests from ordinary,
non-elevated Windows PowerShell with a fresh directory outside the repository:

```powershell
.\tests\Test-Broker.ps1 -TestRoot (Join-Path $env:TEMP ('AdminBridge-test-'+[guid]::NewGuid().ToString('N')))
```

Development integration tests use only newly generated credentials/state. They
exercise the real broker and native Windows process APIs, including adversarial
authentication, cross-job traversal rejection, Unicode output, pagination, quotas,
concurrency, timeout, descendant termination, crash cleanup, revocation persistence,
shutdown and audit verification. Tests retain private output under TestRoot for
diagnosis. Never publish that directory or upload raw CI test artifacts.

For a production test, use a disposable Windows VM, install the generated `dist`
package, and run the same test in administrator PowerShell:

```powershell
.\tests\Test-Broker.ps1 -Production `
  -BrokerExe (Join-Path $env:ProgramFiles 'WizeMindsAdminBridge\Broker.exe') `
  -TestRoot (Join-Path $env:TEMP ('AdminBridge-admin-test-'+[guid]::NewGuid().ToString('N')))
```

This creates an isolated production test grant. Do not point tests at real
credentials. Also verify a normal same-user client can connect to the elevated
server, and validate the actual assistant connection separately. Test emergency
revocation and installer ownership/ACL refusal paths in that disposable VM.

The source ZIP excludes binaries, credentials, state, captures and old test reports.
Builds are not claimed reproducible byte-for-byte. A local sanitized review report
records which checks actually ran; unrun target/platform checks remain pending.
Public CI and a Windows version matrix must be established before stable release.
