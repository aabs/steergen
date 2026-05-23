# Tasks: Upgrade External Pack References

**Input**: Design documents from `/specs/003-upgrade-pack-refs/`  
**Prerequisites**: `plan.md` (required), `spec.md` (required), `research.md`, `data-model.md`, `contracts/`, `quickstart.md`

**Tests**: Tests are MANDATORY. Follow Red-Green-Refactor and author tests before implementation. Prefer property-based testing (CsCheck + xUnit) for invariants.

**Organization**: Tasks are grouped by user story so each story can be implemented and validated independently once foundational work is complete.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no incomplete-task dependency)
- **[Story]**: User story label (`[US1]`, `[US2]`, `[US3]`)
- Every task includes an exact file path

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add shared fixtures and documentation scaffolding used by all upgrade stories.

- [ ] T001 [P] Add realistic external pack upgrade fixture corpus in `tests/Fixtures/RealisticGovernance/PackUpgrades/`
- [ ] T002 [P] Add rollback/failure fixture inputs for fetch and restore scenarios in `tests/Fixtures/RealisticGovernance/PackUpgrades/rollback/`
- [ ] T003 [P] Add feature progress tracker stub in `specs/003-upgrade-pack-refs/progress.md`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared selector, config, upgrade orchestration, and rollback primitives required before story-specific command work.

**CRITICAL**: No user story work starts until this phase is complete.

### Tests for Foundational (MANDATORY) ✅

- [ ] T004 [P] Add unit tests for canonical selector parsing/validation in `tests/Steergen.Core.UnitTests/Configuration/PackSelectorResolverTests.cs`
- [ ] T005 [P] Add unit tests for config round-trip of pin tuple fields in `tests/Steergen.Core.UnitTests/Configuration/SteergenConfigUpgradePinRoundTripTests.cs`
- [ ] T006 [P] Add property tests proving failed upgrades never mutate targeted config refs in `tests/Steergen.Core.PropertyTests/Updates/UpgradeFailureConfigInvariantsProperties.cs`
- [ ] T007 [P] Add unit tests for snapshot restore behavior after fetch failure in `tests/Steergen.Core.UnitTests/Updates/ExternalPackUpgradeServiceRollbackTests.cs`

### Implementation for Foundational

- [ ] T008 [P] Add pin tuple and selector-focused model updates in `src/Steergen.Core/Model/SteeringConfiguration.cs`
- [ ] T009 [P] Implement canonical selector resolver service in `src/Steergen.Core/Configuration/PackSelectorResolver.cs`
- [ ] T010 [P] Extend config load/write mapping for tuple pin fields in `src/Steergen.Core/Configuration/SteergenConfigLoader.cs` and `src/Steergen.Core/Configuration/SteergenConfigWriter.cs`
- [ ] T011 [P] Implement shared external pack upgrade orchestrator in `src/Steergen.Core/Updates/ExternalPackUpgradeService.cs`
- [ ] T012 [P] Implement targeted cache snapshot/restore helper in `src/Steergen.Core/Updates/PackCacheSnapshotStore.cs`
- [ ] T013 Integrate foundational upgrade diagnostics contracts in `src/Steergen.Core/Updates/ExternalPackUpgradeService.cs` and `src/Steergen.Cli/Composition/ExitCodeMapper.cs`

**Checkpoint**: Core selector/config/upgrade primitives are ready for independent story delivery.

---

## Phase 3: User Story 1 - Upgrade a Rules Pack to Current Version (Priority: P1) 🎯 MVP

**Goal**: Provide `steergen rules-pack upgrade` with deterministic selector resolution, no-tag full refresh, explicit-tag refresh, and pin tuple updates.

**Independent Test**: Run rules-pack upgrade using valid selector in latest and explicit modes, verify cache refresh and targeted pin tuple update only.

### Tests for User Story 1 (MANDATORY) ✅

- [ ] T014 [US1] Add integration tests for `rules-pack upgrade` latest and explicit tag flows in `tests/Steergen.Cli.IntegrationTests/RulesPackUpgradeCommandTests.cs`
- [ ] T015 [US1] Add integration tests for missing/ambiguous/invalid selector failures in `tests/Steergen.Cli.IntegrationTests/RulesPackUpgradeCommandTests.cs`
- [ ] T016 [P] [US1] Add unit tests for targeted rules-pack entry resolution and update semantics in `tests/Steergen.Core.UnitTests/Configuration/RulesPackRegistrationServiceTests.cs`
- [ ] T043 [US1] Add integration assertions that unrelated rules-pack and template-pack references remain unchanged after targeted upgrade in `tests/Steergen.Cli.IntegrationTests/RulesPackUpgradeCommandTests.cs` and `tests/Steergen.Cli.IntegrationTests/TemplatePackUpgradeCommandTests.cs`

### Implementation for User Story 1

- [ ] T017 [P] [US1] Implement `rules-pack upgrade` command in `src/Steergen.Cli/Commands/RulesPackUpgradeCommand.cs`
- [ ] T018 [US1] Register upgrade subcommand under rules-pack in `src/Steergen.Cli/Commands/RulesPackCommand.cs`
- [ ] T019 [US1] Wire rules-pack upgrade execution path to shared upgrade service in `src/Steergen.Cli/Commands/RulesPackUpgradeCommand.cs` and `src/Steergen.Core/Updates/ExternalPackUpgradeService.cs`
- [ ] T020 [US1] Implement rules-pack targeted config pin tuple mutation logic in `src/Steergen.Core/Configuration/RulesPackRegistrationService.cs`
- [ ] T021 [US1] Add command factory regression coverage for new subcommand wiring in `tests/Steergen.Cli.IntegrationTests/CommandFactoryRegressionTests.cs`

**Checkpoint**: US1 is fully functional and independently testable.

---

## Phase 4: User Story 2 - Upgrade a Template Pack with the Same Workflow (Priority: P2)

**Goal**: Provide `steergen template-pack upgrade` with behavior parity to rules-pack upgrade.

**Independent Test**: Run template-pack upgrade in latest and explicit modes and verify equivalent selector, refresh, and pin tuple behavior.

### Tests for User Story 2 (MANDATORY) ✅

- [ ] T022 [US2] Add integration tests for `template-pack upgrade` latest and explicit tag flows in `tests/Steergen.Cli.IntegrationTests/TemplatePackUpgradeCommandTests.cs`
- [ ] T023 [US2] Add integration tests for selector validation and fail-closed behavior in `tests/Steergen.Cli.IntegrationTests/TemplatePackUpgradeCommandTests.cs`
- [ ] T024 [P] [US2] Extend template pack config mutation tests for tuple pin persistence in `tests/Steergen.Core.UnitTests/Configuration/TemplatePackServiceTests.cs`

### Implementation for User Story 2

- [ ] T025 [P] [US2] Implement `template-pack upgrade` command in `src/Steergen.Cli/Commands/TemplatePackUpgradeCommand.cs`
- [ ] T026 [US2] Register template-pack upgrade subcommand in `src/Steergen.Cli/Commands/TemplatePackCommand.cs`
- [ ] T027 [US2] Wire template-pack upgrade execution path to shared upgrade service in `src/Steergen.Cli/Commands/TemplatePackUpgradeCommand.cs` and `src/Steergen.Core/Updates/ExternalPackUpgradeService.cs`
- [ ] T028 [US2] Implement template-pack targeted config pin tuple mutation logic in `src/Steergen.Core/Configuration/TemplatePackService.cs`

**Checkpoint**: US2 is fully functional and independently testable.

---

## Phase 5: User Story 3 - Safe, Predictable Upgrade Operations (Priority: P3)

**Goal**: Enforce fail-closed behavior under malformed input and runtime failures, including rollback and dual-failure diagnostics.

**Independent Test**: Trigger malformed selector/tag and fetch-failure cases; verify unchanged config, restored cache, deterministic non-zero exits, and complete diagnostics.

### Tests for User Story 3 (MANDATORY) ✅

- [ ] T029 [P] [US3] Add property tests for deterministic explicit-tag convergence to identical pin tuple in `tests/Steergen.Core.PropertyTests/Updates/UpgradeDeterminismProperties.cs`
- [ ] T030 [P] [US3] Add integration tests for fetch-failure rollback and rollback-failure dual diagnostics in `tests/Steergen.Cli.IntegrationTests/PackUpgradeRollbackTests.cs`
- [ ] T031 [P] [US3] Add security tests for malformed selector/tag and inert remote metadata handling in `tests/Steergen.Core.UnitTests/Security/MaliciousInputValidationTests.cs`

### Implementation for User Story 3

- [ ] T032 [P] [US3] Enforce preflight selector format rejection before side effects in `src/Steergen.Cli/Commands/RulesPackUpgradeCommand.cs`, `src/Steergen.Cli/Commands/TemplatePackUpgradeCommand.cs`, and `src/Steergen.Core/Configuration/PackSelectorResolver.cs`
- [ ] T033 [US3] Implement rollback-first failure handling and dual-failure diagnostics in `src/Steergen.Core/Updates/ExternalPackUpgradeService.cs`
- [ ] T034 [US3] Map rollback and selector failure modes to stable CLI exits in `src/Steergen.Cli/Composition/ExitCodeMapper.cs`
- [ ] T035 [US3] Add deterministic command output fields (mode, selector, final tuple, rollback status) in `src/Steergen.Cli/Commands/RulesPackUpgradeCommand.cs` and `src/Steergen.Cli/Commands/TemplatePackUpgradeCommand.cs`

**Checkpoint**: US3 is fully functional and independently testable.

---

## Final Phase: Polish & Cross-Cutting Concerns

**Purpose**: Documentation, contract alignment, and end-to-end validation.

- [ ] T036 [P] Update CLI usage docs for rules/template upgrade commands in `README.md` and `docs/getting-started.md`
- [ ] T037 [P] Align finalized behavior and selector format details in `specs/003-upgrade-pack-refs/contracts/cli-contract.md` and `specs/003-upgrade-pack-refs/contracts/config-schema.md`
- [ ] T038 [P] Document upgrade operational guidance and examples in `docs/authoring-rules-packs.md` and `specs/003-upgrade-pack-refs/quickstart.md`
- [ ] T039 Run full validation sequence from `specs/003-upgrade-pack-refs/quickstart.md`
- [ ] T040 [P] Add timed integration performance tests for `<=100MB` packs and assert p95 `<=60s` in `tests/Steergen.Cli.IntegrationTests/PackUpgradePerformanceTests.cs`
- [ ] T041 [P] Add CI/report step for upgrade performance budget tracking in `.github/workflows/ci.yml` and `tests/Steergen.Benchmarks/README.md`
- [ ] T042 [P] Run structured operator acceptance exercise (`n>=10`) and record first-attempt success metrics for latest and explicit flows in `specs/003-upgrade-pack-refs/progress.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- Setup (Phase 1): no dependencies.
- Foundational (Phase 2): depends on Setup completion and blocks user stories.
- User Stories (Phase 3+): depend on Foundational completion.
- Final Phase: depends on all desired user stories being complete.

### User Story Dependencies

- US1 (P1): starts after Foundational and delivers MVP upgrade behavior for rules packs.
- US2 (P2): starts after Foundational; independent of US1 output files but must preserve behavior parity.
- US3 (P3): starts after Foundational; hardens deterministic/fail-closed behavior for both command families.

### Within Each Story

- Tests MUST be written and fail before implementation.
- Property-based tests SHOULD precede example-based tests for invariants.
- Command parsing/validation before service side effects.
- Service orchestration before config mutation and CLI diagnostics finalization.

---

## Parallel Opportunities

- Phase 1 tasks marked [P] can run in parallel.
- Foundational tests (T004-T007) can run in parallel.
- Foundational implementation tasks T008-T012 can run in parallel before integration task T013.
- US1 tests can run in parallel only for cross-file tasks; T014 and T015 share one file and should execute sequentially, while T016 can run in parallel.
- US2 tests can run in parallel only for cross-file tasks; T022 and T023 share one file and should execute sequentially, while T024 can run in parallel.
- US3 test tasks (T029-T031) can run in parallel.
- Documentation tasks (T036-T038) can run in parallel after story completion.

---

## Parallel Example: User Story 1

```bash
# Launch US1 tests together:
Task: "Add integration tests for rules-pack upgrade latest and explicit tag flows in tests/Steergen.Cli.IntegrationTests/RulesPackUpgradeCommandTests.cs"
Task: "Add integration tests for missing/ambiguous/invalid selector failures in tests/Steergen.Cli.IntegrationTests/RulesPackUpgradeCommandTests.cs"
Task: "Add unit tests for targeted rules-pack entry resolution and update semantics in tests/Steergen.Core.UnitTests/Configuration/RulesPackRegistrationServiceTests.cs"

# Launch US1 implementation in parallel where possible:
Task: "Implement rules-pack upgrade command in src/Steergen.Cli/Commands/RulesPackUpgradeCommand.cs"
Task: "Implement rules-pack targeted config pin tuple mutation logic in src/Steergen.Core/Configuration/RulesPackRegistrationService.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Complete Setup and Foundational phases.
2. Complete US1 tests and implementation.
3. Validate no-tag and explicit-tag rules-pack upgrade flows end-to-end.

### Incremental Delivery

1. Deliver US1 (`rules-pack upgrade`) as MVP.
2. Deliver US2 (`template-pack upgrade`) with behavior parity.
3. Deliver US3 deterministic/fail-closed hardening.
4. Finalize docs/contracts and run full validation.

### Parallel Team Strategy

- Developer A: Foundational selector/config models and tests.
- Developer B: US1 rules-pack command and integration tests.
- Developer C: US2 template-pack command and parity tests.
- Developer D: US3 rollback/security hardening and diagnostics.

---

## Notes

- Keep implementation incremental and additive to existing command architecture.
- Reuse existing pack download/update services before introducing new abstractions.
- Ensure unchanged-on-failure guarantees for targeted config references.
- Keep diagnostics deterministic and script-friendly for CI/operator workflows.
