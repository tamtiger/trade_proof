# ADR 0004: Queue, Worker and Idempotency

- Status: Accepted
- Date: 2026-08-27
- Owner: TamNT167

## Context

`TP-PLAN` requires a durable job queue for import, context, metrics, export, deletion and optional AI. `TP-SEC` requires versioned tenant work control, fences, external-operation leases, terminal markers and deletion-generation checks.

## Decision

Implement the MVP queue on PostgreSQL tables using the `TenantControlJob`, `TenantWorkItemFence`, terminal marker and idempotency contracts from `TP-PLAN`/`TP-SEC`. Workers claim jobs with transaction locks and `FOR UPDATE SKIP LOCKED`. External brokers are deferred until the database-backed control graph passes conformance tests.

## Alternatives

- Hangfire: useful later, but its storage abstractions do not replace the product-specific fence/marker/deletion contract.
- Cloud queue first: rejected for MVP because cross-store enqueue consistency would complicate Phase 1 foundation.

## Security/privacy impact

The job payload schema pins workspace scope, initiator, deletion generation and digest profile. Workers re-authorize against current workspace lifecycle before read/write or external dispatch.

## Rollback

An external queue may be introduced behind the same tenant-work table as delivery acceleration, but the database record remains the source of truth for idempotency and deletion drain.

