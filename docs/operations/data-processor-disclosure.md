# Data processor disclosure

This disclosure draft records the processor and region assumptions for pilot readiness. It must be reviewed before onboarding pilot users and does not stand in for signed legal terms.

## Region and core processors

| Area | Planned processor | Purpose | Boundary |
|---|---|---|---|
| Hosting | Azure Southeast Asia | ASP.NET Core API/workers and managed PostgreSQL | Single-region pilot target |
| Object storage | Azure private object storage | Raw upload shell, sanitized attachment and export archive storage | Encrypted at rest; short-lived grants |
| Backups | Azure managed backup | daily encrypted backups | Restore applies deletion tombstones before traffic |
| Observability | Azure Monitor/Application Insights | redacted logs, metrics, traces and alerts | No raw content, tokens or raw identifiers in operational logs |

## Processor review status

- processor contracts/disclosures ready: documentation package prepared for legal/compliance review.
- DPA, no-training, retention, deletion, location and subprocessor evidence remain required before production or paid pilot onboarding.
- Azure Monitor/Application Insights must preserve OpenTelemetry semantics and redaction controls if replaced.

## Disabled processor classes

AI processors are not configured for the core release profile. Voice transcription, taxonomy suggestion and AI weekly summary remain disabled until their extension gates, processor contracts and disclosures pass.
