# specgen
a simple CLI tool to work with tool independent specification files

## Quick Command Reference

### `steergen init` — Bootstrap folder layout

```sh
steergen init <project-root> --target speckit --target kiro
```

Creates `steering/global/`, `steering/project/`, and per-target output folders.  
Writes `steergen.config.yaml` with the bootstrapped global/project paths and registered targets.  
`--target` is repeatable. Idempotent: safe to re-run.

### `steergen validate` — Gate on document correctness

```sh
steergen validate --global steering/global --project steering/project
steergen validate --global steering/global --project steering/project --quiet
```

Reads all `*.md` files in both directories, parses frontmatter and rule blocks, and reports schema errors, duplicate IDs, invalid severities, and supersedes warnings.

### `steergen run` — Generate target outputs

```sh
steergen run --global steering/global --project steering/project --output .steergen/out
steergen run --global steering/global --project steering/project --output .steergen/out --target speckit
steergen run --quiet
```

Resolves overlays and profiles, then generates output for all registered targets (or the specified `--target` list). When `steergen.config.yaml` exists in the current directory, it is discovered automatically unless `--config` points elsewhere. Writes a `generation-manifest.json` alongside output artefacts.

### `steergen inspect` — View the resolved model as JSON

```sh
steergen inspect --global steering/global --project steering/project
steergen inspect --global steering/global --project steering/project --profile production
```

Outputs the fully-resolved, profile-filtered steering model to stdout as JSON.

### `steergen target add / remove` — Manage registered targets

```sh
steergen target add copilot-agent
steergen target remove kiro
```

Idempotent: `add` is safe when the target is already registered. `remove` only updates the config and does not delete generated artefacts. Both commands use `steergen.config.yaml` from the current directory by default.

### `steergen update` — Update template-pack version

```sh
steergen update
steergen update --version 1.2.3
steergen update --preview
```

Resolves latest compatible (`steergen update`), pins an exact version, or includes preview candidates with `--preview`.

## CI Integration

### Exit-code contract

| Exit code | Meaning |
|-----------|---------|
| `0` | Success — no errors |
| `1` | Validation error — one or more steering documents have schema or rule violations |
| `2` | Configuration error — missing directory, unreadable file, or bad CLI arguments |
| `3` | Generation error — target component failed to write output |
| `5` | Conflict error — optimistic-lock conflict detected (config changed between read and write) |

### validate step

Add a `validate` step to your pipeline to gate on document correctness:

```sh
steergen validate --global steering/global --project steering/project
```

Exit code `1` blocks the build. Exit code `0` allows it to proceed.

### run step with deterministic manifest

The `run` command produces a `generation-manifest.json` alongside output artefacts.  
The manifest records SHA-256 hashes for every generated file and a `success` boolean.

```sh
steergen run --global steering/global --output .steergen/out --target speckit
cat .steergen/out/generation-manifest.json
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

