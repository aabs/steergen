# Phase 0 Research

## Decision 1: Keep Existing Architecture and Technologies
- Decision: Implement `upgrade` as an additive command within existing CLI/Core pack update pathways, using current technology stack.
- Rationale: The feature is explicitly incremental and does not require new architectural patterns.
- Alternatives considered:
  - Introduce a new upgrade orchestration layer. Rejected as unnecessary complexity.
  - Add plugin-style provider abstraction for pack updates. Rejected due to constitution constraints and scope mismatch.

## Decision 2: Canonical Selector Resolution
- Decision: Require a canonical composite selector (`source + path|entry-key`) and resolve exactly one target reference before any side effects.
- Rationale: Prevents accidental upgrades in multi-pack configurations and ensures deterministic command behavior.
- Alternatives considered:
  - Source-only selector. Rejected because duplicates are common and ambiguous.
  - Positional index selector. Rejected as brittle and order-dependent.

## Decision 3: Full Cache Refresh for No-Tag Upgrades
- Decision: When no explicit tag is provided, always purge and refetch the targeted cache, even if staleness is unknown.
- Rationale: Aligns with operator intent for explicit refresh and avoids stale-cache ambiguity.
- Alternatives considered:
  - Refresh only when staleness can be proven. Rejected due to non-deterministic freshness signals.
  - Conditional merge update in place. Rejected to preserve disposable-cache semantics.

## Decision 4: Pin Tuple Format
- Decision: Persist upgraded references as resolved `(tag, commitSha)` tuple.
- Rationale: Keeps human-readable version intent while anchoring immutably for supply-chain integrity.
- Alternatives considered:
  - Tag-only pins. Rejected due to mutable tag drift risk.
  - SHA-only pins. Rejected due to reduced operator ergonomics.

## Decision 5: Rollback on Fetch Failure After Purge
- Decision: Snapshot targeted cache before purge; if refetch fails, restore snapshot and keep config unchanged.
- Rationale: Enforces fail-closed semantics and minimizes operator disruption.
- Alternatives considered:
  - Leave cache empty and fail. Rejected as operationally disruptive.
  - Keep partial refetch contents. Rejected for unsafe, non-deterministic state.

## Decision 6: Rules/Template Command Parity
- Decision: Apply identical upgrade behavior contracts for rules packs and template packs.
- Rationale: Reduces cognitive load, improves scriptability, and simplifies test matrices.
- Alternatives considered:
  - Divergent behavior by pack type. Rejected due to higher long-term maintenance and UX inconsistency.
