# Phase 1 Data Model

## SteeringDocument
- Fields: `id`, `version`, `title`, `description`, `tags[]`, `profiles[]`, `rules[]`, `sourcePath`
- Validation:
  - `id` required and unique per resolved model
  - `version` must be semantic version
- Relationships: contains many `SteeringRule`

## SteeringRule
- Fields: `id`, `severity`, `category`, `domain`, `profile`, `appliesTo[]`, `tags[]`, `deprecated`, `supersedes`, `primaryText`, `explanatoryText`
- Validation:
  - `id` required and unique post-overlay/filter
  - `severity` in allowed enum
  - `domain` required; `core` marks universally applicable rules
- State/Transitions:
  - Optional deprecation lifecycle (`deprecated=true`)

## ResolvedSteeringModel
- Fields: `documents[]`, `rules[]`, `activeProfiles[]`, `sourceIndex`
- Derived by: load -> validate -> overlay -> profile filter
- Invariants:
  - deterministic ordering
  - stable output for identical inputs/config

## SteeringConfiguration
- Fields: `globalRoot`, `projectRoot`, `activeProfiles[]`, `targets[]`, `registeredTargets[]`, `templatePackVersion`
- Stored in: `steergen.config.yaml`
- Validation:
  - paths resolvable
  - target IDs known and unique in registered set

## TargetConfiguration
- Fields: `id`, `enabled`, `outputPath`, `formatOptions`, `requiredMetadata`
- Relationships: linked to a `TargetComponent` via deterministic registry

## TargetRegistrationRecord
- Fields: `targetId`, `registrationState`, `folderLayout`, `addedAt`, `addedByCommand`
- Created by: `target add`

## UpdateManifest
- Fields: `selectedVersion`, `resolvedVersion`, `compatibilityResult`, `artifactsApplied[]`, `timestamp`
- Produced by: `update` command

## CoreConstitutionRuleSet
- Definition: subset of rules where `domain == core`
- Output contract: must be the only content in main `constitution.md` (or platform equivalent)

## DomainGuidanceModule
- Definition: rules where `domain != core`
- Output contract: emitted as platform-specific modular files (agents/skills/equivalent) when platform supports modularization
