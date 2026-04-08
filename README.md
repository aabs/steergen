# Steergen

Steergen is a CLI for maintaining a single set of steering and constitution documents in your repository, then converting that corpus into the formats expected by spec-driven development tools such as Speckit and Kiro.

The point of the tool is to let you switch between downstream tools without having to manually reconcile multiple copies of the same guidance.

The examples below are intended as a guide to using the tool in everyday work:

- initialize a repo for one or more targets
- write steering docs in Markdown
- run generation
- validate or regenerate whenever the source docs change

For the full onboarding flow, use the Getting Started guide on the wiki:

- <https://github.com/aabs/steergen/wiki/Getting-Started>

## Install

```bash
dotnet tool install --global aabs.steergen
```

## Everyday Usage

Initialize a repository for the targets you want to generate:

```bash
steergen init . --target kiro --target copilot-agent
```

That creates:

- `steering/global/`
- `steering/project/`
- `steergen.config.yaml`
- target-native output folders for the selected targets

Add a steering document under `steering/global/` or `steering/project/`.

Example:

```md
---
id: engineering-baseline
title: Engineering Baseline
---

# Engineering Baseline

:::rule id="CORE-001" severity="error" category="quality" domain="core"
Prefer small, composable changes that are easy to review and easy to revert.
:::

:::rule id="CORE-002" severity="warning" category="testing" domain="core"
Add or update automated tests when behaviour changes.
:::
```

Generate outputs:

```bash
steergen run
```

Validate source documents without generating:

```bash
steergen validate
```

If `steergen.config.yaml` is present in the current directory, `run` and `validate` will discover it automatically.

## Configuration Roots

Steergen supports separate roots for source discovery and generated output:

- `globalRoot`: global steering source folder
- `projectRoot`: project steering source folder
- `generationRoot`: base folder where routed output files are written when `--output` is not provided

`steergen run` resolves output base in this order:

1. `--output`
2. `generationRoot` from `steergen.config.yaml`
3. current working directory

Example `steergen.config.yaml`:

```yaml
globalRoot: steering/global
projectRoot: steering/project
generationRoot: .
registeredTargets:
	- speckit
	- kiro
```

## Scenario: Sources Under docs/steering, Output Under Solution Root

If your steering sources live under `docs/steering` but you want generated target files rooted at the solution folder:

```yaml
globalRoot: docs/steering/global
projectRoot: docs/steering/project
generationRoot: .
registeredTargets:
	- speckit
	- kiro
```

From the solution root, run:

```bash
steergen run
```

This keeps source discovery under `docs/steering/*` while writing routed outputs (for example `.kiro/steering/*`, `.speckit/memory/*`) relative to the solution root.

## A Few Common Examples

Generate for a single target:

```bash
steergen run --target kiro
```

Generate under an explicit output base:

```bash
steergen run --output .steergen/out
```

Validate explicit source roots:

```bash
steergen validate --global steering/global --project steering/project
```

Inspect the resolved model as JSON:

```bash
steergen inspect --global steering/global --project steering/project
```

Add or remove registered targets later:

```bash
steergen target add speckit
steergen target remove kiro
```

## Supported Built-In Targets

Steergen currently includes built-in support for:

- `kiro`
- `speckit`
- `copilot-agent`
- `kiro-agent`

The exact generated folder layout is target-specific. `steergen init` bootstraps the expected target folders, and `steergen run` writes output using the selected target's built-in layout.

## Command Summary

```bash
steergen init [project-root] [--target <id>...]
steergen run [--config <path>] [--global <dir>] [--project <dir>] [--output <dir>] [--target <id>...]
steergen validate [--config <path>] [--global <dir>] [--project <dir>]
steergen inspect [--global <dir>] [--project <dir>]
steergen target add <id>
steergen target remove <id>
steergen update [--version <version>] [--preview]
```

## More Detail

The README stays focused on basic usage. For deeper material:

- Getting started and setup walkthrough: <https://github.com/aabs/steergen/wiki/Getting-Started>
- Developer guide: [docs/development/how-steergen-works.md](docs/development/how-steergen-works.md)
- Code tour: [docs/development/end-to-end-pipeline.tour](docs/development/end-to-end-pipeline.tour)

## CI Notes

For automation, the most useful commands are:

```bash
steergen validate
steergen run --output .steergen/out
```

`validate` exits non-zero on document errors. `run` can write a deterministic generation manifest alongside output artefacts when an explicit output base is used.

