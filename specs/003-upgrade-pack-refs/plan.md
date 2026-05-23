# Implementation Plan: Upgrade External Pack References

**Branch**: `003-upgrade-pack-refs` | **Date**: 2026-05-23 | **Spec**: `/Users/aabs/dev/aabs/active/steergen/specs/003-upgrade-pack-refs/spec.md`
**Input**: Feature specification from `/Users/aabs/dev/aabs/active/steergen/specs/003-upgrade-pack-refs/spec.md`

## Summary

Add incremental `upgrade` subcommands for external rules packs and template packs using the existing pull/cache/config update pipeline, without introducing new architecture or new techniques. The workflow enforces canonical composite target selection, full cache refresh by purge-and-refetch when tag is omitted, deterministic pinning to `(tag, commitSha)`, and fail-closed rollback to the previous cache snapshot when refetch fails.

## Technical Context

**Language/Version**: C# 14, .NET 10  
**Primary Dependencies**: `System.CommandLine`, `YamlDotNet`, `Scriban`, `CsCheck`, `xUnit`, `NSubstitute`, `BenchmarkDotNet`  
**Storage**: Local filesystem (`steergen.config.yaml`, local pack cache, fetched pack archives/expanded files)  
**Testing**: xUnit + CsCheck property tests, CLI integration tests, focused unit tests with NSubstitute where mocking is needed  
**Target Platform**: Cross-platform CLI (macOS/Linux/Windows)  
**Project Type**: CLI + core domain library  
**Performance Goals**: For pack payloads <= 100 MB, 95% of upgrade runs complete <= 60s under healthy network conditions  
**Constraints**: Incremental command only; preserve existing pack acquisition architecture; deterministic behavior; fail-closed config/cache updates; no plugin model changes  
**Scale/Scope**: External rules/template pack references in single/multi-pack configurations, including ambiguous selectors and rollback failure paths

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- PASS: Runtime/language remains idiomatic .NET 10 and C# 14.
- PASS: Deterministic behavior is explicit via canonical selector resolution and `(tag, commitSha)` pin tuple.
- PASS: Red-Green-Refactor approach is defined with tests authored before implementation slices.
- PASS: Property-based tests are defined for invariants (pin determinism, unchanged config on failures, rollback behavior).
- PASS: Tests include realistic fixture configurations and targeted edge/failure fixtures.
- PASS: Security/misuse analysis includes malformed selector/tag payload handling and inert remote metadata.
- PASS: Performance budget is explicitly defined and validated with integration timing checks (optionally benchmarked).
- PASS: CLI UX and diagnostics are explicit for selector format, version source, and rollback failures.
- PASS: Documentation updates are planned for command usage and selector format.
- PASS: Release impact is SemVer-compatible (additive command surface, no breaking architecture changes).

## Project Structure

### Documentation (this feature)

```text
/Users/aabs/dev/aabs/active/steergen/specs/003-upgrade-pack-refs/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── cli-contract.md
│   └── config-schema.md
└── tasks.md
```

### Source Code (repository root)

```text
src/
├── Steergen.Cli/
│   ├── Commands/
│   ├── Composition/
│   └── Program.cs
├── Steergen.Core/
│   ├── Configuration/
│   ├── Packs/
│   ├── Updates/
│   ├── Validation/
│   └── Model/
└── Steergen.Templates/

tests/
├── Fixtures/
├── Steergen.Cli.IntegrationTests/
├── Steergen.Core.PropertyTests/
└── Steergen.Core.UnitTests/
```

**Structure Decision**: Use the current CLI/Core split and extend existing pack update/pull pathways additively. No new project layers, plugin systems, or architectural rewrites are introduced.

## Architecture and Design Decisions

1. Incremental command extension only
- Add `upgrade` under existing rules/template pack command groups.
- Reuse existing command registration and execution pipeline patterns.

2. Canonical selector contract
- Upgrade requires explicit canonical composite selector `(source + path|entry-key)`.
- Selector validation and unique resolution happen before side effects.

3. Full refresh semantics when tag omitted
- No-tag upgrade always purges targeted cache then refetches latest resolution.
- This applies even when staleness cannot be determined.

4. Explicit-tag upgrade semantics
- Tag-provided upgrade purges targeted cache and refetches that exact tag.
- Final pinned tuple remains deterministic and auditable.

5. Pin format tuple
- On success, update `steergen.config.yaml` with resolved `(tag, commitSha)` tuple.
- Keep unrelated references untouched.

6. Fail-closed rollback
- If fetch fails after purge, restore previous cache snapshot and keep config unchanged.
- If rollback fails, report both failures and exit non-zero.

## Risks and Mitigations

- Risk: Selector ambiguity across multiple pack references.
- Mitigation: Canonical selector requirement, unique-match enforcement, preflight validation errors.

- Risk: Partial state when purge succeeds but fetch fails.
- Mitigation: Snapshot-and-rollback policy with dual-failure diagnostics.

- Risk: Drift from mutable tags.
- Mitigation: Persist immutable commit SHA with tag in final pin tuple.

- Risk: Regressions across rules/template command parity.
- Mitigation: Shared command behavior contract and parity integration tests.

## Test Strategy

1. Property tests (PBT-first)
- Determinism: repeated explicit-tag upgrades converge to identical `(tag, commitSha)` tuple.
- Safety: failed upgrade leaves config unchanged.
- Rollback: fetch-failure-after-purge restores prior cache snapshot state.

2. Unit tests
- Canonical selector parser/validator acceptance and rejection paths.
- Pin tuple serialization/deserialization in config model.
- Failure classification and diagnostic message shaping.

3. Integration tests
- `rules-pack upgrade` no-tag full refresh path.
- `rules-pack upgrade` explicit-tag path.
- Template-pack parity for both paths.
- Ambiguous/missing/invalid selector failure paths.
- Rollback success path and rollback-failure path.

4. Performance and robustness
- Timed integration scenario for <=100MB packs under healthy network/stubbed fetch.
- Negative suites for malformed tags/selectors and malformed remote metadata.

## Phased Implementation Plan

### Phase 0: Research and Constraints
- Confirm canonical selector surface aligns with existing config reference schema.
- Confirm rollback snapshot boundary and error taxonomy in current update flow.
- Confirm pin tuple persistence format with no migration breakage.

### Phase 1: Design and Contracts
- Define updated command contract for rules/template `upgrade`.
- Define config contract for persisted `(tag, commitSha)` pin tuple.
- Define data model for upgrade request/result and snapshot rollback outcome.
- Publish quickstart usage and failure-handling operator guidance.

### Phase 2: Implementation Slices
- Slice A: CLI command wiring and selector validation (no side effects yet).
- Slice B: Full-refresh engine (no-tag + explicit-tag) with shared execution path.
- Slice C: Config pin tuple update logic and unchanged-on-failure guarantees.
- Slice D: Rollback support and dual-failure diagnostics.
- Slice E: Test hardening and docs updates.

### Phase 3: Validation and Release Readiness
- Run full test matrix (property/unit/integration).
- Validate command help + docs examples against real CLI behavior.
- Confirm SemVer minor release note framing (additive command surface).

## Post-Design Constitution Check

- PASS: Design remains within existing .NET 10/C# 14 codebase and architecture.
- PASS: Determinism is strengthened via canonical selector and pin tuple rules.
- PASS: PBT-first and fail-closed behavior are encoded as primary verification targets.
- PASS: Security and misuse handling cover malformed input and untrusted remote metadata.
- PASS: Performance objective and validation path remain explicit and measurable.
- PASS: CLI UX/documentation impacts are defined and limited to incremental command additions.
- PASS: Extensibility model remains stable (no runtime plugin loading, no new architecture).

## Complexity Tracking

No constitution violations requiring justification.
