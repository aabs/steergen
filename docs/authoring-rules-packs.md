# Authoring a Rules Pack

A rules pack is a collection of steering documents published to a GitHub repository (or stored locally) that can be shared across multiple Steergen projects. This guide covers how to create, structure, and publish a rules pack from scratch.

---

## Contents

1. [What is a rules pack?](#1-what-is-a-rules-pack)
2. [Directory structure](#2-directory-structure)
3. [Writing the pack manifest](#3-writing-the-pack-manifest)
4. [Writing steering documents](#4-writing-steering-documents)
5. [Choosing a scope](#5-choosing-a-scope)
6. [Multi-pack repositories](#6-multi-pack-repositories)
7. [Publishing to GitHub](#7-publishing-to-github)
8. [Versioning and compatibility](#8-versioning-and-compatibility)
9. [Testing your pack locally](#9-testing-your-pack-locally)
10. [Best practices](#10-best-practices)

---

## 1. What is a rules pack?

A rules pack is a directory containing:

- A **`pack.yaml` manifest** declaring metadata (name, version, scope, compatibility)
- One or more **steering documents** (Markdown files with YAML frontmatter and `:::rule` blocks)

When a consumer adds your pack to their `steergen.config.yaml`, Steergen downloads it to a local cache and merges its rules alongside the consumer's project-local rules during generation. The pack's scope determines its merge precedence.

---

## 2. Directory structure

A minimal rules pack:

```
my-rules-pack/
├── pack.yaml              # Required manifest
├── security-rules.md      # Steering document
├── quality-rules.md       # Steering document
└── governance-rules.md    # Steering document
```

With a `rulesRoot` subdirectory:

```
my-rules-pack/
├── pack.yaml              # Required manifest (rulesRoot: "rules/")
└── rules/
    ├── security-rules.md
    ├── quality-rules.md
    └── governance/
        ├── code-review.md
        └── release-process.md
```

Steergen discovers all `.md` files recursively under the rules root in deterministic ordinal sort order.

---

## 3. Writing the pack manifest

Every rules pack must have a `pack.yaml` file at its root. This is the manifest that declares the pack's identity and compatibility requirements.

### Required fields

```yaml
name: "acme-baseline-rules"
version: "1.0.0"
minSteergenVersion: "1.5.0"
scope: global
```

| Field | Required | Description |
|-------|----------|-------------|
| `name` | Yes | Unique identifier for the pack. Use kebab-case. |
| `version` | Yes | Semantic version of the pack (e.g., `1.0.0`, `2.3.1`). |
| `minSteergenVersion` | Yes | Minimum Steergen version required to load this pack. If the consumer's Steergen is older, loading fails with RP002. |
| `scope` | Yes | Default merge scope: `global`, `supplemental`, or `project`. |

### Optional fields

| Field | Description |
|-------|-------------|
| `rulesRoot` | Subdirectory containing the `.md` steering documents. Defaults to the pack root if omitted. |

### Complete example

```yaml
name: "acme-engineering-standards"
version: "2.1.0"
minSteergenVersion: "1.5.0"
scope: supplemental
rulesRoot: "rules/"
```

---

## 4. Writing steering documents

Steering documents in a rules pack use the same format as project-local documents. Each file is a Markdown document with YAML frontmatter and one or more `:::rule` blocks.

### Document structure

```markdown
---
id: security-baseline
version: "1.0.0"
title: Security Baseline Rules
scope: global
status: active
---

# Security Baseline Rules

:::rule id="SEC-001" mandatory="true" category="security" tags="auth,access-control"
All API endpoints must require authentication unless explicitly marked as public
in the service's API specification.
:::

:::rule id="SEC-002" mandatory="true" category="security" tags="secrets"
Secrets and credentials must never be committed to source control. Use environment
variables or a secrets manager for all sensitive configuration.
:::

:::rule id="SEC-003" mandatory="false" category="security" tags="encryption"
Data at rest should be encrypted using AES-256 or equivalent. Exceptions require
an approved Architecture Decision Record.
:::
```

### Frontmatter fields

| Field | Required | Description |
|-------|----------|-------------|
| `id` | Yes | Unique document identifier within the pack |
| `version` | No | Document version |
| `title` | No | Human-readable title |
| `scope` | No | Document-level scope hint |
| `status` | No | `active`, `draft`, `deprecated` |

### Rule block attributes

| Attribute | Required | Description |
|-----------|----------|-------------|
| `id` | Yes | Unique rule identifier (e.g., `SEC-001`). Must be unique across all documents in the pack. |
| `mandatory` | No | `"true"` or `"false"`. Indicates whether the rule is mandatory or advisory. |
| `category` | No | Rule category for routing (e.g., `security`, `quality`, `governance`). |
| `severity` | No | Rule severity (e.g., `error`, `warning`, `info`). |
| `domain` | No | Domain classification for routing. |
| `tags` | No | Comma-separated tags for filtering and routing. |
| `profiles` | No | Comma-separated profile names. Rule only applies when the profile is active. |

### Rule content

The text between `:::rule` and `:::` is the rule's primary text. Write it as clear, actionable guidance. This text is what appears in the generated output consumed by downstream tools.

---

## 5. Choosing a scope

The scope determines where your pack's rules sit in the merge precedence hierarchy:

| Scope | Precedence | Use case |
|-------|-----------|----------|
| `global` | Lowest | Organisation-wide baseline rules that any project can override |
| `supplemental` | Middle | Team or department rules that override global but yield to project-local |
| `project` | Highest (same as local) | Shared project-specific rules with the same weight as local documents |

**Merge order:** project-local > project-scoped packs > supplemental-scoped packs > global-scoped packs.

Choose `global` for broad organisational baselines. Choose `supplemental` for team-level standards. Choose `project` only when the pack's rules should have the same authority as the consumer's own project rules.

Consumers can override your declared scope using `--scope` when adding the pack, so the manifest scope is a default recommendation rather than an enforcement.

---

## 6. Multi-pack repositories

A single GitHub repository can host multiple independent rules packs by placing each in its own subdirectory, each with its own `pack.yaml`:

```
governance-packs/
├── baseline/
│   ├── pack.yaml          # name: "baseline-rules", scope: global
│   └── rules.md
├── backend-team/
│   ├── pack.yaml          # name: "backend-team-rules", scope: supplemental
│   └── api-standards.md
└── frontend-team/
    ├── pack.yaml          # name: "frontend-team-rules", scope: supplemental
    └── ui-standards.md
```

Consumers reference a specific subdirectory using the `--path` option:

```bash
steergen rules-pack add github:acme-corp/governance-packs --ref v1.0.0 --path backend-team
```

Or in configuration:

```yaml
rulesPacks:
  - source: "github:acme-corp/governance-packs"
    ref: "v1.0.0"
    path: "backend-team"
```

---

## 7. Publishing to GitHub

Once your pack is ready, push it to a public GitHub repository:

```bash
cd my-rules-pack
git init
git add .
git commit -m "Initial rules pack release"
git remote add origin https://github.com/your-org/my-rules-pack.git
git push -u origin main
```

Tag a release for consumers to pin to:

```bash
git tag v1.0.0
git push --tags
```

Consumers can then add your pack:

```bash
steergen rules-pack add github:your-org/my-rules-pack --ref v1.0.0 --scope global
```

### Pinning recommendations

- **Tags** (e.g., `v1.0.0`) — Recommended for most use cases. Conventionally immutable.
- **Commit SHAs** (40-character hex) — Strongest guarantee of immutability. Steergen skips re-download for SHA-pinned packs.
- **Branches** (e.g., `main`) — Works but Steergen emits a warning recommending pinning. Content can change without notice.

### Consumer upgrade workflow

Consumers can upgrade a specific configured rules pack reference using canonical selectors:

```bash
steergen rules-pack upgrade --selector "github:your-org/my-rules-pack|backend-team" --tag v1.1.0
```

If `--tag` is omitted, Steergen performs a full targeted cache refresh (`latest-refresh`) and still re-pins to a deterministic `(tag, commitSha)` tuple.

---

## 8. Versioning and compatibility

### Pack versioning

Follow semantic versioning for your pack:

- **MAJOR** — Breaking changes (removed rules, changed rule IDs, incompatible restructuring)
- **MINOR** — New rules added, non-breaking scope changes
- **PATCH** — Wording fixes, clarifications, metadata updates

### `minSteergenVersion`

Set this to the oldest Steergen version that can correctly load your pack. If your pack uses features introduced in a specific Steergen release, set `minSteergenVersion` to that release.

When a consumer's Steergen version is older than `minSteergenVersion`, loading fails with diagnostic RP002 and a clear error message.

---

## 9. Testing your pack locally

Before publishing, test your pack by adding it to a local project:

**Option A: Reference via local cache simulation**

1. Create the cache directory structure manually:
   ```bash
   mkdir -p ~/.steergen/rules/your-org/my-rules-pack/v1.0.0
   cp -r ./* ~/.steergen/rules/your-org/my-rules-pack/v1.0.0/
   ```

2. Add to your test project's config:
   ```yaml
   rulesPacks:
     - source: "github:your-org/my-rules-pack"
       ref: "v1.0.0"
       scope: global
   ```

3. Run generation:
   ```bash
   steergen run
   ```

**Option B: Validate documents directly**

Copy your pack's `.md` files into a project's `projectRoot` directory temporarily and run:

```bash
steergen validate
```

This validates the document format, frontmatter, and rule block syntax using the same parser that `RulesPackLoader` uses.

**Option C: Inspect merged output**

After setting up the cache (Option A), inspect the merged model:

```bash
steergen inspect --rules
```

This shows your pack's name, version, scope, and number of rules loaded.

---

## 10. Best practices

### Naming conventions

- Use kebab-case for pack names: `acme-security-rules`, `backend-team-standards`
- Use uppercase prefixed IDs for rules: `SEC-001`, `QUAL-003`, `GOV-012`
- Prefix rule IDs with a short namespace to avoid collisions across packs

### Rule ID uniqueness

Rule IDs must be unique within a pack. When two packs at the same scope declare the same rule ID, the pack listed earlier in the consumer's `rulesPacks` wins and a diagnostic warning is emitted. Use distinctive prefixes to avoid collisions.

### Document organisation

- Group related rules into focused documents (one document per concern area)
- Keep individual documents under 1 MB (Steergen rejects files exceeding this limit)
- Use descriptive filenames: `api-security.md`, `code-review-standards.md`

### Scope selection

- Default to `global` for organisation-wide baselines
- Use `supplemental` for team-specific rules that should override global but not project-local
- Avoid `project` scope unless the pack is tightly coupled to specific projects

### Security considerations

- Do not include secrets, tokens, or credentials in rule documents
- Be aware that rule content appears in generated output consumed by AI tools
- Steergen does not follow symbolic links in pack directories
- Individual files exceeding 1 MB are rejected

### Compatibility

- Set `minSteergenVersion` conservatively — only bump it when you use features from a newer release
- Test your pack against the `minSteergenVersion` you declare
- Document breaking changes in your repository's CHANGELOG

---

## Quick reference: complete example

```
acme-governance-rules/
├── pack.yaml
├── CHANGELOG.md
├── README.md
└── rules/
    ├── security/
    │   ├── authentication.md
    │   └── data-protection.md
    ├── quality/
    │   ├── testing-standards.md
    │   └── code-review.md
    └── operations/
        ├── observability.md
        └── incident-response.md
```

**pack.yaml:**

```yaml
name: "acme-governance-rules"
version: "2.0.0"
minSteergenVersion: "1.5.0"
scope: global
rulesRoot: "rules/"
```

**rules/security/authentication.md:**

```markdown
---
id: security-authentication
version: "2.0.0"
title: Authentication Standards
scope: global
status: active
---

# Authentication Standards

:::rule id="AUTH-001" mandatory="true" category="security" tags="auth,api"
All API endpoints must require authentication. Public endpoints must be
explicitly declared in the service's OpenAPI specification with a
`security: []` override.
:::

:::rule id="AUTH-002" mandatory="true" category="security" tags="auth,tokens"
Authentication tokens must have a maximum lifetime of 1 hour. Refresh
tokens must have a maximum lifetime of 24 hours. Longer-lived tokens
require an approved security exception.
:::

:::rule id="AUTH-003" mandatory="false" category="security" tags="auth,mfa"
Administrative endpoints should require multi-factor authentication.
Services handling PII or financial data must require MFA for all
write operations.
:::
```
