# Security policy

## Reporting

Do not include credentials, licenses, proprietary robot files, controller identifiers, private network addresses or raw machine paths in public issues. Prefer synthetic reproductions and sanitized diagnostics.

Once the remote repository has private vulnerability reporting enabled, use its **Security → Report a vulnerability** form. That channel is not yet verified during repository bootstrap. If it is unavailable, ask the maintainer to establish a private reporting channel without posting vulnerability details or sensitive artifacts publicly. No response-time commitment or supported release range has been established yet.

## Evidence boundary

A hash-bound receipt can detect changes to referenced bytes and verify the recorded evidence relationships. It is not a cryptographic attestation of a trusted execution environment. A producer able to fabricate both artifacts and receipts is outside a hash-only authenticity guarantee. Offline verification still requires the referenced evidence to be available.

Static preflight does not prove successful execution. Simulation does not prove native KSS execution. Virtual native KSS results do not qualify a physical robot, tooling, workcell or safety configuration. A result must retain its candidate identity, environment, check scope and unresolved findings.

## Repository boundary

No physical controller access is supported by this project. Commercial tool operations require user-provided licensed environments and must remain inside their documented bounded virtual workflows. Do not add generic controller, credential, VM or license management to the plugin.

The external source workspace is read-only and may be accessed only by the allowlisted importer. Imported material must pass the privacy gate before entering generated paths. No vendor binaries, licenses, virtual machine images or proprietary robot assets may be committed or included in a release. Redistribution rights must also be checked for bundled dependencies.
