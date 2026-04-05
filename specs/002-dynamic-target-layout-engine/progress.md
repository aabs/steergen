# Ralph Progress Log

Feature: 002-dynamic-target-layout-engine
Started: 2026-04-05 11:17:23

## Codebase Patterns

- Routing fixture docs use `:::rule id="ID" severity="..." category="..." domain="..."` quoted-attribute format (same as existing fixtures in RealisticGovernance/)
- Target source folders: Speckit/ and Kiro/ are directly under Targets/; agent targets are in Targets/Agents/ (flat, no subdirs before this iteration)
- Build: `dotnet build specgen.slnx --no-incremental`; Test: `dotnet test specgen.slnx --no-build`
- TreatWarningsAsErrors=true; build output showing "1 Error(s)" after "Deleting file" warnings is misleading — check `0 Error(s)` line to confirm success

---
## Iteration 1 - 2026-04-05
**User Story**: Phase 1 Setup (T001, T002, T003 — no US label)
**Tasks Completed**:
- [x] T001: Create docs/routing-syntax.md — framework-agnostic routing syntax reference
- [x] T002: Add default-layout.yaml to Speckit/, Kiro/, Agents/Copilot/, Agents/Kiro/ target folders
- [x] T003: Add catch-all-fixture.md, fallback-fixture.md, mixed-domains-fixture.md + README to tests/Fixtures/RealisticGovernance/RoutingLayouts/
**Tasks Remaining in Story**: None - phase complete
**Commit**: 22ba941
**Files Changed**:
- docs/routing-syntax.md
- src/Steergen.Core/Targets/Speckit/default-layout.yaml
- src/Steergen.Core/Targets/Kiro/default-layout.yaml
- src/Steergen.Core/Targets/Agents/Copilot/default-layout.yaml (new subdirectory)
- src/Steergen.Core/Targets/Agents/Kiro/default-layout.yaml (new subdirectory)
- tests/Fixtures/RealisticGovernance/RoutingLayouts/README.md
- tests/Fixtures/RealisticGovernance/RoutingLayouts/catch-all-fixture.md
- tests/Fixtures/RealisticGovernance/RoutingLayouts/fallback-fixture.md
- tests/Fixtures/RealisticGovernance/RoutingLayouts/mixed-domains-fixture.md
- specs/002-dynamic-target-layout-engine/tasks.md (T001-T003 marked done)
**Learnings**:
- Agents/ target folder had no subdirectories before this iteration; Copilot/ and Kiro/ subdirs were created to hold default-layout.yaml files
- All 275 existing tests pass after adding these scaffold files (no C# changes)
- The default-layout.yaml format follows the config-schema.md contract: version, roots, routes (with core anchors per scope), fallback, purge
---


---
## Iteration 2 - 2026-04-05
**User Story**: Phase 2 Foundational — Domain Models + Loader + Tests (T006, T008, T009, T047)
**Tasks Completed**: 
- [x] T008: Domain model records/enums in src/Steergen.Core/Model/ (TargetLayoutDefinition, RouteRuleDefinition, FallbackRuleDefinition, PurgePolicyDefinition, RouteMatchExpression, DestinationTemplate, LayoutRootsDefinition, VariableDefinition, enums, RouteResolutionResult, WritePlan/WritePlanFile/ContentUnit)
- [x] T009: LayoutOverrideLoader with embedded YAML loading + deep-merge (scalar/map recursive merge, list replacement, StringOrListConverter for bare-string YAML fields)
- [x] T047: DefaultLayoutYamlContractTests — validates all 4 built-in target YAMLs satisfy schema contract
- [x] T006: LayoutOverrideLoaderTests — validates deep-merge semantics via public API
**Tasks Remaining in Story**: Many (T004, T005, T007, T010, T011, T012, T013, T014, T038, T039)
**Commit**: 84c560f
**Files Changed**: 
- src/Steergen.Core/Model/RouteScope.cs (new)
- src/Steergen.Core/Model/RouteAnchor.cs (new)
- src/Steergen.Core/Model/FallbackMode.cs (new)
- src/Steergen.Core/Model/RouteProvenance.cs (new)
- src/Steergen.Core/Model/RouteMatchExpression.cs (new)
- src/Steergen.Core/Model/DestinationTemplate.cs (new)
- src/Steergen.Core/Model/RouteRuleDefinition.cs (new)
- src/Steergen.Core/Model/FallbackRuleDefinition.cs (new)
- src/Steergen.Core/Model/PurgePolicyDefinition.cs (new)
- src/Steergen.Core/Model/VariableDefinition.cs (new)
- src/Steergen.Core/Model/LayoutRootsDefinition.cs (new)
- src/Steergen.Core/Model/TargetLayoutDefinition.cs (new)
- src/Steergen.Core/Model/RouteResolutionResult.cs (new)
- src/Steergen.Core/Model/WritePlan.cs (new)
- src/Steergen.Core/Configuration/LayoutOverrideLoader.cs (new)
- src/Steergen.Core/Steergen.Core.csproj (embedded resource for default-layout.yaml files)
- tests/Steergen.Core.UnitTests/Targets/DefaultLayoutYamlContractTests.cs (new)
- tests/Steergen.Core.UnitTests/Configuration/LayoutOverrideLoaderTests.cs (new)
- specs/002-dynamic-target-layout-engine/tasks.md (T006, T008, T009, T047 marked done)
**Learnings**:
- YamlDotNet's IYamlTypeConverter for List<string> must manually consume SequenceStart/SequenceEnd and individual Scalars — calling rootDeserializer(typeof(List<string>)) recursively re-enters the same converter causing a stack overflow
- Default-layout.yaml files must be added as EmbeddedResource in Steergen.Core.csproj; resource names follow dotted namespace convention: Steergen.Core.Targets.{Folder}.default-layout.yaml
- Enum values in YAML are lowercase with hyphens (e.g. other-at-core-anchor, core) — parsed via explicit switch expressions in MapToModel
---


---
## Iteration 3 - 2026-04-05
**User Story**: Partial progress on Phase 2 Foundational (T007, T010)
**Tasks Completed**: 
- [x] T007: RoutePathSafetyTests — 22 tests covering traversal rejection (RS004), absolute path rejection (RS005), filename separator rejection (RS006), structural validation (RS001/RS002/RS003), and valid path acceptance
- [x] T010: RoutingSchemaValidator — validates TargetLayoutDefinition with codes RS001 (empty routes), RS002 (duplicate IDs), RS003 (no core anchor), RS004 (path traversal), RS005 (absolute path), RS006 (filename separator)
**Tasks Remaining in Story**: T004, T005, T011, T012, T013, T014, T038, T039
**Commit**: No commit - partial progress (Phase 2 not yet complete)
**Files Changed**: 
- src/Steergen.Core/Configuration/RoutingSchemaValidator.cs (new)
- tests/Steergen.Core.UnitTests/Generation/RoutePathSafetyTests.cs (new)
- specs/002-dynamic-target-layout-engine/tasks.md (T007, T010 marked done)
**Learnings**:
- `Path.IsPathRooted("C:\\Windows\\System32")` returns false on macOS — must also check for drive-letter patterns (letter + colon + slash) explicitly for cross-platform absolute path detection
- RoutingSchemaValidator follows existing Diagnostic/DiagnosticSeverity pattern from SteeringValidator; returns diagnostics sorted by code for deterministic output
- 182 unit tests + 131 integration tests all pass after this iteration (313 total)
---

---
## Iteration 4 - 2026-04-05
**User Story**: Partial progress on Phase 2 Foundational (T004, T005, T011)
**Tasks Completed**: 
- [x] T004: RouteResolverProperties — 6 property tests covering single-destination resolution, determinism, explicit-over-non-explicit precedence, unresolved-when-no-match, specific-over-wildcard, matched-route-ids set
- [x] T005: CatchAllRoutingProperties — 7 property tests covering catch-all matching, specific-beats-catchall precedence, catch-all for unknown domains, fallback-not-applied-when-catchall-exists, fallback-applied-at-core-anchor-dir, non-null path, WritePlanBuilder grouping
- [x] T011: RouteResolver (match eval, specificity scoring, precedence tuple, destination template resolution), RoutePlanner (resolve-with-fallback, other.* at core-anchor dir), WritePlanBuilder (groups by dest path, orders by rule ID, stable file ordering)
**Tasks Remaining in Story**: T012, T013, T014, T038, T039
**Commit**: No commit - partial progress (Phase 2 not yet complete)
**Files Changed**: 
- src/Steergen.Core/Generation/RouteResolver.cs (new)
- src/Steergen.Core/Generation/RoutePlanner.cs (new)
- src/Steergen.Core/Generation/WritePlanBuilder.cs (new)
- tests/Steergen.Core.PropertyTests/Generation/RouteResolverProperties.cs (new)
- tests/Steergen.Core.PropertyTests/Generation/CatchAllRoutingProperties.cs (new)
- specs/002-dynamic-target-layout-engine/tasks.md (T004, T005, T011 marked done)
**Learnings**:
- RouteResolver precedence tuple: (explicit desc, specificity desc, order asc, declaration-index asc, routeId asc) — Order field takes priority over declaration index since routes declare their own `order:` integer
- Destination template substitution: substitute rule-specific vars (${domain}, ${category}, etc.) at resolve time; leave context vars (${globalRoot}, ${projectRoot}, ${inputFileStem}) as literal strings for execution-time resolution
- FallbackMode.OtherAtCoreAnchor: uses the FIRST route with anchor=Core for its Directory; fallback path = "{coreAnchorDir}/other{coreAnchorExtension}"
- 364 total tests (51 property + 182 unit + 131 integration) all pass after this iteration
---

---
## Iteration 5 - 2026-04-05
**User Story**: Phase 2 Foundational complete (T038, T039, T013, T014, T012)
**Tasks Completed**: 
- [x] T038: InertContentRoutingProperties — 5 property tests proving injection-like body/title content never affects route selection
- [x] T039: InertContentRoutingTests — 5 CLI integration tests verifying no behavioral influence from malicious fixture content on route selection or write plans; also verifies parser doesn't create phantom rules from injection-in-body
- [x] T013: TargetRegistry.GetDefaultLayoutResourceName + HasDefaultLayout for per-target default-layout asset lookup (delegates to LayoutOverrideLoader)
- [x] T014: LayoutOverridePath added to TargetConfiguration model, SteergenConfigLoader (YAML deserialization), and SteergenConfigWriter (YAML serialization)
- [x] T012: GenerationPipeline computes WritePlans per enabled target (LayoutOverrideLoader → RoutePlanner → WritePlanBuilder); WritePlans returned in GenerationResult; LAYOUT-001 warning diagnostic on layout load failure; existing target.GenerateAsync flow preserved for backward compat pending T020
**Tasks Remaining in Story**: None — Phase 2 complete; Phase 3 (US1) is next
**Commit**: 4f2c819
**Files Changed**: 
- tests/Steergen.Core.PropertyTests/Security/InertContentRoutingProperties.cs (new)
- tests/Steergen.Cli.IntegrationTests/Security/InertContentRoutingTests.cs (new)
- src/Steergen.Core/Targets/TargetRegistry.cs (GetDefaultLayoutResourceName, HasDefaultLayout)
- src/Steergen.Core/Model/SteeringConfiguration.cs (LayoutOverridePath on TargetConfiguration)
- src/Steergen.Core/Configuration/SteergenConfigLoader.cs (LayoutOverridePath)
- src/Steergen.Core/Configuration/SteergenConfigWriter.cs (LayoutOverridePath)
- src/Steergen.Core/Generation/GenerationPipeline.cs (WritePlans, LayoutOverrideLoader, RoutePlanner, WritePlanBuilder)
- specs/002-dynamic-target-layout-engine/tasks.md (T038, T039, T013, T014, T012 marked done)
**Learnings**:
- xUnit Assert.Equal does NOT have a `userMessage` parameter — use Assert.True(a == b, message) instead
- SteeringMarkdownParser is a static class — use SteeringMarkdownParser.Parse(...) not new SteeringMarkdownParser()
- GenerationPipeline: WritePlan computation is guarded with HasDefaultLayout check and try/catch (emits LAYOUT-001 warning diagnostic on failure) to preserve backward compat for targets without default layouts
- All 376 tests pass (56 property + 182 unit + 138 integration) after iteration
---

---
## Iteration 7 - 2026-04-05
**User Story**: US1 Route Rules to Target-Specific Layouts — COMPLETE (T015, T016, T021, T042, T044)
**Tasks Completed**:
- [x] T015: RunTargetLayoutRoutingTests — 6 tests: exit code, core rules to constitution.md, non-core to domain modules, catch-all for unknown domains, determinism, no duplicate placement
- [x] T016: RunCatchAllRoutingTests — 5 tests: specific beats catch-all, unrecognized domains via catch-all, no-catch-all layout falls back to other.md, catch-all prevents fallback, WritePlan all-resolved property
- [x] T042: RunCompatibilityBaselineTests — 7 regression tests verifying existing speckit+kiro output behavior preserved with no layout override
- [x] T044: RunLayoutConventionsAcceptanceTests — 4 acceptance tests: workspace-local override, user-home global override, mixed per-target overrides, default without override
- [x] T021: GenerationResult gains RouteResolutions dict; RunCommand --verbose emits route diagnostics (destination, route ID, source, failure traces)
**Tasks Remaining in Story**: None — US1 complete
**Commit**: 95fbb8f
**Files Changed**:
- tests/Steergen.Cli.IntegrationTests/RunTargetLayoutRoutingTests.cs (new)
- tests/Steergen.Cli.IntegrationTests/RunCatchAllRoutingTests.cs (new)
- tests/Steergen.Cli.IntegrationTests/RunCompatibilityBaselineTests.cs (new)
- tests/Steergen.Cli.IntegrationTests/RunLayoutConventionsAcceptanceTests.cs (new)
- src/Steergen.Core/Generation/GenerationPipeline.cs (RouteResolutions in GenerationResult)
- src/Steergen.Cli/Commands/RunCommand.cs (EmitRoutingDiagnostics, verbose routing output)
- specs/002-dynamic-target-layout-engine/tasks.md (T015, T016, T021, T042, T044 marked done)
**Learnings**:
- xUnit1031: test methods must not use .GetAwaiter().GetResult() — make the test async instead
- SpeckitTargetComponent.ResolveOutputPath uses Path.GetFileName only — all output files land in the flat outputPath dir regardless of planned directory structure
- RouteResolutionResult is in Steergen.Core.Model, not Steergen.Core.Generation
- For integration tests using custom layout overrides, write the YAML to a temp dir and reference it via TargetConfiguration.LayoutOverridePath
- 406 total tests (56 property + 190 unit + 160 integration) all pass after this iteration
---
