---
id: data-platform-steering-v1
version: "1.0.0"
title: Data Platform Steering Rules
scope: project
inherits: global-constitution-v1
status: active
profiles: [data, compliance]
---

# Data Platform Steering Rules

Domain-specific steering rules for the Data Platform service. Extends and refines global governance
for batch ingestion, streaming pipelines, and data lake management. These rules do not override
CORE or SEC rules.

:::rule id="DATA-001" severity="error" category="data-quality" domain="data"
title: Schema Contract Enforcement

All data pipeline inputs and outputs must have a registered schema in the central schema registry
before deployment. Breaking schema changes (field removal, type narrowing, required field addition)
require a versioned migration path and a minimum 30-day deprecation notice communicated to all
downstream consumers. Schema validation must run at ingestion boundaries; malformed records must be
routed to a dead-letter queue, never silently dropped.
:::

:::rule id="DATA-002" severity="error" category="security" domain="data"
title: PII Classification and Handling

All data assets must be classified at creation time using the four-tier classification scheme
(Public, Internal, Confidential, Restricted). PII falls in Confidential or Restricted tiers. PII
must be tokenised or pseudonymised before it enters the data lake. Raw PII must never be persisted
in any analytics store accessible to more than three named principals. Classification metadata must
be stored in the data catalogue and reviewed quarterly.
:::

:::rule id="DATA-003" severity="warning" category="reliability" domain="data"
title: Idempotent Processing and Exactly-Once Semantics

Batch and streaming jobs must be idempotent: re-running a job with the same input watermark must
produce the same output without duplicating records. Deduplication keys must be documented in the
pipeline manifest. Exactly-once semantics are required for all jobs writing to financial or
compliance datasets. For other datasets, at-least-once with idempotent sinks is acceptable.
:::

:::rule id="DATA-004" severity="warning" category="observability" domain="data"
title: Pipeline Health Metrics and SLOs

Every production pipeline must publish the following metrics: records-in, records-out, records-DLQ,
processing-lag-seconds, and last-successful-run-timestamp. SLOs must be defined for each pipeline
before production deployment: maximum acceptable lag, minimum throughput, and allowable DLQ rate.
SLO breaches must trigger PagerDuty alerts within 5 minutes of breach onset.
:::

:::rule id="DATA-005" severity="error" category="compliance" domain="data"
title: Data Retention and Right-to-Erasure

All data stores must implement a documented retention policy registered in the data catalogue. For
data subject to GDPR or equivalent regulation, erasure requests must be processed within 72 hours
and propagated to all downstream replicas and derived datasets. Erasure propagation must be
verified by automated reconciliation checks and logged to the compliance audit trail.
:::
