# Routing Syntax Reference

This document describes the framework-agnostic routing syntax used in `default-layout.yaml` and user override YAML files. The syntax applies to all built-in targets (speckit, kiro, copilot-agent, kiro-agent) and to custom additive targets.

## Overview

A layout YAML file defines how steering rules are routed to output files. Each rule is matched against a list of route definitions and assigned to exactly one destination path. If no route matches, the rule falls back to an `other.*` file colocated with the `core` anchor route.

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
      domain: core            # match on rule metadata fields
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

A match expression filters rules by their metadata. All specified fields must match (logical AND).

```yaml
match:
  domain: core                  # string or list of strings
  category: security            # string or list of strings
  severity: error               # string or list of strings
  tagsAny:                      # any of these tags present (OR)
    - pii
    - compliance
  profile: strict               # string or list of strings
  sourceContext:                 # arbitrary key-value metadata from source doc
    team: platform
```

### Wildcard Catch-All

Any match field supports `"*"` as a wildcard to match all values of that field.

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
| `${scope}`         | Rule scope (`global` or `project`). |
| `${targetId}`      | ID of the target being generated. |
| `${domain}`        | Rule domain value. |
| `${category}`      | Rule category value. |
| `${severity}`      | Rule severity value. |
| `${profile}`       | Active profile name (when profile-scoped). |
| `${inputFileName}` | Source document file name (without extension). |
| `${inputFileStem}` | Source document file name stem. |

Variables defined in the `variables` section may also be referenced.

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
      domain: core
    destination:
      directory: "${globalRoot}/.speckit"
      fileName: "constitution"
      extension: ".md"

  # Domain-specific global route
  - id: domain-global
    scope: global
    explicit: false
    order: 20
    match:
      domain: "*"
    destination:
      directory: "${globalRoot}/.speckit"
      fileName: "${domain}"
      extension: ".md"

  # Core anchor for project scope
  - id: core-project
    scope: project
    explicit: true
    anchor: core
    order: 10
    match:
      domain: core
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
