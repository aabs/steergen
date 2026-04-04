# Configuration Contract (`steergen.config.yaml`)

## Top-Level Fields
- `globalRoot`: path to global steering docs
- `projectRoot`: path to project steering docs
- `activeProfiles`: list of active profile names
- `targets`: map/list of target configurations
- `registeredTargets`: canonical list of target IDs
- `templatePackVersion`: currently applied template/metadata version

## Target Configuration
- `id`: normalized target identifier (e.g., `speckit`, `kiro`, `copilot-agent`, `kiro-agent`)
- `enabled`: boolean
- `outputPath`: output directory path
- `requiredMetadata`: list of required metadata key names that must be present in each steering document
- `formatOptions`: renderer options per target

## Concurrency Contract
- Commands modifying config must use optimistic write checks:
  - read snapshot
  - modify in memory
  - verify unchanged on write
  - if changed, fail non-zero with conflict diagnostics

## Security/Scope Contract
- No secrets/credentials/tokens/private keys in config for v1
- Lock or cache data (if needed) must be kept in separate files
