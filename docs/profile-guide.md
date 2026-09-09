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

Use the same profile as a guard for a CLI operation:

```powershell
node scripts/kuka-profile-run.mjs --profile profiles/example.json krl candidate-intake --source samples/raw-krl --output .local/receipt.json
```

The wrapper validates and probes first, then forwards the remaining arguments unchanged to the CLI. When the command writes a receipt, it writes a companion `.profile.json` evidence file so existing receipt verifiers remain byte-compatible. MCP clients can apply the same sequence before invoking a vendor operation.

The MCP server also exposes `kuka_lab_profile_probe`. Configure any stdio-capable client with the server command from `plugins/kuka-virtual-validation/mcp-server/package.json`, then call that tool with the profile path. The tool is read-only and returns the same `Ready`-equivalent `Unverified`, `Missing`, `NotRun` and `UnsupportedPlatform` vocabulary used by the local probe.

For the Windows exact-C01 PowerShell adapter, run through the profile runner:

```powershell
node scripts/vendor-profile-run.mjs --profile profiles/my-machine.json -Action Preflight
```

The runner creates an ignored temporary script, replaces the reference software roots with profile values, points the adapter at the public checkout, and deletes the temporary script after exit. It does not modify generated files. The adapter still requires the user's licensed KUKA.Sim/OfficeLite assets and remains bounded by its receipt contract.

Example generic MCP client configuration:

```json
{
  "mcpServers": {
    "kuka-validation-lab": {
      "command": "node",
      "args": ["<repo>/plugins/kuka-virtual-validation/mcp-server/src/server.mjs"]
    }
  }
}
```

Replace `<repo>` with the checkout path. The profile path is passed as the tool argument, so the same server works with one adapter or a combination of adapters.

## Compatibility matrix

| Installed software | Usable capabilities | Evidence boundary |
| --- | --- | --- |
| None | file intake, hashing, receipt verification, package verification, static preflight | no vendor execution |
| KUKA.Sim | simulation adapter after capability probe | simulation only |
| OfficeLite | native KSS adapter after VM/profile probe | virtual controller only |
| WorkVisual | read-only inventory and project extraction | project evidence only |
| Any two or three | composition of the corresponding receipts | each receipt keeps its own tool/version scope |

Commercial software, licenses and VM images remain user supplied.
