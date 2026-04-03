# Quickstart

## Prerequisites
- .NET 10 SDK
- Supported shell (`bash`, `sh`, `zsh`, `fish`, PowerShell 7+, cmd)

## Build
```bash
dotnet build
```

## Initialize project structure
```bash
steergen init . --target speckit --target kiro-ide
```

## Validate steering sources
```bash
steergen validate --global steering/global --project steering/project
```

## Run generation (all registered targets)
```bash
steergen run --global steering/global --project steering/project --output .steergen/out
```

## Run generation for selected targets
```bash
steergen run --global steering/global --project steering/project --output .steergen/out --target speckit --target kiro-ide
```

## Inspect resolved steering model as JSON
```bash
steergen inspect --global steering/global --project steering/project
steergen inspect --global steering/global --project steering/project --profile production
```

## Register additional targets
```bash
steergen target add copilot-agent
```

## Deregister target (non-destructive to generated artifacts)
```bash
steergen target remove copilot-agent
```

## Update templates and metadata
```bash
steergen update
steergen update --version 1.2.3
```

## Run test suite (PBT-first)
```bash
dotnet test
```

- Test fixtures should use plausible constitution and steering rules representative of real-world governance content where practical.

## Run benchmarks
```bash
dotnet run -c Release --project tests/Steergen.Benchmarks
```

## Release flow
- Tag `master` with SemVer tag format: `vMAJOR.MINOR.PATCH`
- For testing preview releases, use SemVer pre-release format: `vMAJOR.MINOR.PATCH-previewN` (example: `v1.2.3-preview4`)
- CI validates/tests and publishes NuGet package after gates pass
