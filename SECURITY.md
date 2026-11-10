# Security reporting

AdminBridge provides general administrator execution to a trusted local account.
Read [the security model](docs/SECURITY-MODEL.md) before granting a project.

This preview has no published vulnerability-reporting endpoint yet. Before public
release, the maintainer must establish and verify a private reporting channel and
update this document. Do not publish credentials, command output, personal data or
exploit details in a public issue. A reporting channel must not be invented.

Reports should identify the version/commit, Windows version, affected boundary,
minimal reproduction in a disposable VM, expected behavior, and observed impact.
Do not send live grant or credential files. Administrator/SYSTEM modification and
arbitrary actions by an already-authorized administrator command are outside the
containment guarantee; unauthorized pipe access, grant bypass and authentication
failures are within scope.

No stable security-support commitment is made for this review candidate. Public
support policy and disclosure handling must be agreed before publication.
