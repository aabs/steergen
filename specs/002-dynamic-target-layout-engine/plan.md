# Implementation Plan: Dynamic Target Layout Engine

**Branch**: `002-dynamic-target-layout-engine` | **Date**: 2026-04-05 | **Spec**: `/Users/aabs/dev/aabs/prototypes/specgen/specs/002-dynamic-target-layout-engine/spec.md`
**Input**: Feature specification from `/Users/aabs/dev/aabs/prototypes/specgen/specs/002-dynamic-target-layout-engine/spec.md`

## Summary

Implement a deterministic target layout and routing engine that sends each generated rule to exactly one destination path per target without relying on a shared output folder. The feature adds per-target default YAML layout definitions under each target source folder, per-target user override YAML with recursive deep-merge semantics (map merge + list replacement), required `core` anchor enforcement per target/scope, wildcard catch-all route support (for example `category: "*"`) for default user-defined routing before fallback, unmatched fallback to `other.*` colocated with the `core` anchor, and a safe no-manifest `purge` command that deletes only configured glob matches under configured target roots while reporting removed/skipped/no-op outcomes.

## Technical Context

**Language/Version**: C# 14, .NET 10  
**Primary Dependencies**: `System.CommandLine`, `YamlDotNet`, `Scriban`, `CsCheck`, `xUnit`, `NSubstitute`, `BenchmarkDotNet`  
**Storage**: Local filesystem (spec/steering docs, built-in target YAML defaults, user override YAML, generated output files)  
**Testing**: xUnit + CsCheck (PBT-first), integration fixtures in `Steergen.Cli.IntegrationTests`, targeted mocking with NSubstitute, benchmark guardrails with BenchmarkDotNet  
**Target Platform**: Cross-platform CLI on macOS/Linux/Windows  
**Project Type**: CLI application + core domain library  
**Performance Goals**: Route evaluation and write planning for 1,000 rules across 4 targets in <= 3 seconds excluding external IO contention (NFR-003)  
**Constraints**: Fail-closed validation before writes/deletes, deterministic route selection and file ordering, path safety/root-bounded normalization, no runtime plugin loading, no-manifest purge support, framework-agnostic routing syntax documentation in `docs/`  
**Scale/Scope**: Built-in targets plus additive target growth; mixed global/project scope routing; repeated generation/purge workflows in CI and local developer runs

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- PASS: Runtime/language choice is idiomatic .NET 10 and C# 14.
- PASS: Deterministic single-destination routing and deterministic write/purge behavior are explicit.
- PASS: Red-Green-Refactor sequencing is planned with tests authored first.
- PASS: PBT-first coverage (CsCheck + xUnit) is planned for routing invariants, merge semantics, catch-all behavior, and deterministic ordering.
- PASS: Test plan includes plausible real-world steering corpora plus targeted edge/failure fixtures.
- PASS: Security plan includes untrusted override validation, traversal rejection, and inert-content handling for prompt-injection-like text.
- PASS: Performance budget and benchmarking approach are defined against NFR-003.
- PASS: CLI UX/error semantics for `run` and `purge` are explicit and script-friendly.
- PASS: Documentation deliverables include quickstart updates plus framework-agnostic routing syntax documentation under `docs/`.
- PASS: Release workflow impact remains SemVer-aligned with preview and stable tag conventions.

## Project Structure

### Documentation (this feature)

```text
/Users/aabs/dev/aabs/prototypes/specgen/specs/002-dynamic-target-layout-engine/
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
│   ├── Diagnostics/
│   └── Program.cs
├── Steergen.Core/
│   ├── Configuration/
│   ├── Generation/
│   ├── Merge/
│   ├── Model/
│   ├── Parsing/
│   ├── Targets/
│   ├── Updates/
│   └── Validation/
└── Steergen.Templates/
    └── Scriban/

tests/
├── Fixtures/
├── Steergen.Cli.IntegrationTests/
├── Steergen.Core.PropertyTests/
├── Steergen.Core.UnitTests/
└── Steergen.Benchmarks/
```

**Structure Decision**: Keep the existing CLI/Core/Templates separation and add this feature through additive routing/layout components plus target-scoped default YAML assets under target source folders, avoiding refactors of existing target implementations.

## Architecture and Design Decisions

1. Deterministic single-destination routing
- Build a normalized candidate set per rule from explicit routes first, then non-explicit routes.
- Apply stable precedence tuple: `(scopePriority, explicitPriority, conditionSpecificity, declarationOrder, routeId)`.
- Require exactly one selected route; multiple matches fail closed.

2. Catch-all wildcard routing
- Support wildcard match expressions in route conditions (for example `category: "*"`).
- Catch-all routes are ordinary routes and participate in deterministic precedence.
- Specific matches must outrank catch-all matches.

3. Fallback to `other.*` at core-anchor location
- Every target/scope must define at least one `core` anchor route.
- Unmatched rules are redirected to `other.*` in the same resolved directory/extension family as that core anchor.
- Fallback applies only when no route (including catch-all routes) matches.
- Missing core anchor is a validation error before any write operations.

4. Deep-merge override semantics
- Load built-in default YAML for target.
- If configured, load user override YAML for that target only.
- Apply deterministic deep merge: map/object fields merge recursively, scalar/array values in override replace defaults.
- Preserve unknown-field rejection and actionable diagnostics with source location.

5. Per-target default YAML under target source folders
- Ship versioned `default-layout.yaml` files in each target folder to preserve transparency and discoverability.
- `TargetRegistry` maps target IDs to default YAML paths.
- Documentation links to these files and their override usage.

6. Purge command with configured globs and no-manifest operation
- Add `steergen purge` command with `--target` filtering and config autodiscovery.
- Purge matches only target-configured glob patterns rooted at configured target roots.
- If target has no purge globs, report deterministic no-op.
- Purge does not require prior generation manifest; pattern matching alone determines eligibility.

## Risks and Mitigations

- Risk: Overlapping route conditions cause ambiguous selections.
- Mitigation: explicit precedence tuple, ambiguity diagnostics include candidate route IDs and condition traces.

- Risk: Catch-all route unintentionally swallows specific routes.
- Mitigation: enforce specificity ordering and test precedence between specific and wildcard conditions.

- Risk: Path template variables introduce traversal or unsafe absolute paths.
- Mitigation: normalize/canonicalize against allowed roots, reject escapes and invalid path segments pre-write.

- Risk: Purge glob misconfiguration could over-delete.
- Mitigation: require root-bounded glob evaluation, emit remove/skip report, non-success exit when target safety checks fail.

## Test Strategy

1. Property tests (first-class)
- Single-destination invariant: every routable rule resolves to exactly one path.
- Determinism invariant: permuted input order yields identical route selections and write plans.
- Catch-all invariant: unmatched-by-specific rules route to catch-all destination when catch-all is configured.
- Purge invariant: only files matching configured globs within target roots are eligible.

2. Unit tests
- Override deep merge precedence and unknown-field validation.
- Wildcard matching and specificity ordering between specific vs catch-all routes.
- Core-anchor enforcement and `other.*` fallback location equivalence.
- Path sanitization and unsafe path rejection.

3. Integration and golden tests
- End-to-end `run` with realistic mixed global/project corpora.
- End-to-end catch-all route behavior with category-based dynamic destination expansion.
- End-to-end `purge` across selected targets, including no-glob no-op and no-manifest cleanup.
- Regression fixtures for existing targets with no overrides (compatibility baseline).

4. Performance and robustness
- Benchmark route planning and purge scanning against NFR-003 envelope.
- Negative suites for malformed YAML, ambiguous routing, missing core anchor, wildcard misuse, and path traversal payloads.

## Phased Implementation Plan

### Phase 0: Research and Constraints Finalization
- Finalize precedence algorithm and ambiguity diagnostics structure.
- Finalize catch-all wildcard semantics and specificity rules.
- Finalize deep-merge contract and override trust boundary rules.
- Finalize purge glob evaluation model (root-bounded matching and report schema).

### Phase 1: Design and Contracts
- Define data model types for layout/routing/fallback/purge policies.
- Define YAML contract updates for target config, wildcard routes, and override linkage.
- Define CLI contract updates for `run` diagnostics and new `purge` command.
- Publish quickstart flow for authoring override YAML with catch-all route and running purge safely.

### Phase 2: Implementation Slices
- Slice A (P1): Route planner, resolver, deterministic write plan, core-anchor + `other.*` fallback.
- Slice B (P1b): Wildcard catch-all support with precedence over fallback and below specific matches.
- Slice C (P2): Per-target default YAML loading and per-target override deep merge.
- Slice D (P3): File lifecycle rules and `purge` command with glob/no-manifest behavior.
- Slice E: Documentation and regression hardening (routing syntax reference, target default links).

### Phase 3: Validation and Release Readiness
- Run full test suite (property/unit/integration) and targeted benchmarks.
- Validate docs against actual CLI behavior.
- Confirm SemVer minor release note entries and migration guidance.

## Post-Design Constitution Check

- PASS: Design remains .NET 10/C# 14 and preserves parser/model/validation/merge/generation/target separation.
- PASS: Determinism is reinforced through explicit precedence tuple, single-destination enforcement, catch-all specificity handling, required core anchors, and stable ordering.
- PASS: Test-first and PBT-first strategy is reflected in research, data model, contracts, and quickstart test guidance.
- PASS: Security/fail-closed gates are encoded for validation, path safety, and purge constraints before side effects.
- PASS: Performance validation path is defined with benchmark and integration checks at declared scale.
- PASS: CLI contracts include clear diagnostics, no-manifest purge behavior, and no-op semantics when globs are absent.
- PASS: Documentation plan includes framework-agnostic routing syntax reference under `docs/` and links to per-target defaults.
- PASS: Extensibility remains additive with no runtime plugin loading and no mandatory refactors of existing target implementations.

## Complexity Tracking

No constitution violations requiring justification.
