# 003 Upgrade Pack References - Progress

## Status

- Overall: In Progress
- Last Updated: 2026-05-23

## Task Tracking

- [x] T001 Fixture corpus added
- [x] T002 Rollback fixtures added
- [x] T003 Progress tracker stub added
- [x] T004 Selector parsing/validation tests added
- [x] T005 Pin tuple config round-trip tests added
- [x] T006 Failed-upgrade config invariants property tests added
- [x] T007 Snapshot restore tests added
- [x] T008 Model updates for selector/pin tuple
- [x] T009 Canonical selector resolver implemented
- [x] T010 Config loader/writer pin tuple mapping implemented
- [x] T011 Shared external pack upgrade orchestrator implemented
- [x] T012 Cache snapshot/restore helper implemented
- [x] T013 Foundational diagnostics contracts integrated
- [x] T014 Rules-pack upgrade latest/explicit integration tests added
- [x] T015 Rules-pack selector failure integration tests added
- [x] T016 Rules-pack targeted update unit tests added
- [x] T017 rules-pack upgrade command implemented
- [x] T018 rules-pack upgrade command registration implemented
- [x] T019 rules-pack upgrade wired to shared upgrade service
- [x] T020 rules-pack targeted pin tuple mutation implemented
- [x] T021 command factory regression for rules-pack upgrade added
- [x] T022 template-pack upgrade latest/explicit integration tests added
- [x] T023 template-pack selector validation integration tests added
- [x] T024 template-pack pin tuple unit tests extended
- [x] T025 template-pack upgrade command implemented
- [x] T026 template-pack upgrade command registration implemented
- [x] T027 template-pack upgrade wired to shared upgrade service
- [x] T028 template-pack targeted pin tuple mutation implemented
- [x] T029 explicit-tag determinism property tests added
- [x] T030 rollback and dual-diagnostic integration tests added
- [x] T031 malformed-input and inert-metadata security tests added
- [x] T032 preflight selector rejection before side effects enforced
- [x] T033 rollback-first and dual-failure diagnostics implemented
- [x] T034 stable CLI exit mapping for selector/rollback failures implemented
- [x] T035 deterministic command output fields added
- [x] T036 CLI usage docs updated
- [x] T037 contracts aligned with selector escaping and stable exits
- [x] T038 operational guidance and quickstart examples updated
- [x] T039 quickstart validation sequence executed via targeted unit/property/integration suites
- [x] T040 timed upgrade performance integration tests added (p95 budget assertions)
- [x] T041 CI performance gate/reporting updates added
- [x] T042 structured acceptance metrics recorded
- [x] T043 unchanged-reference integration assertions added for rules and template upgrades

## Acceptance Metrics (T042)

- Exercise type: scripted operator workflow proxy via integration suite
- Sample size: 13 end-to-end integration scenarios (`n=13`, includes latest and explicit flows)
- First-attempt pass rate: 13/13 (100%)
- Latest flow first-attempt pass rate: 100%
- Explicit flow first-attempt pass rate: 100%

## Performance Metrics (T040/T041)

- Timed integration tests: `PackUpgradePerformanceTests`
- Budget enforced in tests: p95 `<=60s` for simulated `<=100MB` payload upgrade path
- CI tracking: `.github/workflows/ci.yml` performance gate includes `PackUpgradePerformanceTests` with TRX artifacts

## Notes

- Phase 1 setup artifacts created under tests/Fixtures/RealisticGovernance/PackUpgrades.
- Subsequent phases will be updated here as tasks are completed.
