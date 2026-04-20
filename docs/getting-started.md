# Getting Started with Steergen

Steergen is a command-line tool that keeps a single authoritative set of steering and governance documents for your organisation and automatically transforms them into the format expected by AI-assisted development tools such as [Speckit](https://github.com/aabs/speckit), [Kiro](https://kiro.dev), and GitHub Copilot. Write your rules once, generate everywhere.

This guide walks you through everything from installation to team workflows. You do not need prior experience with any of these tools.

---

## Contents

1. [Install the tool](#1-install-the-tool)
2. [Setting up a new (greenfield) project](#2-setting-up-a-new-greenfield-project)
3. [Adding steergen to an existing (legacy) project](#3-adding-steergen-to-an-existing-legacy-project)
4. [Using a shared policy collection for global steering](#4-using-a-shared-policy-collection-for-global-steering)
5. [Controlling where generated files end up (roots)](#5-controlling-where-generated-files-end-up-roots)
6. [Controlling which rules go where (routing)](#6-controlling-which-rules-go-where-routing)
7. [Wiring steergen into your .csproj build](#7-wiring-steergen-into-your-csproj-build)
8. [Adding steergen validation to your CI/CD pipeline](#8-adding-steergen-validation-to-your-cicd-pipeline)
9. [Writing project steering documents](#9-writing-project-steering-documents)
10. [Tips for teams](#10-tips-for-teams)

---

## 1. Install the tool

Steergen is distributed as a .NET tool. You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) or later installed.

```bash
dotnet tool install --global aabs.steergen
```

Verify the installation:

```bash
steergen --version
```

To update the steergen tool itself to the latest stable release:

```bash
dotnet tool update --global aabs.steergen
```

For a local tool manifest install:

```bash
dotnet tool update aabs.steergen
```

> **Note — `steergen update` updates template-pack metadata, not the tool binary.**
> Running `steergen update` (with or without `--version`/`--preview`) records a new `templatePackVersion`
> in your `steergen.config.yaml`. Use `dotnet tool update` to upgrade the steergen binary itself.

> **Tip — local tool install when you want version pinning**
>
> If you want contributors to use the same tool version, you can use a .NET local tool manifest instead of a global install:
>
> ```bash
> dotnet new tool-manifest          # creates .config/dotnet-tools.json
> dotnet tool install aabs.steergen
> dotnet tool restore               # team members run this after cloning
> steergen --version
> ```
>
> You can commit `.config/dotnet-tools.json` to source control. Running `dotnet tool restore` in CI then installs the pinned version.

---

## 2. Setting up a new (greenfield) project

From your repository root, you can run:

```bash
steergen init . --target speckit --target kiro
```

The `--target` values can be adjusted to match the tools you plan to use. Supported built-in targets are:

| Target           | Generates for                               |
|------------------|---------------------------------------------|
| `speckit`        | Speckit memory files → `<generationRoot>/.specify/memory/`     |
| `kiro`           | Kiro steering files → `<generationRoot>/.kiro/steering/`       |
| `copilot-agent`  | GitHub Copilot instructions → `<projectRoot>/.github/`         |
| `kiro-agent`     | Kiro agent instructions → `<projectRoot>/.kiro/agents/`        |

`init` creates three things in your repository:

```
steering/
  global/          ← bootstrap folder created by init (optional for long-term use)
  project/         ← rules specific to this project go here
steergen.config.yaml ← tool configuration
```

It also bootstraps the target-native output folders so that downstream tools find files where they expect them.

For multi-project use, you can keep the organisation-wide global corpus in a separate repository (or shared folder)
and set `globalRoot` to that external location. Project-specific rules can remain in this repository.
After switching `globalRoot` to the shared location, the bootstrap `steering/global/` folder in this
project can be left empty or removed.

### Write your first steering document

A first global baseline can live in your shared policy repo, for example `../org-steering/global/constitution.md`:

```markdown
---
id: engineering-baseline
title: Engineering Baseline
scope: global
---

# Engineering Baseline

:::rule id="CORE-001" severity="error" category="quality" domain="core" tags="baseline,quality,reviewability"
Prefer small, composable changes that are easy to review and revert.
:::

:::rule id="CORE-002" severity="warning" category="testing" domain="core" tags="testing,regression,ci"
Add or update automated tests whenever observable behaviour changes.
:::
```

Generation can then be run with:

```bash
steergen run
```

Steergen reads your steering documents and writes the correct format for each registered target.

### Commit generated files

Consider committing generated output files alongside your project steering sources. This allows reviewers to see the exact content that will be loaded by AI tools without running the toolchain locally.

---

## 3. Adding steergen to an existing (legacy) project

If a project already has Speckit or Kiro configuration, steergen can be layered on top without immediately replacing those files.

**Step 1 — Create a project steering folder in this repo.**

A project-local location such as `docs/steering/project` works well:

```bash
mkdir -p docs/steering/project
```

You can keep global rules in a shared external location, for example `../org-steering/global`.

**Step 2 — Copy or write project steering documents.**

You can gradually migrate rules from existing agent instruction files, `.kiro/steering/*.md` files, or free-form policy docs into the steergen format. Start with the highest-value rules.

**Step 3 — Create `steergen.config.yaml` by hand or with `init`.**

If you want `init` to bootstrap with your chosen target set, you can pass targets explicitly:

```bash
steergen init . --target kiro --target copilot-agent
```

After that, `steergen.config.yaml` can be updated so roots match your folder structure:

```yaml
globalRoot: ../org-steering/global
projectRoot: docs/steering/project
generationRoot: .
registeredTargets:
  - kiro
  - copilot-agent
```

**Step 4 — Inspect before generation.**

The resolved model can be inspected without writing files:

```bash
steergen inspect
```

After inspection, generation and diff review can be run with:

```bash
steergen run
git diff
```

**Step 5 — Validate.**

Validation can be used to confirm source documents are well-formed:

```bash
steergen validate
```

---

## 4. Using a shared policy collection for global steering

If a single organisation-wide baseline is needed across multiple repositories, a shared policy collection can be referenced instead of copying documents into each project.

The [aabs/steergen-sample-policies](https://github.com/aabs/steergen-sample-policies) repository is a public example of such a collection. It contains ready-to-use steering documents covering quality, security, observability, and supply-chain governance.  This is derived from a mixture of global and project steering rules, and will hopefully mature over time.

### Option A — Shared checkout outside project repositories

```bash
git clone https://github.com/aabs/steergen-sample-policies.git ../org-steering
```

`globalRoot` can point at the shared policies folder:

```yaml
globalRoot: ../org-steering/global
projectRoot: steering/project
generationRoot: .
```

A newer release of the policies can be selected with:

```bash
cd ../org-steering
git fetch
git checkout v2.1.0      # or whatever tag you want to pin
cd -
```

You can keep that shared global repository outside each project repository so one global corpus can be reused across projects with less drift.

### Mixing shared and local global rules

`globalRoot` points to a single folder, so if you want both shared and local global rules, you can keep them together in your shared global repository. One possible layout:

```
org-steering/
  global/
    shared/  ← upstream baseline policies
    local/   ← organisation-specific additions
```

Steergen recursively discovers all `.md` files under `globalRoot`, so nesting works naturally.

---

## 5. Controlling where generated files end up (roots)

`steergen.config.yaml` exposes three root paths that control where steergen looks for source files and where it writes output.

```yaml
globalRoot: ../org-steering/global   # where global steering .md files are discovered
projectRoot: steering/project        # where project steering .md files are discovered
generationRoot: .                    # base path for all generated output
```

### How `generationRoot` is resolved

When `steergen run` writes files, this priority order is used:

1. `--output <path>` command-line flag (highest priority)
2. `generationRoot` from `steergen.config.yaml`
3. Current working directory (fallback)

### Common scenario — shared global corpus + local project docs

This layout keeps organisation-wide policy files outside the project repo while project-specific steering stays local. Generated output still lands in the project root where downstream tools usually expect it:

```yaml
globalRoot: ../org-steering/global
projectRoot: docs/steering/project
generationRoot: .
registeredTargets:
  - speckit
  - kiro
```

### Using `${profileRoot}` and `${tempRoot}` in layout paths

If you need generated files to land outside the repository — for example in a user-profile config folder — the routing layout supports two platform-independent path variables:

| Variable          | Expands to                                        |
|-------------------|---------------------------------------------------|
| `${profileRoot}`  | User's home directory (`~` / `%USERPROFILE%`)     |
| `${tempRoot}`     | System temporary directory                        |

These can be useful in layout override files (see [section 6](#6-controlling-which-rules-go-where-routing)) when you want a target to write to a user-level location, such as a shared Copilot instructions folder.

---

## 6. Controlling which rules go where (routing)

By default steergen uses a built-in routing layout for each target. For example, the Speckit target routes:

- Global rules with `domain: core` → `constitution.md`
- All other global rules → `{domain}.md` (one file per domain)
- Project rules with `domain: core` → `project-constitution.md`
- All other project rules → `project-{domain}.md`

You can override this layout for any target without affecting others.

### Creating a layout override

A layout override YAML file can be created, for example `config/layouts/speckit.yaml`:

```yaml
version: "1.0"

routes:
  # Keep core rules in a single constitution file
  - id: core-global
    scope: global
    explicit: true
    anchor: core
    order: 10
    match:
      domain: core
    destination:
      directory: "${targetRoot}"
      fileName: "constitution"
      extension: ".md"

  # Route security rules to a dedicated file
  - id: security-global
    scope: global
    explicit: true
    order: 20
    match:
      domain: security
    destination:
      directory: "${targetRoot}"
      fileName: "security"
      extension: ".md"

  # Everything else falls into domain-named files
  - id: catch-all-global
    scope: global
    explicit: false
    order: 100
    match:
      domain: "*"
    destination:
      directory: "${targetRoot}"
      fileName: "${domain}"
      extension: ".md"

fallback:
  mode: other-at-core-anchor
  fileBaseName: other
```

The override can be linked in `steergen.config.yaml`:

```yaml
targets:
  - id: speckit
    layoutOverridePath: config/layouts/speckit.yaml
  - id: kiro
    # no override — uses built-in default
```

### Match expressions

Routes match on rule metadata. All specified fields must match (logical AND):

```yaml
match:
  domain: api               # exact domain value
  category: security        # exact category value
  severity: error           # exact severity value
  tagsAny:                  # any of these tags present (OR)
    - pii
    - compliance
  profile: strict           # only for rules with profile="strict"
```

Use `"*"` to match any value of a field. A more specific route always wins over a wildcard route.

### Profile-gated rules

A rule can include a `profile` attribute in the source document:

```markdown
:::rule id="SEC-010" severity="error" category="security" domain="core" profile="strict" tags="security,strict,token-lifecycle"
Token lifetime must not exceed 15 minutes in production environments.
:::
```

Profiles can be activated by setting `activeProfiles` in `steergen.config.yaml`:

```yaml
activeProfiles:
  - default
  - strict
```

Rules with no `profile` attribute are included by default. Rules with a `profile` attribute are included only when that profile is active.

---

## 7. Wiring steergen into your .csproj build

You can make generation run automatically as part of your normal build so generated files can stay current with project changes.

An MSBuild target can be added to your `.csproj` (or to a shared `Directory.Build.targets` file for repo-wide coverage):

```xml
<Target Name="SteergenRun"
        BeforeTargets="Build"
        Condition="'$(SteergenSkip)' != 'true'">
  <Exec Command="steergen run" WorkingDirectory="$(MSBuildProjectDirectory)" />
</Target>
```

For repositories using a local tool manifest (`.config/dotnet-tools.json`), commands can run through `dotnet tool run` to ensure the pinned version is used:

```xml
<Target Name="SteergenRun"
        BeforeTargets="Build"
        Condition="'$(SteergenSkip)' != 'true'">
  <Exec Command="dotnet tool run steergen run"
        WorkingDirectory="$(MSBuildProjectDirectory)" />
</Target>
```

### Validate instead of generate during CI

On CI, you may prefer to validate source documents and assert that committed generated files are up to date rather than re-generating. A separate target for this:

```xml
<Target Name="SteergenValidate"
        AfterTargets="Build"
        Condition="'$(CI)' == 'true'">
  <Exec Command="steergen validate"
        WorkingDirectory="$(MSBuildProjectDirectory)" />
</Target>
```

`-p:SteergenSkip=true` can be passed to builds that should skip the steergen step entirely:

```bash
dotnet build -p:SteergenSkip=true
```

---

## 8. Adding steergen validation to your CI/CD pipeline

Steergen's `validate` command exits with a well-defined exit code, making it straightforward to integrate into any CI system.

| Exit code | Meaning                                           |
|-----------|---------------------------------------------------|
| `0`       | All documents are valid                           |
| `1`       | One or more validation errors found               |
| `2`       | Configuration error (missing directory, etc.)     |

### GitHub Actions

```yaml
name: Steergen Validate

on:
  pull_request:
  push:
    branches: [main]

jobs:
  validate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Checkout shared global policies
        run: git clone https://github.com/aabs/steergen-sample-policies.git ../org-steering

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.x'

      - name: Restore local tools
        run: dotnet tool restore   # installs from .config/dotnet-tools.json

      - name: Validate steering documents
        run: steergen validate
```

### Asserting generated files are up to date

To detect output drift after steering edits:

```yaml
      - name: Regenerate and check for drift
        run: |
          steergen run
          git diff --exit-code -- '*.md' '*.instructions.md'
```

`git diff --exit-code` returns a non-zero exit code if any tracked file has changed, failing the build when the committed output is stale.

### Azure DevOps Pipelines

```yaml
trigger:
  branches:
    include: ['*']

pool:
  vmImage: 'ubuntu-latest'

steps:
  - checkout: self

  - script: git clone https://github.com/aabs/steergen-sample-policies.git ../org-steering
    displayName: Checkout shared global policies

  - task: UseDotNet@2
    inputs:
      version: '10.x'

  - script: dotnet tool restore
    displayName: Restore local tools

  - script: steergen validate
    displayName: Validate steering documents
    failOnStderr: false   # steergen writes diagnostics to stdout

  - script: |
      steergen run
      git diff --exit-code
    displayName: Assert generated files are not stale
```

---

## 9. Writing project steering documents

Project documents extend global rules with guidance specific to one service, domain, or team. They are discovered under `projectRoot` (by default `steering/project/`).

### Document format

A steering document is a Markdown file with a YAML front-matter header followed by free text and rule blocks:

```markdown
---
id: platform-api-steering-v1
title: Platform API Steering Rules
scope: project
inherits: engineering-baseline
status: active
---

# Platform API Steering Rules

These rules extend the organisation baseline for the Platform API service.

:::rule id="API-001" category="api-design" domain="api" tags="api,versioning"
All REST endpoints must include an explicit version prefix (e.g. `/v1/`).
:::

:::rule id="API-002" category="api-design" domain="api" tags="api,versioning"
Header-based version negotiation is not permitted.
:::

:::rule id="API-003" category="api-design" domain="api" tags="api,versioning,backward-compatibility"
When introducing a new major version, the previous version must remain
available for a minimum of 90 days.
:::

:::rule id="API-004" severity="warning" category="observability" domain="api" tags="observability,structured-logging,correlation-id"
All log statements must use structured logging with JSON output in production.
Every request must carry a `X-Correlation-Id` that is propagated to all
downstream calls and included in every log entry.
:::
```

### Rule block attributes

| Attribute    | Required | Description                                                     |
|--------------|----------|-----------------------------------------------------------------|
| `id`         | Yes      | Unique rule identifier (e.g. `API-001`)                         |
| `severity`   | No       | `error`, `warning`, `info`, or `hint`                           |
| `category`   | No       | Logical grouping (e.g. `security`, `testing`, `api-design`)     |
| `domain`     | No       | Routing domain — controls which file the rule ends up in        |
| `title`      | No       | Short human-readable label shown in generated output            |
| `profile`    | No       | Profile name — rule only included when that profile is active   |
| `tags`       | No       | Comma-separated labels (e.g. `tags="pii,compliance"`)           |
| `supersedes` | No       | ID of a global rule this project rule overrides or supersedes   |

### Where project documents live

Any number of documents can exist under `projectRoot`. Steergen discovers all `.md` files recursively, so sub-folder organization is supported:

```
steering/project/
    platform-api/
        api-design.md
        observability.md
    data-platform/
        data-governance.md
```

### Configuring your tool to pick up local files

If a project lives in a subdirectory of a larger repository, `projectRoot` can be set relative to the `steergen.config.yaml` location:

```yaml
globalRoot: ../../../org-steering/global
projectRoot: steering/project
generationRoot: .
```

`steergen inspect` can be used at any time to view how steergen has resolved rules from both roots:

```bash
steergen inspect
```

---

## 10. Tips for teams

### Treat steering documents like code

You can review steering documents using the same process you use for code. Pull requests that change rules may benefit from consistency and completeness checks, especially when they touch domain boundaries or severity levels that downstream tooling enforces.

### Use `steergen inspect` in code review

Reviewers can run `steergen inspect` on a branch to view the resolved rule set as JSON, which can make regressions or conflicts between global and project rules easier to spot:

```bash
steergen inspect | jq '.rules[] | {id, severity, domain}'
```

### Pin the tool version in version control

If you want contributors and CI runs to use the same version, you can use `.config/dotnet-tools.json` (see [section 1](#1-install-the-tool)). Deliberate upgrades can then happen through normal PRs.

### Separate global from project concerns clearly

- **Global rules** are organisational standards that apply to every project. You can keep them in a shared repository (or shared central folder) that multiple project repositories reference.
- **Project rules** are specific to one service or team. They live under `steering/project/` in the service's own repository.

Consider avoiding per-project copies of the global corpus. A shared global corpus can reduce drift and make policy changes easier to keep consistent across projects.

### Use profiles for environment-specific rules

If certain rules apply only in production or in a hardened security posture, the `profile` attribute can be used instead of maintaining separate document sets:

```markdown
:::rule id="SEC-010" severity="error" domain="core" profile="strict" tags="security,strict,production"
Token lifetime must not exceed 15 minutes in production deployments.
:::
```

The profile can be activated by setting it in `steergen.config.yaml` before running generation:

```yaml
activeProfiles:
  - strict
```

Alternatively, a separate config file can be maintained for each posture and passed explicitly:

```bash
steergen run --config steergen.strict.config.yaml
```

> **Note — `steergen run` has no `--profile` flag.** Profiles for `run` must be set via
> `activeProfiles` in the config file. The `steergen inspect` command does accept `--profile`
> flags for ad-hoc inspection: `steergen inspect --profile strict`.

Builds without active profiles include only rules with no `profile` attribute, which can keep the day-to-day development experience less cluttered.

### Check generated files into source control

Committing generated output can provide these benefits:

- Contributors can use generated AI-tool files immediately after cloning, without running steergen first.
- Pull request diffs show the exact impact of a rule change on all targets.
- The CI drift check (see [section 8](#8-adding-steergen-validation-to-your-cicd-pipeline)) catches forgotten regeneration.

### Use the `purge` command when removing rules

When rules are deleted or renamed, steergen no longer writes to prior output files, and it does not delete them automatically during `run`. The purge command can remove stale generated files for a target:

```bash
steergen purge --target speckit
steergen purge --target kiro --dry-run    # preview first
```

The purge policy is defined per-target in the layout YAML (built-in or override). Only files matching the configured globs within the configured roots are eligible for deletion.

### Add a README note for contributors

Once steergen is set up, you can add a short note to your project README directing contributors to the steering folders and explaining the generation workflow:

> Project rules live in `steering/project/` in this repository. Global rules are maintained in our shared policy repository referenced by `globalRoot`. After editing project rules, run `steergen run` and commit generated output changes.

For multi-project organisations, you can call out both roots explicitly in contributor docs.
