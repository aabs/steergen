# Steergen

**Write your AI steering docs once. Generate for every tool.**

[![Build](https://github.com/aabs/steergen/actions/workflows/ci.yml/badge.svg)](https://github.com/aabs/steergen/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/aabs.steergen.svg)](https://www.nuget.org/packages/aabs.steergen)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

Steergen is a .NET CLI tool that maintains a single set of steering and constitution documents, then generates the target-specific formats expected by tools like [Kiro](https://kiro.dev) and [Speckit](https://github.com/aabs/speckit). Change your guidance once; every downstream tool stays in sync.

> For a full walkthrough — including greenfield setup, CI integration, and writing rules — see the **[Getting Started guide](docs/getting-started.md)**.

---

## Table of Contents

- [Requirements](#requirements)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Supported Targets](#supported-targets)
- [Command Reference](#command-reference)
- [Configuration](#configuration)
- [Template Packs](#template-packs)
- [Rules Packs](#rules-packs)
- [Authoring a Rules Pack](docs/authoring-rules-packs.md)
- [Sample Packs](docs/getting-started.md#12-sample-packs)
- [Exit Codes](#exit-codes)
- [Contributing](#contributing)
- [Troubleshooting](#troubleshooting)
- [License](#license)

---

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later

---

## Installation

```bash
dotnet tool install --global aabs.steergen
```

To upgrade an existing installation:

```bash
dotnet tool update --global aabs.steergen
```

Verify the install:

```bash
steergen --version
```

---

## Quick Start

**1. Initialise a project** for the targets you want to use:

```bash
steergen init . --target kiro --target copilot-agent
```

This creates `steering/global/`, `steering/project/`, `steergen.config.yaml`, and the target-native output folders.

**2. Write a steering document** under `steering/project/`:

```md
---
id: engineering-baseline
title: Engineering Baseline
---

# Engineering Baseline

:::rule id="CORE-001" mandatory="true" category="quality" tags="quality,reviews"
Prefer small, composable changes that are easy to review and easy to revert.
:::
```

**3. Generate outputs** for all registered targets:

```bash
steergen run
```

**4. Validate** source documents without regenerating:

```bash
steergen validate
```

That's it. For more scenarios — shared policy collections, custom output paths, MSBuild integration, CI setup — see the **[Getting Started guide](docs/getting-started.md)**.

---

## Supported Targets

| Target | Default output path |
|---|---|
| `kiro` | `.kiro/steering/` |
| `speckit` | `.specify/memory/` |
| `copilot-agent` | `.github/` |
| `kiro-agent` | `.kiro/agents/` |

Add or remove targets at any time:

```bash
steergen target add speckit
steergen target remove kiro
```

---

## Command Reference

| Command | Purpose |
|---|---|
| `steergen init [root] [--target <id>...]` | Bootstrap config and target folders |
| `steergen run [options]` | Generate output files for all registered targets |
| `steergen validate [options]` | Validate source documents and template packs |
| `steergen inspect [--templates] [--rules]` | Print resolved model, template chain, or rules pack info |
| `steergen target add <id>` | Register a new target (built-in or pack-provided) |
| `steergen target remove <id>` | Unregister a target |
| `steergen purge [options]` | Remove generated files managed by steergen |
| `steergen update [--templates] [--rules] [--force]` | Re-download configured packs |
| `steergen template-pack add <source> [--ref <ref>] [--path <localPath>]` | Add a template pack |
| `steergen template-pack upgrade --selector <source\|entryKey> [--tag <tag>]` | Upgrade the configured template pack reference |
| `steergen template-pack remove` | Remove the configured template pack |
| `steergen rules-pack add <source> [--ref <ref>] [--path <subdir>] [--scope <scope>]` | Add a rules pack |
| `steergen rules-pack upgrade --selector <source\|path> [--tag <tag>]` | Upgrade one configured rules pack reference |
| `steergen rules-pack remove <name>` | Remove a rules pack by name |
| `steergen rules-pack list` | List configured rules packs with status |

**Commonly used `run` options:**

```
--config <path>     Path to steergen.config.yaml
--project <dir>     Override projectRoot
--output <dir>      Override generationRoot
--target <id>       Generate for one target only (repeatable)
--quiet             Suppress informational output
--verbose           Show detailed output
```

---

## Configuration

Steergen looks for `steergen.config.yaml` in the current directory (or the path given by `--config`).

A minimal config file:

```yaml
projectRoot: steering/project
generationRoot: .
registeredTargets:
  - kiro
  - copilot-agent
```

A config with template pack and rules packs:

```yaml
projectRoot: steering/project
generationRoot: .

templatePack:
  source: "github:acme-corp/steergen-templates"
  ref: "v2.1.0"

rulesPacks:
  - source: "github:acme-corp/baseline-rules"
    ref: "abc123def456789012345678901234567890abcd"
    scope: global
  - source: "github:acme-corp/team-rules"
    ref: "v1.0.0"
    path: "backend-team"

registeredTargets:
  - kiro
  - copilot-agent
```

Key fields:

| Field | Purpose |
|---|---|
| `projectRoot` | Source folder for project-specific steering docs |
| `generationRoot` | Base folder for all generated output |
| `registeredTargets` | List of targets to generate by default |
| `templatePack` | Template pack source configuration (see [Template Packs](#template-packs)) |
| `rulesPacks` | List of rules pack entries (see [Rules Packs](#rules-packs)) |
| `activeProfiles` | Profile names (legacy; retained for backward compatibility) |

> **Note:** The `globalRoot` field has been removed. If your config still contains `globalRoot`, Steergen will report error CFG001 and exit with code 2. See the [migration guide](docs/migration-globalroot.md) for how to convert existing global rules to a rules pack.

For full configuration options and advanced routing, see [Section 5](docs/getting-started.md#5-controlling-where-generated-files-end-up-roots) and [Section 6](docs/getting-started.md#6-controlling-which-rules-go-where-routing) of the Getting Started guide.

---

## Template Packs

Template packs let you override the built-in Scriban templates that Steergen uses to render output, or provide complete target definitions for new external targets. Packs can be sourced from a local directory or a public GitHub repository.

### Adding a template pack from GitHub

```bash
steergen template-pack add github:acme-corp/steergen-templates --ref v2.1.0
```

This writes the source to `steergen.config.yaml` and downloads the pack to the local cache at `~/.steergen/packs/acme-corp/steergen-templates/v2.1.0/`.

### Adding a local template override path

```bash
steergen template-pack add --path ./custom-templates
```

Local overrides take the highest precedence in the resolution chain.

### Template resolution precedence

When rendering, Steergen resolves templates in this order:

1. **Local override path** (`templatePack.localPath`) — highest precedence
2. **Cached GitHub pack** (`templatePack.source`) — middle precedence
3. **Built-in embedded templates** — fallback

### Updating a template pack

Re-download the configured GitHub pack to pick up changes:

```bash
steergen update --templates
```

On success, displays the pack name, version, and number of template files. If the pack is pinned to a 40-character SHA, re-download is skipped unless `--force` is specified:

```bash
steergen update --templates --force
```

If no template pack is configured, the command exits with code 0 and reports that no pack source is configured.

Upgrade a specific template pack reference and persist a deterministic `(tag, commitSha)` tuple:

```bash
steergen template-pack upgrade --selector "github:acme-corp/steergen-templates|templates/default" --tag v2.1.0
```

When `--tag` is omitted, the command runs in `latest-refresh` mode, snapshots cache, purges the targeted cache copy, and refetches.

### Inspecting the template chain

See which templates come from which source:

```bash
steergen inspect --templates
```

Displays the active resolution chain showing the source (local override, cached GitHub pack, or built-in) for each template.

### Removing a template pack

```bash
steergen template-pack remove
```

Removes the template pack configuration from `steergen.config.yaml`.

### Configuration reference

```yaml
templatePack:
  source: "github:acme-corp/steergen-templates"  # GitHub source
  ref: "v2.1.0"                                   # Tag, branch, or 40-char SHA
  # OR use a local path instead:
  # localPath: "./custom-templates"
```

---

## Rules Packs

Rules packs are shared governance rule sets published to GitHub repositories. They let teams share steering documents across projects without copying files. Each pack declares a scope that determines its merge precedence relative to project-local rules.

> To create and publish your own rules pack, see the **[Authoring a Rules Pack](docs/authoring-rules-packs.md)** guide.

### Adding a rules pack

```bash
steergen rules-pack add github:acme-corp/baseline-rules --ref v1.0.0 --scope global
```

Options:

| Option | Purpose |
|---|---|
| `--ref <ref>` | Git tag, branch, or 40-character SHA |
| `--path <subdir>` | Subdirectory within the repo (for multi-pack repos) |
| `--scope <scope>` | Override the pack's manifest scope (`global`, `supplemental`, or `project`) |

The command appends the pack to the `rulesPacks` list in `steergen.config.yaml` and downloads it to `~/.steergen/rules/{owner}/{repo}/{ref}/`.

### Scope-based merge precedence

When multiple rule sources define the same rule ID, Steergen resolves conflicts using scope-based precedence:

1. **Project-local rules** — highest precedence (your `projectRoot` documents)
2. **Project-scoped packs** — rules packs with `scope: project`
3. **Supplemental-scoped packs** — rules packs with `scope: supplemental`
4. **Global-scoped packs** — rules packs with `scope: global` (lowest precedence)

Within the same scope level, packs declared earlier in the `rulesPacks` list take precedence. Duplicate rule IDs at the same scope emit a diagnostic warning.

The `--scope` option on `rules-pack add` overrides the scope declared in the pack's own manifest, letting consumers elevate or demote a pack's precedence.

### Listing configured rules packs

```bash
steergen rules-pack list
```

Displays all configured rules packs with their source, ref, scope, and cache status.

### Updating rules packs

Re-download all configured rules packs:

```bash
steergen update --rules
```

SHA-pinned packs are skipped unless `--force` is specified:

```bash
steergen update --rules --force
```

Upgrade exactly one configured rules pack reference by canonical selector:

```bash
steergen rules-pack upgrade --selector "github:acme-corp/team-rules|backend-team" --tag v1.1.0
```

Selector escaping rules:

- Use `\\|` for a literal `|` inside either selector component.
- Use `\\\\` for a literal backslash.

### Inspecting rules packs

```bash
steergen inspect --rules
```

Displays all configured rules packs with their name, version, source, scope, and number of rules loaded.

### Removing a rules pack

```bash
steergen rules-pack remove acme-baseline-rules
```

Removes the matching entry from the `rulesPacks` list in `steergen.config.yaml`.

### Configuration reference

```yaml
rulesPacks:
  - source: "github:acme-corp/baseline-rules"
    ref: "abc123def456789012345678901234567890abcd"  # Pinned SHA (recommended)
    scope: global
  - source: "github:acme-corp/team-rules"
    ref: "v1.0.0"
    path: "backend-team"                             # Subdirectory within repo
  - source: "github:acme-corp/security-rules"
    ref: "main"                                      # Branch (pinning recommended)
    scope: supplemental
```

> **Tip:** Pin rules packs to a tag or full SHA for deterministic builds. Branch refs work but Steergen will recommend pinning in diagnostic output.

---

## Exit Codes

| Code | Meaning |
|---|---|
| `0` | Success |
| `1` | Validation errors in source documents |
| `2` | Configuration or I/O error |
| `3` | Generation or purge error |
| `5` | Output conflict (file already exists with different content) |

---

## Contributing

Contributions are welcome. To get started locally:

```bash
git clone https://github.com/aabs/steergen.git
cd steergen
dotnet build
dotnet test
```

For an overview of how the codebase is structured, see the [developer guide](docs/development/how-steergen-works.md) or load the [code tour](docs/development/end-to-end-pipeline.tour) in VS Code with the CodeTour extension.

Please open an issue before submitting a pull request for significant changes.

---

## Troubleshooting

**`steergen` not found after install**
Ensure `~/.dotnet/tools` (Linux/macOS) or `%USERPROFILE%\.dotnet\tools` (Windows) is on your `PATH`.

**No output files generated**
Run `steergen validate` first — generation is skipped when source documents contain errors.

**Generated files differ between machines**
Check that `projectRoot` points to the same content on each machine and that rules packs are pinned to the same ref. Use `steergen inspect` to compare the resolved model.

**Something else?**
Open an issue at <https://github.com/aabs/steergen/issues>.

---

## License

[MIT](LICENSE)

