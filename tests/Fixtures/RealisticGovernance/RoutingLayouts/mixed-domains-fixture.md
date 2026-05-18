---
id: routing-layout-mixed-domains-v1
version: "1.0.0"
title: Routing Layout Mixed Categories Fixture
scope: both
status: active
---

# Routing Layout Mixed Categories Fixture

This fixture provides a realistic corpus with mixed global and project scoping, multiple categories,
and a variety of rule types. It is used to validate:
- Specific routes for known categories are applied correctly.
- Catch-all routes activate only when no specific route matches.
- Fallback routes activate only when no route (including catch-all) matches.
- Deterministic single-destination assignment is preserved regardless of rule ordering.
- No rule appears in more than one output file.

:::rule id="MIX-001" mandatory="true" category="core"

Every HTTP endpoint must require authentication unless explicitly annotated as public. Anonymous
access to private endpoints is a security defect. Routes to core anchor.
:::

:::rule id="MIX-002" mandatory="true" category="security"

All service-to-service and client-to-service communication must use TLS 1.2 or higher.
Plain HTTP traffic is not permitted in any environment. Routes to security category module.
:::

:::rule id="MIX-003" category="operations"

Every deployed service must expose a `/health` endpoint that returns HTTP 200 and a structured
body describing component health. This endpoint must not require authentication.
Routes to operations category module.
:::

:::rule id="MIX-004" category="core"

Significant architecture decisions must be documented as Architecture Decision Records (ADRs)
stored in the repository. The ADR must include context, decision, and consequences.
Routes to core anchor.
:::

:::rule id="MIX-005" category="testing"

Services that depend on external APIs must maintain a contract test suite. Contract tests must
run in CI against a mock or sandbox environment. Routes to testing category module.
:::

:::rule id="MIX-006" category="cloud"

All cloud resources must be tagged with project, environment, and team identifiers before
deployment. Untagged resources are not permitted in production. Routes via catch-all because
category=cloud has no specific route defined.
:::

:::rule id="MIX-007" category="delivery"

All production deployments must be executed through the CI/CD pipeline. Direct manual changes
to production environments are not permitted. Routes via catch-all (category=delivery has no
specific route) or falls back if no catch-all covers it.
:::

:::rule id="MIX-008" mandatory="true" category="data"

All data schemas used by production services must be versioned and backward-compatible changes
must not break existing consumers. Schema evolution must go through a migration plan. This rule
exercises fallback behavior if category=data has no specific or catch-all route.
:::
