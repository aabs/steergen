# Implementation Plan: Custom Template Packs and Rules Packs

## Overview

This plan implements two pack-based extensibility mechanisms for Steergen: Template Packs (user-provided Scriban templates that override built-in rendering or provide external targets) and Rules Packs (shared governance rule sets from GitHub repositories), while retiring the legacy `globalRoot` configuration. Implementation follows test-first (Red-Green-Refactor) with CsCheck property-based testing as the primary strategy. Language: C# 14 / .NET 10.

## Tasks

- [x] 1. Core data models and pack manifest infrastructure
  - [x] 1.1 Create pack data models in `src/Steergen.Core/Packs/`
    - Create `PackManifest.cs` with `Name`, `Version`, `MinSteergenVersion`, `Scope`, `Targets`, `ProvidedTargets`, `RulesRoot` fields
    - Create `ProvidedTargetDefinition.cs` with `TargetId`, `DefaultLayout`, `Description`
    - Create `PackScope.cs` enum (`Global`, `Supplemental`, `Project`)
    - Create `GitHubPackSource.cs` with `Owner`, `Repo`, `Ref`, `Path`
    - Create `PackDownloadResult.cs` with `Success`, `CachePath`, `Diagnostics`
    - Create `PackType.cs` enum (`Template`, `Rules`)
    - _Requirements: 2.1, 2.2, 2.3, 9.3, 9.4, 16.1, 16.2_

  - [x] 1.2 Write property test for pack manifest validation (Property 2)
    - **Property 2: Pack Manifest Validation**
    - Generate random YAML documents with random field presence/absence; assert valid iff all required fields present and well-formed
    - Test class: `PackManifestProperties` in `tests/Steergen.Core.PropertyTests/Packs/`
    - **Validates: Requirements 2.2, 2.3, 9.3, 9.4**

  - [x] 1.3 Implement `PackManifestParser` with `Parse` and `Validate` methods in `src/Steergen.Core/Packs/PackManifestParser.cs`
    - Parse `pack.yaml` from a given directory using YamlDotNet
    - Return null if `pack.yaml` does not exist
    - Validate required fields: `name` (non-empty), `version` (valid semver), `minSteergenVersion` (valid semver)
    - For rules packs, additionally validate `scope` is one of `global`, `supplemental`, `project`
    - Return diagnostics for missing/invalid fields
    - _Requirements: 2.1, 2.2, 2.4, 2.5, 9.3_

  - [x] 1.4 Write property test for version compatibility check (Property 3)
    - **Property 3: Version Compatibility Check**
    - Generate random semver pairs; assert compatible iff runningVersion >= minSteergenVersion
    - Test class: `VersionCompatibilityProperties` in `tests/Steergen.Core.PropertyTests/Packs/`
    - **Validates: Requirements 2.4, 2.6, 13.1, 13.2**

  - [x] 1.5 Write property test for SHA pinning detection (Property 4)
    - **Property 4: SHA Pinning Detection**
    - Generate random strings including valid/invalid 40-char hex; assert `IsImmutablePin` returns true iff exactly 40 lowercase hex chars
    - Test class: `ShaDetectionProperties` in `tests/Steergen.Core.PropertyTests/Packs/`
    - **Validates: Requirements 3.6, 10.7**

  - [x] 1.6 Write property test for cache path construction (Property 5)
    - **Property 5: Cache Path Construction**
    - Generate random (owner, repo, ref) tuples; assert computed path equals `{userProfile}/.steergen/{packTypeDir}/{owner}/{repo}/{ref}/`
    - Test class: `CachePathProperties` in `tests/Steergen.Core.PropertyTests/Packs/`
    - **Validates: Requirements 4.1, 12.1**

- [x] 2. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 3. Template resolution engine
  - [x] 3.1 Create `ITemplateProvider` interface and `TemplateSource` enum in `src/Steergen.Core/Targets/`
    - Define `GetTemplate(string targetId, string templateName)` method
    - Define `TemplateSource` enum: `LocalOverride`, `CachedGitHubPack`, `BuiltInEmbedded`, `ProvidedTarget`
    - _Requirements: 1.4, 5.1_

  - [x] 3.2 Write property test for template override precedence with target scoping (Property 1)
    - **Property 1: Template Override Precedence with Target Scoping**
    - Generate random (targetId, templateName) pairs with random availability across layers and random declared-targets sets
    - Assert resolver returns content from highest-precedence layer; assert target-scoped packs only consulted for declared targets
    - Test class: `TemplateResolverProperties` in `tests/Steergen.Core.PropertyTests/Packs/`
    - **Validates: Requirements 1.1, 1.2, 1.3, 1.4, 15.1, 15.2, 15.3, 15.4**

  - [x] 3.3 Implement `TemplateResolver` class in `src/Steergen.Core/Targets/TemplateResolver.cs`
    - Implement three-level override precedence: local override path > cached GitHub pack > built-in embedded
    - Implement target-scoped filtering via `declaredTargets` set
    - Implement `GetTemplate`, `GetTemplateSource`, `ProvidesForTarget` methods
    - Reject files > 1 MB, do not follow symbolic links, use ordinal file path comparison
    - Make zero network requests
    - IF configured `localOverridePath` does not exist on the filesystem, THROW with diagnostic TP001 and exit code 2
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 5.1, 5.2, 5.4, 14.1, 14.2, 14.5, 15.2, 15.3, 15.4, 15.5_

  - [x] 3.4 Write property test for template resolution determinism (Property 6)
    - **Property 6: Template Resolution Determinism**
    - Generate random resolver states, call `GetTemplate` twice with same args, assert identical results
    - Test class: `TemplateResolverProperties` in `tests/Steergen.Core.PropertyTests/Packs/`
    - **Validates: Requirements 5.1, 5.4**

  - [x] 3.5 Write property test for file size limit enforcement (Property 12)
    - **Property 12: File Size Limit Enforcement**
    - Generate random file sizes around the 1 MB boundary; assert rejected if > 1,048,576 bytes, accepted if <=
    - Test class: `FileSizeLimitProperties` in `tests/Steergen.Core.PropertyTests/Packs/`
    - **Validates: Requirements 14.2, 14.7**

- [x] 4. Pack downloader and security
  - [x] 4.1 Implement `GitHubPackSourceParser` in `src/Steergen.Core/Packs/GitHubPackSourceParser.cs`
    - Parse `github:{owner}/{repo}` format into `GitHubPackSource`
    - Format `GitHubPackSource` back to canonical string
    - Return null for invalid formats
    - _Requirements: 3.1, 10.1_

  - [x] 4.2 Write property test for path traversal rejection (Property 13)
    - **Property 13: Path Traversal Rejection**
    - Generate random file paths including `../` sequences; assert rejected if contains traversal or resolves outside pack directory
    - Test class: `PathTraversalProperties` in `tests/Steergen.Core.PropertyTests/Packs/`
    - **Validates: Requirements 14.3, 14.4**

  - [x] 4.3 Implement `PackDownloader` in `src/Steergen.Core/Packs/PackDownloader.cs`
    - Download GitHub archive tarballs via unauthenticated public URL (`https://github.com/{owner}/{repo}/archive/{ref}.tar.gz`)
    - WHEN no `ref` is specified, use `HEAD` as the ref value in the archive URL
    - Extract to temp directory, validate `pack.yaml` presence, then atomically swap into cache
    - WHEN a `path` field is specified on the source, extract only the contents of that subdirectory from the archive
    - Validate no path traversal (`../`) in archive entry paths
    - Reject entries outside expected directory structure
    - Implement `IsImmutablePin` (40-char lowercase hex detection)
    - Implement `GetCachedPath` for cache lookup
    - Preserve existing cache on download failure
    - _Requirements: 3.2, 3.3, 3.5, 3.6, 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 4.8, 9.5, 14.3, 14.4_

  - [x] 4.4 Write unit tests for `PackDownloader` HTTP interactions
    - Mock `HttpClient` for success/failure scenarios
    - Test atomic replacement behaviour
    - Test immutable pin skip logic
    - Test that HTTP error responses produce DL001 diagnostic with HTTP status code and repository URL
    - Test default-branch resolution: when `ref` is null, archive URL uses `HEAD`
    - Test subdirectory extraction: when `path` is specified, only that subdirectory's contents are cached
    - _Requirements: 3.3, 3.5, 4.4, 4.6, 4.8, 9.5_

- [x] 5. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Configuration model extension and globalRoot retirement
  - [x] 6.1 Extend `SteeringConfiguration` in `src/Steergen.Core/Model/` with `TemplatePack` and `RulesPacks` fields
    - Add `TemplatePackConfig` record with `Source`, `Ref`, `LocalPath`
    - Add `RulesPackEntry` record with `Source`, `Ref`, `Path`, `Scope`
    - Add `RulesPacks` list to `SteeringConfiguration`
    - Remove `GlobalRoot` field from `SteeringConfiguration`
    - _Requirements: 3.1, 8.1, 10.1, 10.2, 10.6_

  - [x] 6.2 Write property test for configuration round-trip (Property 8)
    - **Property 8: Configuration Round-Trip**
    - Generate random `SteeringConfiguration` with template pack and rules pack entries; serialize to YAML and deserialize back; assert equivalence
    - Test class: `ConfigurationProperties` in `tests/Steergen.Core.PropertyTests/Packs/`
    - **Validates: Requirements 3.1, 10.1, 10.2**

  - [x] 6.3 Implement `globalRoot` deprecation detection in config loader
    - When `globalRoot` is present in `steergen.config.yaml`, emit diagnostic error CFG001 and exit with code 2
    - Remove all code paths that discover and load steering documents from a `globalRoot` directory
    - _Requirements: 8.1, 8.2, 8.3_

  - [x] 6.4 Write unit test for `globalRoot` deprecation error
    - Verify config with `globalRoot` produces CFG001 diagnostic and exit code 2
    - _Requirements: 8.2_

- [ ] 7. Rules pack loader and merge
  - [x] 7.1 Create `RulesPackConfiguration`, `RulesPackLoadResult`, and `ScopedPackDocuments` records in `src/Steergen.Core/Packs/`
    - `RulesPackConfiguration` with `Source` (GitHubPackSource) and `ScopeOverride`
    - `RulesPackLoadResult` with `Documents` and `Diagnostics`
    - `ScopedPackDocuments` with `Scope` (PackScope) and `Documents` (IReadOnlyList<SteeringDocument>) — used by extended `SteeringResolver.Resolve` signature
    - _Requirements: 10.1, 10.6_

  - [x] 7.2 Write property test for rules merge with scope-based precedence (Property 9)
    - **Property 9: Rules Merge with Scope-Based Precedence**
    - Generate random rule sets at random scopes with overlapping IDs; assert merge selects highest-precedence source; assert declaration order wins within same scope; assert consumer scope override is respected
    - Test class: `RulesMergeProperties` in `tests/Steergen.Core.PropertyTests/Packs/`
    - **Validates: Requirements 10.3, 10.4, 10.5, 10.6, 11.5, 11.7**

  - [x] 7.3 Write property test for rule source tagging (Property 10)
    - **Property 10: Rule Source Tagging**
    - Generate random rules from random packs; assert each resolved rule carries correct `SourcePackName` and `SourcePackScope`
    - Test class: `RulesMergeProperties` in `tests/Steergen.Core.PropertyTests/Packs/`
    - **Validates: Requirements 11.6**

  - [x] 7.4 Write property test for rules pack file discovery (Property 11)
    - **Property 11: Rules Pack File Discovery**
    - Generate random directory trees; assert discovery returns all and only `.md` files recursively in ordinal sort order, excluding symlinks
    - Test class: `FileDiscoveryProperties` in `tests/Steergen.Core.PropertyTests/Packs/`
    - **Validates: Requirements 11.1**

  - [x] 7.5 Implement `RulesPackLoader` in `src/Steergen.Core/Packs/RulesPackLoader.cs`
    - For each configured pack: resolve cache path, parse manifest, validate version compatibility
    - Determine effective scope (consumer override or manifest scope)
    - Enumerate `.md` files recursively under rules root (ordinal sort, no symlink follow)
    - Reject files > 1 MB
    - Parse each file with `SteeringMarkdownParser`, validate with `SteeringValidator`
    - Tag each rule with `SourcePackName` and effective scope
    - Return all documents grouped by effective scope
    - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.5, 11.6, 11.7, 12.2, 13.1, 13.2, 13.3, 14.6, 14.7, 14.8_

  - [ ] 7.6 Extend `SteeringResolver.Resolve` to accept `ScopedPackDocuments` and apply merge precedence
    - Accept rules pack documents with scope metadata alongside project documents
    - Apply merge order: project-local > project-scoped packs > supplemental > global
    - Within same scope, earlier declaration order wins
    - Emit warning diagnostic for duplicate rule IDs at same scope
    - Extend `SteeringRule` with `SourcePackName` and `SourcePackScope` fields
    - _Requirements: 10.3, 10.4, 10.5, 11.5, 11.7_

- [ ] 8. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 9. External target packs
  - [ ] 9.1 Implement `PackTargetComponent` in `src/Steergen.Core/Targets/PackTargetComponent.cs`
    - Generic `ITargetComponent` implementation that delegates rendering to pack Scriban templates
    - Load default layout YAML from pack directory via `LayoutOverrideLoader`
    - Expose same render model fields as built-in targets: `rules`, `targetId`, `filePath`, `formatOptions`
    - Use write-plan-driven generation flow identical to built-in targets
    - _Requirements: 16.3, 16.5, 16.7_

  - [ ] 9.2 Write property test for external target registration consistency (Property 14)
    - **Property 14: External Target Registration Consistency**
    - Generate random manifests with `providedTargets` and random layout file presence; assert targets available iff `defaultLayout` exists; assert `IsAvailable` correctness
    - Test class: `TargetRegistryProperties` in `tests/Steergen.Core.PropertyTests/Packs/`
    - **Validates: Requirements 16.1, 16.3, 16.4, 16.6**

  - [ ] 9.3 Write property test for pack-provided target rendering equivalence (Property 15)
    - **Property 15: Pack-Provided Target Rendering Equivalence**
    - Generate random rule sets and write plans; assert `PackTargetComponent` produces deterministic output with correct model fields
    - Test class: `PackTargetComponentProperties` in `tests/Steergen.Core.PropertyTests/Packs/`
    - **Validates: Requirements 16.5, 16.7**

  - [ ] 9.4 Extend `TargetRegistry` with `RegisterPackTargets` and `IsAvailable` in `src/Steergen.Core/Targets/`
    - Add `GetAvailableTargets()` returning built-in + pack-provided targets
    - Add `RegisterPackTargets(PackManifest, packBasePath, ITemplateProvider)` to register external targets
    - Add `IsAvailable(string targetId)` check
    - Create `TargetDescriptor` and `TargetOrigin` types
    - Validate `defaultLayout` file exists before registering; emit TP009 if missing
    - _Requirements: 16.1, 16.2, 16.3, 16.4, 16.6, 16.8_

- [ ] 10. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 11. CLI commands for template pack management
  - [ ] 11.1 Implement `steergen template-pack add` command in `src/Steergen.Cli/Commands/TemplatePackAddCommand.cs`
    - Accept `github:{owner}/{repo}` source argument
    - Accept `--ref {ref}` option for tag/branch/SHA
    - Accept `--path {localPath}` option for local override
    - Add template pack source to `steergen.config.yaml` and trigger download
    - _Requirements: 7.1, 7.2, 7.3_

  - [ ] 11.2 Implement `steergen template-pack remove` command in `src/Steergen.Cli/Commands/TemplatePackRemoveCommand.cs`
    - Remove template pack configuration from `steergen.config.yaml`
    - _Requirements: 7.4_

  - [ ] 11.3 Extend `steergen update` command with `--templates` flag in `src/Steergen.Cli/Commands/UpdateCommand.cs`
    - Re-download configured template pack from GitHub source
    - Display pack name, version, and number of template files on success
    - Report "no template pack configured" and exit 0 if none configured
    - Respect `--force` flag to override immutable pin skip
    - _Requirements: 7.5, 7.7, 7.8_

  - [ ] 11.4 Extend `steergen inspect` command with `--templates` flag in `src/Steergen.Cli/Commands/InspectCommand.cs`
    - Display active template resolution chain showing source per template
    - _Requirements: 7.6_

- [ ] 12. CLI commands for rules pack management
  - [ ] 12.1 Implement `steergen rules-pack add` command in `src/Steergen.Cli/Commands/RulesPackAddCommand.cs`
    - Accept `github:{owner}/{repo}` source argument
    - Accept `--ref {ref}`, `--path {subdir}`, `--scope {scope}` options
    - Append rules pack to `rulesPacks` list in config and trigger download
    - _Requirements: 17.1, 17.2, 17.3, 17.4_

  - [ ] 12.2 Implement `steergen rules-pack remove` command in `src/Steergen.Cli/Commands/RulesPackRemoveCommand.cs`
    - Remove matching rules pack entry from `steergen.config.yaml` by name
    - _Requirements: 17.5_

  - [ ] 12.3 Implement `steergen rules-pack list` command in `src/Steergen.Cli/Commands/RulesPackListCommand.cs`
    - Display all configured rules packs with source, ref, scope, and cache status
    - _Requirements: 17.6_

  - [ ] 12.4 Extend `steergen update` command with `--rules` flag
    - Re-download all configured rules packs regardless of cache state
    - Respect `--force` flag to override immutable pin skip
    - _Requirements: 17.7_

  - [ ] 12.5 Extend `steergen inspect` command with `--rules` flag
    - Display all configured rules packs with name, version, source, scope, and number of rules loaded
    - _Requirements: 17.8, 13.4_

- [ ] 13. Template pack validation command
  - [ ] 13.1 Extend `steergen validate` to validate template packs
    - Validate all template files are parseable Scriban templates
    - Report file path, line number, and error description for syntax errors
    - Validate template file names match known template names for declared target IDs
    - Report warning for template files targeting unregistered targets
    - _Requirements: 6.1, 6.2, 6.3, 6.4_

  - [ ] 13.2 Write property test for template pack validation (Property 7)
    - **Property 7: Template Pack Validation**
    - Generate random Scriban-like strings; assert valid iff Scriban parser succeeds; assert warning for unknown template names
    - Test class: `TemplateValidationProperties` in `tests/Steergen.Core.PropertyTests/Packs/`
    - **Validates: Requirements 6.1, 6.3**

- [ ] 14. Pipeline integration and wiring
  - [ ] 14.1 Wire `TemplateResolver` into the generation pipeline replacing direct `EmbeddedTemplateProvider` usage
    - Update DI composition in `src/Steergen.Cli/Composition/` to construct `TemplateResolver` from config
    - Ensure default (no-pack) configuration still uses `EmbeddedTemplateProvider` directly via resolver fallback
    - Wire `PackTargetComponent` for registered external targets
    - _Requirements: 1.1, 1.4, 5.2, 5.3, 16.5_

  - [ ] 14.2 Wire `RulesPackLoader` into the generation pipeline
    - Load rules packs during `steergen run` before merge step
    - Feed loaded documents into extended `SteeringResolver.Resolve`
    - Emit RP005 error if configured pack not in cache
    - Emit TP007 error if configured template pack not in cache
    - _Requirements: 5.3, 11.1, 11.5, 12.2, 12.7_

  - [ ] 14.3 Wire `PackDownloader` into CLI commands
    - Inject `PackDownloader` via DI for `template-pack add`, `rules-pack add`, `update --templates`, `update --rules`
    - Configure `HttpClient` for GitHub archive downloads
    - Emit diagnostic warnings for branch refs (recommend pinning to SHA/tag)
    - _Requirements: 3.7, 4.4, 10.8, 12.4_

  - [ ] 14.4 Wire `TargetRegistry` extension for `steergen target add` validation
    - When user runs `steergen target add {targetId}`, verify target is available as built-in or from configured pack's `providedTargets`
    - Emit TP010 error if pack providing a registered target is removed
    - _Requirements: 16.6, 16.8_

- [ ] 15. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 16. Integration tests
  - [ ] 16.1 Write integration tests for template pack CLI commands
    - Test `steergen template-pack add/remove` modifies config correctly
    - Test `steergen update --templates` downloads and caches
    - Test `steergen run` with template pack produces overridden output
    - Test `steergen validate` with malformed template pack reports errors
    - _Requirements: 7.1, 7.4, 7.5, 6.1_

  - [ ] 16.2 Write integration tests for rules pack CLI commands
    - Test `steergen rules-pack add/remove/list` modifies config correctly
    - Test `steergen update --rules` downloads and caches
    - Test `steergen run` with rules packs merges rules correctly
    - _Requirements: 17.1, 17.5, 17.6, 17.7_

  - [ ] 16.3 Write integration test for globalRoot deprecation
    - Test `steergen run` with `globalRoot` in config fails with CFG001 and exit code 2
    - _Requirements: 8.2_

  - [ ] 16.4 Write integration tests for external target packs
    - Test `steergen target add` with pack-provided target succeeds
    - Test `steergen run` with external target renders via pack templates
    - Test removal of pack providing registered target emits TP010
    - _Requirements: 16.3, 16.5, 16.6, 16.8_

  - [ ] 16.5 Write security integration tests
    - Test archives with path traversal entries are rejected
    - Test template files > 1 MB are rejected
    - Test symlinks in pack directories are not followed
    - _Requirements: 14.2, 14.3, 14.4, 14.5_

- [ ] 17. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 18. Documentation and migration guidance
  - [ ] 18.1 Update README with template pack and rules pack usage documentation
    - Add section on configuring template packs (local and GitHub)
    - Add section on configuring rules packs with scope-based precedence
    - Document new CLI commands: `template-pack add/remove`, `rules-pack add/remove/list`
    - Document `steergen update --templates` and `steergen update --rules`
    - Document `steergen inspect --templates` and `steergen inspect --rules`
    - _Requirements: Req 7, 10, 17 (CLI surface documentation)_

  - [ ] 18.2 Write migration guide for `globalRoot` removal
    - Document that `globalRoot` is removed and replaced by rules packs
    - Provide step-by-step migration: convert existing global rules directory to a rules pack with `scope: global`
    - Include example `pack.yaml` for a migrated global rules directory
    - Document the CFG001 error and remediation steps
    - _Requirements: 8.1, 8.2_

  - [ ] 18.3 Document error codes and diagnostics
    - Document all new diagnostic codes (TP001–TP011, RP001–RP007, DL001–DL004, CFG001)
    - Include remediation guidance for each error
    - _Requirements: All error-producing requirements_

- [ ] 19. Security analysis
  - [ ] 19.1 Produce explicit misuse and abuse analysis document
    - Analyse prompt-injection-style payloads in template content and rule documents
    - Analyse path traversal attack vectors in downloaded archives
    - Analyse symlink-based escape attempts in pack directories
    - Analyse denial-of-service via oversized files
    - Analyse supply-chain risks from unauthenticated GitHub downloads (pack substitution, typosquatting)
    - Document mitigations implemented (Scriban sandboxing, size limits, symlink rejection, path validation, atomic replacement)
    - _Requirements: 14.1–14.8_

## Notes

- Property-based tests are NON-NEGOTIABLE per constitution and must be implemented before their corresponding implementation tasks (Red-Green-Refactor)
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases where PBT is not practical
- The project uses .NET 10, C# 14, CsCheck for PBT, xUnit for test framework, NSubstitute for mocking
- All property tests go in `tests/Steergen.Core.PropertyTests/Packs/` directory
- Unit tests go in `tests/Steergen.Core.UnitTests/`
- Integration tests go in `tests/Steergen.Cli.IntegrationTests/`
- Minimum 100 iterations per property test

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "3.1", "4.1"] },
    { "id": 1, "tasks": ["1.2", "1.3", "1.4", "1.5", "1.6", "4.2"] },
    { "id": 2, "tasks": ["3.2", "3.3", "3.5", "4.3", "6.1"] },
    { "id": 3, "tasks": ["3.4", "4.4", "6.2", "6.3", "7.1"] },
    { "id": 4, "tasks": ["6.4", "7.2", "7.3", "7.4", "7.5"] },
    { "id": 5, "tasks": ["7.6", "9.1", "9.2"] },
    { "id": 6, "tasks": ["9.3", "9.4", "13.2"] },
    { "id": 7, "tasks": ["11.1", "11.2", "12.1", "12.2", "12.3", "13.1"] },
    { "id": 8, "tasks": ["11.3", "11.4", "12.4", "12.5"] },
    { "id": 9, "tasks": ["14.1", "14.2", "14.3", "14.4"] },
    { "id": 10, "tasks": ["16.1", "16.2", "16.3", "16.4", "16.5"] },
    { "id": 11, "tasks": ["18.1", "18.2", "18.3", "19.1"] }
  ]
}
```
