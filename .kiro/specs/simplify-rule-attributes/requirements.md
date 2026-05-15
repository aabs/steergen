# Requirements Document

## Introduction

Simplify the SteeringRule attribute model by removing unused or redundant attributes (`domain`, `profile`, `supersedes`) and replacing the multi-valued `severity` attribute with a simple boolean `mandatory` flag. This reduces cognitive load for authors, simplifies routing logic, removes dead validation code, and shrinks the public surface area of the tool.

## Glossary

- **Parser**: The `SteeringMarkdownParser` component that extracts frontmatter and `:::rule` blocks from Markdown source documents into the in-memory model.
- **Validator**: The `SteeringValidator` component that checks documents and corpora for structural correctness.
- **Route_Resolver**: The `RouteResolver` component that matches a `SteeringRule` against a `TargetLayoutDefinition` to determine a single output destination.
- **Route_Planner**: The `RoutePlanner` component that resolves all rules in a corpus against a layout, applying fallback when no route matches.
- **Route_Match_Expression**: The `RouteMatchExpression` model type that defines declarative filters over rule metadata for route matching.
- **Inspect_Writer**: The `InspectModelWriter` component that serializes the resolved steering model to deterministic JSON.
- **Layout_Loader**: The `LayoutOverrideLoader` component that deserializes layout YAML into the canonical `TargetLayoutDefinition`.
- **Write_Plan_Builder**: The `WritePlanBuilder` component that groups resolved route results into per-file write plans.
- **Template_Engine**: The Scriban-based rendering pipeline that produces target-specific output files from document models.
- **Steering_Rule**: The `SteeringRule` domain record representing a single governance rule with its metadata attributes.
- **Mandatory_Flag**: A boolean attribute (`mandatory`) on `SteeringRule` indicating whether the rule is a hard requirement (true) or advisory guidance (false). Defaults to false.
- **Sample_Corpus**: The realistic governance documents in `tests/Fixtures/RealisticGovernance/` used for integration and property-based testing.

## Requirements

### Requirement 1: Replace severity with mandatory flag on SteeringRule

**User Story:** As a steering document author, I want a simple boolean `mandatory` flag instead of a multi-valued severity enum, so that rule importance is expressed clearly without ambiguity about what "warning" vs "info" means in practice.

#### Acceptance Criteria

1. THE Steering_Rule SHALL expose a boolean property `Mandatory` that defaults to `false`.
2. THE Steering_Rule SHALL NOT expose a `Severity` property.
3. WHEN a `:::rule` block contains the attribute `mandatory="true"`, THE Parser SHALL set `Mandatory` to `true` on the parsed Steering_Rule.
4. WHEN a `:::rule` block does not contain a `mandatory` attribute, THE Parser SHALL set `Mandatory` to `false` on the parsed Steering_Rule.
5. WHEN a `:::rule` block contains a `severity` attribute, THE Parser SHALL ignore the attribute without producing an error diagnostic.
6. FOR ALL valid Steering_Rule instances, parsing then inspecting SHALL preserve the `mandatory` value through the round-trip (parse → model → inspect JSON → verify value).

### Requirement 2: Remove domain attribute from SteeringRule

**User Story:** As a steering document author, I want the redundant `domain` attribute removed, so that I do not need to maintain a field that duplicates information already expressed by file organisation and routing configuration.

#### Acceptance Criteria

1. THE Steering_Rule SHALL NOT expose a `Domain` property.
2. WHEN a `:::rule` block contains a `domain` attribute, THE Parser SHALL ignore the attribute without producing an error diagnostic.
3. THE Validator SHALL NOT produce diagnostic V004 (missing domain).
4. THE Route_Match_Expression SHALL NOT expose a `Domain` field.
5. THE Route_Resolver SHALL NOT match rules based on a domain field.
6. WHEN a layout YAML contains a `match.domain` field, THE Layout_Loader SHALL ignore the field without producing a validation error.
7. THE Route_Resolver SHALL use `category` as the primary routing discriminator in place of `domain`.
8. WHEN a destination template contains `${domain}`, THE Route_Resolver SHALL substitute an empty string.

### Requirement 3: Remove profile attribute from SteeringRule

**User Story:** As a steering document author, I want the unused `profile` attribute removed from rules, so that the attribute model only contains fields that are actively used.

#### Acceptance Criteria

1. THE Steering_Rule SHALL NOT expose a `Profile` property.
2. WHEN a `:::rule` block contains a `profile` attribute, THE Parser SHALL ignore the attribute without producing an error diagnostic.
3. THE Route_Match_Expression SHALL NOT expose a `Profile` field.
4. THE Route_Resolver SHALL NOT match rules based on a profile field.
5. WHEN a layout YAML contains a `match.profile` field, THE Layout_Loader SHALL ignore the field without producing a validation error.
6. WHEN a destination template contains `${profile}`, THE Route_Resolver SHALL substitute an empty string.

### Requirement 4: Remove supersedes attribute from SteeringRule

**User Story:** As a steering document author, I want the unused `supersedes` attribute removed, so that the model is simpler and the validator does not perform cross-reference checks on a field nobody uses.

#### Acceptance Criteria

1. THE Steering_Rule SHALL NOT expose a `Supersedes` property.
2. WHEN a `:::rule` block contains a `supersedes` attribute, THE Parser SHALL ignore the attribute without producing an error diagnostic.
3. THE Validator SHALL NOT produce diagnostic V008 (supersedes reference check).
4. THE Inspect_Writer SHALL NOT include a `supersedes` field in the JSON output.
5. THE Template_Engine SHALL NOT render supersedes annotations in any target output.

### Requirement 5: Update routing to use category as primary discriminator

**User Story:** As a platform operator, I want routing to use `category` as the primary discriminator now that `domain` is removed, so that rules are still routed to meaningful output files.

#### Acceptance Criteria

1. THE Route_Match_Expression SHALL retain the `Category` field as a list of matchable values.
2. WHEN a layout route specifies `match.category`, THE Route_Resolver SHALL match rules whose `Category` value is contained in the filter list.
3. THE default layout YAML for each built-in target SHALL use `category`-based matching for core anchor routes and catch-all routes.
4. WHEN a destination template contains `${category}`, THE Route_Resolver SHALL substitute the rule's category value.
5. THE Route_Planner SHALL produce deterministic results when multiple rules share the same category.

### Requirement 6: Update default layout YAML files

**User Story:** As a platform operator, I want the built-in default layout YAML files updated to remove domain-based matching and use category-based routing, so that the tool works correctly after the attribute removal.

#### Acceptance Criteria

1. THE Kiro target default layout SHALL route rules using `match.category` instead of `match.domain`.
2. THE Speckit target default layout SHALL route rules using `match.category` instead of `match.domain`.
3. EACH default layout SHALL retain at least one core-anchor route per scope.
4. EACH default layout SHALL retain a catch-all route using `category: "*"` per scope.
5. THE fallback configuration SHALL remain unchanged (mode: other-at-core-anchor).

### Requirement 7: Update validation rules

**User Story:** As a developer, I want the validator updated to reflect the simplified attribute model, so that validation produces correct diagnostics for the new schema.

#### Acceptance Criteria

1. THE Validator SHALL NOT check for valid severity values (remove V003 diagnostic).
2. THE Validator SHALL NOT check for missing domain (remove V004 diagnostic).
3. THE Validator SHALL NOT perform supersedes cross-reference checks (remove V008 diagnostic).
4. THE Validator SHALL retain all other existing validation checks (V001, V002, V005, V006, V007).
5. WHEN a rule has `Mandatory` set to `true` or `false`, THE Validator SHALL accept both values without diagnostic.

### Requirement 8: Update inspect JSON output

**User Story:** As a developer using `steergen inspect`, I want the JSON output to reflect the simplified model, so that tooling consumers see the correct schema.

#### Acceptance Criteria

1. THE Inspect_Writer SHALL include a `mandatory` boolean field for each rule in the JSON output.
2. THE Inspect_Writer SHALL NOT include `severity`, `domain`, `profile`, or `supersedes` fields in the rule JSON output.
3. THE Inspect_Writer SHALL produce deterministic JSON output sorted by rule ID.

### Requirement 9: Update sample corpus and test fixtures

**User Story:** As a developer, I want the test fixtures updated to use the new simplified attributes, so that all tests exercise the current schema rather than the legacy one.

#### Acceptance Criteria

1. EACH `:::rule` block in the Sample_Corpus SHALL use `mandatory="true"` or omit the mandatory attribute (defaulting to false) instead of specifying `severity`.
2. EACH `:::rule` block in the Sample_Corpus SHALL NOT contain `domain`, `profile`, or `supersedes` attributes.
3. THE Sample_Corpus SHALL retain realistic, plausible governance content representative of real-world steering documents.
4. THE routing layout test fixtures SHALL use `category`-based matching instead of `domain`-based matching.

### Requirement 10: Update Scriban templates

**User Story:** As a platform operator, I want the Scriban templates updated to remove references to removed attributes, so that generated output files do not contain stale or broken template expressions.

#### Acceptance Criteria

1. THE speckit constitution template SHALL NOT reference `supersedes` in rule rendering.
2. THE speckit module template SHALL NOT reference `supersedes` or `domain` in rule rendering.
3. THE kiro document template SHALL NOT reference `supersedes` in rule rendering.
4. EACH template SHALL render the `mandatory` flag as a visual indicator when the rule is mandatory.
5. THE Template_Engine SHALL produce valid output for rules with `mandatory` set to both `true` and `false`.

### Requirement 11: Backward-compatible parsing of legacy documents

**User Story:** As a user with existing steering documents, I want the parser to silently ignore removed attributes in legacy documents, so that I can migrate incrementally without breaking my workflow.

#### Acceptance Criteria

1. WHEN a source document contains `severity`, `domain`, `profile`, or `supersedes` attributes, THE Parser SHALL parse the document without error.
2. THE Parser SHALL NOT populate any removed fields on the Steering_Rule model from legacy attributes.
3. WHEN a legacy document is parsed, THE Parser SHALL produce a valid Steering_Rule with `Mandatory` defaulting to `false`.
4. FOR ALL documents containing any combination of legacy and new attributes, THE Parser SHALL produce a deterministic parse result.

### Requirement 12: Remove severity from routing match expressions

**User Story:** As a platform operator, I want the routing match expression to no longer support severity-based matching, so that the routing model is consistent with the simplified rule model.

#### Acceptance Criteria

1. THE Route_Match_Expression SHALL NOT expose a `Severity` field.
2. WHEN a layout YAML contains a `match.severity` field, THE Layout_Loader SHALL ignore the field without producing a validation error.
3. THE Route_Resolver specificity calculation SHALL NOT include severity in its scoring.
4. THE Route_Match_Expression `IsEmpty` check SHALL NOT consider severity.

### Requirement 13: Update routing documentation

**User Story:** As a user reading the routing syntax reference, I want the documentation to reflect the simplified attribute model, so that I can write correct layout overrides.

#### Acceptance Criteria

1. THE routing syntax documentation SHALL remove `domain` from match expression examples and field tables.
2. THE routing syntax documentation SHALL remove `severity` from match expression examples and field tables.
3. THE routing syntax documentation SHALL remove `profile` from match expression examples and field tables.
4. THE routing syntax documentation SHALL remove `${domain}`, `${severity}`, and `${profile}` from the available context variables table.
5. THE routing syntax documentation SHALL document `category` as the primary routing discriminator.
