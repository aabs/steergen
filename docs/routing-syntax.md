# Routing Syntax Reference

This document describes the framework-agnostic routing syntax used in `default-layout.yaml` and user override YAML files. The syntax applies to all built-in targets (speckit, kiro, copilot-agent, kiro-agent) and to custom additive targets.

## Overview

A layout YAML file defines how steering rules are routed to output files. Each rule is matched against a list of route definitions and assigned to exactly one destination path. If no route matches, the rule falls back to an `other.*` file colocated with the `core` anchor route.

**Primary routing discriminator:** `category` is the primary field used to route rules to output files. Routes match rules by category, mandatory status, and tags.

## Top-Level Structure

```yaml
version: "1.0"

roots:
  globalRoot: "${globalRoot}"
  projectRoot: "${projectRoot}"
  targetRoot: "${targetRoot}"

variables: {}              # optional: named helper variables

routes:
  - ...                    # one or more route definitions (required)

fallback:
  mode: other-at-core-anchor
  fileBaseName: other

purge:
  roots:
    - "${targetRoot}"
  globs:
    - "**/*.md"
```

### Fields

| Field        | Required | Description |
|---|---|---|
| `version`    | Yes      | Schema version string (e.g. `"1.0"`). |
| `roots`      | Yes      | Root path templates used in route destinations and purge policies. |
| `variables`  | No       | Named variable definitions for reuse in route templates. |
| `routes`     | Yes      | List of route rule definitions. At least one per scope. |
| `fallback`   | Yes      | Fallback behavior for unmatched rules. |
| `purge`      | No       | Purge policy for stale file cleanup. |

---

## Route Rule Definition

```yaml
routes:
  - id: core-global           # unique identifier within this layout
    scope: global             # global | project | both
    explicit: true            # explicit routes win over non-explicit
    anchor: core              # core | none  (at least one core per scope)
    match:
      category: core          # match on rule metadata fields
    destination:
      directory: "${globalRoot}/.speckit"
      fileName: "constitution"
      extension: ".md"
    order: 10                 # tiebreak within same precedence tier
```

### Route Fields

| Field         | Required | Description |
|---|---|---|
| `id`          | Yes      | Unique route identifier within the layout. Used in diagnostics. |
| `scope`       | Yes      | Which scope this route applies to: `global`, `project`, or `both`. |
| `explicit`    | No       | Default `false`. Explicit routes take precedence over non-explicit. |
| `anchor`      | No       | Default `none`. Set to `core` for the required core anchor route. |
| `match`       | Yes      | Match expression. See [Match Expressions](#match-expressions). |
| `destination` | Yes      | Destination template. See [Destination Templates](#destination-templates). |
| `order`       | No       | Declaration order tiebreaker (integer). Lower values have higher priority. |

### Precedence Tuple

Routes are sorted by this stable tuple (lower = higher priority):

```
(scopePriority, explicitPriority, conditionSpecificity, declarationOrder, routeId)
```

- `scopePriority`: `global` < `project` < `both`
- `explicitPriority`: explicit (`true`) < non-explicit (`false`)
- `conditionSpecificity`: more specific conditions rank higher than wildcard/empty
- `declarationOrder`: `order` field value
- `routeId`: alphabetical tiebreak for reproducibility

---

## Match Expressions

A match expression filters rules by their metadata. All specified fields must match (logical AND). The primary routing discriminator is `category`.

```yaml
match:
  category: security            # string or list of strings
  mandatory: true               # nullable bool: null (omit) = match all, true = mandatory only, false = advisory only
  tagsAny:                      # any of these tags present (OR)
    - pii
    - compliance
  sourceContext:                 # arbitrary key-value metadata from source doc
    team: platform
```

### Match Expression Fields

| Field           | Type                    | Default | Description |
|---|---|---|---|
| `category`      | string or list          | (empty) | Match rules whose category is in this list. Primary routing discriminator. |
| `mandatory`     | bool (nullable)         | `null`  | Filter by mandatory status. `null` (omitted) = match all rules regardless of mandatory status. `true` = match only mandatory rules. `false` = match only advisory (non-mandatory) rules. |
| `tagsAny`       | list of strings         | (empty) | Match if the rule has any of these tags (OR within field). |
| `sourceContext` | map of key-value pairs  | (empty) | Match arbitrary key-value metadata from the source document. |

Fields are ANDed: all specified fields must match for the route to apply. An empty or absent field imposes no constraint (matches any value).

### Wildcard Catch-All

The `category` field supports `"*"` as a wildcard to match all values.

```yaml
match:
  category: "*"    # matches any category value
```

Wildcard routes participate in deterministic precedence. Specific matches always outrank wildcard matches for the same field.

### Empty Match Expression

An empty `match: {}` matches all rules in scope. Use only for designated fallback or catch-all routes and assign a low `order` value to prevent shadowing more specific routes.

---

## Destination Templates

Destinations are path templates resolved against routing context variables.

```yaml
destination:
  directory: "${projectRoot}/.speckit/${category}"
  fileName: "${category}-rules"
  extension: ".md"
```

### Available Context Variables

| Variable           | Description |
|---|---|
| `${globalRoot}`    | Configured global steering docs root. |
| `${projectRoot}`   | Configured project steering docs root. |
| `${targetRoot}`    | Per-target output root. |
| `${profileRoot}`   | User profile/home directory on the current platform. |
| `${tempRoot}`      | System temporary directory on the current platform. |
| `${scope}`         | Rule scope (`global` or `project`). |
| `${targetId}`      | ID of the target being generated. |
| `${category}`      | Rule category value. |
| `${ruleId}`        | Rule identifier. |
| `${inputFileName}` | Source document file name (without extension). |
| `${inputFileStem}` | Source document file name stem. |

Variables defined in the `variables` section may also be referenced.

### Legacy Template Variables

The following variables are retained for backward compatibility with existing user override YAMLs. They always resolve to an empty string:

| Variable        | Description |
|---|---|
| `${domain}`     | Legacy. Always resolves to empty string. |
| `${severity}`   | Legacy. Always resolves to empty string. |
| `${profile}`    | Legacy. Always resolves to empty string. |

> **Note:** If your override YAML references `${domain}`, `${severity}`, or `${profile}` in destination templates, the tokens will resolve to empty string. Update your templates to use `${category}` as the primary routing variable.

### Path Safety

- Resolved paths must remain inside configured roots. Traversal segments (`..`) and absolute path forms are rejected.
- Validation runs before any file writes or deletions.

---

## Fallback Behavior

```yaml
fallback:
  mode: other-at-core-anchor
  fileBaseName: other
```

When no route (including catch-all routes) matches a rule, the fallback applies:

- `mode: other-at-core-anchor` — routes unmatched rules to `{fileBaseName}.*` in the same directory and extension as the `core` anchor route for the same scope.
- The `core` anchor route must exist for the fallback to resolve. Missing core anchor is a validation error.

---

## Purge Policy

```yaml
purge:
  roots:
    - "${targetRoot}"
    - "${projectRoot}/.speckit"
  globs:
    - "**/*.md"
    - "**/*.instructions.md"
```

The purge policy controls which files `steergen purge` may delete for this target.

- If `globs` is empty or absent, purge is a no-op for this target.
- Only files matching configured globs within configured roots are eligible.
- Purge does not require a generation manifest.

---

## Override YAML

Users may provide a per-target override YAML that is deep-merged over the built-in defaults.

Deep-merge rules:
- Map/object fields merge recursively.
- Scalar values in the override replace default values.
- Lists in the override replace default lists entirely.

Link the override in `steergen.config.yaml`:

```yaml
targets:
  - id: speckit
    layoutOverridePath: config/layout-overrides/speckit.yaml
```

Override YAML is validated with fail-closed semantics. Unknown fields, unknown variables, and missing core anchors are all validation errors.

### Path resolution

The `layoutOverridePath` value may be either an absolute path or a path relative to the
`steergen.config.yaml` file's directory. Both conventions are equivalent:

```yaml
# Relative to config file directory (workspace-local convention):
layoutOverridePath: layouts/my-speckit-layout.yaml

# Absolute path (user-home global convention):
layoutOverridePath: /home/user/.config/steergen/speckit-layout.yaml
```

### Per-target isolation

Each target's `layoutOverridePath` is independent. Overriding one target does not affect any
other target's layout. Targets with no `layoutOverridePath` use the built-in default layout:

```yaml
targets:
  - id: speckit
    layoutOverridePath: layouts/speckit-override.yaml   # custom layout
  - id: kiro
    # no layoutOverridePath — uses built-in default
```

### Provenance tracking

When running with `--verbose`, the route diagnostics line for each resolved rule includes a
`source` field:

- `Default` — the rule was routed using the built-in default layout only.
- `Merged` — the rule was routed using a layout produced by deep-merging the default with a
  user-provided override YAML.

Example verbose output:

```
[routing] speckit: 12/12 rules routed
  [routing] CORE-001 → constitution.md (route: core-anchor, source: Merged)
  [routing] SEC-001 → security.md (route: security-module, source: Merged)
```

---

## Catch-All vs Fallback: Worked Examples

The following examples show the distinction between **catch-all** routing (a wildcard route) and
**fallback** routing (the `other-at-core-anchor` fallback when no route matches at all).

### Example 1: Category-specific + catch-all

```yaml
routes:
  - id: core-global
    scope: global
    explicit: true
    anchor: core
    order: 10
    match:
      category: core
    destination:
      directory: "${globalRoot}/.speckit"
      fileName: "constitution"
      extension: ".md"

  - id: security-module
    scope: global
    explicit: true
    order: 20
    match:
      category: security
    destination:
      directory: "${globalRoot}/.speckit"
      fileName: "security"
      extension: ".md"

  - id: catch-all-global
    scope: global
    explicit: false
    order: 100
    match:
      category: "*"           # matches any category not already matched above
    destination:
      directory: "${globalRoot}/.speckit"
      fileName: "${category}"
      extension: ".md"

fallback:
  mode: other-at-core-anchor
  fileBaseName: other
```

**Routing outcome**:
- `category: core` → `constitution.md` (matched by `core-global`, explicit = higher priority).
- `category: security` → `security.md` (matched by `security-module`, explicit = higher priority).
- `category: performance` → `performance.md` (matched by `catch-all-global` via `category: "*"`).
- A rule with no matching route at all → `other.md` in `.speckit/` (fallback, colocated with `core-global`).

### Example 2: Fallback only (no catch-all)

If the layout has no wildcard catch-all route, all rules that do not match a specific route fall back
to `other.*` at the core anchor location:

```yaml
routes:
  - id: core-project
    scope: project
    explicit: true
    anchor: core
    order: 10
    match:
      category: core
    destination:
      directory: "${projectRoot}/.speckit"
      fileName: "constitution"
      extension: ".md"

fallback:
  mode: other-at-core-anchor
  fileBaseName: other
```

**Routing outcome**:
- `category: core` → `constitution.md`.
- Any other rule (e.g., `category: security`, `category: frontend`) → `other.md` in `${projectRoot}/.speckit/`
  (fallback; same directory and extension as the `core-project` anchor).

### Example 3: Mandatory segregation

Routes can use the `mandatory` filter to segregate mandatory rules into dedicated output files:

```yaml
routes:
  - id: core-global
    scope: global
    explicit: true
    anchor: core
    order: 10
    match:
      category: core
    destination:
      directory: "${globalRoot}/.speckit"
      fileName: "constitution"
      extension: ".md"

  - id: mandatory-global
    scope: global
    explicit: false
    order: 50
    match:
      category: "*"
      mandatory: true         # only mandatory rules
    destination:
      directory: "${globalRoot}/.speckit"
      fileName: "${category}-mandatory"
      extension: ".md"

  - id: catch-all-global
    scope: global
    explicit: false
    order: 100
    match:
      category: "*"           # mandatory is omitted = matches all remaining rules
    destination:
      directory: "${globalRoot}/.speckit"
      fileName: "${category}"
      extension: ".md"

fallback:
  mode: other-at-core-anchor
  fileBaseName: other
```

**Routing outcome**:
- `category: core` → `constitution.md` (matched by `core-global`, explicit = higher priority).
- `category: security, mandatory: true` → `security-mandatory.md` (matched by `mandatory-global`; more specific than catch-all because `mandatory` adds specificity).
- `category: security, mandatory: false` → `security.md` (matched by `catch-all-global`; `mandatory-global` does not match because the rule is not mandatory).

### Catch-all prevents fallback

The fallback only fires when *no route* (including catch-all routes) matches. A `category: "*"` catch-all
consumes all otherwise-unmatched rules and prevents the fallback from applying.

---

## Example: Complete Layout

```yaml
version: "1.0"

roots:
  globalRoot: "${globalRoot}"
  projectRoot: "${projectRoot}"
  targetRoot: "${projectRoot}/.speckit"

routes:
  # Core anchor for global scope
  - id: core-global
    scope: global
    explicit: true
    anchor: core
    order: 10
    match:
      category: core
    destination:
      directory: "${globalRoot}/.speckit"
      fileName: "constitution"
      extension: ".md"

  # Category-specific global route
  - id: category-global
    scope: global
    explicit: false
    order: 20
    match:
      category: "*"
    destination:
      directory: "${globalRoot}/.speckit"
      fileName: "${category}"
      extension: ".md"

  # Core anchor for project scope
  - id: core-project
    scope: project
    explicit: true
    anchor: core
    order: 10
    match:
      category: core
    destination:
      directory: "${projectRoot}/.speckit"
      fileName: "constitution"
      extension: ".md"

  # Category catch-all for project scope
  - id: catch-all-project
    scope: project
    explicit: false
    order: 100
    match:
      category: "*"
    destination:
      directory: "${projectRoot}/.speckit/${category}"
      fileName: "${category}-rules"
      extension: ".md"

fallback:
  mode: other-at-core-anchor
  fileBaseName: other

purge:
  roots:
    - "${projectRoot}/.speckit"
    - "${globalRoot}/.speckit"
  globs:
    - "**/*.md"
```
