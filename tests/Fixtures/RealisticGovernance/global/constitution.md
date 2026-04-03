---
id: global-constitution-v1
version: "1.0.0"
title: Global Engineering Constitution
scope: global
status: active
---

# Global Engineering Constitution

This document defines the foundational governance rules that apply to all projects in this organisation.
These rules may be extended or overridden at the project level, but may never be removed.

:::rule CORE-001
title: Minimum Test Coverage
severity: error
category: quality

All production code must maintain a minimum of 80% line coverage and 70% branch coverage as reported
by the CI pipeline. Coverage thresholds are enforced on every pull request. New code added without
accompanying tests will block merge.
:::

:::rule CORE-002
title: Secrets Must Never Be Committed
severity: error
category: security

No credentials, API keys, tokens, certificates, or other secrets may be committed to version control
in any form, including encrypted blobs or base64-encoded values. Use a secrets manager (e.g. Azure
Key Vault, AWS Secrets Manager) or environment variable injection at runtime. Violations trigger
an immediate security incident.
:::

:::rule CORE-003
title: Public API Documentation Required
severity: warning
category: documentation

Every public API surface (REST endpoints, public library methods, CLI commands) must be documented
with a human-readable description, at least one usage example, and documented error conditions.
Documentation must be kept in sync with code; stale docs are treated as bugs.
:::

:::rule CORE-004
title: Dependency Hygiene
severity: warning
category: supply-chain

All third-party dependencies must be pinned to an exact version in source control. Dependencies
with known critical CVEs must be upgraded within 14 days of disclosure. Transitive dependencies
are monitored via automated scanning. Unpinned or wildcard version constraints are not permitted.
:::

:::rule CORE-005
title: Breaking Changes Require Migration Guide
severity: error
category: compatibility

Any change that alters a public API contract (removes fields, changes types, renames endpoints,
modifies required parameters) is a breaking change. Breaking changes must be accompanied by a
migration guide published before the release, a deprecation period of at least one minor version,
and a changelog entry. Silent breaking changes are not permitted.
:::
