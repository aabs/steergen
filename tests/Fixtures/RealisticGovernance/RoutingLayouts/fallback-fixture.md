---
id: routing-layout-fallback-v1
version: "1.0.0"
title: Routing Layout Fallback Fixture
scope: project
status: active
---

# Routing Layout Fallback Fixture

This fixture exercises the `other-at-core-anchor` fallback routing behavior.
Rules in this document are intentionally designed to have no matching route and no matching
catch-all route, so they must be routed to `other.*` colocated with the core anchor file.

This fixture is used to verify:
- Unmatched rules land in `other.*` (e.g., `other.md`) colocated with the core anchor.
- The fallback location is derived from the `core` anchor route for the matching scope.
- Multiple unmatched rules are aggregated into the same fallback file.
- No duplicate placement occurs.

:::rule id="FALL-001" category="uncategorised"
title: Unmatched Routing Rule Alpha

This rule has category=uncategorised. No specific route or catch-all covers this value.
It should fall back to other.md in the same directory as the core anchor.
:::

:::rule id="FALL-002" category="experimental"
title: Unmatched Routing Rule Beta

This rule has category=experimental. No route definition covers this value.
It should be placed in other.md alongside FALL-001.
:::

:::rule id="FALL-003" mandatory="true" category="unknown"
title: Unmatched Routing Rule Gamma

This rule uses category=unknown. Fallback placement verifies that even mandatory rules
without matching routes are safely written to other.md.
:::

:::rule id="CORE-FALLBACK-001" mandatory="true" category="core"
title: Core Anchor Rule for Fallback Test

This rule has category=core and routes to the core anchor (constitution.md). It establishes
the directory and extension used for the other.md fallback destination. The presence of this
rule ensures the core anchor is resolvable for the fallback tests above.
:::
