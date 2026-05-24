---
id: security-authentication-v1
version: "1.0.0"
title: Authentication Baseline
scope: global
status: active
---

# Authentication Baseline

:::rule id="SEC-001" mandatory="true" category="security" tags="security,authentication"
All service endpoints must enforce authenticated access unless explicitly documented as public.
:::

:::rule id="SEC-002" mandatory="true" category="security" tags="security,secrets"
Secrets must not be stored in source control and must be loaded from managed secret stores.
:::
