# Tasks: Steering Document Transformation Tool

**Input**: Design documents from `/specs/001-steering-doc-transform/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Tests are MANDATORY. Follow Red-Green-Refactor and author tests before implementation. Prefer property-based testing with CsCheck + xUnit for invariants. Where practical, use plausible real-world constitution/steering rules in fixtures instead of toy placeholders.

**Organization**: Tasks are grouped by user story so each story can be implemented and validated independently once the foundational phase is complete.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no incomplete-task dependency)
- **[Story]**: User story label (`[US1]` ... `[US10]`)
- Every task includes an exact file path

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the solution, projects, and baseline repository structure required by all stories.

- [x] T001 Create the .NET solution and project skeleton in `specgen.sln`, `src/Steergen.Cli/Steergen.Cli.csproj`, `src/Steergen.Core/Steergen.Core.csproj`, `src/Steergen.Templates/Steergen.Templates.csproj`, `tests/Steergen.Core.PropertyTests/Steergen.Core.PropertyTests.csproj`, `tests/Steergen.Core.UnitTests/Steergen.Core.UnitTests.csproj`, `tests/Steergen.Cli.IntegrationTests/Steergen.Cli.IntegrationTests.csproj`, and `tests/Steergen.Benchmarks/Steergen.Benchmarks.csproj`
- [x] T002 Configure repository-wide SDK, nullable, analyzers, and deterministic build defaults in `global.json`, `Directory.Build.props`, and `Directory.Build.targets`
- [x] T003 [P] Add runtime dependencies for the CLI and rendering pipeline in `src/Steergen.Cli/Steergen.Cli.csproj`, `src/Steergen.Core/Steergen.Core.csproj`, and `src/Steergen.Templates/Steergen.Templates.csproj`
- [x] T004 [P] Add test and benchmark dependencies for xUnit, CsCheck, NSubstitute, and BenchmarkDotNet in `tests/Steergen.Core.PropertyTests/Steergen.Core.PropertyTests.csproj`, `tests/Steergen.Core.UnitTests/Steergen.Core.UnitTests.csproj`, `tests/Steergen.Cli.IntegrationTests/Steergen.Cli.IntegrationTests.csproj`, and `tests/Steergen.Benchmarks/Steergen.Benchmarks.csproj`
- [x] T005 [P] Create realistic governance fixture scaffolding for constitution and steering corpora in `tests/Fixtures/RealisticGovernance/README.md`, `tests/Fixtures/RealisticGovernance/global/constitution.md`, and `tests/Fixtures/RealisticGovernance/project/project-steering.md`
- [x] T006 [P] Add initial template asset folders and placeholder files in `src/Steergen.Templates/Scriban/speckit/.gitkeep`, `src/Steergen.Templates/Scriban/kiro/.gitkeep`, and `src/Steergen.Templates/Scriban/agents/.gitkeep`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Build the core model, parser, validation, configuration, target contract, and CLI composition used by every story.

**⚠️ CRITICAL**: No user story work starts until this phase is complete.

- [x] T007 [P] Add parser property tests for Markdown/frontmatter/rule-block invariants in `tests/Steergen.Core.PropertyTests/Parsing/SteeringParserProperties.cs`
- [x] T008 [P] Add overlay and profile-filter determinism property tests in `tests/Steergen.Core.PropertyTests/Merge/OverlayAndProfileProperties.cs`
- [x] T009 [P] Add malicious-input and prompt-injection-style validation tests in `tests/Steergen.Core.UnitTests/Security/MaliciousInputValidationTests.cs`
- [x] T010 [P] Add optimistic config-write conflict tests in `tests/Steergen.Core.UnitTests/Configuration/OptimisticConfigWriterTests.cs`
- [x] T011 [P] Implement core document and rule model types in `src/Steergen.Core/Model/SteeringDocument.cs` and `src/Steergen.Core/Model/SteeringRule.cs`
- [x] T012 [P] Implement resolved-model, target-config, and source-location types in `src/Steergen.Core/Model/ResolvedSteeringModel.cs`, `src/Steergen.Core/Model/SteeringConfiguration.cs`, and `src/Steergen.Core/Model/SourceLocation.cs`
- [x] T013 Implement the Markdown/frontmatter parser in `src/Steergen.Core/Parsing/SteeringMarkdownParser.cs`
- [x] T014 Implement the validation pipeline and diagnostic model in `src/Steergen.Core/Validation/SteeringValidator.cs` and `src/Steergen.Core/Validation/Diagnostic.cs`
- [x] T015 Implement overlay resolution, profile filtering, and deterministic ordering in `src/Steergen.Core/Merge/SteeringResolver.cs`
- [x] T016 Implement YAML configuration load/write with optimistic locking in `src/Steergen.Core/Configuration/SteergenConfigLoader.cs` and `src/Steergen.Core/Configuration/SteergenConfigWriter.cs`
- [x] T017 Implement the built-in target contract and deterministic registry in `src/Steergen.Core/Targets/ITargetComponent.cs`, `src/Steergen.Core/Targets/TargetRegistry.cs`, and `src/Steergen.Core/Targets/TargetDescriptor.cs`
- [x] T018 Implement shared command composition, quiet/verbose diagnostics, and exit-code mapping in `src/Steergen.Cli/Program.cs`, `src/Steergen.Cli/Composition/CommandFactory.cs`, and `src/Steergen.Cli/Composition/ExitCodeMapper.cs`
- [x] T019 [P] Add benchmark scaffolding for parser, resolver, and generation hot paths in `tests/Steergen.Benchmarks/CorePipelineBenchmarks.cs`

**Checkpoint**: Core pipeline and CLI infrastructure are ready for independent story delivery.

---

## Phase 3: User Story 1 - Run Steering Generation to Speckit Artefacts (Priority: P1) 🎯 MVP

**Goal**: Run deterministic steering generation into Speckit Markdown artifacts, including a core-only `constitution.md` plus modular domain guidance files.

**Independent Test**: Run `run` against realistic global and project fixtures and verify deterministic Speckit outputs, overlay replacement, profile filtering, and preserved deprecation/supersedes metadata.

### Tests for User Story 1 (MANDATORY) ✅

- [x] T020 [P] [US1] Add run-to-Speckit integration tests using realistic governance fixtures in `tests/Steergen.Cli.IntegrationTests/RunSpeckitCommandTests.cs`
- [x] T021 [P] [US1] Add property tests for core-vs-domain partitioning and constitution-only output rules in `tests/Steergen.Core.PropertyTests/Generation/SpeckitPartitionProperties.cs`
- [x] T022 [P] [US1] Add Speckit golden tests covering overlay, profile, deprecation, and supersedes behavior in `tests/Steergen.Core.UnitTests/Targets/SpeckitTargetComponentTests.cs`

### Implementation for User Story 1

- [x] T023 [P] [US1] Implement generation pipeline orchestration in `src/Steergen.Core/Generation/GenerationPipeline.cs`
- [x] T024 [P] [US1] Implement Speckit target mapping and rendering in `src/Steergen.Core/Targets/Speckit/SpeckitTargetComponent.cs` and `src/Steergen.Core/Targets/Speckit/SpeckitDocumentModel.cs`
- [x] T025 [P] [US1] Add Speckit Scriban templates for constitution and modular guidance in `src/Steergen.Templates/Scriban/speckit/constitution.scriban` and `src/Steergen.Templates/Scriban/speckit/module.scriban`
- [x] T026 [US1] Implement Speckit generation orchestration and output persistence service consumed by `run` in `src/Steergen.Core/Generation/SpeckitGenerationService.cs`
- [x] T027 [US1] Implement core-only constitution emission and domain-module splitting in `src/Steergen.Core/Generation/CoreGuidancePartitioner.cs`

**Checkpoint**: User Story 1 independently produces Speckit outputs and forms the MVP.

---

## Phase 4: User Story 2 - Run Steering Generation to Kiro IDE Steering Files (Priority: P2)

**Goal**: Generate one Kiro-compatible Markdown file per source document with correct frontmatter, prose rendering, inclusion mapping, and deprecated-rule exclusion.

**Independent Test**: Run `run` with the Kiro target enabled and verify per-document outputs, inclusion defaults/mappings, natural-language rendering, and deprecated-rule filtering.

### Tests for User Story 2 (MANDATORY) ✅

- [x] T028 [P] [US2] Add Kiro run integration tests for per-document output, inclusion modes, and deprecated-rule exclusion in `tests/Steergen.Cli.IntegrationTests/RunKiroCommandTests.cs`
- [x] T029 [P] [US2] Add Kiro renderer unit tests for `always`, `fileMatch`, and `auto` frontmatter behavior in `tests/Steergen.Core.UnitTests/Targets/KiroTargetComponentTests.cs`
- [x] T030 [P] [US2] Add property tests proving prose output never leaks rule IDs, severities, or `:::rule` syntax in `tests/Steergen.Core.PropertyTests/Generation/KiroRenderingProperties.cs`

### Implementation for User Story 2

- [x] T031 [P] [US2] Implement Kiro inclusion mapping and options handling in `src/Steergen.Core/Targets/Kiro/KiroTargetOptions.cs` and `src/Steergen.Core/Targets/Kiro/KiroInclusionMapper.cs`
- [x] T032 [P] [US2] Implement the Kiro target renderer in `src/Steergen.Core/Targets/Kiro/KiroTargetComponent.cs`
- [x] T033 [P] [US2] Add the Kiro document template in `src/Steergen.Templates/Scriban/kiro/document.scriban`
- [x] T034 [US2] Register the Kiro target and configuration translation in `src/Steergen.Core/Targets/TargetRegistry.cs` and `src/Steergen.Core/Model/SteeringConfiguration.cs`

**Checkpoint**: User Story 2 independently produces Kiro-ready steering documents.

---

## Phase 5: User Story 3 - Run Steering Generation to Agent Specification Files (Priority: P3)

**Goal**: Generate target-specific agent instruction files while preserving equivalent normative guidance semantics across platforms.

**Independent Test**: Run the same fixture corpus to multiple agent targets and verify syntax differs by platform while required metadata and normative intent remain aligned.

### Tests for User Story 3 (MANDATORY) ✅

- [x] T035 [P] [US3] Add semantic-parity tests across Copilot and Kiro agent outputs in `tests/Steergen.Core.UnitTests/Targets/AgentTargetSemanticParityTests.cs`
- [x] T036 [P] [US3] Add integration tests for agent-target metadata validation and exit code 3 failures in `tests/Steergen.Cli.IntegrationTests/RunAgentTargetsCommandTests.cs`

### Implementation for User Story 3

- [x] T037 [P] [US3] Implement shared agent-target document abstractions in `src/Steergen.Core/Targets/Agents/AgentTargetDocument.cs` and `src/Steergen.Core/Targets/Agents/AgentTargetMetadata.cs`
- [x] T038 [P] [US3] Implement the Copilot agent renderer and template in `src/Steergen.Core/Targets/Agents/CopilotAgentTargetComponent.cs` and `src/Steergen.Templates/Scriban/agents/copilot.agent.scriban`
- [x] T039 [P] [US3] Implement the Kiro agent renderer and template in `src/Steergen.Core/Targets/Agents/KiroAgentTargetComponent.cs` and `src/Steergen.Templates/Scriban/agents/kiro.agent.scriban`
- [x] T040 [US3] Register agent targets and generation-error handling in `src/Steergen.Core/Targets/TargetRegistry.cs` and `src/Steergen.Core/Generation/TargetGenerationException.cs`

**Checkpoint**: User Story 3 independently generates platform-specific agent files from the shared steering model.

---

## Phase 6: User Story 4 - Validate Steering Documents (Priority: P4)

**Goal**: Detect malformed steering documents with clear diagnostics, correct exit codes, and source locations before generation.

**Independent Test**: Run `validate` against mixed valid and invalid corpora and verify errors/warnings, source locations, and exit code behavior.

### Tests for User Story 4 (MANDATORY) ✅

- [x] T041 [P] [US4] Add validate-command integration tests for schema errors, duplicate IDs, severity validation, and warnings in `tests/Steergen.Cli.IntegrationTests/ValidateCommandTests.cs`
- [x] T042 [P] [US4] Add property tests for stable diagnostic ordering and location reporting in `tests/Steergen.Core.PropertyTests/Validation/ValidationDiagnosticProperties.cs`

### Implementation for User Story 4

- [x] T043 [US4] Implement the validate command handler in `src/Steergen.Cli/Commands/ValidateCommand.cs`
- [x] T044 [US4] Extend the validation pipeline for frontmatter, duplicate IDs, severity values, and supersedes warnings in `src/Steergen.Core/Validation/SteeringValidator.cs`

**Checkpoint**: User Story 4 independently provides CI-ready document validation.

---

## Phase 7: User Story 5 - Inspect the Merged Steering Model (Priority: P5)

**Goal**: Expose the resolved steering model as stable JSON for debugging and integration use cases without generating artifacts.

**Independent Test**: Run `inspect` with and without profile filters and verify stdout JSON matches the resolved overlay/profile model deterministically.

### Tests for User Story 5 (MANDATORY) ✅

- [x] T045 [P] [US5] Add inspect-command integration tests for stdout JSON and profile-scoped output in `tests/Steergen.Cli.IntegrationTests/InspectCommandTests.cs`
- [x] T046 [P] [US5] Add property tests for deterministic inspect JSON ordering in `tests/Steergen.Core.PropertyTests/Inspection/InspectJsonProperties.cs`

### Implementation for User Story 5

- [x] T047 [US5] Implement the inspect model serializer in `src/Steergen.Core/Generation/InspectModelWriter.cs`
- [x] T048 [US5] Implement the inspect command handler in `src/Steergen.Cli/Commands/InspectCommand.cs`

**Checkpoint**: User Story 5 independently supports resolved-model inspection workflows.

---

## Phase 8: User Story 6 - Extend the Tool with a New Output Target (Priority: P6)

**Goal**: Prove additive target extensibility without runtime plugins and without refactoring existing targets.

**Independent Test**: Register a minimal fixture target, run compilation, and verify it produces output while existing target outputs remain unchanged.

### Tests for User Story 6 (MANDATORY) ✅

- [x] T049 [P] [US6] Add compatibility tests proving additive registration does not change existing target outputs in `tests/Steergen.Core.UnitTests/Targets/TargetRegistryCompatibilityTests.cs`
- [x] T050 [P] [US6] Add fixture-target run integration tests in `tests/Steergen.Cli.IntegrationTests/RunFixtureTargetCommandTests.cs`

### Implementation for User Story 6

- [x] T051 [P] [US6] Implement a minimal fixture target used to verify the extension seam in `src/Steergen.Core/Targets/Fixtures/FixtureTargetComponent.cs`
- [x] T052 [US6] Implement additive registration metadata and target-extension guidance in `src/Steergen.Core/Targets/TargetRegistrationMetadata.cs` and `docs/targets/additive-targets.md`

**Checkpoint**: User Story 6 independently validates the non-plugin additive target model.

---

## Phase 9: User Story 7 - Integrate with CI Pipelines (Priority: P7)

**Goal**: Make validate and run deterministic, scriptable CI gates with explicit exit-code behavior and performance visibility.

**Independent Test**: Run a CI-style validate/run sequence and verify expected exit codes, deterministic artifacts, and benchmark coverage for the supported scale envelope.

### Tests for User Story 7 (MANDATORY) ✅

- [x] T053 [P] [US7] Add CI workflow regression tests for validate/run exit codes and deterministic outputs in `tests/Steergen.Cli.IntegrationTests/CiWorkflowRegressionTests.cs`
- [x] T054 [P] [US7] Add scalability-envelope benchmarks for 100 docs/1,000 rules and warning behavior beyond the envelope in `tests/Steergen.Benchmarks/ScalabilityEnvelopeBenchmarks.cs`

### Implementation for User Story 7

- [x] T055 [US7] Implement deterministic output manifest generation and CI-facing failure reporting in `src/Steergen.Core/Generation/DeterministicOutputManifest.cs` and `src/Steergen.Core/Generation/GenerationPipeline.cs`
- [x] T056 [US7] Add CI workflow and release-gate documentation preserving stable and preview tag guidance in `.github/workflows/ci.yml` and `README.md`

**Checkpoint**: User Story 7 independently supports CI gating and deterministic release verification.

---

## Phase 10: User Story 8 - Initialize Project Structure via CLI (Priority: P8)

**Goal**: Bootstrap steering and target output folders from the CLI without overwriting existing project artifacts.

**Independent Test**: Run `steergen init . --target ...` in an empty project, verify expected folders are created, then rerun to confirm idempotency and non-destructive behavior.

### Tests for User Story 8 (MANDATORY) ✅

- [x] T057 [P] [US8] Add init-command integration tests for multi-target bootstrap and idempotency in `tests/Steergen.Cli.IntegrationTests/InitCommandTests.cs`
- [x] T058 [P] [US8] Add target-layout unit tests for invalid target identifiers and folder rules in `tests/Steergen.Core.UnitTests/Targets/TargetLayoutInitializerTests.cs`

### Implementation for User Story 8

- [x] T059 [US8] Implement target folder layout initialization in `src/Steergen.Core/Targets/TargetLayoutInitializer.cs`
- [x] T060 [US8] Implement the init command handler in `src/Steergen.Cli/Commands/InitCommand.cs`

**Checkpoint**: User Story 8 independently bootstraps project structure for supported targets.

---

## Phase 11: User Story 9 - Update Project Templates and Metadata In Place (Priority: P9)

**Goal**: Update template-pack content independently of the CLI binary while preserving stable and preview version rules.

**Independent Test**: Run `update` with latest-compatible and explicit version flows, including preview tags, and verify template metadata advances safely with clear diagnostics on invalid versions.

### Tests for User Story 9 (MANDATORY) ✅

- [x] T061 [P] [US9] Add update-command integration tests for latest-compatible, exact-version, and preview-version flows in `tests/Steergen.Cli.IntegrationTests/UpdateCommandTests.cs`
- [x] T062 [P] [US9] Add version-resolution unit tests preserving stable and `previewN` SemVer behavior in `tests/Steergen.Core.UnitTests/Updates/TemplateVersionResolverTests.cs`

### Implementation for User Story 9

- [x] T063 [US9] Implement template-pack update orchestration and version resolution in `src/Steergen.Core/Updates/TemplatePackUpdater.cs` and `src/Steergen.Core/Updates/TemplateVersionResolver.cs`
- [x] T064 [US9] Implement the update command and config version persistence in `src/Steergen.Cli/Commands/UpdateCommand.cs` and `src/Steergen.Core/Configuration/SteergenConfigWriter.cs`

**Checkpoint**: User Story 9 independently manages template-pack lifecycle and release-version semantics.

---

## Phase 12: User Story 10 - Execute Generation and Manage Target Registration via CLI (Priority: P10)

**Goal**: Expose generation and target registration entirely through the CLI with correct scoping, idempotency, and optimistic config updates.

**Independent Test**: Register targets, run generation with and without `--target`, deregister targets, and verify config mutations and execution scope behave correctly without destructive artifact cleanup.

### Tests for User Story 10 (MANDATORY) ✅

- [x] T065 [P] [US10] Add run/target-add/target-remove integration tests in `tests/Steergen.Cli.IntegrationTests/RunAndTargetCommandsTests.cs`
- [x] T066 [P] [US10] Add optimistic-lock conflict tests for target registration commands in `tests/Steergen.Core.UnitTests/Configuration/TargetRegistrationConfigLockTests.cs`

### Implementation for User Story 10

- [x] T067 [US10] Extend the existing run command with explicit target scoping and registered-target selection behavior in `src/Steergen.Cli/Commands/RunCommand.cs`
- [x] T068 [US10] Implement target add/remove command handling and idempotent config mutation in `src/Steergen.Cli/Commands/TargetCommand.cs` and `src/Steergen.Core/Configuration/TargetRegistrationService.cs`

**Checkpoint**: User Story 10 independently supports day-to-day generation and target registration workflows.

---

## Final Phase: Polish & Cross-Cutting Concerns

**Purpose**: Close out documentation, security, performance, and validation tasks that affect multiple stories.

- [x] T069 [P] Update end-user command documentation and validated examples in `README.md` and `specs/001-steering-doc-transform/quickstart.md`
- [x] T070 [P] Add end-to-end malicious-input regression coverage and expand realistic governance corpora in `tests/Steergen.Cli.IntegrationTests/Security/CliSecurityRegressionTests.cs` and `tests/Fixtures/RealisticGovernance/`
- [x] T071 [P] Record benchmark execution guidance and release/versioning checks for stable and preview tags in `docs/release/release-checklist.md` and `tests/Steergen.Benchmarks/README.md`
- [x] T072 Run quickstart and contract validation cleanup against `specs/001-steering-doc-transform/quickstart.md`, `specs/001-steering-doc-transform/contracts/cli-contract.md`, and `specs/001-steering-doc-transform/contracts/config-schema.md`
- [x] T073 [P] Add integration tests for constitution amendment provenance capture (version rationale, amendment date, impacted-artifact sync record) in `tests/Steergen.Cli.IntegrationTests/ConstitutionProvenanceTests.cs`
- [x] T074 Implement constitution provenance recording in `src/Steergen.Core/Updates/ConstitutionProvenanceRecorder.cs` and wire it into `src/Steergen.Core/Updates/TemplatePackUpdater.cs`
- [x] T075 [P] Add publish-profile and CI validation for single portable executable distribution in `src/Steergen.Cli/Steergen.Cli.csproj` and `.github/workflows/ci.yml`
- [x] T076 [P] Add trimming and executable-size budget verification checks in `.github/workflows/ci.yml` and `tests/Steergen.Benchmarks/README.md`
- [x] T077 [P] Add optimization-mode verification (AOT/ReadyToRun decision path) in `docs/release/release-checklist.md` and `Directory.Build.props`
- [x] T078 [P] Add cross-target include/reference path resolution tests for constitution modular references in `tests/Steergen.Core.UnitTests/Targets/ConstitutionReferenceResolutionTests.cs`
- [x] T079 [P] Add integration tests that validate measurement protocol routines for SC-001/SC-005 only execute when `--verbose` or `--debug` is enabled, and remain disabled in default mode, in `tests/Steergen.Cli.IntegrationTests/Measurement/MeasurementProtocolOptInTests.cs`
- [x] T080 Implement timing-measurement protocol instrumentation for SC-001/SC-005 as opt-in diagnostics routed to stderr and gated behind verbose/debug flags in `src/Steergen.Cli/Diagnostics/MeasurementProtocolReporter.cs` and `src/Steergen.Cli/Commands/RunCommand.cs`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies
- **Foundational (Phase 2)**: Depends on Setup and blocks all user stories
- **User Stories (Phases 3-12)**: Depend on Foundational; then can proceed in priority order or in parallel where staffing allows
- **Polish (Final Phase)**: Depends on the stories being delivered

### User Story Dependencies

- **US1 (P1)**: Starts immediately after Foundational; establishes the MVP generation path
- **US2 (P2)**: Starts after Foundational; shares generation infrastructure but is independently testable
- **US3 (P3)**: Starts after Foundational; depends on the target contract, not on US1 or US2 output files
- **US4 (P4)**: Starts after Foundational; validates the same parser/resolver pipeline independently of generation
- **US5 (P5)**: Starts after Foundational; depends on the resolved model only
- **US6 (P6)**: Starts after Foundational; depends on the registry contract and verifies additive growth
- **US7 (P7)**: Starts after Foundational; becomes most valuable once run and validate paths exist
- **US8 (P8)**: Starts after Foundational; depends on target registration metadata and layout rules
- **US9 (P9)**: Starts after Foundational; depends on configuration write support and release/version rules
- **US10 (P10)**: Starts after Foundational; depends on configuration mutation, registered target behavior, and the baseline run generation flow delivered in earlier stories

### Within Each User Story

- Tests MUST be written and failing before implementation tasks begin
- Prefer property-based CsCheck tests before example-based tests where invariants exist
- Renderer/templates before command wiring when output contracts are involved
- Command handlers after core services and models they orchestrate

### Suggested Execution Order

1. Complete Phase 1
2. Complete Phase 2
3. Deliver US1 as the MVP
4. Deliver US2 and US4 next to harden generation and validation workflows
5. Deliver US3, US5, and US6 to widen target coverage and observability
6. Deliver US7 through US10 for operational and maintenance workflows
7. Finish with the Polish phase

---

## Parallel Opportunities

### Setup

- T003, T004, T005, and T006 can run in parallel after T001 and T002

### Foundational

- T007, T008, T009, and T010 can run in parallel before implementation
- T011 and T012 can run in parallel before T013 through T018

### User Story 1

- T020, T021, and T022 can run in parallel
- T023, T024, and T025 can run in parallel after the tests are in place

### User Story 2

- T028, T029, and T030 can run in parallel
- T031, T032, and T033 can run in parallel after the tests are in place

### User Story 3

- T035 and T036 can run in parallel
- T037, T038, and T039 can run in parallel after the tests are in place

### User Story 4

- T041 and T042 can run in parallel before T043 and T044

### User Story 5

- T045 and T046 can run in parallel before T047 and T048

### User Story 6

- T049 and T050 can run in parallel before T051 and T052

### User Story 7

- T053 and T054 can run in parallel before T055 and T056

### User Story 8

- T057 and T058 can run in parallel before T059 and T060

### User Story 9

- T061 and T062 can run in parallel before T063 and T064

### User Story 10

- T065 and T066 can run in parallel before T067 and T068

---

## Parallel Example: User Story 1

```text
Run together: T020, T021, T022
Then run together: T023, T024, T025
Then finish: T026, T027
```

## Parallel Example: User Story 2

```text
Run together: T028, T029, T030
Then run together: T031, T032, T033
Then finish: T034
```

## Parallel Example: User Story 3

```text
Run together: T035, T036
Then run together: T037, T038, T039
Then finish: T040
```

## Parallel Example: User Story 4

```text
Run together: T041, T042
Then finish: T043, T044
```

## Parallel Example: User Story 5

```text
Run together: T045, T046
Then finish: T047, T048
```

## Parallel Example: User Story 6

```text
Run together: T049, T050
Then finish: T051, T052
```

## Parallel Example: User Story 7

```text
Run together: T053, T054
Then finish: T055, T056
```

## Parallel Example: User Story 8

```text
Run together: T057, T058
Then finish: T059, T060
```

## Parallel Example: User Story 9

```text
Run together: T061, T062
Then finish: T063, T064
```

## Parallel Example: User Story 10

```text
Run together: T065, T066
Then finish: T067, T068
```

---

## Implementation Strategy

### MVP First

1. Finish Setup and Foundational phases
2. Deliver User Story 1
3. Validate deterministic Speckit output, realistic fixtures, and core-only `constitution.md`
4. Stop and review before expanding target coverage

### Incremental Delivery

1. US1 establishes core generation value
2. US2 adds Kiro output support
3. US4 hardens validation as a CI gate
4. US3, US5, and US6 expand platform reach and observability
5. US7 through US10 complete operational CLI workflows

### Validation Notes

- Keep Red-Green-Refactor explicit for every story
- Maintain CsCheck + xUnit as the default invariant-testing stack
- Use plausible real-world constitution/steering fixture corpora where practical
- Preserve stable release tags `vMAJOR.MINOR.PATCH` and preview tags `vMAJOR.MINOR.PATCH-previewN` in update, CI, and release tasks