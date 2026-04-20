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

:::rule id="CORE-001" severity="error" category="quality" domain="core" tags="quality,reviews"
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
| `steergen validate [options]` | Validate source documents; exits non-zero on errors |
| `steergen inspect [options]` | Print the resolved steering model as JSON |
| `steergen target add <id>` | Register a new target |
| `steergen target remove <id>` | Unregister a target |
| `steergen purge [options]` | Remove generated files managed by steergen |
| `steergen update [--version <v>] [--preview]` | Update `templatePackVersion` in the config file |

**Commonly used `run` options:**

```
--config <path>     Path to steergen.config.yaml
--global <dir>      Override globalRoot
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
globalRoot: steering/global
projectRoot: steering/project
generationRoot: .
registeredTargets:
  - kiro
  - copilot-agent
```

Key fields:

| Field | Purpose |
|---|---|
| `globalRoot` | Source folder for organisation-wide steering docs |
| `projectRoot` | Source folder for project-specific steering docs |
| `generationRoot` | Base folder for all generated output |
| `registeredTargets` | List of targets to generate by default |
| `activeProfiles` | Profile names used to filter which rules are included |
| `templatePackVersion` | Template pack version (managed by `steergen update`) |

For full configuration options and advanced routing, see [Section 5](docs/getting-started.md#5-controlling-where-generated-files-end-up-roots) and [Section 6](docs/getting-started.md#6-controlling-which-rules-go-where-routing) of the Getting Started guide.

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
Check that `globalRoot` and `projectRoot` point to the same content on each machine. Use `steergen inspect` to compare the resolved model.

**Something else?**
Open an issue at <https://github.com/aabs/steergen/issues>.

---

## License

[MIT](LICENSE)

