# Implementation Plan: Steering Document Transformation Tool

**Branch**: `001-steering-doc-transform` | **Date**: 2026-04-03 | **Spec**: `/Users/aabs/dev/aabs/prototypes/specgen/specs/001-steering-doc-transform/spec.md`
**Input**: Feature specification from `/Users/aabs/dev/aabs/prototypes/specgen/specs/001-steering-doc-transform/spec.md`

## Summary

Build a deterministic, security-hardened .NET 10/C# 14 CLI (`steergen`) that parses steering Markdown, resolves overlays/profiles, and generates platform-compliant outputs for Speckit, Kiro, and agent-spec targets. Use a non-plugin additive target architecture, YAML project config (`steergen.config.yaml`), command system via `System.CommandLine`, and templating via Scriban. Generation is invoked via the `run` command as the single canonical command. Development is strict Red-Green-Refactor with PBT-first testing (CsCheck + xUnit), targeted mocking (NSubstitute), and performance regression measurement with BenchmarkDotNet. Template/rule examples are standards-first; public repository examples are fallback only when standards are incomplete, and test fixtures should use plausible real-world constitution/steering rules where practical.

## Technical Context

**Language/Version**: C# 14, .NET 10  
**Primary Dependencies**: `System.CommandLine`, `Scriban`, `CsCheck`, `xUnit`, `NSubstitute`, `BenchmarkDotNet`  
**Storage**: Local filesystem (Markdown sources, generated artifacts, YAML config)  
**Testing**: xUnit + CsCheck (PBT-first), NSubstitute for isolation seams, BenchmarkDotNet for performance baselines, and plausible real-world constitution/steering rule fixtures where practical  
**Target Platform**: Cross-platform CLI on Linux, macOS, Windows  
**Project Type**: CLI application + domain library  
**Performance Goals**: Meet SC-006 baseline (100 docs / 1,000 rules under 5s), preserve scalability envelope behavior up to 1,000 docs / 10,000 rules  
**Constraints**: Deterministic outputs, no runtime plugin loading, core-only `constitution.md` main artifact, secure fail-closed behavior, single portable executable distribution goal, and opt-in-only measurement protocol execution gated behind `--verbose`/`--debug`  
**Scale/Scope**: Multiple target platforms, many users, large steering corpora, in-repo additive target growth without refactoring existing targets

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- PASS: Runtime/language choice is idiomatic .NET 10 and C# 14.
- PASS: Design preserves deterministic behavior for identical inputs/configuration.
- PASS: Red-Green-Refactor plan is explicit, with tests authored before implementation.
- PASS: Property-based tests (CsCheck + xUnit) are defined for domain invariants.
- PASS: Test suites are expected to use plausible real-world constitution/steering rules where practical, reserving synthetic fixtures for isolated edge and failure cases.
- PASS: Security plan includes misuse/abuse analysis and prompt-injection-style payload resistance tests.
- PASS: Performance budgets and validation approach are defined for expected scale.
- PASS: CLI UX and error semantics are explicit and consistent.
- PASS: Documentation updates (README/quickstart/migration notes as applicable) are planned.
- PASS: Release impact is assessed against SemVer, including testing preview tags (`vMAJOR.MINOR.PATCH-previewN`) and stable release tags (`vMAJOR.MINOR.PATCH`) on master.

## Project Structure

### Documentation (this feature)

```text
/Users/aabs/dev/aabs/prototypes/specgen/specs/001-steering-doc-transform/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── cli-contract.md
│   └── config-schema.md
└── tasks.md
```

### Source Code (repository root)

```text
src/
├── Steergen.Cli/
│   ├── Commands/
│   ├── Composition/
│   └── Program.cs
├── Steergen.Core/
│   ├── Parsing/
│   ├── Validation/
│   ├── Model/
│   ├── Merge/
│   ├── Generation/
│   └── Targets/
└── Steergen.Templates/
    └── Scriban/

tests/
├── Steergen.Core.PropertyTests/
├── Steergen.Core.UnitTests/
├── Steergen.Cli.IntegrationTests/
└── Steergen.Benchmarks/
```

**Structure Decision**: Use a single solution with separated CLI, core domain pipeline, and templating assets so command orchestration, deterministic transformation logic, and target renderers evolve independently while preserving additive target growth guarantees.

## Post-Design Constitution Check

- PASS: Design uses .NET 10/C# 14 and keeps parser/validation/merge/generation/target separation.
- PASS: No runtime plugin loading; target growth remains additive with static registration.
- PASS: Test strategy is Red-Green-Refactor with PBT-first coverage using CsCheck/xUnit.
- PASS: Design supports realistic fixture corpora for golden, integration, and end-to-end tests.
- PASS: Security includes adversarial input and prompt-injection-style payload handling tests.
- PASS: Performance validation uses BenchmarkDotNet and tracks declared performance/scalability goals.
- PASS: CLI UX and deterministic/error semantics are represented in contracts and quickstart.
- PASS: Documentation deliverables are included (quickstart/contracts) and tied to release readiness.
- PASS: Release governance aligns with SemVer stable tags (`vMAJOR.MINOR.PATCH`) and permitted testing preview tags (`vMAJOR.MINOR.PATCH-previewN`).

## Complexity Tracking

No constitution violations requiring justification.
