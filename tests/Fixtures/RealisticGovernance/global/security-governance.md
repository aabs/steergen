---
id: security-governance-v1
version: "1.0.0"
title: Security Governance Rules
scope: global
status: active
profiles: [security, compliance]
---

# Security Governance Rules

This document defines organisation-wide security governance rules that apply across all projects and
services. These rules encode baseline security requirements derived from industry standards (OWASP,
NIST CSF 2.0, SOC 2 Type II). They complement the global constitution and may not be weakened at the
project level.

:::rule id="SEC-001" severity="error" category="security" domain="core"
title: Dependency Vulnerability Scanning

All production dependencies must be scanned for known CVEs on every CI build. Any dependency with a
CVSS score ≥ 7.0 (high or critical) must be remediated before merge. Automated scanners (e.g.
Dependabot, Snyk, or equivalent) must be active on every repository. Suppressions require a written
exception with an expiry date not exceeding 30 days.
:::

:::rule id="SEC-002" severity="error" category="security" domain="core"
title: Secure Communication — TLS Minimum Version

All network communication carrying sensitive data or authentication tokens must use TLS 1.2 or
higher. TLS 1.0 and 1.1 are prohibited. Mutual TLS (mTLS) is required for service-to-service
communication in the production cluster. Certificate expiry must be monitored with automated alerts
triggering at least 14 days before expiry.
:::

:::rule id="SEC-003" severity="error" category="security" domain="core"
title: Authentication — Short-Lived Credentials

Access tokens must have a maximum lifetime of 1 hour. Refresh tokens may not exceed 24 hours for
interactive user sessions or 7 days for machine-to-machine flows. Tokens must be signed with
RS256 or ES256. HS256 is prohibited for any token crossing a service boundary. Token revocation
endpoints must be implemented and tested.
:::

:::rule id="SEC-004" severity="warning" category="security" domain="core"
title: Security Event Logging

All authentication events (success, failure, lockout), privilege escalations, and access to PII
must be logged to the centralised SIEM. Logs must be tamper-evident and retained for a minimum of
90 days online and 1 year in cold storage. Security event logs must never be sent to
application-level logging pipelines that developers have write access to.
:::

:::rule id="SEC-005" severity="error" category="security" domain="core"
title: Supply-Chain Integrity — Package Provenance

All published packages and container images must include SLSA provenance attestations (SLSA Level 2
minimum). Build provenance must reference a pinned, audited build environment. Base container images
must be sourced only from the approved internal registry. Third-party base images require a signed
security review before onboarding.
:::
