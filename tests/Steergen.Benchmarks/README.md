# Steergen.Benchmarks

Micro-benchmark suite for steergen's core pipeline hot paths using [BenchmarkDotNet](https://benchmarkdot.net/).

## Running benchmarks

Always run in Release mode from the solution root:

```bash
dotnet run -c Release --project tests/Steergen.Benchmarks
```

BenchmarkDotNet requires Release configuration for accurate measurements. Debug/development builds will produce artificially inflated timings.

## Available benchmarks

### CorePipelineBenchmarks

Core transformation pipeline: parse → validate → resolve.

| Benchmark | Description |
|-----------|-------------|
| `ParseSmallDocument` | Parse a 10-rule steering document |
| `ParseLargeDocument` | Parse a 100-rule steering document |
| `ValidateDocument` | Validate a 50-rule document |
| `ResolveSmallCorpus` | Resolve 5+5 docs (10 rules each) |

### ScalabilityEnvelopeBenchmarks

Scalability envelope verification against SC-006 targets (100 docs / 1,000 rules under 5 s).

| Benchmark | Description | Envelope |
|-----------|-------------|---------|
| `ParseHundredDocuments` | Parse 100 documents | < 5 s |
| `ValidateEnvelope` | Validate 100 docs × 10 rules | < 5 s |
| `ValidateBeyondEnvelope` | Validate 100 docs × 100 rules (warning zone) | — |
| `ResolveEnvelope` | Resolve 100 docs | < 5 s |
| `ResolveBeyondEnvelope` | Resolve 100 docs (heavy) | — |

## Interpreting results

- **Mean**: average execution time per operation.
- **Gen0/Gen1/Gen2**: GC collection counts per 1,000 operations — lower is better.
- **Allocated**: managed heap bytes per operation.

A benchmark regression is defined as a Mean increase > 10% from the previously recorded baseline. Log baseline results in the release checklist (`docs/release/release-checklist.md`) before each release.

## Performance goals (SC-006)

The target envelope is **100 documents / 1,000 rules in under 5 seconds** end-to-end.  
`ScalabilityEnvelopeBenchmarks` validates this. The "Beyond Envelope" benchmarks are informational: they confirm degradation is graceful (linear, not exponential) and do not have hard pass/fail thresholds.

## Exporting results

BenchmarkDotNet exports results to `BenchmarkDotNet.Artifacts/` by default:

```text
BenchmarkDotNet.Artifacts/
  results/
    Steergen.Benchmarks.CorePipelineBenchmarks-report.md
    Steergen.Benchmarks.ScalabilityEnvelopeBenchmarks-report.md
```

Attach the Markdown report to release PRs as evidence of performance compliance.
