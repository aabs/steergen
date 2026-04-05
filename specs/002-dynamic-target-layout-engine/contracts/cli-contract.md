# CLI Contract

## Command Surface Updates
- `steergen run [--config <path>] [--global <dir>] [--project <dir>] [--target <target>...] [--quiet]`
  - Uses target layout engine for destination paths.
  - Ignores legacy shared output folder as routing destination authority.
- `steergen purge [--config <path>] [--target <target>...] [--dry-run] [--quiet]`
  - Deletes generated steering artifacts for selected targets based on configured purge roots and globs.
  - Works without a prior manifest.

## Exit Codes
- `0`: success
- `1`: validation error (invalid layout/routing/override, unresolved route, missing core anchor)
- `2`: configuration/argument error
- `3`: generation or purge execution error
- `5`: conflict error for config write operations (unchanged behavior)

## Behavioral Rules
- Route resolution per target MUST produce exactly one destination per rule.
- Explicit routes MUST be evaluated before non-explicit routes.
- Deterministic precedence MUST be stable for identical inputs/config and use a stable tuple equivalent to `(scopePriority, explicitPriority, conditionSpecificity, declarationOrder, routeId)`.
- Wildcard catch-all routes (for example `category: "*"`) MUST be supported and selected for rules not matched by more-specific routes.
- Unmatched rules MUST route to `other.*` in the same location as the scope's `core` anchor destination.
- `other.*` fallback is applied only when no route (including catch-all routes) matches.
- Missing `core` anchor for target/scope MUST fail before writes.
- `run` truncates each selected destination file once, then appends routed content deterministically.
- Files not selected by current write plan remain unchanged.
- `purge` MUST only remove files matching configured globs under configured roots.
- If a target has no purge globs, purge reports no-op for that target.
- `purge` MUST function when no prior generation manifest exists.
- `purge` MUST enforce root-bounded deletion eligibility and reject unsafe traversal/root-escape candidates.

## Diagnostic Contract
For each failed route resolution, diagnostics include:
- target ID
- rule ID
- candidate route IDs considered
- selected route source (`default`, `override`, `merged`) when successful
- failure reason (`no-match`, `ambiguous-match`, `invalid-path`, `missing-core-anchor`, `invalid-variable`)

For purge, diagnostics/report include:
- requested targets
- removed file paths
- skipped file paths with reasons
- no-op reasons when globs are absent
- non-success summary for unsafe/blocked targets
- deterministic per-target and per-file report ordering

## Safety Contract
- Override YAML is untrusted input and fully validated before write/delete operations.
- Rule text/content is inert data and cannot alter command execution semantics.
- Path traversal or root escape attempts are rejected.
