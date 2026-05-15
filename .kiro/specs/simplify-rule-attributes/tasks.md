# Implementation Plan: Simplify Rule Attributes

## Overview

Simplify the `SteeringRule` attribute model by replacing `Severity` with a boolean `Mandatory` flag, removing `Domain`, `Profile`, and `Supersedes` properties, and updating all dependent components (routing, parsing, validation, inspect output, templates, layouts, fixtures, documentation). Implementation follows strict test-first development: property-based tests are written before implementation code for each component layer.

## Tasks

- [x] 1. Update domain model types
  - [x] 1.1 Write property tests for SteeringRule model changes
    - Add PBT in `tests/Steergen.Core.PropertyTests/Parsing/MandatoryRoundTripProperties.cs`
    - Test that default-constructed `SteeringRule` has `Mandatory == false`
    - Test that `SteeringRule` does not expose `Severity`, `Domain`, `Profile`, or `Supersedes` properties
    - **Property 1: Mandatory attribute round-trip preservation**
    - **Validates: Requirements 1.1, 1.6**
  - [x] 1.2 Modify `SteeringRule` record
    - Remove `Severity`, `Domain`, `Profile`, `Supersedes` properties from `src/Steergen.Core/Model/SteeringRule.cs`
    - Add `public bool Mandatory { get; init; } = false;`
    - _Requirements: 1.1, 1.2, 2.1, 3.1, 4.1_
  - [x] 1.3 Modify `RouteMatchExpression` record
    - Remove `Domain`, `Severity`, `Profile` fields from `src/Steergen.Core/Model/RouteMatchExpression.cs`
    - Add `public bool? Mandatory { get; init; } = null;`
    - Update `IsEmpty` property to check only `TagsAny`, `Category`, `Mandatory`, and `SourceContext`
    - _Requirements: 2.4, 3.3, 12.1, 12.4_

- [x] 2. Update parser with backward-compatible legacy attribute handling
  - [x] 2.1 Write property tests for mandatory parsing
    - Add PBT in `tests/Steergen.Core.PropertyTests/Parsing/MandatoryParsingProperties.cs`
    - Generate random attribute strings with/without `mandatory`; verify parsed value equals `true` iff `mandatory="true"` (case-insensitive)
    - **Property 2: Mandatory parsing correctness**
    - **Validates: Requirements 1.3, 1.4**
  - [x] 2.2 Write property tests for legacy attribute backward compatibility
    - Add PBT in `tests/Steergen.Core.PropertyTests/Parsing/LegacyAttributeProperties.cs`
    - Generate rule blocks with random combinations of legacy (`severity`, `domain`, `profile`, `supersedes`) and new attributes
    - Verify parser produces valid `SteeringRule` without error, `Mandatory` defaults to `false` when absent, no model properties reflect legacy values
    - **Property 3: Legacy attribute backward compatibility**
    - **Validates: Requirements 1.5, 2.2, 3.2, 4.2, 11.1, 11.2, 11.3, 11.4**
  - [x] 2.3 Implement parser changes in `SteeringMarkdownParser`
    - Modify `src/Steergen.Core/Parsing/SteeringMarkdownParser.cs`
    - Parse `mandatory="true"` → set `Mandatory = true`; only exact string `"true"` (case-insensitive) sets true
    - Silently ignore `severity`, `domain`, `profile`, `supersedes` attributes (no error, no model population)
    - Default `Mandatory = false` when attribute is absent or has any value other than "true"
    - _Requirements: 1.3, 1.4, 1.5, 2.2, 3.2, 4.2, 11.1, 11.2, 11.3, 11.4_

- [x] 3. Update validation rules
  - [x] 3.1 Write property tests for removed diagnostics
    - Add PBT in `tests/Steergen.Core.PropertyTests/Validation/RemovedDiagnosticProperties.cs`
    - Generate random rules/corpora; verify V003, V004, V008 are never produced
    - **Property 4: Removed diagnostics never produced**
    - **Validates: Requirements 2.3, 4.3, 7.1, 7.2, 7.3**
  - [x] 3.2 Write property tests for retained diagnostics
    - Add PBT in `tests/Steergen.Core.PropertyTests/Validation/RetainedDiagnosticProperties.cs`
    - Generate rules violating V001 (missing doc ID), V002 (missing rule ID), V005 (empty body), V006 (control chars), V007 (duplicate rule IDs)
    - Verify corresponding diagnostics still fire
    - **Property 5: Retained diagnostics still fire**
    - **Validates: Requirements 7.4, 7.5**
  - [x] 3.3 Implement validator changes
    - Modify `src/Steergen.Core/Validation/SteeringValidator.cs`
    - Remove `ValidSeverities` set and V003 check (invalid severity)
    - Remove V004 check (missing domain)
    - Remove `CheckSupersededRuleReferences` method and V008 diagnostic
    - Retain V001, V002, V005, V006, V007 unchanged
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5_

- [x] 4. Checkpoint - Ensure model, parser, and validator tests pass
  - Ensure all tests pass with `dotnet test`. Ask the user if questions arise.

- [x] 5. Update route resolver and matching logic
  - [x] 5.1 Write property tests for category-based route matching
    - Add PBT in `tests/Steergen.Core.PropertyTests/Generation/CategoryRoutingProperties.cs`
    - Generate random rules and match expressions; verify `Matches(expr, rule)` depends only on `Category`, `Mandatory`, `Tags` (and `SourceContext`)
    - Verify changing `Id`, `AppliesTo`, `Deprecated`, `PrimaryText` does not affect match result
    - **Property 6: Route matching depends only on category, mandatory, and tags**
    - **Validates: Requirements 2.5, 2.7, 3.4, 5.2, 12.3, 12.4**
  - [x] 5.2 Write property tests for mandatory filter semantics
    - Add PBT in `tests/Steergen.Core.PropertyTests/Generation/MandatoryFilterProperties.cs`
    - Generate rules with random mandatory values and expressions with `Mandatory` = null/true/false
    - Verify: null matches all, true matches only mandatory rules, false matches only non-mandatory rules
    - **Property 6a: Mandatory filter semantics**
    - **Validates: Requirements 5.2**
  - [x] 5.3 Write property tests for destination template substitution
    - Add PBT in `tests/Steergen.Core.PropertyTests/Generation/DestinationSubstitutionProperties.cs`
    - Generate rules with non-null categories and templates containing `${category}`
    - Verify `${category}` resolves to rule's category value; `${domain}`, `${severity}`, `${profile}` resolve to empty string
    - **Property 7: Category template substitution**
    - **Validates: Requirements 2.8, 3.6, 5.4**
  - [x] 5.4 Write property tests for route resolution determinism
    - Add PBT in `tests/Steergen.Core.PropertyTests/Generation/RouteResolverDeterminismProperties.cs`
    - Generate random rules and layouts; call `RouteResolver.Resolve` multiple times with same inputs
    - Verify identical `RouteResolutionResult` values each time
    - **Property 10: Route resolution determinism**
    - **Validates: Requirements 5.5, 8.3**
  - [x] 5.5 Implement RouteResolver changes
    - Modify `src/Steergen.Core/Generation/RouteResolver.cs`
    - Remove domain/severity/profile matching from `Matches` method
    - Add `MatchesMandatory(bool? filter, bool ruleValue)` helper
    - Update `ConditionSpecificity` to use only `Category`, `TagsAny`, and `Mandatory`
    - Update `ResolveDestination` to substitute `${domain}`, `${severity}`, `${profile}` with empty string; `${category}` with rule's category
    - _Requirements: 2.5, 2.7, 2.8, 3.4, 3.6, 5.2, 5.4, 5.5, 12.3, 12.4_

- [x] 6. Update layout loader and YAML DTO
  - [x] 6.1 Implement LayoutOverrideLoader changes
    - Modify `src/Steergen.Core/Configuration/LayoutOverrideLoader.cs`
    - Add `bool? Mandatory` property to `RouteMatchYamlDto`
    - Update `MapMatch` to discard `Domain`, `Severity`, `Profile` values from YAML and map `Mandatory` to `RouteMatchExpression.Mandatory`
    - Retain `Domain`, `Severity`, `Profile` on DTO for backward-compatible deserialization (legacy override files don't cause parse errors)
    - _Requirements: 2.6, 3.5, 12.2_
  - [x] 6.2 Write unit tests for legacy layout YAML loading
    - Test that layout YAML with `match.domain`, `match.severity`, `match.profile` fields loads without error
    - Test that `match.mandatory: true` maps correctly to `RouteMatchExpression.Mandatory`
    - _Requirements: 2.6, 3.5, 12.2_

- [x] 7. Checkpoint - Ensure routing and layout tests pass
  - Ensure all tests pass with `dotnet test`. Ask the user if questions arise.

- [ ] 8. Update inspect JSON output
  - [x] 8.1 Write property tests for inspect JSON schema
    - Add PBT in `tests/Steergen.Core.PropertyTests/Generation/InspectJsonSchemaProperties.cs`
    - Generate random `ResolvedSteeringModel` instances
    - Verify JSON output includes `mandatory` boolean for every rule, does not include `severity`/`domain`/`profile`/`supersedes` fields, and rules are sorted by ID
    - **Property 8: Inspect JSON schema correctness**
    - **Validates: Requirements 4.4, 8.1, 8.2, 8.3**
  - [x] 8.2 Implement InspectModelWriter changes
    - Modify `src/Steergen.Core/Generation/InspectModelWriter.cs`
    - Update `InspectRuleDto` to include `Mandatory` (bool), remove `Severity`, `Domain`, `Profile`, `Supersedes`
    - Ensure rules are serialized sorted by ID
    - _Requirements: 4.4, 8.1, 8.2, 8.3_

- [x] 9. Update Scriban templates
  - [x] 9.1 Write property tests for template rendering
    - Add PBT in `tests/Steergen.Core.PropertyTests/Generation/TemplateRenderingProperties.cs`
    - Generate random rules; render through each Scriban template
    - Verify output does not contain `[Supersedes:` text; when `Mandatory` is `true`, output contains `[MANDATORY]` indicator
    - **Property 9: Template rendering excludes removed attributes and includes mandatory**
    - **Validates: Requirements 4.5, 10.1, 10.2, 10.3, 10.4, 10.5**
  - [x] 9.2 Update kiro document template
    - Modify `src/Steergen.Templates/Scriban/kiro/document.scriban`
    - Remove any `supersedes` references
    - Add `[MANDATORY]` indicator when `rule.mandatory` is true
    - _Requirements: 10.3, 10.4, 10.5_
  - [x] 9.3 Update speckit constitution template
    - Modify `src/Steergen.Templates/Scriban/speckit/constitution.scriban`
    - Remove any `supersedes` references
    - Add `[MANDATORY]` indicator when `rule.mandatory` is true
    - _Requirements: 10.1, 10.4, 10.5_
  - [x] 9.4 Update speckit module template
    - Modify `src/Steergen.Templates/Scriban/speckit/module.scriban`
    - Remove `supersedes` and `domain` references
    - Add `[MANDATORY]` indicator when `rule.mandatory` is true
    - _Requirements: 10.2, 10.4, 10.5_

- [x] 10. Update default layout YAML files
  - [x] 10.1 Update Kiro default layout
    - Modify `src/Steergen.Core/Targets/Kiro/default-layout.yaml`
    - Replace `match.domain`-based routes with `match.category`-based routes
    - Retain at least one core-anchor route per scope
    - Retain a catch-all route using `category: "*"` per scope
    - Keep fallback configuration unchanged (`mode: other-at-core-anchor`)
    - _Requirements: 6.1, 6.3, 6.4, 6.5_
  - [x] 10.2 Update Speckit default layout
    - Modify `src/Steergen.Core/Targets/Speckit/default-layout.yaml`
    - Replace `match.domain`-based routes with `match.category`-based routes
    - Retain at least one core-anchor route per scope
    - Retain a catch-all route using `category: "*"` per scope
    - Keep fallback configuration unchanged (`mode: other-at-core-anchor`)
    - _Requirements: 6.2, 6.3, 6.4, 6.5_

- [x] 11. Checkpoint - Ensure templates and layouts pass all tests
  - Ensure all tests pass with `dotnet test`. Ask the user if questions arise.

- [x] 12. Update sample corpus and test fixtures
  - [x] 12.1 Update RealisticGovernance test fixtures
    - Modify all `:::rule` blocks in `tests/Fixtures/RealisticGovernance/`
    - Replace `severity` attributes with `mandatory="true"` or omit (defaulting to false)
    - Remove `domain`, `profile`, `supersedes` attributes from all rule blocks
    - Retain realistic, plausible governance content
    - _Requirements: 9.1, 9.2, 9.3_
  - [x] 12.2 Update routing layout test fixtures
    - Update any layout YAML test fixtures to use `category`-based matching instead of `domain`-based matching
    - _Requirements: 9.4_

- [x] 13. Update routing documentation
  - [x] 13.1 Update `docs/routing-syntax.md`
    - Remove `domain` from match expression examples and field tables
    - Remove `severity` from match expression examples and field tables
    - Remove `profile` from match expression examples and field tables
    - Remove `${domain}`, `${severity}`, `${profile}` from the available context variables table
    - Document `category` as the primary routing discriminator
    - Update all worked examples to use `category`-based matching
    - Add `mandatory` to match expression field table with nullable bool semantics
    - _Requirements: 13.1, 13.2, 13.3, 13.4, 13.5_

- [x] 14. Final checkpoint - Ensure all tests pass
  - Run `dotnet test` across the entire solution
  - Run `dotnet build` to confirm no compilation errors
  - Ensure all property tests, unit tests, and integration tests pass
  - Ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation after each major layer change
- Property tests validate universal correctness properties from the design document
- Test-first development: PBT tests are written before implementation code in each task group
- The design uses C# 14 with CsCheck for property-based testing — all code examples target this stack
- Legacy attributes are silently ignored for backward compatibility (no migration tooling needed)
