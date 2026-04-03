---
id: project-steering-v1
version: "1.2.0"
title: Project Steering Rules — Platform API
scope: project
inherits: global-constitution-v1
status: active
---

# Project Steering Rules — Platform API

This document extends the global constitution with domain-specific steering rules for the Platform
API service. Rules defined here narrow, clarify, or add to the global constitution for this project.
They do not override CORE rules but may add stricter constraints.

:::rule API-001
title: Versioning Strategy — URI Prefix
severity: error
category: api-design
overrides: CORE-005

All REST API endpoints must include an explicit version prefix in the URI path (e.g. `/v1/`, `/v2/`).
Version negotiation via HTTP headers is not permitted. When a new major version is introduced, the
previous version must remain available for a minimum of 90 days. Semver patch and minor changes
within a version must be backward compatible.
:::

:::rule API-002
title: Input Validation at API Boundary
severity: error
category: security

Every API endpoint must validate all incoming request payloads at the boundary using a strongly-typed
schema. Validation errors must return HTTP 422 with a structured error body listing all validation
failures. Raw database or internal error messages must never be surfaced to callers. Validation logic
must be unit-tested with at least boundary-value and invalid-type cases.
:::

:::rule API-003
title: Structured Logging and Correlation IDs
severity: warning
category: observability

All log statements must use structured logging (key-value pairs, JSON output in production). Every
inbound request must be assigned a correlation ID (from the `X-Correlation-Id` header or generated
if absent) that is propagated to all downstream calls and included in every log entry for that
request. Log levels must be appropriate: DEBUG for diagnostics, INFO for lifecycle events, WARN for
recoverable anomalies, ERROR for unhandled failures.
:::

:::rule API-004
title: Data Classification and PII Handling
severity: error
category: privacy

Personally Identifiable Information (PII) fields must be identified in the data model with a
`[PiiData]` annotation. PII must not appear in logs, tracing spans, or error messages. Responses
containing PII must only be served over TLS to authenticated and authorised principals. Data
retention policies for PII fields must be documented in the data dictionary.
:::
