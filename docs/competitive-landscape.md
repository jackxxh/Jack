# Competitive landscape and design notes

This note records a small public comparison used to improve this repository's usability. It does not copy code or prose.

## What we reviewed

| Project | Signal | Useful pattern |
| --- | ---: | --- |
| [LangChain](https://github.com/langchain-ai/langchain) | 145,984 stars at review time | Strong README promise, many runnable examples, clear integrations and contribution entry points |
| [AutoGen](https://github.com/microsoft/autogen) | 60,889 stars at review time | Architecture diagrams, examples by capability, explicit package boundaries and active CI |
| [MCPEval](https://github.com/SalesforceAIResearch/MCPEval) | 156 stars at review time | Reproducible evaluation workflow, test suite and research motivation in the first screen |
| [mcp-eval](https://github.com/lastmile-ai/mcp-eval) | 36 stars at review time | Lightweight install path and separation between agent and server evaluation |
| [agent-gate](https://github.com/Jott2121/agent-gate) | 3 stars at review time | Closest conceptual match: fail-closed gates and hash-chained receipts |

Star counts are snapshots, not quality scores. The closest receipt-oriented projects are still small, so discoverability and a concrete demo matter as much as the protocol design.

## Decisions applied here

- The README starts with a two-command, vendor-free path and states exactly what the receipt proves.
- CI and MIT badges make project health and reuse terms visible before a user reads the architecture.
- `samples/`, `fixtures/`, `probes/` and `docs/playbook/` are separate so a reader can distinguish a runnable example from vendor material and operational lessons.
- `CHANGELOG.md`, `SECURITY.md` and `CONTRIBUTING.md` provide the release, safety and contribution paths expected by established projects.
- Claims remain bounded: a passing hash check is never presented as simulator execution or physical safety.

## Next adoption experiments

The next high-value additions are a 60-second terminal recording or screenshot, one deliberately tampered receipt example, and a second non-KUKA adapter. The latter is intentionally deferred until the second real domain exists; extracting a generic framework before then would obscure the reference implementation.
