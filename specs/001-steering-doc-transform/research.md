# Phase 0 Research

## Decision 1: Standards-first target templates and examples
- Decision: Base each target template on official platform standards/documentation first; only use public repository examples when standards are incomplete or ambiguous.
- Rationale: This keeps generated output compliant and future-proof, and avoids encoding accidental conventions from random repositories.
- Alternatives considered: Examples-first reverse engineering from mature repositories. Rejected because it can drift from canonical standards and introduce inconsistent target behavior.

## Decision 2: CLI architecture with System.CommandLine
- Decision: Use `System.CommandLine` with modular command handlers (`init`, `update`, `run`, `target add`, `target remove`) and shared option parsing.
- Rationale: Strong typed command model, composable subcommands, and robust validation/diagnostics for a cross-platform CLI.
- Alternatives considered: Hand-rolled argument parsing. Rejected due to higher maintenance cost and weaker consistency/error handling.

## Decision 3: Rendering layer with Scriban
- Decision: Use Scriban templates for all target render output and metadata frontmatter generation.
- Rationale: Keeps rendering deterministic and maintainable while allowing target-specific formatting without branching logic explosion.
- Alternatives considered: String-concatenation renderers. Rejected due to readability and correctness risk as target count grows.

## Decision 4: PBT-first testing stack
- Decision: Use CsCheck + xUnit for core invariants first, with targeted example tests and NSubstitute only where seams require mocking.
- Rationale: Property-based testing best fits parser/merge/generation invariants and deterministic transformation guarantees.
- Alternatives considered: Example-only unit testing. Rejected because it under-covers edge/input spaces and weakens invariance guarantees.

## Decision 5: Performance validation with BenchmarkDotNet
- Decision: Track parser/merge/generation and end-to-end command benchmarks in `tests/Steergen.Benchmarks` using BenchmarkDotNet.
- Rationale: Prevents regressions against SC-006 and scalability envelope requirements; benchmark history informs tuning.
- Alternatives considered: Ad-hoc stopwatch tests. Rejected due to low reliability and poor reproducibility.

## Decision 6: Additive non-plugin target growth
- Decision: Keep targets built-in and registered via static deterministic metadata; prohibit runtime plugin discovery/loading.
- Rationale: Aligns with constitution security constraints and ensures new target addition does not refactor existing targets.
- Alternatives considered: Plugin model via reflection/runtime load. Rejected due to supply-chain/runtime risk and compatibility instability.

## Decision 7: Config and concurrency model
- Decision: Single YAML source of truth (`steergen.config.yaml`) with optimistic concurrency checks before write.
- Rationale: Human-readable VCS-friendly config plus safe conflict detection for parallel edits.
- Alternatives considered: Multi-file config or lock-file-first config. Rejected due to complexity and review overhead.
