# Alert dashboard and on-call ownership

The pilot alert dashboard tracks minimized operational counters only. It must not expose raw CSV rows, notes, screenshot URLs, tokens, email, exact trade values, workspace IDs or user IDs as public dimensions.

## Alert dashboard

| Signal | Source | Threshold | Owner |
|---|---|---|---|
| authentication failure rate | OpenTelemetry counter | sustained spike over baseline | Product engineering |
| cross-tenant denial | authorization/audit counter | any unexpected increase | Security owner |
| export/deletion age | queue and deletion counters | approaching SLA boundary | Data rights owner |
| queue health | worker queue depth and terminal marker lag | backlog or stuck terminal marker | Platform owner |
| upload rejection/malware | upload validation counters | unexpected spike | Product engineering |
| processor error | processor status counter | any configured processor failing | Security owner |
| break-glass | audited support action | any invocation | Security owner |

## On-call ownership table

| Area | Primary | Escalation | Evidence |
|---|---|---|---|
| Product/API | Product engineering | Founder/operator | incident ticket and trace ID |
| Security/privacy | Security owner | Founder/operator | audit event and containment note |
| Data rights/export/deletion | Data rights owner | Security owner | deletion/export age report |
| Infrastructure/backup | Platform owner | Founder/operator | backup status and restore log |
| Processor/vendor | Security owner | Legal/compliance reviewer | disclosure and processor ticket |

## Escalation rule

Suspected cross-tenant exposure, account compromise, missing deletion evidence or processor retention failure opens an incident immediately. User and authority notification follows applicable obligation and is recorded in the incident log.
