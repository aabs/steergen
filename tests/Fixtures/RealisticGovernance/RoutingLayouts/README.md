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

### `workspace-local-layout.yaml`

Acceptance fixture for the workspace-local layout convention. The `layoutOverridePath` in
`steergen.config.yaml` references this file by relative path, resolved from the config file's
directory. Routes domain=core to `constitution.md`, specific domains to named module files, and
catches unknown domains via a wildcard catch-all.

### `user-home-global-layout.yaml`

Acceptance fixture for the user-home global convention. The `layoutOverridePath` is an absolute
path pointing to a user's home config directory. Routes all rules to a flat single-directory
layout under `${globalRoot}/.speckit-global`, suitable for cross-project governance baselines.

### `mixed-scope-layout.yaml`

Acceptance fixture for mixed-scope per-target overrides. Demonstrates a layout where one target
(e.g. speckit) uses a workspace-local path while another uses a home-global absolute path and a
third falls back to the built-in default. Includes both specific domain routes and a
category-bucketed catch-all.

## Usage

These fixtures are used by:
- `tests/Steergen.Cli.IntegrationTests/RunCatchAllRoutingTests.cs`
- `tests/Steergen.Cli.IntegrationTests/RunTargetLayoutRoutingTests.cs`
- `tests/Steergen.Cli.IntegrationTests/RunLayoutConventionsAcceptanceTests.cs`
- `tests/Steergen.Core.UnitTests/Generation/FallbackRoutingTests.cs`
- `tests/Steergen.Core.PropertyTests/Generation/CatchAllRoutingProperties.cs`

See `docs/routing-syntax.md` for the routing YAML syntax reference.
