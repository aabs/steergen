# Feature Specification: Steering Document Transformation Tool

**Feature Branch**: `001-steering-doc-transform`  
**Created**: 2026-04-03  
**Status**: Draft  
**Input**: User description: "Steering Document Transformation Tool"

## Clarifications

### Session 2026-04-03

- Q: What logging and diagnostic output should the `steergen` tool provide? → A: Silent by default; optional `--verbose`/`--debug` diagnostics.
- Q: How should `steergen` handle concurrent modifications to the configuration file? → A: Use optimistic locking with conflict detection before write; fail safely with a clear conflict error.
- Q: What is the security and secrets-handling design for the configuration file? → A: No secrets are supported in the config file for v1.
- Q: What reliability target model should apply for v1? → A: No additional explicit reliability SLO targets beyond existing command correctness and failure handling requirements.
- Q: Should scalability limits be formalized as a supported envelope? → A: Yes; define a validated support envelope with graceful warning/failure behavior beyond it.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Compile Steering Documents to Speckit Artefacts (Priority: P1)

A governance author or tooling engineer has one or more steering documents (Markdown files with embedded rule blocks) and wants to produce Speckit-compatible Markdown artefacts for consumption by downstream tooling. They run the `compile` command, which discovers all documents, validates them, applies profile filtering and overlay resolution, and writes the output artefacts to the configured output directory.

**Why this priority**: This is the core value proposition of the tool. Without the ability to compile steering documents into consumable artefacts, no other feature matters. All other user stories depend on or extend this capability.

**Independent Test**: Can be fully tested by providing a directory of valid steering documents and verifying that the output directory contains correctly structured Speckit Markdown files matching the document content.

**Acceptance Scenarios**:

1. **Given** a valid configuration file and a directory containing well-formed steering documents, **When** the user runs `compile`, **Then** the tool produces Speckit Markdown artefacts in the configured output directory without errors and exits with code 0.
2. **Given** both global and project-level steering documents where the project document defines a rule with the same ID as a global rule, **When** the user runs `compile`, **Then** the project rule replaces the global rule in the output artefacts.
3. **Given** a configuration specifying an active profile (e.g., `clientA`), **When** the user runs `compile`, **Then** only rules whose profile matches the active profile set or the `default` profile are included in the output.
4. **Given** a steering document containing deprecated rules, **When** the user runs `compile`, **Then** deprecation metadata is preserved in the output artefacts.
5. **Given** multiple steering documents with rules referencing `supersedes` relationships, **When** the user runs `compile`, **Then** supersession metadata is preserved in the output artefacts.

---

### User Story 2 - Compile Steering Documents to Kiro IDE Steering Files (Priority: P2)

A governance author or tooling engineer has steering documents and wants to produce Kiro IDE-compatible steering files for use in one or more Kiro workspaces. They run the `compile` command with the Kiro target enabled, which generates one Markdown steering file per source document in the configured output directory. Each output file contains YAML frontmatter specifying the appropriate Kiro inclusion mode and prose-rendered rule content — ready to be placed in a `.kiro/steering/` directory.

**Why this priority**: Kiro IDE's steering format is a first-class output target with meaningfully different semantics from Speckit Markdown outputs. Unlike Kiro's prose-first steering files, Speckit preserves structured guidance representation in Markdown modules. Both output formats are primary deliverables, making this story equally critical to Story 1.

**Independent Test**: Can be fully tested by providing a directory of valid steering documents and verifying that the output directory contains one Markdown file per source document, with correctly structured YAML frontmatter and prose-rendered rule content, with no structured IDs, severity fields, or rule block syntax appearing in the output.

**Acceptance Scenarios**:

1. **Given** a valid configuration with the Kiro target enabled and a directory of well-formed steering documents, **When** the user runs `compile`, **Then** the tool produces one Markdown file per source document in the configured output directory, each containing valid YAML frontmatter and prose-rendered rule content, and exits with code 0.
2. **Given** a steering document whose rules have no explicit `inclusion` mapping configured, **When** the Kiro adapter generates the output file, **Then** the frontmatter defaults to `inclusion: always`.
3. **Given** a steering document whose target configuration specifies `inclusion: fileMatch` and a `fileMatchPattern`, **When** the Kiro adapter generates the output file, **Then** the frontmatter contains `inclusion: fileMatch` and the configured `fileMatchPattern` value.
4. **Given** a steering document whose target configuration specifies `inclusion: auto`, **When** the Kiro adapter generates the output file, **Then** the frontmatter contains `inclusion: auto`, a `name` field derived from the document title, and a `description` field derived from the document description.
5. **Given** a steering document with multiple rules, **When** the Kiro adapter generates the output file, **Then** each rule's primary directive and explanatory material are rendered as natural language prose paragraphs; no rule IDs, severity values, or `:::rule` syntax appear in the output.
6. **Given** a steering document containing deprecated rules, **When** the Kiro adapter generates the output file, **Then** deprecated rules are excluded from the prose output.
7. **Given** both global and project-level steering documents where the project document defines a rule with the same ID as a global rule, **When** the user runs `compile` with the Kiro target, **Then** the project rule's prose replaces the global rule's prose in the output file.

---

### User Story 3 - Compile Steering Guidance to Agent Specification Files (Priority: P3)

A governance author or tooling engineer wants to generate agent-specification files for a chosen AI platform from the same neutral steering model. They run `compile` with an agent-spec target (for example `copilot-agent`, `kiro-agent`, or `agents-md`), and the tool emits target-specific agent instruction files that preserve platform-compatible semantics.

**Why this priority**: Agent specification files are a core output class alongside Speckit and Kiro steering. Platform formats differ materially (for example Kiro agent format differs from Copilot agent format), so the transformation pipeline must support format-specific generation from shared source guidance.

**Independent Test**: Can be fully tested by running `compile` for two distinct agent-spec targets against the same steering corpus and verifying both outputs are syntactically valid for their platforms and semantically consistent with the source guidance.

**Acceptance Scenarios**:

1. **Given** a valid configuration with `copilot-agent` target enabled, **When** the user runs `compile`, **Then** the tool generates Copilot-compatible agent specification files in the configured output path.
2. **Given** a valid configuration with `kiro-agent` target enabled, **When** the user runs `compile`, **Then** the tool generates Kiro-compatible agent specification files in the configured output path.
3. **Given** the same source steering guidance and profile selection, **When** the user compiles for multiple agent targets, **Then** target-specific syntax differs but the normative guidance semantics remain equivalent.
4. **Given** a target-specific required metadata field is missing for an agent target, **When** `compile` runs, **Then** the tool reports a target generation error and exits with code 3.

---

### User Story 4 - Validate Steering Documents (Priority: P4)

A governance author wants to check that their steering documents are well-formed before committing them to a repository or triggering a CI pipeline. They run the `validate` command, which checks document structure, rule uniqueness, severity values, and profile references, and reports any errors with source locations.

**Why this priority**: Validation is a critical quality gate that prevents malformed documents from propagating to artefact generation. It is independently useful as a pre-commit check and CI step.

**Independent Test**: Can be fully tested by providing a set of documents—some valid and some containing deliberate errors (duplicate rule IDs, invalid severity values, missing required fields)—and verifying that the tool reports the correct errors with document identifiers and source locations.

**Acceptance Scenarios**:

1. **Given** a directory of valid steering documents, **When** the user runs `validate`, **Then** the tool reports no errors and exits with code 0.
2. **Given** a steering document missing the required `id` or `version` frontmatter fields, **When** the user runs `validate`, **Then** the tool reports a schema error referencing the document and exits with code 1.
3. **Given** two rules with the same ID after overlay resolution, **When** the user runs `validate`, **Then** the tool reports a duplicate ID error with source locations for both rules.
4. **Given** a rule with an unrecognised severity value, **When** the user runs `validate`, **Then** the tool reports a validation error identifying the rule and its source location.
5. **Given** a rule whose `supersedes` attribute references a non-existent rule ID, **When** the user runs `validate`, **Then** the tool emits a warning (not an error) and exits with code 0.

---

### User Story 5 - Inspect the Merged Steering Model (Priority: P5)

A tooling engineer or integrator wants to inspect the fully merged and resolved steering model (after overlay resolution and profile filtering) without generating target artefacts. They run the `inspect` command to output the merged model as structured data for debugging and integration purposes.

**Why this priority**: The inspect command enables debugging of complex overlay and profile scenarios and supports integration with other tooling that consumes the model. It is independently useful without requiring full compilation.

**Independent Test**: Can be fully tested by running `inspect` on a set of documents with known overlays and profile configurations, then verifying that the output JSON precisely reflects the expected merged model.

**Acceptance Scenarios**:

1. **Given** a valid configuration and documents, **When** the user runs `inspect`, **Then** the tool outputs the merged steering model as JSON to stdout and exits with code 0.
2. **Given** global and project documents with overlapping rule IDs, **When** the user runs `inspect`, **Then** the output reflects the resolved model with project rules replacing global rules.
3. **Given** a profile filter via `--profile`, **When** the user runs `inspect`, **Then** the output contains only rules matching the active profile set.

---

### User Story 6 - Extend the Tool with a New Output Target (Priority: P6)

A tooling engineer wants to add support for a new SDD output format (e.g., a different artefact schema or a proprietary governance platform) without modifying the core tool or refactoring existing target components. They implement and register a new target component in-repo, and the tool invokes it during compilation.

**Why this priority**: Extensibility is a stated architectural requirement. Without a verified non-plugin extension seam, the tool cannot scale to many targets while preserving stability and maintainability.

**Independent Test**: Can be fully tested by implementing a minimal test adapter that writes a fixed file, registering it, and verifying that `compile` invokes it and produces the expected output.

**Acceptance Scenarios**:

1. **Given** a new target component registered via static target registration metadata, **When** the user runs `compile` with that target enabled, **Then** the tool invokes the target component and includes its output in the output directory.
2. **Given** an adapter that returns a generation error, **When** the user runs `compile`, **Then** the tool reports the error and exits with code 3.
3. **Given** multiple targets enabled in configuration, **When** the user runs `compile`, **Then** all enabled targets produce output; disabled targets are skipped.
4. **Given** an existing target suite (Speckit, Kiro, and agent targets), **When** a new target is added, **Then** no existing target implementation files require refactoring to enable the new target.

---

### User Story 7 - Integrate with CI Pipelines (Priority: P7)

A DevOps or governance engineer wants to integrate the tool into a CI pipeline so that steering document changes are validated and compiled automatically on every commit, with appropriate failure signals for the pipeline.

**Why this priority**: CI integration is the primary delivery mechanism for governance workflows. The tool must produce reliable, machine-readable exit codes to serve as a CI gate.

**Independent Test**: Can be fully tested by scripting a CI-like sequence (`validate` then `compile`) and verifying that exit codes are correct for both success and failure scenarios.

**Acceptance Scenarios**:

1. **Given** a CI step running `validate`, **When** all documents are valid, **Then** the tool exits with code 0, allowing the pipeline to continue.
2. **Given** a CI step running `validate`, **When** a document has validation errors, **Then** the tool exits with code 1, causing the pipeline to fail.
3. **Given** a CI step running `compile`, **When** a configuration error is present, **Then** the tool exits with code 2, causing the pipeline to fail.
4. **Given** identical steering document inputs across two pipeline runs, **When** both runs execute `compile`, **Then** the output artefacts are byte-for-byte identical (deterministic output).

---

### User Story 8 - Initialize Project Structure via CLI (Priority: P8)

A tooling engineer wants to bootstrap a repository for steering generation by creating required steering and target output folders through a single CLI command. They run `steergen init <project-root> --target <target>...` and the tool initializes missing folders/files without overwriting existing artifacts.

**Why this priority**: Initialization removes setup friction and ensures a consistent baseline structure for teams adopting multiple target platforms.

**Independent Test**: Can be fully tested by running `steergen init . --target speckit --target kiro-ide` in an empty project and verifying expected directories/files are created, then re-running the same command and verifying no existing content is overwritten.

**Acceptance Scenarios**:

1. **Given** a project root path and one or more `--target` arguments, **When** the user runs `steergen init <root> --target <target>...`, **Then** the tool creates initial steering document location(s) and target platform folder structure for each specified target if missing.
2. **Given** existing steering and target folders, **When** the user re-runs `steergen init` with the same targets, **Then** the command is idempotent and does not overwrite existing files.
3. **Given** multiple targets in one invocation, **When** init runs, **Then** each valid target's folder structure is initialized in the same command execution.
4. **Given** an unknown target identifier, **When** init runs, **Then** the tool reports a clear validation error and exits non-zero.

---

### User Story 9 - Update Project Templates and Metadata In Place (Priority: P9)

A tooling engineer wants to refresh the local project template pack and metadata without upgrading the CLI binary. They run `steergen update` to pull and apply the latest compatible template/metadata release, or provide an explicit version to pin to a specific release.

**Why this priority**: Template and metadata evolution happens more frequently than CLI binary releases. Decoupling these update streams keeps projects current with lower operational friction.

**Independent Test**: Can be fully tested by running `steergen update` in an initialized project and verifying template/metadata versions advance while the CLI binary version remains unchanged, then running `steergen update --version <x.y.z>` and verifying the requested template/metadata version is applied.

**Acceptance Scenarios**:

1. **Given** an initialized project, **When** the user runs `steergen update`, **Then** the tool updates local templates and metadata in place to the latest available compatible release.
2. **Given** an initialized project and a valid version argument, **When** the user runs `steergen update --version <x.y.z>`, **Then** the tool applies templates and metadata for that specific version.
3. **Given** an invalid or unavailable version argument, **When** update runs, **Then** the tool reports a clear version resolution error and exits non-zero.
4. **Given** a fresh CLI install, **When** the tool is first run for project initialization, **Then** templates and metadata are updated to the latest available release by default.
5. **Given** subsequent tool usage after initial install, **When** no update command is invoked, **Then** templates and metadata remain at the currently installed template-pack version.

---

### User Story 10 - Execute Generation and Manage Target Registration via CLI (Priority: P10)

A tooling engineer wants to run generation from the CLI and manage target registration directly in the same command system. They run `steergen run` to invoke generation and may use `--target` to scope generation, or omit `--target` to generate for all registered targets. They run `steergen target add <target-id>` to register a new target and initialize required folders.

**Why this priority**: Day-to-day usability requires a direct execution command and a clear target-registration workflow so teams can add and run targets without manual metadata edits.

**Independent Test**: Can be fully tested by registering a new target with `steergen target add <target-id>`, then running `steergen run` with and without `--target` and verifying generation scope and output folders match expectations.

**Acceptance Scenarios**:

1. **Given** one or more registered targets, **When** the user runs `steergen run` without `--target`, **Then** generation runs for all registered targets.
2. **Given** one or more registered targets, **When** the user runs `steergen run --target <target-id>`, **Then** generation runs only for the specified target(s).
3. **Given** an unregistered target supplied to `run --target`, **When** the command executes, **Then** the tool exits non-zero with a clear target resolution error.
4. **Given** a new target identifier, **When** the user runs `steergen target add <target-id>`, **Then** the target is added to the registration metadata and required target folders are created if missing.
5. **Given** an already registered target, **When** `steergen target add <target-id>` is re-run, **Then** the command is idempotent and does not duplicate registration entries or overwrite existing target files.
6. **Given** a registered target identifier, **When** the user runs `steergen target remove <target-id>`, **Then** the target is removed from registration metadata and no longer selected by default `steergen run` execution, and previously generated steering artefacts for that target remain untouched.

---

### Edge Cases

- What happens when a steering document contains no rule blocks (only prose)?
- How does the tool handle a rule block with malformed attribute syntax (unclosed quotes, unknown attributes)?
- What happens when the `globalRoot` or `projectRoot` directories do not exist?
- How does the tool behave when the same document ID appears in both global and project roots?
- What happens when a circular `supersedes` chain is detected between rules?
- How does the tool handle a `version` field that is not valid semver?
- What happens when the output directory is not writable?
- How does the tool behave when no profiles are specified in configuration?

## Requirements *(mandatory)*

### Functional Requirements

#### Parsing, Validation, and Core Model

- **FR-001**: The tool MUST parse Markdown steering documents containing optional YAML frontmatter and embedded rule blocks using the `:::rule` fenced syntax.
- **FR-002**: The tool MUST extract and validate frontmatter fields `id` (required) and `version` (required, semantic version) from each document.
- **FR-003**: The tool MUST parse rule block attributes: `id` (required), `severity`, `category`, `domain`, `profile`, `appliesTo`, `tags`, `deprecated`, and `supersedes`.
- **FR-004**: The tool MUST treat the first paragraph of a rule block body as the primary directive and all subsequent content as explanatory material.
- **FR-005**: The tool MUST load documents from both a global root and a project root as specified in configuration.
- **FR-006**: The tool MUST resolve overlays by replacing any global rule with a project rule sharing the same `id`.
- **FR-007**: The tool MUST filter rules by active profile: include rules whose `profile` matches any active profile name or is `default`.
- **FR-008**: The tool MUST enforce globally unique rule IDs after overlay resolution and profile filtering, reporting errors for duplicates.
- **FR-009**: The tool MUST validate severity values against the permitted enumeration: `info`, `low`, `medium`, `high`, `critical`.
- **FR-010**: The tool MUST emit a warning (not an error) when a `supersedes` attribute references a rule ID not present in the resolved model.
- **FR-011**: The tool MUST preserve deprecation metadata and supersession relationships in transformed output.

#### Core Commands and Runtime Behavior

- **FR-012**: The tool MUST support a `compile` command that runs the full pipeline: load → validate → overlay → profile filter → generate artefacts.
- **FR-013**: The tool MUST support a `validate` command that runs load and validation steps only, reporting all errors with document ID and source location.
- **FR-014**: The tool MUST support an `inspect` command that outputs the fully resolved steering model as JSON to stdout.
- **FR-015**: The tool MUST read configuration from a single YAML-based configuration file (`steergen.config.yaml`) in the working directory by default, overridable via `--config`.
- **FR-016**: The tool MUST support CLI overrides `--profile`, `--target`, `--output`, and `--config` that take precedence over file-based configuration.
- **FR-016-Config-Concurrency**: The tool MUST employ optimistic locking when modifying the configuration file: read the config at command start, apply the intended modification in memory, and validate that the config file has not been externally modified before writing the updated config. If the config file has been modified by another process since the read, the tool MUST report a clear conflict error message and exit non-zero without writing changes, enabling the user to manually resolve the conflict.
- **FR-017**: The tool MUST implement a stable built-in target contract and deterministic registration model that allows new output targets to be added without refactoring existing core pipeline components or existing target components.
- **FR-018**: The tool MUST generate artefacts for all enabled targets defined in configuration; disabled targets MUST be skipped.
- **FR-019**: The tool MUST produce deterministic output: identical inputs MUST always produce identical outputs.
- **FR-020**: The tool MUST exit with code 0 on success, 1 on validation errors, 2 on configuration errors, and 3 on target generation errors.

#### Speckit Target Requirements

- **FR-021**: The Speckit target component MUST map steering documents to Speckit Markdown modules, mapping rules to Speckit rule entries with severity, category, and text fields.
- **FR-022**: The tool MUST generate Markdown output format for the Speckit target.
- **FR-023**: The tool MUST generate a consolidated `speckit.all.md` artefact containing all modules in addition to per-module files.

#### Kiro Target Requirements

- **FR-024**: The Kiro target adapter MUST generate one Markdown file per source steering document, placed in the configured output directory.
- **FR-025**: The Kiro adapter MUST render all included rules as natural language prose paragraphs, combining each rule's primary directive and explanatory material; no structured rule syntax, IDs, or severity values MUST appear in the output.
- **FR-026**: The Kiro adapter MUST generate YAML frontmatter for each output file containing at minimum an `inclusion` mode field.
- **FR-027**: The Kiro adapter MUST support configurable mapping from steering document or rule attributes (category, `appliesTo`, tags) to Kiro inclusion modes: `always`, `fileMatch`, `manual`, or `auto`; when no mapping is configured, `always` MUST be the default.
- **FR-028**: When the configured inclusion mode is `fileMatch`, the Kiro adapter MUST include a `fileMatchPattern` field in the frontmatter, sourced from target configuration or document metadata.
- **FR-029**: When the configured inclusion mode is `auto`, the Kiro adapter MUST include `name` and `description` frontmatter fields derived from the source document's `title` and `description` fields respectively.
- **FR-030**: The Kiro adapter MUST exclude deprecated rules from generated prose output.

#### Agent-Spec Target Requirements

- **FR-031**: The tool MUST support one or more agent-spec generation targets that transform the neutral steering model into agent specification files for the chosen platform.
- **FR-032**: Agent-spec generation targets MUST support target-specific formatting and metadata requirements while preserving equivalent normative guidance semantics across platforms.

#### Extensibility and Compatibility Guarantees

- **FR-033**: The system MUST NOT use runtime plugin loading for target extensibility.
- **FR-034**: Adding a new target MUST be additive: implementation of a new target component and registration metadata MAY be added, but existing target implementation code MUST remain unchanged.
- **FR-035**: The tool MUST validate target-specific required fields for agent-spec outputs and report generation errors when required target metadata is missing.
- **FR-036**: The tool MUST include compatibility tests that verify existing targets continue to produce unchanged output for unchanged inputs when a new target is introduced.

#### Constitution Governance and Provenance

- **FR-037**: If a feature updates a constitution file and the change is substantive, the toolchain and generated update workflow MUST honor the constitution's Governance and version-tracking sections by recording version change rationale, amendment date, and a sync/provenance record of impacted artifacts.

#### Cross-Target Portability and Modularity

- **FR-038**: All targets (current and future) MUST honor platform naming conventions, structural requirements, and required metadata fields for generated outputs.
- **FR-039**: All targets (current and future) MUST support the degree of modularity permitted by the target platform, including splitting guidance into separately invocable modules when supported.
- **FR-040**: Generated main constitution outputs MUST contain only universally applicable core rules.
- **FR-041**: Domain-specific or context-specific rules MUST be emitted as platform-appropriate modular artifacts (for example agents, skills, or equivalent on-demand modules) rather than being inlined into the main constitution when the target platform supports such modularization.
- **FR-042**: When a target platform permits constitution references/includes (for example a main constitution referring to additional guidance files), generated outputs MUST preserve valid references and resolve paths according to platform conventions.
- **FR-043**: Every Steering Rule MUST include domain metadata. The `core` domain value MUST indicate the rule belongs to the core constitution rule set; non-`core` domain values indicate domain-specific guidance eligible for modular target outputs where supported.

#### CLI Architecture and Init Command

- **FR-043-CLI**: The deliverable MUST provide a command-line binary executable named `steergen`.
- **FR-044**: The system MUST provide a modular command-based CLI built on the `System.CommandLine` package.
- **FR-045**: The CLI MUST provide an `init` command for repository bootstrap.
- **FR-046**: `init` MUST accept a positional argument specifying the project root path.
- **FR-047**: `init` MUST accept one or more `--target` options in a single invocation.
- **FR-048**: `init` MUST create initial steering document folder(s) and target platform folder structures for all specified valid targets when those folders do not already exist.
- **FR-049**: `init` MUST be idempotent and MUST NOT overwrite existing steering or target files/folders.
- **FR-050**: The CLI MUST support invocation equivalent to `steergen init . --target speckit --target kiro-ide`.
- **FR-051**: If any provided target identifier is invalid, `init` MUST return a non-zero exit code and emit a clear validation message identifying the invalid target.

#### CLI Update and Template Lifecycle

- **FR-052**: The CLI MUST provide an `update` command that updates project templates and related metadata in place for the current project.
- **FR-053**: The `update` command MUST support an optional version argument (for example `--version <x.y.z>`) to apply a specific template/metadata release.
- **FR-054**: If no version is provided to `update`, the command MUST resolve and apply the latest available compatible template/metadata release.
- **FR-055**: Templates and metadata MUST be versioned and released independently of the CLI binary version.
- **FR-056**: On first installation or first-run bootstrap, the tool MUST update templates and metadata to the latest available release by default.
- **FR-057**: After initial installation, template/metadata updates MUST remain independently invocable without requiring a CLI binary upgrade.
- **FR-058**: The `update` command MUST fail safely on invalid/unavailable versions with non-zero exit code and actionable diagnostics.

#### CLI Run and Target Registration

- **FR-059**: The CLI MUST provide a `run` command that invokes the generation process.
- **FR-060**: If `run` is invoked without any `--target` option, generation MUST execute for all registered targets by default.
- **FR-061**: If `run` is invoked with one or more `--target` options, generation MUST execute only for the specified target(s).
- **FR-062**: The CLI MUST provide a `target add` command/subcommand pair to register a new target identifier.
- **FR-063**: `target add` MUST create required target folders if they do not already exist.
- **FR-064**: `target add` MUST be idempotent and MUST NOT create duplicate target registrations or overwrite existing target files/folders.
- **FR-065**: The CLI MUST provide a corresponding `target remove` subcommand that deregisters a target identifier from the registered target set.
- **FR-066**: `target remove` MUST NOT delete or modify previously generated steering artefacts for the removed target; artifact cleanup is an explicit user-managed action.

### Non-Functional Requirements

- **NFR-001 (Configuration Source of Truth)**: Target registrations and related project configuration MUST be stored in a single YAML configuration file (default filename: `steergen.config.yaml`) that is human-readable and well documented.
- **NFR-002 (CLI Config Operability)**: The CLI MUST provide complete command coverage to create, update, and control configuration content stored in that YAML file, while allowing manual editing as a supported workflow.
- **NFR-003 (Version Control Friendliness)**: The primary YAML configuration file is expected to be committed to version control and MUST remain concise and reviewable rather than growing into a volatile lock-file style artifact.
- **NFR-004 (Locking Separation)**: If lock semantics are required, lock state MUST be written to and tracked in a separate file from the primary YAML configuration file.
- **NFR-005 (Shell Compatibility)**: The `steergen` CLI tool MUST operate correctly when invoked from the following shells: bash (POSIX), sh, zsh, PowerShell 7+, fish shell, and Windows DOS shells (cmd.exe, PowerShell legacy variants). Command invocation syntax may vary by shell, but the tool MUST be executable and produce correct results across all specified shells.
- **NFR-006 (Operating System Compatibility)**: The `steergen` CLI tool MUST be deployable and functional on Linux, macOS, and Windows operating systems. The tool MUST support platform-native conventions for file paths, line endings, and shell invocation where applicable.
- **NFR-007 (Portable Executable Distribution)**: Each release MUST provide a single portable executable file that runs without modification across all supported operating systems (Linux, macOS, Windows). The executable MUST be self-contained with no external runtime dependencies beyond the OS itself, enabling users to download one file and run it immediately on any supported platform without additional setup or installation steps.
- **NFR-008 (Minimal Executable Size)**: The release executable MUST be trimmed before or during publication to achieve the minimum practical size. Assembly trimming, dead code elimination, and other size-reduction techniques MUST be applied during the build process.
- **NFR-009 (AOT Compilation or Optimized Runtime)**: The executable SHOULD employ AOT (Ahead-of-Time) compilation or NGEN (Native Image Generator) optimization prior to or during release to reduce startup time and memory footprint, provided that such optimization does not conflict with the single portable artifact requirement (NFR-007). If platform-specific AOT compilation would violate the single portable artifact constraint, cross-platform optimization techniques (such as ReadyToRun or trimming) MUST be used instead.
- **NFR-010 (Logging and Diagnostics)**: The tool MUST operate silently by default (producing no output except for error messages, validation reports, and command results). The tool MUST support optional `--verbose` and `--debug` flags to enable diagnostic output for troubleshooting; diagnostic output MUST be sent to stderr to allow clean separation of normal output from diagnostic data.
- **NFR-011 (Config Security Scope)**: The primary configuration file (`steergen.config.yaml`) MUST NOT contain secrets, credentials, tokens, or private keys in v1. Configuration content is limited to non-sensitive operational metadata; if future secret support is introduced, it MUST be handled through a separate mechanism.
- **NFR-012 (Reliability Scope for v1)**: v1 does not define additional service-level reliability targets (for example uptime, MTTR, or error-budget SLOs) beyond the explicit command correctness, deterministic output, non-destructive behavior, and safe-failure requirements already specified in this document.
- **NFR-013 (Scalability Envelope)**: The tool MUST define and document a validated support envelope of up to 1,000 steering documents and 10,000 rules per execution. For inputs beyond this envelope, the tool MUST degrade gracefully by issuing a clear warning and either continuing best-effort processing or failing safely with actionable diagnostics.

### Key Entities

- **Steering Document**: A versioned, identifiable Markdown file containing prose and embedded rule blocks. Has an ID, semantic version, tags, profile metadata, and an ordered list of rules.
- **Steering Rule**: An individual governance directive within a document. Has a unique ID, severity, category, domain metadata (including `core` for core constitution rules), profile scope, applicability, tags, deprecation status, supersession reference, primary text, and explanatory text.
- **Profile Metadata**: Describes the activation state and tags for a named profile within a document.
- **Steering Configuration**: Defines the global root, project root, active profiles, and one or more target configurations.
- **Target Configuration**: Defines the name, output path, enabled state, and format options for a specific output target.
- **Target Adapter Contract**: A stable in-repo contract that each built-in target component implements to transform the resolved steering model into target-specific outputs.
- **Target Component**: A built-in, in-repo transformation component that accepts the resolved steering model and produces one or more output files in a target-specific format.
- **Target Registration Metadata**: Deterministic, static metadata defining available targets, target identifiers, required options, and output locations.
- **Agent Specification Profile**: Target-specific mapping rules that transform neutral guidance semantics into platform-specific agent specification syntax and metadata.
- **Core Constitution Rule Set**: A minimal set of universally applicable governance rules that must remain in the generated main constitution artifact for each target platform.
- **Domain Guidance Module**: A platform-specific modular guidance artifact (for example an agent file, skill file, or equivalent) containing domain-scoped rules designed for on-demand inclusion or invocation.
- **CLI Target Descriptor**: A normalized target identifier accepted by CLI options (for example `speckit`, `kiro-ide`) and mapped to initialization/generation rules.
- **Initialization Manifest**: Derived initialization intent from `init` inputs (project root + target list) used to create missing steering and target directories safely.
- **Template Pack Version**: The independently versioned release identifier for templates and project metadata applied to a repository.
- **Update Manifest**: Resolved update intent and provenance for an `update` execution, including selected version, compatibility checks, and applied artifact set.
- **Registered Target Set**: The canonical list of target identifiers configured for generation in a project.
- **Target Registration Record**: Metadata entry created by `target add` describing a target identifier, associated folder layout, and registration state.
- **Resolved Steering Model**: The post-overlay, post-filter model representing the effective set of rules for a given profile configuration.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A governance author can go from zero to producing valid Speckit artefacts from a new set of steering documents in under 10 minutes, including tool installation and configuration setup.
- **SC-002**: Identical steering document inputs across any two invocations produce byte-for-byte identical output artefacts (100% determinism).
- **SC-003**: The `validate` command correctly identifies and reports 100% of schema errors, duplicate rule IDs, and invalid severity values present in a test document set.
- **SC-004**: All validation errors include the document identifier and source location, enabling a developer to locate the issue without reading the entire document.
- **SC-005**: A tooling engineer familiar with the adapter interface can implement and register a new output target in under 2 hours.
- **SC-006**: The tool processes a corpus of 100 steering documents containing 1,000 rules in under 5 seconds on standard developer hardware.
- **SC-007**: CI pipelines integrating the tool via `validate` and `compile` commands receive correct exit codes in 100% of tested pass and fail scenarios.
- **SC-008**: Profile filtering correctly includes and excludes rules in all tested multi-profile scenarios, with zero false inclusions or exclusions.
- **SC-009**: Kiro IDE correctly applies the configured inclusion mode for every generated steering file in 100% of tested workspace scenarios (always-included files load on every interaction; fileMatch files load only for matching file patterns; auto files load only when request context matches the description).
- **SC-010**: For at least two distinct agent-spec targets, generated outputs are validated as platform-compatible and semantically equivalent to source guidance in 100% of golden test scenarios.
- **SC-011**: Adding a new target requires only additive changes (new target component + registration metadata) and zero refactor edits to existing target components in 100% of audited target-addition PRs.
- **SC-012**: For every supported target, generated artifacts pass platform naming and structure validation in 100% of golden test scenarios.
- **SC-013**: For every supported target that allows modular guidance, generated outputs separate universal core constitution rules from domain-specific modules in 100% of audited output bundles.
- **SC-014**: `steergen init` successfully initializes missing baseline steering and target folder structures for 100% of supported-target test cases.
- **SC-015**: Re-running the same `steergen init` command in an already initialized project performs zero destructive changes in 100% of idempotency test scenarios.
- **SC-016**: `steergen update` applies the latest compatible template/metadata release successfully in 100% of supported-project update test scenarios.
- **SC-017**: Version-pinned updates via `steergen update --version <x.y.z>` apply the exact requested template/metadata version in 100% of valid-version test scenarios.
- **SC-018**: Template and metadata updates can be performed without changing CLI binary version in 100% of lifecycle regression tests.
- **SC-019**: `steergen run` without explicit targets generates outputs for 100% of registered targets in default-scope test scenarios.
- **SC-020**: `steergen target add` correctly registers targets and initializes missing target folders with idempotent behavior in 100% of registration lifecycle tests.
- **SC-021**: For input sets up to 1,000 steering documents and 10,000 rules, `steergen` completes processing without correctness regressions in 100% of scalability-envelope test scenarios; for inputs beyond this envelope, the tool emits a clear warning or safe-failure diagnostic in 100% of tested cases.

## Assumptions

- Steering documents are authored by engineers or governance practitioners who understand YAML frontmatter and the `:::rule` fenced block syntax; no GUI authoring interface is in scope.
- The initial release targets Speckit, Kiro, and at least one agent-spec output target; additional built-in targets are additive extensibility points.
- The tool runs on standard .NET-supported platforms (Windows, macOS, Linux) and is distributed as a self-contained portable executable with no additional runtime installation required.
- Document encoding is UTF-8; other encodings are out of scope.
- The tool consumes steering documents from the local filesystem; remote document sources (URLs, git repositories) are out of scope for v1.
- The `speckit.all.md` consolidated output is always generated when the Speckit target is enabled; selective module output is a future enhancement.
- Profile names are case-sensitive strings; no normalisation or aliasing is assumed.
- The `default` profile is always implicitly active and cannot be excluded by profile filtering.
- Target extensibility is intentionally non-plugin-based; all targets are implemented and versioned in-repo.
- Target platforms differ in modularity capabilities; outputs are generated to use the highest level of valid modular decomposition each platform supports while preserving core-rule portability.
- Template and metadata distribution is decoupled from CLI binary distribution and supports independent semantic versioning.
- The tool is intended for open-source distribution under a license permitting commercial and private use; steering document content remains the intellectual property of the document authors.
