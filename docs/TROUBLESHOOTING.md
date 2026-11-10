# Troubleshooting

## Access denied from the assistant

Test `status` from ordinary PowerShell, administrator PowerShell, and the actual
assistant command runner. The WizeMinds guide uses the chat's Unrestricted Access
setting; verify the actual account and token after changing it. Do not assume
that installing the broker as administrator validates assistant connectivity.

- Ordinary/admin clients pass, assistant fails: inspect the host application's
  supported permission/approval mechanism, runner account and restricting SIDs.
- Only administrator passes: inspect the actual pipe DACL, integrity and granted
  SID. Do not grant Everyone access or lower integrity blindly.
- All fail: verify the broker is running for the recorded state and project.
- Pipe opens but authentication fails: inspect project/credential mismatch first.

The same account name is insufficient evidence of the same token. UAC can mark
Administrators deny-only; sandbox tokens can add restricting SIDs or use another
account. User and restricting-SID access checks can both be required. Filesystem
permission to read a script is not permission to open a named pipe.

## Credentials and sessions

Use the grant creator's account, elevated through normal UAC during setup.
Do not use alternate administrator credentials. DPAPI decryption can fail under
another account or logon mechanism. Never copy or print grant.json or a credential
to troubleshoot. Compare selected project ID, SID, root and pipe metadata locally.
Do not create a fresh grant merely to investigate an existing revoked/stale state.

## Jobs and output

`Completed` may have a nonzero exit code. Check stderr. Use explicit exit codes
in multi-step commands. Interactive input is unsupported. Receive background-job
output before returning. For exact Unicode, concatenate decoded base64 pages.
After a submission connection failure, inspect existing jobs before resubmitting.
After broker failure, retained jobs can be Interrupted rather than Completed.

## Sources

- https://learn.microsoft.com/en-us/windows/win32/secauthz/restricted-tokens
- https://learn.microsoft.com/en-us/windows/win32/secauthz/mandatory-integrity-control
- https://learn.microsoft.com/en-us/windows/win32/ipc/named-pipe-security-and-access-rights
- https://learn.chatgpt.com/docs/windows/windows-sandbox
