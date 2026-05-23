# CLI Contract: Pack Upgrade Commands

## Commands

### Rules Pack Upgrade
```text
steergen rules-pack upgrade --selector <source|path-or-entry-key> [--tag <tag>]
```

### Template Pack Upgrade
```text
steergen template-pack upgrade --selector <source|path-or-entry-key> [--tag <tag>]
```

## Inputs
- `--selector` (required): canonical composite selector that uniquely identifies exactly one configured pack reference.
- `--tag` (optional): explicit tag to fetch. If omitted, command performs latest refresh by purging and refetching targeted cache.

Selector escaping rules:
- Use `\\|` for a literal `|` in either selector component.
- Use `\\\\` for a literal backslash.
- Any other escape sequence is invalid and must fail before side effects.

## Behavioral Contract
1. Selector validation and unique-match resolution MUST happen before purge/fetch.
2. Command MUST snapshot targeted cache before purge.
3. Command MUST purge targeted cache and refetch (latest when no `--tag`, explicit when provided).
4. On success, command MUST update the targeted config reference pin to `(tag, commitSha)`.
5. On fetch failure after purge, command MUST restore previous snapshot and keep config unchanged.
6. On rollback failure, command MUST return non-zero and report both fetch and rollback failures.

## Exit Semantics
- `0`: Upgrade completed successfully and targeted reference updated.
- `6`: Selector validation/resolution failure.
- `7`: Fetch/config update execution failure.
- `8`: Rollback failure after fetch failure.

## Diagnostics Requirements
- Must state command mode: `latest-refresh` or `explicit-tag`.
- Must report targeted selector.
- On success, must report final `(tag, commitSha)`.
- On failure, must report actionable reason and whether rollback succeeded.
- On rollback failure, diagnostics must include both fetch and rollback error codes/messages.
