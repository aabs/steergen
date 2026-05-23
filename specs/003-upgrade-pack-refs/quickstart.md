# Quickstart

## Prerequisites
- .NET 10 SDK installed
- Existing `steergen.config.yaml` with external rules/template pack references
- Network access to the configured pack sources

## 1. Inspect current pack references
Use `steergen inspect` (or existing config inspection workflow) to identify the canonical selector values for the target reference.

Expected selector format:
- `<source>|<path-or-entry-key>`

## 2. Upgrade a rules pack to latest (forced full refresh)
```bash
steergen rules-pack upgrade --selector "owner/repo|packs/security"
```

Expected behavior:
- Validates selector format and unique match.
- Takes snapshot of targeted cache.
- Purges targeted cache and refetches latest version.
- Updates config pin to resolved `(tag, commitSha)`.

## 3. Upgrade a rules pack to explicit tag
```bash
steergen rules-pack upgrade --selector "owner/repo|packs/security" --tag v1.4.2
```

Expected behavior:
- Purges targeted cache and fetches specified tag.
- Updates config pin tuple to that resolved version.

## 4. Upgrade a template pack with same behavior
```bash
steergen template-pack upgrade --selector "owner/repo|templates/default" --tag v2.0.0
```

Expected behavior is identical to rules-pack upgrade semantics.

## 5. Verify fail-closed rollback behavior
Simulate fetch failure (invalid tag or unavailable remote).

Expected behavior:
- Config pin remains unchanged.
- Previous cache snapshot is restored.
- Command exits non-zero with actionable diagnostics.
- If restore fails, diagnostics include both fetch and rollback failure.

## 6. Run tests
```bash
dotnet test
```

Focus areas:
- Selector validation and unique resolution
- No-tag full refresh path
- Explicit-tag path
- Pin tuple persistence
- Rollback success/failure paths
- Rules/template parity
