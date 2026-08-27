# ADR 0003: Relational Database and Tenant Enforcement

- Status: Accepted
- Date: 2026-08-27
- Owner: TamNT167

## Context

TradeProof needs tenant-owned records, append-only revisions, decimal finance, versioned artifacts, idempotency keys, export cutoffs and deletion drains.

## Decision

Use PostgreSQL 17 as the primary relational database. Enforce tenant isolation through application authorization, composite tenant foreign keys, non-null `workspace_id`, unique ownership constraints and row-level security for production tables once schema lands.

## Alternatives

- SQL Server: compatible with .NET, but PostgreSQL has stronger fit for JSONB, `FOR UPDATE SKIP LOCKED`, advisory locks and managed hosting portability.
- Document database: rejected because finance, export closure, uniqueness and tenant joins need relational guarantees.

## Security/privacy impact

Every tenant-owned table must include `workspace_id`. Cross-workspace foreign keys require composite ownership constraints or equivalent database policy. Raw user content stays out of logs and operational aggregate tables.

## Rollback

Before schema migrations, switch by replacing this ADR. After migrations, a database switch requires explicit data migration, export compatibility and tenant isolation re-certification.

