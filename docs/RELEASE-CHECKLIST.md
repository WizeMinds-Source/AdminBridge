# Maintainer review before publication

- [ ] Approve product AdminBridge, company WizeMinds, author NGL/GPT.
- [ ] Confirm MIT licensing and rights to all included source.
- [x] Publish under WizeMinds-Source/AdminBridge.
- [ ] Review all files and scan the final source ZIP for secrets and private data.
- [ ] Review authentication, state ACLs, process controls and installer security.
- [ ] Run production installation and access tests in disposable Windows VMs.
- [ ] Validate the assistant permission instructions against the actual target app.
- [ ] Establish a private vulnerability-reporting channel and update SECURITY.md.
- [ ] Set support policy, public CI and release signing/provenance policy.
- [ ] Inspect documentation and sanitized test results.
- [x] Maintainer authorized publication as an experimental public preview.

Suggested collection structure: one company organization, separate repositories
for AdminBridge, PLC function-block libraries and connection drivers. A company
landing page can index each product's docs and releases. Apply the same company
and author metadata, but choose licensing/support for each repository separately.
PLC libraries and drivers require their own customer-data and machine-settings
review before inclusion. They are not part of this AdminBridge source package.
