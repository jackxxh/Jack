# Contributing

This repository has one writer per file. Read [AGENTS.md](AGENTS.md) before changing anything. Documentation, synthetic samples, workflows and the importer are handwritten. The importer exclusively owns `src/`, `tests/`, `tools/`, `plugins/` and `IMPORT_MANIFEST.json`.

## Refresh generated files

Refresh the generated closure from an immutable source commit:

```sh
node scripts/import-from-lab.mjs --lab <WORKSPACE_PATH> --source-commit <sha>
```

`<WORKSPACE_PATH>` is a locally authorized, read-only source workspace containing the lab subtree. It is never a public prerequisite for using a published checkout. Only maintainers refreshing generated code need it.

1. Record the source commit SHA, then run the importer. Never edit generated files or the private source. Check redistribution rights for every imported dependency.
2. Run the importer. Resolve privacy findings in the import policy without weakening the gate or changing source behavior. If a required source change is unavoidable, stop and ask the owner to decide.
3. Run it again with the same inputs. Compare per-file hashes for all generated files and the manifest; retain the comparison and zero-hit privacy report for review. Confirm every generated file appears in the manifest.
4. Run synthetic Node MCP and pure-logic C# tests in this checkout. Keep vendor-dependent tests and document their environment skips. Execute the README quickstart from a fresh clone without vendor software.
5. Review generated diffs and manifest provenance together. Commit with an owner-approved local Git identity. During initial establishment, present the final file inventory and manifest and wait for explicit approval before any first push.

The exact test commands, generated layout and clean-clone evidence will be documented after the implementation audit. A source-less contributor can edit the handwritten area; generated-code requests should describe the intended change for a maintainer to assess through the importer.
