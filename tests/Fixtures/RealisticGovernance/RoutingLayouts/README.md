# Routing Layout Fixtures

This directory contains steering rule fixtures designed to test the dynamic target layout and
routing engine.

## Files

### `catch-all-fixture.md`

Exercises catch-all and fallback routing behavior. Rules span multiple domains and categories:
- Specific domain routes are preferred over wildcard/catch-all routes.
- Catch-all routes (`category: "*"`) capture rules not matched by specific routes.
- Unmatched rules fall back to `other.*` colocated with the core anchor.

### `fallback-fixture.md`

Exercises the `other-at-core-anchor` fallback routing behavior. Rules are intentionally
designed to have no matching route and no matching catch-all, so they must be routed to
`other.*` colocated with the core anchor file.

### `mixed-domains-fixture.md`

A realistic corpus with mixed global and project scoping, multiple domains, and a variety of
categories. Validates deterministic single-destination routing across specific, catch-all, and
fallback routes.

## Usage

These fixtures are used by:
- `tests/Steergen.Cli.IntegrationTests/RunCatchAllRoutingTests.cs`
- `tests/Steergen.Cli.IntegrationTests/RunTargetLayoutRoutingTests.cs`
- `tests/Steergen.Core.UnitTests/Generation/FallbackRoutingTests.cs`
- `tests/Steergen.Core.PropertyTests/Generation/CatchAllRoutingProperties.cs`

See `docs/routing-syntax.md` for the routing YAML syntax reference.
