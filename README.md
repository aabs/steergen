# specgen
a simple CLI tool to work with tool independent specification files

## CI Integration

### Exit-code contract

| Exit code | Meaning |
|-----------|---------|
| `0` | Success — no errors |
| `1` | Validation error — one or more steering documents have schema or rule violations |
| `2` | Configuration error — missing directory, unreadable file, or bad CLI arguments |
| `3` | Generation error — target component failed to write output |

### validate step

Add a `validate` step to your pipeline to gate on document correctness:

```sh
specgen validate --global <global-docs-dir> --project <project-docs-dir>
```

Exit code `1` blocks the build. Exit code `0` allows it to proceed.

### run step with deterministic manifest

The `run` command produces a `generation-manifest.json` alongside output artefacts.  
The manifest records SHA-256 hashes for every generated file and a `success` boolean.

```sh
specgen run --global <global-docs-dir> --output <out-dir> --target speckit
cat <out-dir>/generation-manifest.json
```

CI pipelines can:
1. **Verify determinism** — compare manifest hashes across two runs on the same inputs.
2. **Gate on failure** — check `"success": true` in the manifest before publishing artefacts.

### Release tags

specgen uses two tag formats:

| Format | Meaning |
|--------|---------|
| `vX.Y.Z` | Stable release — passes full release gate |
| `vX.Y.Z-previewN` | Preview release — for early feedback; no stability guarantee |

The GitHub Actions `release-gate` job enforces this convention and verifies the deterministic manifest on every tagged release.

## GitHub Actions CI

A ready-to-use workflow is provided in `.github/workflows/ci.yml` with three jobs:

| Job | Trigger | Purpose |
|-----|---------|---------|
| `build-and-test` | Every push / PR | Compile and run all tests |
| `validate-gate` | After tests pass | Validate steering documents (exit-code gate) |
| `release-gate` | On `v*` tags only | Verify tag format + deterministic manifest |

