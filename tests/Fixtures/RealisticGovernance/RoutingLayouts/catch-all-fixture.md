---
id: routing-layout-catch-all-v1
version: "1.0.0"
title: Routing Layout Catch-All Fixture
scope: project
status: active
---

# Routing Layout Catch-All Fixture

This fixture exercises catch-all and fallback routing behavior.
Rules span multiple domains and categories to verify that:
- Specific domain routes are preferred over wildcard/catch-all routes.
- Catch-all routes (`category: "*"`) capture rules not matched by specific routes.
- Unmatched rules (no route and no catch-all) fall back to `other.*` colocated with the core anchor.

:::rule id="RLAY-001" severity="error" category="quality" domain="core"
title: Core Quality Gate

All pull requests must pass automated linting, unit tests, and integration tests before merge.
This rule routes to the core anchor destination (e.g., constitution.md).
:::

:::rule id="RLAY-002" severity="error" category="security" domain="security"
title: Injection Attack Prevention

User input must be validated and sanitised before use in queries, commands, or rendered output.
Parameterised queries are required for all database interactions. This rule routes to the
security domain module.
:::

:::rule id="RLAY-003" severity="warning" category="performance" domain="performance"
title: Response Time Budget

API endpoints must respond within 200 ms at p99 under documented peak load. Performance budgets
are enforced in CI via benchmark guardrails. This rule routes to the performance domain module.
:::

:::rule id="RLAY-004" severity="info" category="observability" domain="observability"
title: Structured Logging Required

All services must emit structured JSON logs to stdout. Log entries must include a trace identifier,
severity level, and ISO 8601 timestamp. This rule routes to the observability domain module.
:::

:::rule id="RLAY-005" severity="warning" category="compliance" domain="compliance"
title: Data Retention Policy

Customer data must not be retained beyond 90 days unless explicitly required by legal hold.
Automated retention policies must be configured in all storage systems. This rule routes to the
compliance domain module.
:::

:::rule id="RLAY-006" severity="error" category="accessibility" domain="frontend"
title: WCAG 2.1 AA Compliance

All user-facing UI components must meet WCAG 2.1 AA accessibility standards. Automated
accessibility scanning must run on every pull request. This rule exercises the catch-all route
because `domain=frontend` has no specific route defined.
:::

:::rule id="RLAY-007" severity="info" category="internationalisation" domain="frontend"
title: Locale-Aware Date Formatting

All user-visible dates and times must be formatted according to the user's configured locale.
Hard-coded locale-specific formatting is not permitted. This rule also exercises the catch-all
because `category=internationalisation` has no specific route.
:::

:::rule id="RLAY-008" severity="warning" category="build" domain="infrastructure"
title: Reproducible Builds

Build outputs must be reproducible: the same inputs must produce byte-identical outputs.
Timestamps, random seeds, and non-deterministic dependencies are not permitted in build artefacts.
This rule exercises the fallback (no specific route, no matching catch-all).
:::
