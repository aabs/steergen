---
id: platform-quality-v1
version: "1.0.0"
title: Platform Quality Rules
scope: global
status: active
---

# Platform Quality Rules

:::rule id="QUAL-001" mandatory="true" category="quality" tags="quality,testing"
All behavior changes must include automated tests that cover expected and error paths.
:::

:::rule id="QUAL-002" category="quality" tags="quality,reviewability"
Prefer small, composable changes that are easy to review and revert.
:::
