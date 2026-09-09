# Environment profiles

An environment profile describes the software a user actually owns and the paths on that machine. It is public configuration, not a license file or a machine receipt. Start from [`profiles/example.json`](../profiles/example.json) and validate the paths before running a vendor adapter.

The three software blocks are independent. A profile may contain KUKA.Sim only, OfficeLite only, WorkVisual only, or any combination. Missing software must produce an explicit `Unsupported` or `NotRun` capability; the adapter must never silently fall back to another tool.

The `version` and optional `build` fields are evidence inputs. Adapter output records the resolved version, executable identity and profile hash in its receipt. Paths are referenced by `pathKey`, so changing an installation directory edits one value instead of changing scripts or MCP configuration.

The profile is intentionally separate from AI clients. An MCP client supplies the profile path when starting the server; clients that cannot use MCP can call the equivalent CLI command with the same profile. No client is allowed to provide credentials, license contents or physical-controller commands through this configuration.

Validate a profile before using it:

```powershell
node scripts/validate-profile.mjs profiles/example.json
```

The command is offline and prints a stable SHA-256 identity for the exact profile bytes. Runtime adapters will use this same identity when profile support is enabled.

Probe configured capabilities without starting vendor software:

```powershell
node scripts/probe-profile.mjs profiles/example.json
```

The probe reports `Ready`, `Missing`, or `NotRun` for each declared adapter and exits non-zero when an enabled adapter's root path is missing. It never reads license files or contacts a controller.

## Compatibility matrix

| Installed software | Usable capabilities | Evidence boundary |
| --- | --- | --- |
| None | file intake, hashing, receipt verification, package verification, static preflight | no vendor execution |
| KUKA.Sim | simulation adapter after capability probe | simulation only |
| OfficeLite | native KSS adapter after VM/profile probe | virtual controller only |
| WorkVisual | read-only inventory and project extraction | project evidence only |
| Any two or three | composition of the corresponding receipts | each receipt keeps its own tool/version scope |

Commercial software, licenses and VM images remain user supplied.
