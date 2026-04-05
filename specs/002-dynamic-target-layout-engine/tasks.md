# Tasks: Dynamic Target Layout Engine

**Input**: Design documents from `/specs/002-dynamic-target-layout-engine/`  
**Prerequisites**: `plan.md` (required), `spec.md` (required), `research.md`, `data-model.md`, `contracts/`, `quickstart.md`

**Tests**: Tests are MANDATORY. Follow Red-Green-Refactor and author tests before implementation. Prefer property-based testing (CsCheck + xUnit) for invariants.

**Organization**: Tasks are grouped by user story so each story can be implemented and validated independently once foundational work is complete.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no incomplete-task dependency)
- **[Story]**: User story label (`[US1]`, `[US2]`, `[US3]`)
- Every task includes an exact file path

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add baseline scaffolding and assets used by all stories.

- [x] T001 [P] Create feature-level docs placeholder for framework-agnostic routing syntax in `docs/routing-syntax.md`
- [x] T002 [P] Add default layout YAML assets under target source folders, each including both default routing rules and layout configuration, in `src/Steergen.Core/Targets/Speckit/default-layout.yaml`, `src/Steergen.Core/Targets/Kiro/default-layout.yaml`, `src/Steergen.Core/Targets/Agents/Copilot/default-layout.yaml`, and `src/Steergen.Core/Targets/Agents/Kiro/default-layout.yaml`
- [x] T003 [P] Add realistic routing fixture corpus for catch-all and fallback behavior in `tests/Fixtures/RealisticGovernance/RoutingLayouts/`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core routing/layout models, config contracts, validation, and shared engine primitives required by all stories.

**CRITICAL**: No user story work starts until this phase is complete.

### Tests for Foundational (MANDATORY) ✅

- [x] T004 [P] Add property tests for deterministic single-destination route resolution in `tests/Steergen.Core.PropertyTests/Generation/RouteResolverProperties.cs`
- [x] T005 [P] Add property tests for wildcard catch-all precedence and fallback ordering in `tests/Steergen.Core.PropertyTests/Generation/CatchAllRoutingProperties.cs`
- [x] T006 [P] Add unit tests for deep-merge semantics (recursive map merge + list replacement) in `tests/Steergen.Core.UnitTests/Configuration/LayoutOverrideLoaderTests.cs`
- [x] T007 [P] Add unit tests for unsafe path rejection and root-bounded normalization in `tests/Steergen.Core.UnitTests/Generation/RoutePathSafetyTests.cs`
- [x] T038 [P] Add security property tests that prove instruction-like and prompt-injection-style rule content is treated as inert data during routing in `tests/Steergen.Core.PropertyTests/Security/InertContentRoutingProperties.cs`
- [x] T039 [P] Add CLI integration tests with malicious instruction-like fixture content to verify no behavioral influence on route selection or output in `tests/Steergen.Cli.IntegrationTests/Security/InertContentRoutingTests.cs`

### Implementation for Foundational

- [x] T008 [P] Implement routing/layout domain models in `src/Steergen.Core/Model/TargetLayoutDefinition.cs`, `src/Steergen.Core/Model/RouteRuleDefinition.cs`, `src/Steergen.Core/Model/FallbackRuleDefinition.cs`, and `src/Steergen.Core/Model/PurgePolicyDefinition.cs`
- [x] T009 [P] Implement default-layout and override loaders with deep-merge semantics in `src/Steergen.Core/Configuration/LayoutOverrideLoader.cs`
- [x] T010 [P] Implement routing schema validation (unknown fields, core anchor requirement, wildcard handling) in `src/Steergen.Core/Configuration/RoutingSchemaValidator.cs`
- [x] T011 [P] Implement deterministic route resolution and write-plan building in `src/Steergen.Core/Generation/RouteResolver.cs`, `src/Steergen.Core/Generation/RoutePlanner.cs`, and `src/Steergen.Core/Generation/WritePlanBuilder.cs`
- [x] T012 Integrate layout-driven route planning into generation pipeline in `src/Steergen.Core/Generation/GenerationPipeline.cs`
- [x] T013 Register per-target default-layout asset lookup in `src/Steergen.Core/Targets/TargetRegistry.cs`
- [x] T047 [P] Add unit tests that validate each target default YAML contains required routing-rule and layout sections in `tests/Steergen.Core.UnitTests/Targets/DefaultLayoutYamlContractTests.cs`
- [x] T014 Extend target config model and load/write behavior for `layoutOverridePath` in `src/Steergen.Core/Model/SteeringConfiguration.cs`, `src/Steergen.Core/Configuration/SteergenConfigLoader.cs`, and `src/Steergen.Core/Configuration/SteergenConfigWriter.cs`

**Checkpoint**: Core routing/layout engine is ready for independent story delivery.

---

## Phase 3: User Story 1 - Route Rules to Target-Specific Layouts (Priority: P1) 🎯 MVP

**Goal**: Route every rule deterministically to one target-native destination, including catch-all and `other.*` fallback behavior.

**Independent Test**: Configure routes (specific + catch-all) and verify deterministic single-destination outputs with no duplicate placement.

### Tests for User Story 1 (MANDATORY) ✅

- [ ] T015 [P] [US1] Add integration tests for target-native route mapping and deterministic ordering in `tests/Steergen.Cli.IntegrationTests/RunTargetLayoutRoutingTests.cs`
- [ ] T016 [P] [US1] Add integration tests for catch-all-before-fallback behavior in `tests/Steergen.Cli.IntegrationTests/RunCatchAllRoutingTests.cs`
- [ ] T017 [P] [US1] Add unit tests for core-anchor fallback-to-`other.*` behavior in `tests/Steergen.Core.UnitTests/Generation/FallbackRoutingTests.cs`
- [ ] T042 [P] [US1] Add compatibility regression tests proving existing targets preserve current output behavior when no layout override is configured in `tests/Steergen.Cli.IntegrationTests/RunCompatibilityBaselineTests.cs`
- [ ] T044 [P] [US1] Add integration tests that validate three layout conventions (workspace-local, user-home global, mixed-scope) without code changes in `tests/Steergen.Cli.IntegrationTests/RunLayoutConventionsAcceptanceTests.cs`

### Implementation for User Story 1

- [ ] T018 [P] [US1] Implement wildcard route matching (`*`) and specificity ordering in `src/Steergen.Core/Generation/RouteResolver.cs`
- [ ] T019 [P] [US1] Implement `other.*` fallback colocation with core anchor in `src/Steergen.Core/Generation/RoutePlanner.cs`
- [ ] T020 [US1] Update target generation path to consume write plans instead of ad hoc file naming in `src/Steergen.Core/Targets/Speckit/SpeckitTargetComponent.cs`, `src/Steergen.Core/Targets/Kiro/KiroTargetComponent.cs`, `src/Steergen.Core/Targets/Agents/CopilotAgentTargetComponent.cs`, and `src/Steergen.Core/Targets/Agents/KiroAgentTargetComponent.cs`
- [ ] T021 [US1] Update run-command diagnostics to include route source, selected destination for successful matches, and reason traces for routing failures in `src/Steergen.Cli/Commands/RunCommand.cs`

**Checkpoint**: US1 is fully functional and independently testable.

---

## Phase 4: User Story 2 - Override Standard Layout with User YAML (Priority: P2)

**Goal**: Allow per-target override YAML linkage and deterministic merge behavior over built-in defaults.

**Independent Test**: Link override for one target and verify merged behavior on that target while others stay on defaults.

### Tests for User Story 2 (MANDATORY) ✅

- [ ] T022 [P] [US2] Add integration tests for per-target override linkage and isolation in `tests/Steergen.Cli.IntegrationTests/RunLayoutOverrideTests.cs`
- [ ] T023 [P] [US2] Add unit tests for override validation diagnostics (unknown variable/field, invalid route) in `tests/Steergen.Core.UnitTests/Configuration/RoutingSchemaValidatorTests.cs`

### Implementation for User Story 2

- [ ] T024 [P] [US2] Wire `layoutOverridePath` into run-time target config resolution in `src/Steergen.Cli/Commands/RunCommand.cs`
- [ ] T025 [P] [US2] Implement merged-layout provenance tracking (`default`/`override`/`merged`) in `src/Steergen.Core/Generation/RouteResolutionResult.cs`
- [ ] T026 [US2] Add docs for override linkage and merge semantics in `specs/002-dynamic-target-layout-engine/quickstart.md` and `docs/routing-syntax.md`

**Checkpoint**: US2 is fully functional and independently testable.

---

## Phase 5: User Story 3 - Safe and Predictable File Lifecycle (Priority: P3)

**Goal**: Ensure deterministic truncation/append lifecycle and add purge command for stale file cleanup after routing changes.

**Independent Test**: Change routing config, run purge for selected target(s), verify stale generated files removed and non-generated files preserved.

### Tests for User Story 3 (MANDATORY) ✅

- [ ] T027 [P] [US3] Add property tests for purge eligibility invariants (glob-root bounded) in `tests/Steergen.Core.PropertyTests/Generation/PurgeEligibilityProperties.cs`
- [ ] T028 [P] [US3] Add unit tests for no-glob no-op and unsafe purge blocking in `tests/Steergen.Core.UnitTests/Generation/GeneratedFilePurgerTests.cs`
- [ ] T029 [P] [US3] Add CLI integration tests for purge command including no-manifest operation in `tests/Steergen.Cli.IntegrationTests/PurgeCommandTests.cs`

### Implementation for User Story 3

- [ ] T030 [P] [US3] Implement generated file purger service in `src/Steergen.Core/Generation/GeneratedFilePurger.cs`
- [ ] T031 [P] [US3] Implement `purge` CLI command with target scoping and dry-run/reporting in `src/Steergen.Cli/Commands/PurgeCommand.cs`
- [ ] T032 [US3] Register `purge` command in CLI composition in `src/Steergen.Cli/Composition/CommandFactory.cs`
- [ ] T033 [US3] Implement deterministic file truncation/write lifecycle reporting in `src/Steergen.Core/Generation/WritePlanExecutor.cs`

**Checkpoint**: US3 is fully functional and independently testable.

---

## Final Phase: Polish & Cross-Cutting Concerns

**Purpose**: Finish docs/contracts, validate end-to-end behavior, and lock release readiness.

- [ ] T034 [P] Publish framework-agnostic routing syntax reference including catch-all examples in `docs/routing-syntax.md`
- [ ] T035 [P] Align contract docs with final command behavior in `specs/002-dynamic-target-layout-engine/contracts/cli-contract.md` and `specs/002-dynamic-target-layout-engine/contracts/config-schema.md`
- [ ] T036 [P] Add release-readiness checklist items for default YAML discoverability and purge safety in `docs/release/release-checklist.md`
- [ ] T037 Run full validation sequence from quickstart in `specs/002-dynamic-target-layout-engine/quickstart.md`
- [ ] T040 [P] Add BenchmarkDotNet scenario for 1,000 rules across 4 targets and publish baseline results in `tests/Steergen.Benchmarks/LayoutRoutingBenchmarks.cs`
- [ ] T041 [P] Add CI performance gate/report wiring for NFR-003 budget tracking in `.github/workflows/ci.yml` and `tests/Steergen.Benchmarks/README.md`
- [ ] T043 [P] Add three acceptance fixture sets (workspace-local, user-home global, mixed-scope) for route/layout validation in `tests/Fixtures/RealisticGovernance/RoutingLayouts/`
- [ ] T045 [P] Add repeat-run determinism regression harness (multi-run artifact diff plus flake threshold) in `tests/Steergen.Cli.IntegrationTests/DeterministicRepeatRunRegressionTests.cs`
- [ ] T046 [P] Add CI step and reporting for repeat-run reliability target (99.9%) in `.github/workflows/ci.yml`

---

## Dependencies & Execution Order

### Phase Dependencies

- Setup (Phase 1): no dependencies.
- Foundational (Phase 2): depends on Setup completion and blocks user stories.
- User Stories (Phase 3+): depend on Foundational completion.
- Final Phase: depends on all desired user stories being complete.

### User Story Dependencies

- US1 (P1): starts after Foundational and delivers MVP routing behavior.
- US2 (P2): starts after Foundational; independent from US1 outputs but builds on shared routing engine.
- US3 (P3): starts after Foundational; depends on routing/purge infrastructure and is independently testable.

### Within Each Story

- Tests MUST be written and fail before implementation.
- Property-based tests SHOULD precede example-based tests for invariants.
- Model/config changes before resolver/planner wiring.
- Resolver/planner before command-layer diagnostics.

---

## Parallel Opportunities

- Phase 1 tasks marked [P] can run in parallel.
- Foundational tests (T004-T007) can run in parallel.
- Foundational model/loader/validator implementation (T008-T011) can run in parallel.
- Story-level tests marked [P] can run in parallel before implementation.
- US1 target component updates can be split by target file and parallelized.
- Purge service and purge command implementation (T030, T031) can run in parallel before command registration.
- Benchmark and repeat-run reliability tasks (T040, T041, T045, T046) can run in parallel with documentation polish after core implementation stabilizes.

---

## Implementation Strategy

### MVP First (US1 only)
1. Complete Setup and Foundational phases.
2. Complete US1 tests and implementation.
3. Validate deterministic routing, catch-all behavior, and fallback behavior.

### Incremental Delivery
1. Deliver US1 (routing MVP).
2. Deliver US2 (override linkage and merge behavior).
3. Deliver US3 (purge and lifecycle safety).
4. Polish docs/contracts and run release-readiness validation.

### Team Parallelization
- Developer A: Foundational routing core and property tests.
- Developer B: US1 integration tests and target component integration.
- Developer C: US2 override/linkage and schema validation.
- Developer D: US3 purge command and purge invariants.

---

## Notes

- Keep tasks additive to existing architecture; avoid large refactors unless required by invariants.
- Ensure every task references exact file paths and story traceability.
- Prefer stable deterministic ordering in all diagnostics and reports.
- Treat override YAML as untrusted input and fail closed on validation errors.
