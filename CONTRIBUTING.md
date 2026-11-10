# Contributing

Company: WizeMinds · Author: NGL/GPT

Build with the Windows .NET Framework compiler using Build.ps1. Keep dependencies
explicit; do not add downloaded binaries or publish local state. Use small changes
with a clear problem statement, security implications and actual validation results.

Authentication, grant lifecycle, pipe ACL, installer and native process changes
require targeted tests and review. Preserve denial after revocation, keep development
mode non-elevated, and keep app approvals separate from broker authorization.
Do not add a remote terminal endpoint without a separately reviewed threat model.

Run source checks and non-elevated integration tests. Test elevated paths only in
a disposable VM. Submit sanitized results, not credentials, grant files, raw audit
logs or command captures. Follow SECURITY.md for sensitive vulnerabilities.
