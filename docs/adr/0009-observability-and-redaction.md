# ADR 0009: Observability and Redaction

- Status: Accepted
- Date: 2026-08-27
- Owner: TamNT167

## Context

`TP-PLAN` requires structured redacted logs, metrics, traces, alerts and audit storage. `TP-SEC` prohibits user content, secrets, tokens and raw identifiers in operational logs.

## Decision

Use OpenTelemetry instrumentation in .NET with Azure Monitor/Application Insights as the pilot backend. Emit structured logs with redaction middleware, stable correlation IDs and allowlisted dimensions. Security/audit events stay in product tables, not the general log stream.

## Alternatives

- Console-only logs: insufficient for pilot readiness and incident response.
- External error tracker carrying request payloads: rejected unless redaction and DPA are proven.

## Security/privacy impact

Logs must never contain raw CSV rows, note text, screenshot URL, tokens, email, exact trade values or workspace/user IDs as public dimensions. Cross-tenant denial, export/deletion age and queue health are observable via minimized counters.

## Rollback

If Azure Monitor is replaced, preserve OpenTelemetry semantic conventions and rerun log redaction tests before pilot.

