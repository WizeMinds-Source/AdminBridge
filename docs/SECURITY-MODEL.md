# Security model

## Trust boundaries

The operating system, administrators and SYSTEM are trusted. A project grant
trusts commands from its Windows user and holders of its account-protected secret.
An authorized process can request arbitrary administrator PowerShell/CMD commands.
There is no command allowlist, project filesystem confinement, per-command human
intent check, or distinction between chats running under the same account.

Application permissions remain separate. Unrestricted Access in the recommended
assistant workflow broadens that application's access; it does not elevate its
Windows token. The broker must not be used to circumvent a rejected host approval.

## Authentication and storage

The server uses a local named pipe, rejects remote clients, denies NETWORK and
limits its DACL to SYSTEM, administrators and the granted user. The production
user mask excludes creation of extra server instances. No explicit low-integrity
label is installed. Restricted tokens can fail even when the user SID matches.

The first pipe instance is exclusive. The server checks the connecting SID and
uses mutual HMAC proofs with a fresh nonce exchange and a per-project random
256-bit secret. Requests/responses are bound to that exchange. Application replay
protection is not a promise of exactly-once job submission after connection loss.

Client credentials use DPAPI CurrentUser and a user-only file ACL. State and
installed code use protected directories. The public product has a distinct
namespace and DPAPI context; it does not import grants from other broker products.
Development mode refuses to run elevated. Never publish development credentials.

## Execution and cancellation

Children launch suspended and join a Windows Job Object before resuming. Only
standard-input/output/error handles are inherited. Closed stdin prevents interactive
input. Job objects use kill-on-close; breakaway is not enabled. Stop, timeout,
revoke and broker death target contained process trees.

Administrator commands can deliberately create services, scheduled tasks or other
externally managed work, modify ACLs or terminate the broker. Those activities
are outside the job-tree cancellation guarantee. Revocation cannot undo changes.
Remote revoke and local emergency revoke persist denial. Grants never expire
automatically; revoke explicitly when access is no longer appropriate.

## Limits and audit

- Four active jobs per project, 64 processes per job, 256 retained jobs per grant.
- 64 KiB command text, timeouts from 1 to 86,400 seconds.
- 16 MiB retained combined stdout/stderr per job and 32 KiB read pages.
- Output is monitored periodically; transient disk use can exceed the cap.

Commands/output may contain secrets and remain in protected state. Audit records
contain hashes, actions, identities, times and results rather than command text.
An HMAC chain detects modification/reordering within available audit files, but
cannot prove entire files or a tail were not deleted. Administrators can modify
code, keys or logs; this is not remote immutable auditing.

Verify audit files with the installed executable's `verify-audit --state PATH`.
Storage/audit failure stops acceptance and closes active job handles. Restart
marks previously running jobs Interrupted. Output stays available for inspection.

## Release caveats

The implementation is experimental, unsigned and not independently audited.
Hash manifests provide consistency, not publisher authenticity. No automatic
updates, remote transport, daemon service or cloud inbox is supplied. Review
installer changes and test production mode in a disposable VM before release.
