# Phase 0 Research

## Decision 1: Deterministic single-destination routing precedence
- Decision: Route resolution uses a stable precedence tuple `(scopePriority, explicitPriority, conditionSpecificity, declarationOrder, routeId)` and must end in exactly one selected destination.
- Rationale: FR-005 and FR-006 require deterministic, single-destination outcomes and reproducible behavior under repeated runs.
- Alternatives considered:
  - First-match wins by declaration order only. Rejected because it is too opaque and brittle with mixed explicit/fallback rules.
  - Weighted scoring with floating values. Rejected because it is harder to reason about and explain in diagnostics.

## Decision 2: `other.*` fallback anchored to `core` route location
- Decision: Unmatched rules are routed to `other.*` in the same directory and extension family as the target/scope `core` anchor route.
- Rationale: FR-016 and FR-021 require fallback behavior tied to a concrete, required anchor and must fail closed when anchor is missing.
- Alternatives considered:
  - Global fixed `other.md` path. Rejected because it violates per-target layout requirements.
  - Silent drop of unmatched rules. Rejected because it violates fail-closed correctness requirements.

## Decision 3: Deep-merge override semantics (override precedence)
- Decision: Built-in per-target YAML defaults are loaded first, then per-target user override YAML is deep-merged with recursive map merging and scalar/list replacement by override values.
- Rationale: FR-007 and FR-008 require safe customization while preserving sane defaults and deterministic behavior.
- Alternatives considered:
  - Shallow top-level merge. Rejected because nested route/policy nodes would be difficult to customize safely.
  - Complete replacement model. Rejected because users would have to duplicate full defaults and would lose compatibility resilience.

## Decision 4: Default layout YAML placement under target source folders
- Decision: Each built-in target ships `default-layout.yaml` directly under its source folder, and target registration metadata points to that file.
- Rationale: FR-017, NFR-006, and NFR-007 require discoverable, versioned, human-readable defaults.
- Alternatives considered:
  - Central monolithic defaults file. Rejected because additive target growth becomes harder and coupling increases.
  - Embedded-only defaults with no source file. Rejected due to reduced discoverability and documentation transparency.

## Decision 5: Purge command uses configured globs and does not require manifest
- Decision: Add `steergen purge` that evaluates target-scoped globs rooted at configured target roots; no manifest is required for deletion eligibility.
- Rationale: FR-018 through FR-024 require deterministic stale artifact cleanup based on configuration alone, with no-manifest support.
- Alternatives considered:
  - Manifest-only purge. Rejected because FR-024 explicitly requires no-manifest operation.
  - Recursive target root deletion. Rejected as unsafe and too coarse-grained.

## Decision 5a: Purge safety and reporting are first-class command semantics
- Decision: Purge performs root-bounded candidate discovery and records per-target removed/skipped/no-op results; targets that cannot be purged safely are reported as non-success while still preserving deterministic report ordering.
- Rationale: FR-020, FR-022, FR-023, and NFR-008 require auditable, constrained deletion semantics with explicit operator feedback.
- Alternatives considered:
  - Best-effort silent skip mode. Rejected because operators cannot verify cleanup safety or completeness.
  - Hard-fail all targets on one unsafe target. Rejected because it reduces operational utility for multi-target cleanup runs.

## Decision 6: Validation and fail-closed write barrier
- Decision: Validate merged layout definitions, route expressions, variable references, core anchors, and purge policies before any writes or deletions execute.
- Rationale: FR-009, NFR-001, and NFR-004 require preflight validation and fail-closed semantics.
- Alternatives considered:
  - Best-effort write with partial success. Rejected due to correctness and operational safety risks.

## Decision 7: Diagnostics contract with route provenance
- Decision: Route diagnostics report candidate routes, selected route source (default vs override), resolved destination, and failure reasons.
- Rationale: FR-013 and NFR-005 require actionable troubleshooting without source inspection.
- Alternatives considered:
  - Minimal error-only diagnostics. Rejected because ambiguous route behavior would be difficult to debug.

## Decision 8: Framework-agnostic routing syntax documentation under docs
- Decision: Publish a dedicated routing syntax reference under `docs/` that defines grammar, variables, precedence, anchor/fallback rules, and worked examples without tying semantics to a single target framework.
- Rationale: FR-015 and NFR-006 require docs that are reusable across all supported targets and understandable without implementation internals.
- Alternatives considered:
  - Target-specific docs only. Rejected because routing semantics would diverge in presentation and become harder to maintain.
  - Inline-only README section. Rejected because grammar/reference content needs a stable standalone doc surface.
