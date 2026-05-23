# Configuration Contract: External Pack Pinning

## Scope
Defines the required configuration semantics used by `rules-pack upgrade` and `template-pack upgrade`.

## Pack Reference Requirements
Each external pack reference must provide values needed to construct a canonical selector:
- `source` (required)
- `path` or `entryKey` (required for canonical composite selector)

Canonical selector:
- `<source>|<path-or-entry-key>`

## Pin Format Requirements
After successful upgrade, targeted reference must persist:
- `tag` (resolved tag)
- `commitSha` (immutable commit hash)

The command must update only the targeted reference and leave all others unchanged.

## Validation Rules
1. Selector parts must be syntactically valid and non-empty.
2. Selector must resolve to exactly one configured reference.
3. Invalid selector format must fail before side effects.
4. Failure in fetch/update path must not alter targeted pin.

## Compatibility Notes
- Existing references without tuple-form pins require normalization during implementation handling.
- No new top-level config file is introduced; updates remain in `steergen.config.yaml`.
