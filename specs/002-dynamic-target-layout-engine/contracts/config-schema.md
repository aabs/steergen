# Configuration Contract (`steergen.config.yaml`)

## Top-Level Fields
- `globalRoot`: path to global steering docs
- `projectRoot`: path to project steering docs
- `activeProfiles`: active profile names
- `targets`: target configuration list
- `registeredTargets`: registered target IDs
- `templatePackVersion`: template/metadata version

## Target Configuration (Extended)
- `id`: target identifier (required)
- `enabled`: boolean, default true
- `outputPath`: optional legacy compatibility field; no longer authoritative for routing destination selection
- `layoutOverridePath`: optional path to per-target override YAML (new)
- `requiredMetadata`: required metadata names
- `formatOptions`: renderer options

## Target Default Layout YAML Contract
Each built-in target provides `default-layout.yaml` in its source folder.

Required top-level nodes:
- `version`: schema version
- `roots`:
  - `globalRoot`
  - `projectRoot`
  - `targetRoot`
- `routes`: list of route definitions
- `fallback`:
  - `mode` (must support `other-at-core-anchor`)
  - `fileBaseName` (default `other`)
- `purge`:
  - `roots`: list of root templates
  - `globs`: list of glob patterns (optional)

Route entry fields:
- `id` (required, unique)
- `scope` (`global` | `project` | `both`)
- `explicit` (bool)
- `anchor` (`core` | `none`)
- `match` object:
  - `domain`, `tagsAny`, `category`, `severity`, `profile`, `sourceContext`
- `destination` object:
  - `directory`
  - `fileName`
  - `extension`

Catch-all semantics:
- Route match fields MUST support wildcard values (for example `category: "*"`) to enable user-defined catch-all routes.
- Catch-all routes are ordinary routes that participate in deterministic precedence and are selected only when no more-specific route wins.
- Fallback `other-at-core-anchor` applies only when no route (including catch-all) matches.

Anchor requirement:
- Each target/scope combination used for routing MUST define at least one route with `anchor: core`.

## Deep-Merge Semantics
When `layoutOverridePath` is present:
- Maps merge recursively.
- Scalars in override replace default values.
- Lists in override replace default lists.
- Resulting merged model is validated as if authored directly.

## Purge Eligibility Contract
- Eligible files are discovered only by evaluating configured `purge.globs` under configured `purge.roots` for each selected target.
- If `purge.globs` is empty or absent, purge is no-op for that target.
- Purge does not depend on generation manifests.
- Purge reporting includes removed files, skipped files with reasons, and explicit no-op reasons.

## Validation Rules
- Unknown schema fields in layout YAML or override YAML are invalid.
- Unknown template variables are invalid.
- At least one `core` anchor route per target/scope is required.
- Resolved destination and purge candidate paths must remain inside configured roots.

## Security/Trust Contract
- Built-in target defaults are trusted repository assets.
- Override YAML is untrusted and validated with fail-closed behavior.
- No secret material should be stored in layout config or overrides.
