# Migration Guide: `globalRoot` Removal

## Summary

The `globalRoot` configuration field has been removed from `steergen.config.yaml`. Its functionality is fully replaced by **rules packs** with `scope: global`.

If your configuration still contains `globalRoot`, Steergen will emit diagnostic error **CFG001** and exit with code 2. This guide walks you through converting your existing global rules directory into a rules pack.

## Why Was `globalRoot` Removed?

The `globalRoot` mechanism was a filesystem path coupling that:

- Did not support versioning
- Could not be shared across teams without manual file copying
- Had no scope-based precedence model
- Required every developer to maintain the same local directory structure

Rules packs solve all of these problems. They support versioning via Git refs, team sharing via GitHub repositories, and explicit scope-based merge precedence.

## The CFG001 Error

When `globalRoot` is present in your `steergen.config.yaml`, Steergen emits:

```
CFG001 [Error]: The 'globalRoot' configuration field has been removed.
Use rules packs with 'scope: global' instead.
See migration guide: https://github.com/aabs/steergen/docs/migration/globalroot-removal.md
```

Steergen exits with code 2 and does not proceed with generation.

### Remediation

Remove the `globalRoot` field from your configuration and follow the migration steps below to convert your global rules directory into a rules pack.

## Step-by-Step Migration

### 1. Locate Your Global Rules Directory

Find the path referenced by `globalRoot` in your current `steergen.config.yaml`:

```yaml
# Before (no longer supported)
globalRoot: /path/to/shared/governance-rules
projectRoot: ./steering
```

### 2. Create a `pack.yaml` Manifest

In the root of your global rules directory, create a `pack.yaml` file:

```yaml
# /path/to/shared/governance-rules/pack.yaml
name: "my-org-baseline-rules"
version: "1.0.0"
minSteergenVersion: "1.5.0"
scope: global
```

Field reference:

| Field | Required | Description |
|-------|----------|-------------|
| `name` | Yes | A unique identifier for the pack |
| `version` | Yes | Semantic version of the pack |
| `minSteergenVersion` | Yes | Minimum Steergen version required to load this pack |
| `scope` | Yes | One of `global`, `supplemental`, or `project` |
| `rulesRoot` | No | Subdirectory containing `.md` rule files (defaults to pack root) |

If your rules are in a subdirectory, add the `rulesRoot` field:

```yaml
name: "my-org-baseline-rules"
version: "1.0.0"
minSteergenVersion: "1.5.0"
scope: global
rulesRoot: "rules/"
```

### 3. Publish to a GitHub Repository (Recommended)

Push your global rules directory to a GitHub repository so it can be shared across projects:

```bash
cd /path/to/shared/governance-rules
git init
git add .
git commit -m "Initial rules pack"
git remote add origin https://github.com/my-org/baseline-rules.git
git push -u origin main
git tag v1.0.0
git push --tags
```

### 4. Update `steergen.config.yaml`

Remove `globalRoot` and add the rules pack to `rulesPacks`:

```yaml
# After (using GitHub-published rules pack)
projectRoot: ./steering
generationRoot: .

rulesPacks:
  - source: "github:my-org/baseline-rules"
    ref: "v1.0.0"
    scope: global

registeredTargets:
  - kiro
  - speckit
```

### 5. Download the Pack

Run the following command to download the rules pack to your local cache:

```bash
steergen update --rules
```

Or add it directly via the CLI (which downloads automatically):

```bash
steergen rules-pack add github:my-org/baseline-rules --ref v1.0.0 --scope global
```

### 6. Verify

Run generation to confirm everything works:

```bash
steergen run
```

## Alternative: Use a Local Path (Development Only)

If you are not ready to publish to GitHub, you can reference the rules pack locally during development by keeping the directory on disk and using `steergen update --rules` after adding it to your config. Note that the pack must still contain a valid `pack.yaml` manifest.

For local-only development workflows, the rules pack cache is located at:

```
~/.steergen/rules/{owner}/{repo}/{ref}/
```

## Scope Reference

When migrating from `globalRoot`, use `scope: global` to maintain the same merge precedence behaviour. Global-scoped rules have the lowest precedence and are overridden by project-local rules.

| Scope | Precedence | Use Case |
|-------|-----------|----------|
| `global` | Lowest | Organisation-wide baseline rules (replaces `globalRoot`) |
| `supplemental` | Middle | Team or department rules |
| `project` | Highest (same as local) | Project-specific shared rules |

Merge order: project-local rules > project-scoped packs > supplemental-scoped packs > global-scoped packs.

## Pinning Recommendations

For deterministic builds, pin your rules pack to a specific Git tag or commit SHA:

```yaml
# Pinned to tag (recommended)
rulesPacks:
  - source: "github:my-org/baseline-rules"
    ref: "v1.0.0"
    scope: global
```

```yaml
# Pinned to commit SHA (immutable, skips re-download)
rulesPacks:
  - source: "github:my-org/baseline-rules"
    ref: "abc123def456789012345678901234567890abcd"
    scope: global
```

Using a branch name (e.g., `main`) works but Steergen will emit a diagnostic warning recommending you pin to a tag or SHA for reproducibility.

## Complete Example: Migrated `pack.yaml`

```yaml
name: "acme-governance-baseline"
version: "2.0.0"
minSteergenVersion: "1.5.0"
scope: global
rulesRoot: "rules/"
```

This manifest declares a rules pack named `acme-governance-baseline` at version `2.0.0`, requiring Steergen 1.5.0 or later, with global scope (lowest merge precedence), and steering documents located in the `rules/` subdirectory.
