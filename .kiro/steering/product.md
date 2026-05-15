# Product Overview

Steergen is a .NET CLI tool that maintains a single set of AI steering and constitution documents, then generates target-specific formats expected by downstream tools (Kiro, Speckit, Copilot Agent, Kiro Agent).

The core value proposition: write guidance once, generate for every tool. Change your steering docs in one place and every downstream integration stays in sync.

## Key Concepts

- **Steering documents**: Markdown files with YAML frontmatter and `:::rule` blocks that define governance rules
- **Targets**: Output format adapters (kiro, speckit, copilot-agent, kiro-agent) that render rules into tool-native file structures
- **Routing**: A layout engine that determines which rules go to which output files based on domain, category, severity, profile, and scope
- **Write plan**: An intermediate representation that separates routing decisions from rendering, allowing targets to focus only on content formatting

## Distribution

Published as a .NET global tool on NuGet (`aabs.steergen`). Installed via `dotnet tool install --global aabs.steergen`.

## CLI Commands

| Command | Purpose |
|---------|---------|
| `steergen init` | Bootstrap config and target folders |
| `steergen run` | Generate output files for registered targets |
| `steergen validate` | Validate source documents without generating |
| `steergen inspect` | Print resolved steering model as JSON |
| `steergen target add/remove` | Manage registered targets |
| `steergen purge` | Remove generated files |
| `steergen update` | Update template pack version in config |
