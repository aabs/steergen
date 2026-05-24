# Phase 1 Data Model

## PackReference
- Purpose: Config entry identifying an external rules or template pack.
- Fields:
  - `kind` (enum: `rules`, `template`)
  - `source` (string)
  - `pathOrEntryKey` (string, optional)
  - `pin` (PinTuple)
- Validation:
  - `source` must be non-empty and syntactically valid.
  - Canonical selector built from `source + pathOrEntryKey` must be unique per config.

## CanonicalSelector
- Purpose: Unambiguous command input to target one pack reference.
- Fields:
  - `source` (string, required)
  - `pathOrEntryKey` (string, required)
  - `raw` (string, required)
- Validation:
  - Must parse into required parts.
  - Must resolve to exactly one `PackReference`.

## UpgradeRequest
- Purpose: Operator intent for one upgrade execution.
- Fields:
  - `kind` (enum: `rules`, `template`)
  - `selector` (CanonicalSelector)
  - `requestedTag` (string, optional)
  - `mode` (enum: `latest-refresh`, `explicit-tag`)
- Validation:
  - `mode=latest-refresh` when `requestedTag` absent.
  - `mode=explicit-tag` when `requestedTag` present and valid.

## PinTuple
- Purpose: Persisted immutable pack version reference.
- Fields:
  - `tag` (string)
  - `commitSha` (string)
- Validation:
  - `tag` must be non-empty.
  - `commitSha` must match expected commit hash shape.

## CacheSnapshot
- Purpose: Recovery point for fail-closed upgrade behavior.
- Fields:
  - `selector` (CanonicalSelector)
  - `snapshotPath` (string)
  - `capturedAtUtc` (datetime)
- Validation:
  - Snapshot must represent only targeted cache scope.

## UpgradeExecutionResult
- Purpose: Deterministic outcome record for one upgrade run.
- Fields:
  - `selector` (CanonicalSelector)
  - `mode` (enum)
  - `resolvedVersion` (PinTuple, optional on failure)
  - `configUpdated` (bool)
  - `cacheReplaced` (bool)
  - `rollbackPerformed` (bool)
  - `success` (bool)
  - `diagnostics` (list<string>)
- Validation:
  - On failure, `configUpdated=false`.
  - If fetch fails after purge and rollback succeeds, `rollbackPerformed=true` and `success=false`.
  - If rollback fails, diagnostics must include both fetch and rollback failures.
