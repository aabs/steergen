# Feature Specification: Dynamic Target Layout Engine

**Feature Branch**: `002-dynamic-target-layout-engine`  
**Created**: 2026-04-05  
**Status**: Draft  
**Input**: User description: "Dynamic Target Layout Engine"

## User Scenarios & Testing *(mandatory)*

<!--
  IMPORTANT: User stories should be PRIORITIZED as user journeys ordered by importance.
  Each user story/journey must be INDEPENDENTLY TESTABLE - meaning if you implement just ONE of them,
  you should still have a viable MVP (Minimum Viable Product) that delivers value.
  
  Assign priorities (P1, P2, P3, etc.) to each story, where P1 is the most critical.
  Think of each story as a standalone slice of functionality that can be:
  - Developed independently
  - Tested independently
  - Deployed independently
  - Demonstrated to users independently
-->

### User Story 1 - Route Rules to Target-Specific Layouts (Priority: P1)

As a tool user, I can define target-specific layout conventions and routing rules so that generated steering content lands in each target system's required directory and filename structure instead of a generic target folder.

**Why this priority**: Without deterministic routing to target-native locations, generated output is not usable by real target systems.

**Independent Test**: Can be fully tested by configuring one target with multiple routing rules and verifying each generated rule is written to exactly one expected file path under that target's declared layout.

**Acceptance Scenarios**:

1. **Given** a target layout with path variables and routing rules by rule attributes, **When** generation runs, **Then** output files are created at target-native locations resolved from those variables.
2. **Given** two matching candidate routes for a rule, **When** generation resolves routing, **Then** exactly one deterministic route is selected according to configured precedence and no duplicate rule placement occurs.
3. **Given** no explicit output folder setting, **When** generation runs for a target, **Then** the target's layout definition determines all destination paths.

---

### User Story 2 - Override Standard Layout with User YAML (Priority: P2)

As a tool user, I can link a user-provided YAML override to a target configuration so that project-specific routing and naming conventions can replace or extend target defaults.

**Why this priority**: Teams need customization of target conventions without changing built-in target definitions.

**Independent Test**: Can be fully tested by defining a built-in layout, linking an override YAML for one target, and verifying generated outputs follow override paths while other targets continue using defaults.

**Acceptance Scenarios**:

1. **Given** a target with a linked override YAML, **When** generation runs, **Then** override routing and layout values are used for that target.
2. **Given** an invalid override YAML, **When** generation starts, **Then** generation fails before writing output files and returns actionable diagnostics.

---

### User Story 3 - Safe and Predictable File Lifecycle (Priority: P3)

As an operator, I can trust that generation truncates only files selected for current output and appends routed rules deterministically within those files, avoiding stale or duplicated content.

**Why this priority**: Reliable repeated generation requires stable output lifecycle and deterministic ordering.

**Independent Test**: Can be fully tested by running generation twice with unchanged input and verifying identical stable output content, then changing one rule route and verifying only impacted files are truncated and rewritten.

**Acceptance Scenarios**:

1. **Given** a prior generation run and a new run with unchanged inputs, **When** generation completes, **Then** stable generated content is identical and no additional duplicate entries are introduced.
2. **Given** at least one file receives routed rules in a run, **When** generation starts writing that file, **Then** the file is truncated once at run start and then repopulated by appending routed content in deterministic order.
3. **Given** a rule cannot be routed to exactly one destination, **When** generation evaluates routing, **Then** generation fails closed and reports the conflicting or missing route cause.

---

### Edge Cases

- A rule matches no route after evaluating explicit and fallback routing rules.
- A rule matches more than one route due to overlapping conditions.
- Two routes resolve to the same destination path and the collision policy is inconsistent.
- Path variable expansion produces an empty, invalid, or non-normalized path.
- User-defined domain and tag names contain characters requiring path sanitization.
- Override YAML references variables not defined by standard or target-specific context.
- A run includes only project rules or only global rules while target layout expects both scopes.

### Security & Misuse Cases *(mandatory)*

- Malformed layout/routing configuration (invalid syntax, unknown fields, bad conditions) must fail before any output writes.
- Path traversal attempts through variables or configuration (for example `../`, absolute path injection where not allowed, home-directory escape when disallowed) must be rejected.
- Rule text and metadata may include instruction-like or prompt-injection-like content; such content must be treated as inert data and must not alter routing logic or execution flow.
- Trust boundaries must separate built-in target layouts from user override files; override input is untrusted and must be fully validated.
- If a rule cannot be resolved to exactly one valid destination, generation must fail closed for that target rather than guessing or duplicating output.

## Requirements *(mandatory)*

<!--
  ACTION REQUIRED: The content in this section represents placeholders.
  Fill them out with the right functional requirements.
-->

### Functional Requirements

- **FR-001**: System MUST support per-target layout definitions that declare destination path conventions without using a single shared output-folder concept.
- **FR-002**: System MUST provide pre-populated routing variables that include standard context variables (including input filename and root context values) and allow target-specific derived variables.
- **FR-003**: System MUST support routing conditions based on rule metadata (including domain, tags, category, severity, profile, and source context) without privileging any domain name except `core` as an allowed predefined value.
- **FR-004**: System MUST allow explicit routing conditions that, when matched, take precedence over non-explicit fallback routing.
- **FR-005**: System MUST route every generated rule to exactly one destination file for a given target and MUST reject any routing result of multiple destinations.
- **FR-006**: System MUST support deterministic route precedence so the same inputs and configuration always produce the same selected destination per rule.
- **FR-007**: System MUST support user-provided target layout override files linked from the main configuration, with per-target scope.
- **FR-008**: System MUST apply user override files using deep-merge semantics where override values take precedence over target defaults.
- **FR-009**: System MUST validate override files and routing rules before generation writes files and MUST return actionable diagnostics for invalid configuration.
- **FR-010**: System MUST truncate each destination file selected for write once at generation start, then append routed content for that file in deterministic order.
- **FR-011**: System MUST leave files not selected by the current route plan unchanged.
- **FR-012**: System MUST sanitize and normalize resolved output paths and reject invalid or unsafe destinations.
- **FR-013**: System MUST provide diagnostic output that identifies, at minimum, route match source, selected destination, and reason for routing failures.
- **FR-014**: System MUST preserve compatibility for existing targets by providing target default layouts that reproduce current behavior when no override is supplied.
- **FR-015**: System MUST include a dedicated user-facing routing syntax reference document under the `docs` directory that defines routing grammar, variables, precedence rules, single-destination constraints, and examples in a way that is applicable to users of any target SDD framework.
- **FR-016**: If a rule does not match any explicit or normal routing rule, the system MUST route it to an `other` file (`other.md` or target-appropriate extension) located in the same destination location used for `core` rules for that target and scope.
- **FR-017**: System MUST provide each target's default routing rules and default layout configuration as a YAML file stored under that target's source folder in the codebase, so users can use it as a baseline for personalization and as reference documentation.
- **FR-018**: System MUST provide a CLI command that purges generated steering files for selected targets on demand, so operators can remove stale files left behind after routing/layout changes.
- **FR-019**: The purge command MUST support scoped execution (single target or multiple targets) and MUST only remove files classified as generated steering artifacts for those targets.
- **FR-020**: The purge command MUST report what was removed and what was skipped, and MUST return a non-success result when a requested target cannot be purged safely.
- **FR-021**: Each target MUST define at least one `core` route anchor; if a `core` route anchor is missing for a target/scope, generation MUST fail for that target/scope.
- **FR-022**: Purge eligibility MUST be determined from target-configured file globs under that target's configured root paths; only files matching those configured globs are eligible for deletion.
- **FR-023**: If a target has no purge glob configured, purge MUST perform no deletions for that target and report a no-op outcome.
- **FR-024**: Purge MUST operate from configured patterns even when no prior manifest exists.

### Non-Functional Requirements *(mandatory)*

- **NFR-001 (Security)**: Routing and layout evaluation MUST reject unsafe path resolution and untrusted override content by default, and must not execute or interpret rule content as instructions.
- **NFR-002 (Determinism/Correctness)**: For identical inputs and configuration, route selection and generated file content ordering MUST be deterministic across repeated runs.
- **NFR-003 (Performance)**: Route evaluation and write planning for 1,000 rules across 4 targets MUST complete within 3 seconds on reference developer hardware, excluding external IO contention.
- **NFR-004 (Robustness)**: Any routing ambiguity, invalid variable, malformed override, or unresolved fallback destination MUST fail before partial writes for the affected target.
- **NFR-008 (Operational Safety)**: Purge operations MUST be deterministic and auditable, and must limit deletion strictly to files matched by configured purge globs within configured target roots.
- **NFR-005 (Usability)**: Operators MUST be able to understand and troubleshoot routing outcomes from CLI diagnostics without reading source code.
- **NFR-006 (Documentation)**: Release documentation MUST include default layouts per target, override examples, variable reference, conflict-resolution behavior, and a framework-agnostic routing syntax reference under `docs`.
- **NFR-007 (Configuration Transparency)**: The shipped per-target default YAML configuration files MUST remain human-readable, versioned, and discoverable from user-facing documentation.

### Key Entities *(include if feature involves data)*

- **Target Layout Definition**: Declares a target's default destination structure, available variables, fallback behavior, and file lifecycle policy.
- **Routing Rule**: A condition-to-destination mapping that matches steering rule metadata and resolves one destination path template.
- **Routing Context**: Resolved values used during matching and path templating (for example filename, scope roots, and target-specific derived values).
- **Route Resolution Result**: The deterministic outcome for one steering rule including matched rule identity, selected destination, and precedence rationale.
- **Target Override Definition**: User-provided YAML configuration linked from main config to override or extend target default layout and routing behavior.
- **Write Plan**: Ordered set of destination files and appended content units produced for a target in one generation run.

## Success Criteria *(mandatory)*

<!--
  ACTION REQUIRED: Define measurable success criteria.
  These must be technology-agnostic and measurable.
-->

### Measurable Outcomes

- **SC-001**: 100% of routed rules in validation fixtures are mapped to exactly one destination file per target, with zero duplicate placements.
- **SC-002**: Re-running generation twice with unchanged inputs produces identical stable output content for all written files in at least 99.9% of regression runs.
- **SC-003**: 100% of invalid routing configurations in negative test fixtures fail before file writes and return diagnostics that identify the failing rule and reason.
- **SC-004**: At least 3 distinct target layout conventions (for example workspace-local, user-home global, and mixed-scope layouts) are supported in acceptance fixtures without custom code changes.
- **SC-005**: A routing syntax reference document under `docs` is published for the release and reviewed to confirm that all routing examples are understandable without target-specific implementation knowledge.
- **SC-006**: In acceptance fixtures that include unmatched rules, 100% of unmatched rules are routed to the target-appropriate `other` file in the same location as `core` routing, with no unmapped rules remaining.
- **SC-007**: 100% of supported targets have a checked-in default YAML configuration artifact that can be copied and adapted by users, and release documentation links to each artifact.
- **SC-008**: In acceptance scenarios where routing definitions change, running the purge command removes 100% of stale generated files for selected targets before regeneration, with zero unintended deletions of non-generated files.
- **SC-009**: 100% of supported targets publish default YAML routing/layout artifacts under their source folders, and documentation links to those files.
- **SC-010**: In purge acceptance fixtures without prior manifests, configured purge globs still remove matching stale files while targets without configured globs report no-op and remove zero files.

### Test Strategy Expectations *(mandatory)*

- Property-based tests must validate single-destination routing invariants, deterministic precedence, and route-plan stability under permuted rule order.
- Example-based tests must cover explicit-route precedence, fallback routing, override-link behavior, and conflict diagnostics where properties alone are insufficient for readability and expected-path assertions.
- Golden and integration fixtures must use plausible real steering corpora with user-defined domains/tags (not toy-only fixtures), including mixed global/project scope layouts.
- Security tests must include path traversal payloads, invalid variable injection, malformed override YAML, and instruction-like rule content treated strictly as inert text.

## Assumptions

- Target definitions provide a default layout profile so existing users are not forced to author custom routing files immediately.
- `core` remains a reserved, valid domain label, but no other domain or tag names are treated as privileged.
- Route destination templates are resolved from validated variables only; unknown variables are treated as configuration errors.
- Initial release scope covers deterministic file-based routing and layout resolution, not live synchronization with external target system APIs.
- Each target default profile declares the configured root paths and purge globs needed for safe on-demand cleanup.
