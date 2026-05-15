# Project Structure

## Solution Layout

```
specgen.slnx                    # Solution file (.slnx format)
Directory.Build.props           # Shared build properties (TFM, analyzers, nullable)
Directory.Build.targets         # Shared build targets
global.json                     # SDK version pinning
NuGet.config                    # Package source configuration
```

## Source Projects (`src/`)

### Steergen.Cli
The CLI entry point. Packaged as a .NET global tool.

- `Commands/` — One file per CLI command (RunCommand, InitCommand, ValidateCommand, etc.)
- `Composition/` — DI setup and command factory
- `Diagnostics/` — Telemetry reporting

### Steergen.Core
Domain logic library. No CLI or UI dependencies.

- `Configuration/` — Config loading, layout override loading, routing schema validation
- `Generation/` — Pipeline orchestration, route resolution, write plan building, target generation services
- `Merge/` — SteeringResolver that merges global + project docs into a resolved model
- `Model/` — All domain types (SteeringDocument, SteeringRule, WritePlan, RouteResolutionResult, etc.)
- `Parsing/` — Markdown parser that extracts frontmatter and `:::rule` blocks
- `Targets/` — Target component implementations and registry
  - `Kiro/` — Kiro target with layout YAML
  - `Speckit/` — Speckit target with layout YAML
  - `Agents/` — Agent targets (copilot-agent, kiro-agent)
  - `Fixtures/` — Test fixture target
- `Updates/` — Template pack version management, constitution provenance
- `Validation/` — Corpus validation (rule IDs, severities, domains, duplicates)

### Steergen.Templates
Embedded Scriban templates for each target.

- `Scriban/` — Templates organized by target ID (e.g., `kiro/`, `speckit/`)

## Test Projects (`tests/`)

| Project | Strategy |
|---------|----------|
| `Steergen.Core.PropertyTests` | Property-based tests with CsCheck (primary test strategy) |
| `Steergen.Core.UnitTests` | Example-based unit tests with NSubstitute |
| `Steergen.Cli.IntegrationTests` | End-to-end CLI command tests |
| `Steergen.Benchmarks` | BenchmarkDotNet performance tests |
| `Fixtures/` | Shared test fixture data (realistic governance docs) |

## Pipeline Flow (runtime order)

1. CLI command parsing → `Commands/RunCommand.cs`
2. Source document discovery → recursive `*.md` enumeration from configured roots
3. Markdown parsing → `Parsing/SteeringMarkdownParser.cs`
4. Validation → `Validation/SteeringValidator.cs`
5. Merge/resolve → `Merge/SteeringResolver.cs`
6. Layout loading → `Configuration/LayoutOverrideLoader.cs`
7. Route planning → `Generation/RoutePlanner.cs` + `RouteResolver.cs`
8. Write plan building → `Generation/WritePlanBuilder.cs`
9. Target rendering → `Targets/{TargetId}/{Target}TargetComponent.cs`
10. File output via Scriban templates

## Architecture Principles

- Clear separation: parsing → model → validation → routing → rendering
- Targets are additive: new targets must not require refactoring existing targets or core pipeline
- No dynamic plugin loading; targets are registered via static registry
- The write plan is the boundary between routing (generic) and rendering (target-specific)
- Embedded resources for templates and default layouts (no runtime file dependencies)
