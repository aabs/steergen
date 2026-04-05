# Phase 1 Data Model

## TargetLayoutDefinition
- Purpose: Canonical merged layout profile used by routing and write planning for one target.
- Fields:
  - `targetId` (string, required)
  - `version` (string, optional)
  - `roots` (object, required)
    - `globalRoot` (path template)
    - `projectRoot` (path template)
    - `targetRoot` (path template)
  - `variables` (map<string, VariableDefinition>, optional)
  - `routes` (list<RouteRuleDefinition>, required, min 1)
  - `fallback` (FallbackRuleDefinition, required)
  - `purge` (PurgePolicyDefinition, optional)
- Validation:
  - At least one `core` anchor route per scope.
  - Every destination template resolves inside configured roots.
  - Unknown variable references are invalid.
  - Deterministic single-destination route selection metadata must be derivable for every routable rule.

## RouteRuleDefinition
- Purpose: Condition-to-destination mapping candidate.
- Fields:
  - `id` (string, required)
  - `scope` (enum: `global`, `project`, `both`, required)
  - `explicit` (bool, default false)
  - `match` (RouteMatchExpression, required)
  - `destination` (DestinationTemplate, required)
  - `anchor` (enum: `core`, `none`, default `none`)
  - `order` (int, required for deterministic declaration order)
- Validation:
  - `id` unique within target layout.
  - `match` may reference only supported rule metadata fields.
  - `destination` must contain a filename with extension.

## RouteMatchExpression
- Purpose: Declarative rule filter over steering metadata.
- Fields:
  - `domain` (string or list<string>, optional)
  - `tagsAny` (list<string>, optional)
  - `category` (string or list<string>, optional)
  - `severity` (string or list<string>, optional)
  - `profile` (string or list<string>, optional)
  - `sourceContext` (map<string, string>, optional)
- Validation:
  - Empty expression is allowed only for designated fallback route forms.
  - Domain names are unprivileged except explicit `core` anchor semantics.

## DestinationTemplate
- Purpose: Produces destination path from routing context variables.
- Fields:
  - `directory` (template string, required)
  - `fileName` (template string, required)
  - `extension` (string, optional; defaults from target)
- Validation:
  - Resolved output path must be normalized and root-bounded.
  - Traversal segments and disallowed absolute forms are rejected.

## FallbackRuleDefinition
- Purpose: Defines unmatched rule handling.
- Fields:
  - `mode` (enum: `other-at-core-anchor`, required)
  - `fileBaseName` (string, default `other`)
- Validation:
  - Requires resolvable `core` anchor for same target/scope.

## RoutingContext
- Purpose: Input bag for match evaluation and template expansion.
- Fields:
  - Standard:
    - `inputFileName`
    - `inputFileStem`
    - `scope`
    - `targetId`
    - `globalRoot`
    - `projectRoot`
    - `targetRoot`
  - Rule-derived:
    - `domain`, `tags`, `category`, `severity`, `profile`
  - Target-derived:
    - Additional variables computed by target-specific providers.
- Validation:
  - Missing required context fields fail preflight.

## RouteResolutionResult
- Purpose: Deterministic resolution output for one steering rule.
- Fields:
  - `ruleId` (string)
  - `matchedRouteIds` (ordered list<string>)
  - `selectedRouteId` (string)
  - `selectedDestinationPath` (string)
  - `selectionReason` (string)
  - `source` (enum: `default`, `override`, `merged`)
- Validation:
  - Exactly one `selectedRouteId` must exist for successful resolution.

## WritePlan
- Purpose: Ordered file-write actions for a target run.
- Fields:
  - `targetId` (string)
  - `files` (list<WritePlanFile>, ordered)
- Validation:
  - Each file appears once.
  - File ordering is deterministic (ordinal path order).

## WritePlanFile
- Purpose: Represents one destination file lifecycle.
- Fields:
  - `path` (string)
  - `truncateAtStart` (bool, always true when selected)
  - `appendUnits` (ordered list<ContentUnit>)
- Validation:
  - `appendUnits` ordering is deterministic and stable across equivalent inputs.

## ContentUnit
- Purpose: Single rendered rule block to append.
- Fields:
  - `ruleId` (string)
  - `renderedContent` (string)
  - `orderKey` (tuple for deterministic sort)

## TargetOverrideDefinition
- Purpose: User-provided per-target YAML patch linked from config.
- Fields:
  - `targetId` (string)
  - `layoutOverridePath` (path)
- Validation:
  - Path must resolve and parse as valid YAML.
  - Override applies only to matching target.
  - Deep-merge uses recursive map merge with scalar/list replacement by override.

## TargetDefaultLayoutArtifact
- Purpose: Checked-in baseline YAML definition for one built-in target under that target source folder.
- Fields:
  - `targetId` (string)
  - `sourcePath` (repository-relative path)
  - `schemaVersion` (string)
- Validation:
  - Artifact path is discoverable from docs and target registration metadata.
  - Artifact is human-readable YAML and versioned with source control.

## PurgePolicyDefinition
- Purpose: Declares eligible generated files for deletion.
- Fields:
  - `enabled` (bool, default true)
  - `roots` (list<path template>, required)
  - `globs` (list<string>, optional)
- Validation:
  - Empty `globs` yields deterministic no-op for that target.
  - Globs must not escape configured roots.

## PurgeResult
- Purpose: Auditable purge outcome per target.
- Fields:
  - `targetId` (string)
  - `removedFiles` (list<string>)
  - `skippedFiles` (list<SkippedPurgeFile>)
  - `noOpReason` (string, optional)
  - `success` (bool)
  - `safetyFailureReason` (string, optional)
- Validation:
  - Non-success required when safety checks fail for requested target.
  - Empty or missing globs produce deterministic no-op with `success=true` and populated `noOpReason`.

## SkippedPurgeFile
- Purpose: Explain why an eligible-looking file was not deleted.
- Fields:
  - `path` (string)
  - `reason` (enum: `outside-root`, `glob-miss`, `permission-denied`, `safety-blocked`, `dry-run`)

## RoutingSyntaxReferenceDoc
- Purpose: User-facing, framework-agnostic routing grammar and behavior reference under `docs/`.
- Fields:
  - `path` (string, must be under `docs/`)
  - `sections` (list<string>)
    - required: `grammar`, `variables`, `precedence`, `single-destination`, `core-anchor`, `fallback-other`, `examples`
- Validation:
  - Content avoids target-specific assumptions and remains valid for all built-in targets.
