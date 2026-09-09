# Integration matrix

The lab has one stable contract across software combinations: a candidate is hashed, an operation is run through an adapter, and the result is represented by a receipt that can be verified offline. Environment profiles select the adapters; they do not contain licenses or credentials.

## Software combinations

| Local setup | What can be requested | What the evidence proves |
| --- | --- | --- |
| No KUKA software | intake, receipt verification, ValidationPackage verification, static preflight | file identity and static findings only |
| KUKA.Sim | simulation adapter, when the licensed installation and assets are present | bounded simulation result for the exact candidate/profile |
| OfficeLite | native KSS adapter, when the VM/controller profile is present | virtual-controller result, never physical safety |
| WorkVisual | project inventory, profile/readback and deployment preflight | project/configuration evidence only |
| Any pair or all three | run each available adapter and compose their separate receipts | each claim keeps its own software/version scope |

Use `node scripts/profile-capability-matrix.mjs profiles/my-machine.json` to see every non-empty combination enabled by a profile. A row marked `Unverified` means the configured directory exists; it is deliberately not a claim that the executable, license, controller or version is usable.

## AI client compatibility

The MCP server uses the standard stdio `mcpServers` shape. This keeps the integration independent of a model vendor: clients that support MCP can start the same server, while clients without MCP can call the equivalent Node and .NET commands. Pass the profile path as a tool argument or wrapper argument; do not bake a machine path into prompts.

```json
{
  "mcpServers": {
    "kuka-validation-lab": {
      "command": "node",
      "args": ["<checkout>/plugins/kuka-virtual-validation/mcp-server/src/server.mjs"]
    }
  }
}
```

The safe agent loop is: validate profile → probe capabilities → submit a candidate → verify the receipt → interpret the status. `Missing`, `NotRun`, `UnsupportedPlatform`, `Blocked`, and `Inconclusive` are not successes. The profile sidecar binds the exact profile bytes and receipt bytes so a later process can detect either being changed.

## Reuse pattern

To adapt another installation, copy `profiles/example.json`, change only `id`, `version`, `pathKey`, and `paths`, then run profile validation and the capability matrix. Adapter-specific launch behavior remains explicit in its runner; adding a new adapter must preserve the same receipt, status vocabulary, and offline verification boundary.

