# PackUpgrades Fixture Corpus

This fixture corpus supports external rules-pack and template-pack upgrade tests.

## Contents

- `baseline-steergen.config.yaml`: Realistic configuration with multiple external pack references.
- `catalog/`: Deterministic remote metadata snapshots used by tests.
- `cache-snapshots/pre-upgrade/`: Targeted cache state before upgrade.
- `cache-snapshots/post-upgrade/`: Expected cache state after successful refresh.
- `rollback/`: Inputs for fetch-failure and restore behavior.

## Selector Examples

- `github.com/acme/security-governance|packs/security`
- `github.com/acme/templates|templates/default`
