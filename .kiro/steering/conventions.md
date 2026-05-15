# Development Conventions (Constitutional Obligations)

This file captures the mandatory engineering rules from the project constitution. These are non-negotiable and override any conflicting guidance.

## Test-First with Property-Based Testing (NON-NEGOTIABLE)

- Development MUST follow strict Red-Green-Refactor. Tests are written BEFORE implementation code.
- **Property-based testing (PBT) with CsCheck + xUnit is the default test strategy.** It is not optional.
- PBT MUST be used for: domain invariants, parsers, transforms, merge rules, ordering guarantees, serialization behavior, and routing logic.
- Example-based unit tests MAY only be used where properties are not practical, and the reason MUST be explicitly stated in a code comment.
- Test coverage MUST include invariants across broad generated input spaces, not just happy-path examples.
- Test fixtures MUST use plausible, realistic governance documents — not toy placeholders.
- Golden, integration, and end-to-end test corpora MUST use plausible constitution and steering rules representative of real-world governance documents rather than toy placeholder-only fixtures.
- Every change MUST include proof that the code works as intended: passing tests that exercise the new or modified behavior.

## Proof of Correctness at Every Step

- No implementation code is considered complete without corresponding passing tests.
- Before submitting any change, run `dotnet test` and confirm all tests pass.
- New features MUST include tests that demonstrate the feature works correctly across edge cases.
- Bug fixes MUST include a regression test that fails without the fix and passes with it.
- Refactoring MUST demonstrate identical behavior via unchanged passing tests.

## Correctness and Determinism

- Identical inputs + configuration + runtime MUST produce identical outputs. Always.
- Parsing, merge, overlay, filtering, and generation MUST be deterministic and test-verified.
- Undefined behavior is forbidden. Invalid input MUST produce explicit diagnostics, never silent corruption.
- All ordering (file discovery, rule routing, write plans) MUST be deterministic and tested.

## Security

- Every feature MUST include explicit misuse and abuse analysis, including prompt-injection-style payload handling.
- NuGet dependencies MUST be pinned or range-restricted, vulnerability scanned, and justified.
- Secrets MUST never be committed.
- Security-critical paths MUST fail closed (deny by default).
- Security test suites MUST include malicious input corpora demonstrating no prompt-injection exploitation, no unsafe interpretation of untrusted content, and no code execution through input documents.

## Performance

- Features MUST define measurable performance budgets (latency, throughput, memory) where risk is non-trivial.
- Performance-sensitive changes MUST include regression tests or benchmarks.
- Error handling MUST preserve process stability and return precise exit codes.

## Target Extensibility (Open/Closed)

- New targets MUST be added through additive implementation units and registration metadata.
- Adding a new target MUST NOT require refactoring existing targets, parsers, validators, merge logic, or core pipeline.
- Dynamic plugin loading at runtime is prohibited.
- Target registration MUST be explicit and deterministic (static registry, not runtime discovery).
- PRs adding new targets MUST include evidence that no existing target component was modified.

## Architecture Boundaries

- Clear separation MUST be maintained between: parsing, model, validation, transformation, and target adapter concerns.
- Dependencies flow inward toward domain logic.
- Public contracts and generated artifact schemas MUST follow semantic versioning compatibility.
- Code MUST be idiomatic .NET 10 and C# 14. Do not use patterns from older framework versions when modern equivalents exist.

## CLI and Configuration

- The public CLI and config model MUST prioritize clarity, minimal surface area, and safe defaults.
- Commands, flags, and diagnostics MUST be intuitive, consistent, and script-friendly.
- New options MUST justify their complexity cost and demonstrate clear user value.

## Documentation

- User-facing behavior MUST be documented as part of feature completion.
- Each released capability MUST include: README/quickstart updates, config and example usage, error semantics, and migration notes where relevant.
- Documentation MUST be validated against real CLI behavior before release.

## PR and Merge Requirements

- Every PR MUST include constitutional compliance notes covering:
  - Security analysis
  - Test-first workflow evidence
  - Performance impact assessment
  - Documentation impact
- Static analysis, formatting, and all tests MUST pass in CI before merge.
- Code review and release review MUST block merge when constitutional obligations are unmet.

## Release Workflow

- Releases follow SemVer, triggered by tagging master with `vMAJOR.MINOR.PATCH`.
- Preview releases use `vMAJOR.MINOR.PATCH-previewN` format.
- Breaking changes MUST include migration guidance and explicit major-version rationale.
- Tagged release pipelines MUST build, run full tests, verify package metadata, and publish only after all checks pass.
