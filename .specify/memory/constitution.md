<!--
Sync Impact Report
- Version change: 1.0.0 -> 1.1.0
- Modified principles:
	- V. Simplicity and Excellent CLI User Experience -> V. Simplicity, Excellent CLI User Experience, and Stable Extensibility
- Added principles:
	- VII. Built-In Target Expansion Without Plugin Loading
- Added sections:
	- None
- Removed sections:
	- None
- Templates requiring updates:
	- .specify/templates/plan-template.md ✅ already aligned
	- .specify/templates/spec-template.md ✅ already aligned
	- .specify/templates/tasks-template.md ✅ already aligned
	- .specify/templates/commands/*.md ⚠ pending (directory not present in repository)
	- README.md ✅ reviewed (no constitutional reference changes required)
- Follow-up TODOs:
	- None
-->

# specgen Constitution

## Core Principles

### I. Security and Supply Chain Integrity First
All code, dependencies, and release operations MUST minimize security risk by
default. Every feature MUST include explicit misuse and abuse analysis,
including prompt-injection-style payload handling even when no LLM is directly
used. Dependencies from NuGet MUST be pinned or range-restricted, vulnerability
scanned, and justified in PR rationale. Secrets MUST never be committed.
Security-critical paths MUST fail closed.

Rationale: This tool is expected to become widely adopted and distributed via
NuGet, so a single weak default can scale into ecosystem-level risk.

### II. Correctness and Deterministic Behavior
Identical inputs, configuration, and runtime environment MUST produce identical
outputs. Parsing, merge, overlay, filtering, and generation behavior MUST be
specified and test-verified for determinism and correctness. Undefined behavior
is forbidden; invalid input MUST produce explicit diagnostics.

Rationale: Tooling reliability depends on predictable behavior in CI, local
development, and downstream integrations.

### III. Test-First Development with Property-Based Testing (NON-NEGOTIABLE)
Development MUST follow strict Red-Green-Refactor. Tests MUST be authored
before implementation. Property-based testing with FsCheck and xUnit MUST be the
default strategy for domain invariants, parsers, transforms, merge rules,
ordering guarantees, and serialization behavior. Example-based unit tests MAY be
used only where properties are not practical, and must be explicitly justified.
Coverage MUST include invariants across broad generated input spaces.

Rationale: PBT validates behavior classes and invariants more effectively than
narrow examples, reducing latent defects in transformation tooling.

### IV. Performance and Robustness for Large-Scale Adoption
The CLI MUST remain performant and resilient for large repositories and large
document sets. Features MUST define measurable performance budgets (latency,
throughput, and memory) and include regression tests or benchmarks where risk is
non-trivial. Error handling MUST preserve process stability and return precise
exit codes.

Rationale: Growth in users and corpus size is expected; performance regressions
and fragile behavior become major operational risks at scale.

### V. Simplicity, Excellent CLI User Experience, and Stable Extensibility
The public CLI and configuration model MUST prioritize clarity, minimal surface
area, and safe defaults. Commands, flags, and diagnostics MUST be intuitive,
consistent, and script-friendly. New options MUST justify complexity cost and
demonstrate clear user value. Adding new transformation targets MUST avoid
refactoring existing parser, validation, merge, or other already-shipped target
components.

Rationale: Ease of use is essential for adoption and for reducing support burden
across diverse users and environments.

### VII. Built-In Target Expansion Without Plugin Loading
The system MUST support growth to many target platforms through a stable,
in-repo target contract and deterministic dispatch model. Dynamic plugin loading
at runtime is prohibited. New targets MUST be added through additive
implementation units and target registration metadata so that existing targets
and core pipeline components do not require refactoring.

Rationale: This keeps supply-chain risk and runtime complexity lower than plugin
systems while still enabling rapid addition of new output platforms.

### VI. Documentation as a Product Surface
User-facing behavior MUST be documented as part of feature completion. Each
released capability MUST include accurate README/quickstart updates, config and
example usage, error semantics, and migration notes when relevant. Documentation
MUST be validated against real CLI behavior before release.

Rationale: For CLI tooling, documentation is part of the API contract and
directly impacts correctness of user workflows.

## Engineering Standards

- Runtime and language baseline MUST be idiomatic .NET 10 and C# 14.
- Architecture MUST preserve clear separation of parsing, model, validation,
	transformation, and target adapter concerns.
- Target extensibility MUST be open for additive target implementations and
	closed for modification of existing target implementations.
- Target registration MUST be explicit and deterministic (for example,
	compile-time registration or static registry tables), not runtime plugin
	discovery.
- Public contracts and generated artifact schemas MUST use semantic versioning
	compatibility expectations.
- Security test suites MUST include malicious input corpora that demonstrate no
	prompt-injection exploitation path, no unsafe interpretation of untrusted
	content, and no code execution/vector escalation through input documents.
- Static analysis, formatting, and test execution MUST pass in CI before merge.

## Delivery and Release Workflow

- Releases MUST follow SemVer and be triggered by tagging master with
	`vMAJOR.MINOR.PATCH` (example: `v0.23.4`).
- Tagged release pipelines MUST build, run full tests, verify package metadata,
	and publish to NuGet only after all required checks pass.
- Every PR MUST include constitutional compliance notes for security, test-first
	workflow, performance impact, and documentation impact.
- PRs adding new targets MUST include evidence that no existing target component
	was refactored to support the addition.
- Any breaking change MUST include migration guidance and explicit major-version
	rationale.

## Governance

This constitution overrides conflicting project conventions. Amendments require:

1. A documented proposal describing the change, rationale, and impact.
2. Approval from project maintainers.
3. Updates to affected templates and workflow guidance in the same change.
4. A version update to this constitution according to semantic versioning:
	 - MAJOR: Incompatible governance changes or principle removal/redefinition.
	 - MINOR: New principle/section or materially expanded guidance.
	 - PATCH: Clarifications, wording improvements, and non-semantic refinements.

Compliance review expectations:

- Plan reviews MUST pass constitution gates before design and again after design.
- Task generation MUST include explicit test-first, PBT-first, security,
	performance, and documentation tasks.
- Code review and release review MUST block merge/publish when constitutional
	obligations are unmet.

**Version**: 1.1.0 | **Ratified**: 2026-04-03 | **Last Amended**: 2026-04-03
