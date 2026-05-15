# Tech Stack and Build

## Runtime and Language

- **.NET 10** (SDK 10.0.105, rollForward: latestMinor)
- **C# 14** (LangVersion 14.0)
- Nullable reference types enabled globally
- Implicit usings enabled
- TreatWarningsAsErrors enabled
- .NET analyzers at latest analysis level

## Key Libraries

| Library | Purpose |
|---------|---------|
| System.CommandLine (3.0.0-preview) | CLI argument parsing and command dispatch |
| Microsoft.Extensions.DependencyInjection | Service composition |
| YamlDotNet (16.3.0) | YAML config and layout parsing |
| Scriban (7.0.6) | Template rendering for target outputs |

## Test Frameworks

| Library | Purpose |
|---------|---------|
| xUnit (2.9.3) | Test framework |
| CsCheck (4.6.2) | Property-based testing (PBT is the default strategy) |
| NSubstitute (5.3.0) | Mocking in unit tests |
| coverlet.collector (6.0.4) | Code coverage |
| BenchmarkDotNet (0.15.8) | Performance benchmarks |

## Solution File

The solution uses the `.slnx` format: `specgen.slnx`

## Common Commands

```bash
# Build the entire solution
dotnet build

# Run all tests
dotnet test

# Run only property tests
dotnet test tests/Steergen.Core.PropertyTests

# Run only unit tests
dotnet test tests/Steergen.Core.UnitTests

# Run integration tests
dotnet test tests/Steergen.Cli.IntegrationTests

# Run benchmarks
dotnet run --project tests/Steergen.Benchmarks -c Release

# Pack the CLI tool
dotnet pack src/Steergen.Cli

# Publish as single-file portable executable
dotnet publish src/Steergen.Cli -c Release -p:PublishPortable=true
```

## CI and Release

- Releases follow SemVer, triggered by tagging master with `vMAJOR.MINOR.PATCH`
- Preview releases use `vMAJOR.MINOR.PATCH-previewN` tags
- The CLI is published as a .NET global tool to NuGet (`aabs.steergen`)
- PublishTrimmed is enabled for the tool package (partial trim mode)

## Code Style Enforcement

- Deterministic builds enabled
- .NET analyzers at latest level with warnings as errors
- Static analysis must pass in CI before merge
