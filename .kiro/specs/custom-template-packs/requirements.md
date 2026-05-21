# Requirements Document

## Introduction

Custom Template Packs and Rules Packs extends Steergen with two capabilities: user-provided Scriban templates that override built-in rendering, and shared governance rule sets published to GitHub repositories. This feature also retires the legacy `globalRoot` configuration concept, replacing it with rules packs that declare their scope. Organisations can now publish departmental, team, or baseline rule sets as packs, reference them in `steergen.config.yaml`, and have them loaded alongside project-local rules during generation. Template packs customise the rendered output format for specific targets or provide complete target definitions for new external targets. Both pack types can be stored locally or published to GitHub repositories. This maintains determinism and the existing architecture principles (no dynamic plugin loading, additive-only changes).

## Clarifications

### Session 2026-05-18

- Q: How should the Pack_Downloader handle GitHub rate limiting and authentication for downloads? → A: Use unauthenticated public archive URLs only (`https://github.com/{owner}/{repo}/archive/{ref}.tar.gz`). No GitHub REST API requiring tokens. Private repositories are out of scope.
- Q: How should the Pack_Downloader handle failed or partial downloads relative to existing cache? → A: Atomic replacement — download to temp directory, validate, then swap into cache. Existing cache is preserved on failure.
- Q: What output should pack download/update operations emit? → A: Minimal structured output — pack name + version on success; diagnostic code + message on failure. No verbose progress by default.

## Glossary

- **Template_Pack**: A directory containing one or more Scriban template files organised by target ID, following the same naming convention as the embedded templates (e.g., `{targetId}/{templateName}.scriban`). A template pack may override templates for specific built-in targets or provide complete target definitions for new external targets.
- **External_Target_Pack**: A template pack that provides a complete target definition including templates, a default layout YAML, and target metadata, enabling new targets to be distributed without modifying the Steergen binary
- **Target_Declaration**: A section in the pack manifest that declares which targets the pack provides templates for, distinguishing between override targets (customising existing built-in targets) and provided targets (supplying complete new target definitions)
- **Template_Resolver**: The component responsible for locating the correct template for a given target and template name, applying the override precedence chain
- **Pack_Manifest**: A YAML metadata file (`pack.yaml`) at the root of a template pack or rules pack that declares the pack name, version, compatible Steergen version range, and content coverage
- **GitHub_Pack_Source**: A reference in configuration to a GitHub repository containing a published template pack or rules pack, specified as `owner/repo` with an optional tag or branch
- **Local_Pack_Cache**: A well-known directory on the local filesystem where downloaded template packs and rules packs are stored for offline use
- **Override_Precedence**: The resolution order for templates: local override path > downloaded GitHub pack > built-in embedded templates
- **Steergen_CLI**: The command-line interface entry point for the Steergen tool
- **Pack_Downloader**: The component responsible for fetching template packs and rules packs from GitHub repositories to the local pack cache
- **Rules_Pack**: A directory containing one or more steering document files (Markdown with YAML frontmatter and `:::rule` blocks) published to a GitHub repository for shared use across projects
- **Rules_Pack_Loader**: The component responsible for discovering and loading steering documents from configured rules packs, merging them into the resolved steering model alongside project rules
- **Rules_Pack_Manifest**: A YAML metadata file (`pack.yaml`) at the root of a rules pack that declares the pack name, version, compatible Steergen version range, scope, and content metadata
- **Pack_Scope**: A declaration in the rules pack manifest indicating how the pack's rules should be treated during merge: `global` (baseline rules, lowest precedence), `supplemental` (mid-precedence, between global and project), or `project` (highest precedence, equivalent to local project rules)
- **Rules_Merge_Order**: The resolution order for steering rules during merge: project-local rules > project-scoped packs > supplemental-scoped packs > global-scoped packs

## Requirements

### Requirement 1: Local Template Override Resolution

**User Story:** As a Steergen user, I want to provide local Scriban template files that override the built-in templates for any target, so that I can customise the generated output format without modifying the tool itself.

#### Acceptance Criteria

1. WHEN a `templatePackPath` is configured in `steergen.config.yaml`, THE Template_Resolver SHALL load templates from that local directory before falling back to built-in embedded templates
2. WHEN a template file exists in the configured local pack path matching the pattern `{targetId}/{templateName}.scriban`, THE Template_Resolver SHALL use that file content instead of the corresponding embedded resource
3. WHEN a template file does not exist in the configured local pack path for a given target and template name, THE Template_Resolver SHALL fall back to the built-in embedded template
4. THE Template_Resolver SHALL resolve templates using the Override_Precedence order: local override path, then downloaded GitHub pack, then built-in embedded templates
5. IF the configured `templatePackPath` does not exist on the filesystem, THEN THE Steergen_CLI SHALL report a diagnostic error and exit with code 2

### Requirement 2: Template Pack Manifest

**User Story:** As a template pack author, I want to declare metadata about my template pack in a manifest file, so that consumers can verify compatibility and understand what the pack provides.

#### Acceptance Criteria

1. THE Pack_Manifest SHALL be a YAML file named `pack.yaml` located at the root of a template pack directory
2. THE Pack_Manifest SHALL contain the following required fields: `name`, `version`, and `minSteergenVersion`
3. THE Pack_Manifest SHALL contain an optional `targets` field listing the target IDs that the pack provides templates for
4. WHEN a template pack is loaded, THE Template_Resolver SHALL parse the Pack_Manifest and validate that the declared `minSteergenVersion` is compatible with the running Steergen version
5. IF the Pack_Manifest is missing from a configured template pack directory, THEN THE Steergen_CLI SHALL report a diagnostic warning and treat the directory as a legacy pack without version constraints
6. IF the running Steergen version is lower than the declared `minSteergenVersion`, THEN THE Steergen_CLI SHALL report a diagnostic error indicating version incompatibility and exit with code 2

### Requirement 15: Target-Scoped Template Overrides

**User Story:** As a template pack author, I want to declare which specific targets my pack overrides, so that consumers know which targets are affected and the resolver only applies my templates to the declared targets.

#### Acceptance Criteria

1. THE Pack_Manifest `targets` field SHALL list the target IDs that the pack provides templates for
2. WHEN a template pack declares a `targets` list, THE Template_Resolver SHALL only use templates from that pack for the declared target IDs
3. WHEN a template pack does not declare a `targets` list, THE Template_Resolver SHALL treat the pack as providing templates for all targets (backward-compatible behaviour)
4. WHEN a template pack declares targets that include both built-in and external targets, THE Template_Resolver SHALL apply override resolution independently per target
5. THE Template_Resolver SHALL ignore template files in a pack that are organised under a target ID not declared in the pack's `targets` list and report a diagnostic warning

### Requirement 16: External Target Packs

**User Story:** As a target author, I want to publish a template pack that provides a complete target definition including templates and default layout, so that new targets can be distributed and used without modifying the Steergen binary.

#### Acceptance Criteria

1. THE Pack_Manifest SHALL support a `providedTargets` section listing target IDs that the pack fully defines (as opposed to `targets` which lists overrides of existing built-in targets)
2. EACH entry in `providedTargets` SHALL include a `targetId`, a `defaultLayout` field referencing a layout YAML file within the pack, and an optional `description` field
3. WHEN a template pack declares `providedTargets`, THE Template_Resolver SHALL register those targets as available for generation, using the pack's templates and default layout
4. WHEN a provided target's `defaultLayout` file is missing from the pack, THE Steergen_CLI SHALL report a diagnostic error and refuse to load the target
5. THE provided target SHALL participate in the same routing and write-plan pipeline as built-in targets, receiving routed rules and rendering via its pack-supplied Scriban templates
6. WHEN a user registers a provided target via `steergen target add {targetId}`, THE Steergen_CLI SHALL verify that the target is available either as a built-in or from a configured template pack's `providedTargets`
7. THE provided target SHALL use the same `ITargetComponent` contract as built-in targets, with a generic pack-based implementation that delegates rendering to the pack's templates
8. WHEN a template pack providing a target is removed, THE Steergen_CLI SHALL report a diagnostic error if the target is still registered in `registeredTargets`

### Requirement 3: GitHub Pack Source Configuration

**User Story:** As a Steergen user, I want to reference a template pack published in a GitHub repository from my configuration, so that my team can share custom templates without manual file distribution.

#### Acceptance Criteria

1. THE SteeringConfiguration SHALL support a `templatePack` section with a `source` field accepting the format `github:{owner}/{repo}` and an optional `ref` field for a Git tag, branch, or commit SHA
2. WHEN a `templatePack.source` is configured with a GitHub reference, THE Pack_Downloader SHALL fetch the pack contents from the specified repository and ref
3. WHEN no `ref` is specified in the GitHub pack source, THE Pack_Downloader SHALL use the repository default branch
4. THE Pack_Downloader SHALL store downloaded template packs in the Local_Pack_Cache directory
5. IF the GitHub repository is not accessible, THEN THE Pack_Downloader SHALL report a diagnostic error with the HTTP status and repository URL
6. WHEN a `ref` field specifies a full 40-character Git commit SHA, THE Pack_Downloader SHALL treat the template pack as immutably pinned and skip re-download even when `steergen update --templates` is executed unless `--force` is also specified
7. THE Steergen_CLI SHALL recommend pinning template packs to a commit SHA or tag in diagnostic output when a branch ref is used, to ensure deterministic template resolution

### Requirement 4: Template Pack Download and Caching

**User Story:** As a Steergen user, I want downloaded template packs to be cached locally, so that generation works offline after the initial download and remains deterministic across runs.

#### Acceptance Criteria

1. THE Local_Pack_Cache for template packs SHALL be located at `{userProfileDirectory}/.steergen/packs/{owner}/{repo}/{ref}/`
2. WHEN a template pack has already been downloaded for the configured source and ref, THE Pack_Downloader SHALL use the cached version without making network requests
3. WHEN the `steergen update --templates` command is executed, THE Pack_Downloader SHALL re-download the configured template pack regardless of cache state
4. THE Pack_Downloader SHALL download packs as GitHub archive tarballs using the unauthenticated public archive URL (`https://github.com/{owner}/{repo}/archive/{ref}.tar.gz`) which does not require API tokens or authentication
5. THE Pack_Downloader SHALL only support public GitHub repositories; private repositories requiring authentication are out of scope
6. WHEN a download completes, THE Pack_Downloader SHALL verify that the downloaded archive contains a valid Pack_Manifest before storing it in the cache
7. IF the downloaded archive does not contain a valid Pack_Manifest, THEN THE Pack_Downloader SHALL report a diagnostic error and discard the download
8. THE Pack_Downloader SHALL use atomic replacement when updating the cache: download and extract to a temporary directory, validate the pack manifest, then atomically swap the temporary directory into the cache location, preserving the existing cache on failure

### Requirement 5: Deterministic Template Resolution

**User Story:** As a Steergen user, I want template resolution to be deterministic, so that identical inputs and configuration always produce identical outputs regardless of network availability.

#### Acceptance Criteria

1. THE Template_Resolver SHALL produce identical output for identical inputs, configuration, and cached template pack state
2. THE Template_Resolver SHALL resolve templates without making network requests during `steergen run` or `steergen validate` commands
3. WHEN a configured GitHub pack source has not been downloaded to the Local_Pack_Cache, THE Steergen_CLI SHALL report a diagnostic error instructing the user to run `steergen update --templates` and exit with code 2
4. THE Template_Resolver SHALL use deterministic file enumeration order when discovering template files in a pack directory

### Requirement 6: Template Pack Validation

**User Story:** As a Steergen user, I want the tool to validate my custom template pack, so that I receive clear diagnostics when templates are malformed or incompatible.

#### Acceptance Criteria

1. WHEN `steergen validate` is executed with a template pack configured, THE Steergen_CLI SHALL validate that all template files in the pack are parseable Scriban templates
2. WHEN a template file in the pack contains Scriban syntax errors, THE Steergen_CLI SHALL report the file path, line number, and error description
3. THE Steergen_CLI SHALL validate that template file names in the pack match known template names for the declared target IDs
4. IF a template pack contains files for a target ID that is not registered, THEN THE Steergen_CLI SHALL report a diagnostic warning

### Requirement 7: CLI Integration for Template Pack Management

**User Story:** As a Steergen user, I want CLI commands to manage template packs, so that I can add, download, update, and inspect template pack state from the command line without hand-editing configuration files.

#### Acceptance Criteria

1. WHEN `steergen template-pack add github:{owner}/{repo}` is executed, THE Steergen_CLI SHALL add the template pack source to `steergen.config.yaml` and download it to the Local_Pack_Cache
2. WHEN `steergen template-pack add github:{owner}/{repo} --ref {ref}` is executed, THE Steergen_CLI SHALL record the specified ref in the configuration
3. WHEN `steergen template-pack add` is executed with a `--path {localPath}` option, THE Steergen_CLI SHALL set the `templatePackPath` in configuration to the specified local directory
4. WHEN `steergen template-pack remove` is executed, THE Steergen_CLI SHALL remove the template pack configuration from `steergen.config.yaml`
5. WHEN `steergen update --templates` is executed, THE Steergen_CLI SHALL download or re-download the configured template pack from the GitHub source to the Local_Pack_Cache
6. WHEN `steergen inspect --templates` is executed, THE Steergen_CLI SHALL display the active template resolution chain showing which templates come from which source (local override, cached GitHub pack, or built-in)
7. WHEN `steergen update --templates` completes successfully, THE Steergen_CLI SHALL display the pack name, version, and number of template files downloaded
8. IF no template pack is configured when `steergen update --templates` is executed, THEN THE Steergen_CLI SHALL report that no template pack source is configured and exit with code 0

### Requirement 8: Removal of Global Root Configuration

**User Story:** As a Steergen user, I want the `globalRoot` configuration to be removed and replaced by rules packs, so that all shared governance rules are managed through a single consistent pack-based mechanism.

#### Acceptance Criteria

1. THE SteeringConfiguration SHALL remove the `globalRoot` field entirely from the configuration schema
2. IF `globalRoot` is present in `steergen.config.yaml`, THEN THE Steergen_CLI SHALL report a diagnostic error stating that `globalRoot` has been removed and rules packs should be used instead, and exit with code 2
3. THE Steergen_CLI SHALL remove all code paths that discover and load steering documents from a `globalRoot` directory

### Requirement 9: Rules Pack Publishing to GitHub

**User Story:** As a steering document author, I want to publish rule sets to GitHub repositories, so that teams across the organisation can share and reuse governance rules without copying files between projects.

#### Acceptance Criteria

1. THE Rules_Pack SHALL be a GitHub repository (or subdirectory within a repository) containing one or more steering document files (Markdown with YAML frontmatter and `:::rule` blocks)
2. THE Rules_Pack SHALL contain a Rules_Pack_Manifest file (`pack.yaml`) at the pack root declaring pack metadata
3. THE Rules_Pack_Manifest SHALL contain the following required fields: `name`, `version`, `minSteergenVersion`, and `scope` (one of `global`, `supplemental`, or `project`)
4. THE Rules_Pack_Manifest SHALL contain an optional `rulesRoot` field specifying the subdirectory containing steering documents (defaulting to the pack root directory)
5. THE Rules_Pack SHALL support publishing multiple independent rule sets within a single GitHub repository by using distinct subdirectories, each with its own `pack.yaml`

### Requirement 10: Rules Pack Configuration

**User Story:** As a Steergen user, I want to reference one or more rules packs in my `steergen.config.yaml`, so that shared governance rules are loaded alongside my project rules without manual file management.

#### Acceptance Criteria

1. THE SteeringConfiguration SHALL support a `rulesPacks` list where each entry specifies a `source` field accepting the format `github:{owner}/{repo}` and an optional `ref` field for a Git tag, branch, or commit SHA
2. THE SteeringConfiguration SHALL support an optional `path` field per rules pack entry to reference a subdirectory within the repository when multiple rule sets are published in one repo
3. WHEN multiple rules packs are configured, THE Rules_Pack_Loader SHALL load them in the order declared in the `rulesPacks` list
4. THE Rules_Pack_Loader SHALL apply Rules_Merge_Order when merging rules: project-local rules override project-scoped packs, which override supplemental-scoped packs, which override global-scoped packs
5. WHEN two rules packs at the same scope level declare rules with the same rule ID, THE Rules_Pack_Loader SHALL use the rule from the pack declared earlier in the `rulesPacks` list and report a diagnostic warning about the duplicate
6. THE SteeringConfiguration SHALL support an optional `scope` field per rules pack entry that overrides the scope declared in the pack manifest, allowing consumers to elevate or demote a pack's precedence
7. WHEN a `ref` field specifies a full 40-character Git commit SHA, THE Rules_Pack_Loader SHALL treat the pack as immutably pinned and skip re-download even when `steergen update --rules` is executed unless `--force` is also specified
8. THE Steergen_CLI SHALL recommend pinning rules packs to a commit SHA or tag in diagnostic output when a branch ref is used, to ensure deterministic rule resolution

### Requirement 17: CLI Integration for Rules Pack Management

**User Story:** As a Steergen user, I want CLI commands to add, remove, and manage rules packs, so that I can configure shared governance rules from the command line without hand-editing configuration files.

#### Acceptance Criteria

1. WHEN `steergen rules-pack add github:{owner}/{repo}` is executed, THE Steergen_CLI SHALL append the rules pack source to the `rulesPacks` list in `steergen.config.yaml` and download it to the Local_Pack_Cache
2. WHEN `steergen rules-pack add github:{owner}/{repo} --ref {ref}` is executed, THE Steergen_CLI SHALL record the specified ref in the configuration entry
3. WHEN `steergen rules-pack add github:{owner}/{repo} --path {subdir}` is executed, THE Steergen_CLI SHALL record the specified subdirectory path in the configuration entry
4. WHEN `steergen rules-pack add github:{owner}/{repo} --scope {scope}` is executed, THE Steergen_CLI SHALL record the specified scope override in the configuration entry
5. WHEN `steergen rules-pack remove {name}` is executed, THE Steergen_CLI SHALL remove the matching rules pack entry from `steergen.config.yaml`
6. WHEN `steergen rules-pack list` is executed, THE Steergen_CLI SHALL display all configured rules packs with their source, ref, scope, and cache status
7. WHEN `steergen update --rules` is executed, THE Steergen_CLI SHALL re-download all configured rules packs regardless of cache state
8. WHEN `steergen inspect --rules` is executed, THE Steergen_CLI SHALL display all configured rules packs with their name, version, source, scope, and number of rules loaded

### Requirement 11: Rules Pack Loading and Merging

**User Story:** As a Steergen user, I want rules from configured packs to be loaded and merged with my project rules during generation, so that shared governance rules are applied to all targets.

#### Acceptance Criteria

1. WHEN `steergen run` is executed with rules packs configured, THE Rules_Pack_Loader SHALL discover all steering document files recursively from each cached rules pack directory
2. THE Rules_Pack_Loader SHALL parse rules pack documents using the same Markdown parser used for local steering documents
3. THE Rules_Pack_Loader SHALL validate rules pack documents using the same validation rules applied to local steering documents
4. WHEN a rules pack document fails validation, THE Steergen_CLI SHALL report the pack name, file path, and validation errors
5. THE Rules_Pack_Loader SHALL merge rules pack documents into the resolved steering model according to the Rules_Merge_Order
6. THE Rules_Pack_Loader SHALL tag each loaded rule with its source pack name for traceability in `steergen inspect` output
7. WHEN a rules pack declares `scope: global`, THE Rules_Pack_Loader SHALL treat its rules as baseline rules with the lowest merge precedence

### Requirement 12: Rules Pack Download and Caching

**User Story:** As a Steergen user, I want rules packs to be downloaded and cached locally, so that generation works offline and remains deterministic after the initial download.

#### Acceptance Criteria

1. THE Local_Pack_Cache for rules packs SHALL be located at `{userProfileDirectory}/.steergen/rules/{owner}/{repo}/{ref}/`
2. WHEN a rules pack has already been downloaded for the configured source and ref, THE Rules_Pack_Loader SHALL use the cached version without making network requests
3. WHEN `steergen update --rules` is executed, THE Pack_Downloader SHALL re-download all configured rules packs regardless of cache state
4. THE Pack_Downloader SHALL download rules packs using the same mechanism as template packs (unauthenticated GitHub archive tarballs via public archive URL)
5. WHEN a download completes, THE Pack_Downloader SHALL verify that the downloaded archive contains a valid Rules_Pack_Manifest before storing it in the cache
6. IF the downloaded archive does not contain a valid Rules_Pack_Manifest, THEN THE Pack_Downloader SHALL report a diagnostic error and discard the download
7. WHEN a configured rules pack has not been downloaded to the Local_Pack_Cache, THE Steergen_CLI SHALL report a diagnostic error instructing the user to run `steergen update --rules` and exit with code 2

### Requirement 13: Rules Pack Manifest Validation

**User Story:** As a Steergen user, I want rules pack manifests to be validated for compatibility, so that I receive clear diagnostics when a pack is incompatible with my Steergen version.

#### Acceptance Criteria

1. WHEN a rules pack is loaded, THE Rules_Pack_Loader SHALL parse the Rules_Pack_Manifest and validate that the declared `minSteergenVersion` is compatible with the running Steergen version
2. IF the running Steergen version is lower than the declared `minSteergenVersion` in a rules pack, THEN THE Steergen_CLI SHALL report a diagnostic error indicating version incompatibility and exit with code 2
3. IF the Rules_Pack_Manifest is missing from a configured rules pack directory, THEN THE Steergen_CLI SHALL report a diagnostic error and refuse to load the pack
4. WHEN `steergen inspect --rules` is executed, THE Steergen_CLI SHALL display all configured rules packs with their name, version, source, scope, and number of rules loaded

### Requirement 14: Security and Integrity

**User Story:** As a Steergen user, I want template and rules pack loading to be safe from injection attacks, so that malicious content cannot compromise the tool or its output.

#### Acceptance Criteria

1. THE Template_Resolver SHALL treat all template file content as untrusted data and parse it exclusively through the Scriban template engine without executing arbitrary code
2. THE Template_Resolver SHALL reject template files larger than 1 MB with a diagnostic error
3. THE Pack_Downloader SHALL validate that downloaded archive contents do not contain path traversal sequences (e.g., `../`) in file paths
4. THE Pack_Downloader SHALL reject archives containing files outside the expected pack directory structure
5. THE Template_Resolver SHALL not follow symbolic links when resolving template files from local or cached pack directories
6. THE Rules_Pack_Loader SHALL treat all rules pack document content as untrusted data and parse it exclusively through the existing steering document parser
7. THE Rules_Pack_Loader SHALL reject individual steering document files larger than 1 MB with a diagnostic error
8. THE Rules_Pack_Loader SHALL not follow symbolic links when discovering steering documents in cached rules pack directories
