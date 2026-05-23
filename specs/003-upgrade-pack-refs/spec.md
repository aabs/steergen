# Feature Specification: Upgrade External Pack References

**Feature Branch**: `[003-upgrade-pack-refs]`  
**Created**: 2026-05-23  
**Status**: Draft  
**Input**: User description: "updating rules and template packs"

## Clarifications

### Session 2026-05-23

- Q: How should the upgrade command select the target pack reference when multiple references exist in configuration? → A: Require an explicit pack selector and fail if missing or ambiguous.
- Q: What format should upgraded pins use in configuration? → A: Pin to resolved tag plus immutable commit SHA.
- Q: How should local cache content be handled during refresh? → A: Treat cache as disposable and fully replace it.
- Q: What should happen if fetch fails after purge? → A: Restore the previous cache snapshot and keep config unchanged.
- Q: What selector format should target a pack reference? → A: Use a canonical composite identifier (source plus path or entry key) resolving to exactly one reference.

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

### User Story 1 - Upgrade a Rules Pack to Current Version (Priority: P1)

As a CLI operator, I can run an upgrade command for an installed external rules pack so I can fetch the newest available version and keep my project configuration pinned to that downloaded version.

**Why this priority**: Rules changes directly affect governance behavior and policy checks, so keeping these packs current is high-value and time-sensitive.

**Independent Test**: Can be fully tested by running the upgrade command for a rules pack that has a newer remote version and verifying the local cached pack and configuration reference both move to the same new version.

**Acceptance Scenarios**:

1. **Given** a project references an external rules pack, **When** the operator runs `steergen rules-pack upgrade` for that pack without specifying a tag, **Then** the command refreshes the entire cached contents for that targeted pack reference and updates the project configuration to pin the downloaded version.
2. **Given** a project references an external rules pack, **When** the operator runs `steergen rules-pack upgrade` with a specific tag, **Then** the existing local copy for that pack reference is purged, the requested version is downloaded, and the configuration is pinned to that exact requested version.
3. **Given** the requested upgrade cannot be retrieved, **When** the command finishes, **Then** the prior pinned reference remains unchanged and the operator receives a clear failure message.

---

### User Story 2 - Upgrade a Template Pack with the Same Workflow (Priority: P2)

As a CLI operator, I can perform the same upgrade workflow for external template packs so pack maintenance is consistent across rules and templates.

**Why this priority**: Template packs are a core content dependency but are typically updated less frequently than rules packs.

**Independent Test**: Can be fully tested by running the upgrade command path for template packs and verifying identical behavior for purge, refetch, and version pin update.

**Acceptance Scenarios**:

1. **Given** a project references an external template pack, **When** the operator runs the template-pack upgrade subcommand without a tag, **Then** the command refreshes the entire cached contents for that targeted pack reference and pins the configuration to the downloaded version.
2. **Given** a project references an external template pack, **When** the operator runs the template-pack upgrade subcommand with a specific tag, **Then** the command purges the existing local copy for that reference, fetches the requested tag, and pins the configuration to that tag.

---

### User Story 3 - Safe, Predictable Upgrade Operations (Priority: P3)

As a CLI operator, I need upgrade operations to be deterministic and safe so failed or malformed upgrade attempts do not silently alter project state.

**Why this priority**: Operator trust depends on predictable outcomes, especially when remote sources are unavailable or user input is malformed.

**Independent Test**: Can be tested by running upgrades with invalid tags, unavailable sources, and malformed input and verifying fail-closed behavior plus diagnostic output.

**Acceptance Scenarios**:

1. **Given** an operator provides an invalid tag or malformed pack identifier, **When** upgrade is executed, **Then** the command fails with actionable diagnostics and makes no configuration changes.
2. **Given** two consecutive upgrade runs with the same explicit tag, **When** both complete successfully, **Then** the resulting pinned reference and local pack version are identical after each run.

---

### Edge Cases

- Upgrade command is invoked for a pack reference that is not present in the project configuration.
- Upgrade command is invoked without a required pack selector.
- Upgrade command receives a selector that matches multiple pack references.
- Upgrade command receives a selector that is not a valid canonical composite identifier.
- Local cache contains manual or unexpected file changes before upgrade.
- A requested explicit tag exists remotely but download or extraction fails midway.
- Latest-version resolution returns no valid releases.
- Local purge succeeds but remote fetch fails; command must not write a new pinned reference.
- Previous cache snapshot restoration fails after fetch failure.
- No explicit tag/ref is provided and the system cannot determine whether the local cache is stale; the full targeted cache must still be refreshed.
- Existing configuration contains multiple references to the same source with different tags; only targeted reference is updated.
- Upgrade is re-run immediately after success; no unintended drift occurs.

### Security & Misuse Cases *(mandatory)*

- How does the system handle malicious, adversarial, or malformed input?
- What prompt-injection-style payloads or instruction-conflict content could appear in input, and how must they be treated as inert data?
- What are the trust boundaries and how are unsafe operations prevented by default?
- Which failure modes must fail closed to avoid security impact?

- Pack identifiers, tags, and repository metadata from user input MUST be validated and treated as untrusted input.
- Remote pack metadata and release labels MUST be treated as inert data, not executable instructions.
- Upgrade operations MUST be constrained to approved local pack storage locations and project configuration files.
- If validation, purge, fetch, or pin update steps fail, the command MUST fail closed and preserve the previous pinned reference.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The CLI MUST provide an `upgrade` subcommand under the rules-pack command group.
- **FR-002**: The CLI MUST provide an equivalent `upgrade` subcommand under the template-pack command group.
- **FR-003**: The upgrade subcommand MUST accept an optional explicit tag argument.
- **FR-004**: If no explicit tag is provided, the upgrade subcommand MUST refresh the entire cached contents for the targeted pack reference by purging and refetching it, even when local staleness cannot be determined.
- **FR-005**: If an explicit tag is provided, the upgrade subcommand MUST purge the currently cached copy for the targeted pack reference and fetch the requested tag.
- **FR-006**: On successful fetch, the system MUST update the corresponding pack reference in `steergen.config.yaml` to pin the downloaded version.
- **FR-007**: The upgrade workflow MUST support external rules packs and external template packs with equivalent behavior and operator-facing semantics.
- **FR-008**: If any upgrade step fails, the system MUST leave `steergen.config.yaml` unchanged for the targeted reference.
- **FR-009**: The command MUST return a non-zero exit status for failed upgrades and a zero exit status for successful upgrades.
- **FR-010**: The command output MUST identify whether the operation targeted latest-version resolution or a specific tag, and must report the final pinned version on success.
- **FR-011**: The command MUST validate tag and reference input and reject malformed values before attempting remote retrieval.
- **FR-012**: The command MUST not modify unrelated pack references in `steergen.config.yaml`.
- **FR-013**: The upgrade subcommand MUST require an explicit canonical composite selector (source plus path or entry key) for the target pack reference and MUST fail when the selector is missing.
- **FR-014**: If the provided selector matches zero or multiple references, the command MUST fail with diagnostics and MUST not modify configuration or cache.
- **FR-015**: On successful upgrade, the pinned reference format in `steergen.config.yaml` MUST include both the resolved tag and the immutable commit SHA for that resolved version.
- **FR-016**: The upgrade workflow MUST treat the targeted local pack cache as disposable and MUST fully purge and replace it during refresh.
- **FR-017**: The command MUST NOT merge, preserve, or restore local cache file edits from the pre-upgrade cache state after a successful upgrade.
- **FR-018**: If fetch fails after purge, the command MUST restore the previous cache snapshot for the targeted pack reference and MUST keep `steergen.config.yaml` unchanged.
- **FR-019**: If snapshot restoration also fails, the command MUST return a non-zero exit status and emit diagnostics that explicitly identify both fetch failure and rollback failure.
- **FR-020**: The command MUST reject selectors that do not conform to the canonical composite selector format before any purge or fetch steps begin.
- **FR-021**: Canonical selector syntax MUST be `source|pathOrEntryKey`, where `source` is `github:{owner}/{repo}` and `pathOrEntryKey` is non-empty.
- **FR-022**: Selector parsing MUST trim surrounding whitespace and reject empty selector components or unescaped pipe delimiters with explicit validation diagnostics.
- **FR-023**: The selector delimiter MUST be a single unescaped pipe (`|`). A literal pipe within selector components MUST be escaped as `\|`.
- **FR-024**: Parsing MUST split on the first unescaped delimiter, unescape `\|` after split, and reject trailing backslash, empty escape sequences, or multiple unescaped delimiters with deterministic diagnostics.

Selector examples:
- Valid: `github:owner/repo|packs/security`
- Valid: `github:owner/repo|team\|security`
- Invalid: `github:owner/repo|packs|security`
- Invalid: `github:owner/repo|`

### Non-Functional Requirements *(mandatory)*

- **NFR-001 (Security)**: Upgrade input and remote metadata MUST be validated as untrusted data; invalid or suspicious values MUST be rejected before state changes.
- **NFR-002 (Determinism/Correctness)**: Re-running upgrade with the same explicit tag and same source state MUST yield the same final pinned tuple (tag plus commit SHA).
- **NFR-003 (Performance)**: For packs under 100 MB and healthy network conditions, 95% of upgrade operations MUST complete within 60 seconds.
- **NFR-004 (Robustness)**: Any failure during purge, resolution, retrieval, rollback, or pin update MUST produce actionable diagnostics and fail without partial configuration updates.
- **NFR-005 (Usability)**: Command help and output MUST clearly communicate usage, canonical selector format, targeted pack type, selected version source (explicit vs latest), and final outcome.
- **NFR-006 (Documentation)**: User documentation MUST include upgrade command usage for rules and template packs, including explicit-tag and latest-version examples.

### Key Entities *(include if feature involves data)*

- **Pack Reference**: A project configuration entry that identifies an external rules or template pack source and its pinned version.
- **Upgrade Request**: Operator-supplied intent to upgrade a specific pack reference identified by canonical composite selector, optionally including an explicit tag.
- **Resolved Version**: The selected version represented as a tuple of resolved tag and immutable commit SHA, used as the new pinned value.
- **Upgrade Result**: The observable outcome of an upgrade attempt, including success/failure, selected version mode, diagnostics, and resulting pin state.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of successful upgrade runs for rules packs result in the targeted project reference being pinned to the downloaded version.
- **SC-002**: 100% of successful upgrade runs for template packs result in the targeted project reference being pinned to the downloaded version.
- **SC-003**: In failure test scenarios (invalid tag, missing release, retrieval failure), 100% of runs leave the targeted configuration reference unchanged.
- **SC-004**: In a structured acceptance exercise with at least 10 operators, at least 90% MUST complete one latest-mode and one explicit-tag upgrade in a single command attempt without consulting source code.

### Test Strategy Expectations *(mandatory)*

- Define core invariants to validate with property-based testing.
- Specify which example-based tests are still needed and why properties are insufficient there.
- Where practical, define golden, integration, and end-to-end fixtures using plausible real-world constitution or steering rules rather than toy placeholders.
- Define required security test scenarios, including malicious input and prompt-injection-style payload validation.

- Property invariants: successful upgrade always results in a pin that matches the downloaded version; failed upgrade never changes the prior pin; failed fetch after purge restores prior cache snapshot; repeated explicit-tag upgrades converge to identical state.
- Example-based tests: explicit tag success/failure paths, latest resolution success/failure paths, and cross-command parity between rules-pack and template-pack upgrade behavior.
- Integration fixtures: project fixtures with external rules and template pack references, including multi-pack configurations where only one target should change.
- Security tests: malformed tag input, path-like injection payloads in tag or source fields, and hostile remote metadata strings treated strictly as inert text.
- Acceptance tests: run a scripted operator task set (>=10 participants) and record first-attempt success rates for latest-mode and explicit-tag flows.

## Assumptions

- Operators have permission to modify local project configuration files and local pack cache content.
- External source endpoints expose version identifiers that can be used for explicit tag retrieval and latest-version resolution.
- This feature targets upgrade behavior only; new source registration flows remain out of scope.
- Existing pull/fetch mechanisms for external packs are reused as the retrieval basis for upgrade operations.
