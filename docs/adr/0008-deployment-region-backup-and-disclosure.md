# ADR 0008: Deployment Region, Backup and Disclosure

- Status: Accepted
- Date: 2026-08-27
- Owner: TamNT167

## Context

The MVP audience is Vietnamese Binance Spot traders. `TP-SEC` requires region disclosure, backup/RPO/RTO, processor disclosure and deletion/restore tombstone behavior.

## Decision

Target Azure Southeast Asia for pilot deployment, with managed PostgreSQL, private object storage, containerized ASP.NET Core API/workers and daily encrypted backups. Publish processor/region disclosure before pilot. Initial RPO is 24 hours and RTO is 8 hours, matching Product Brief NFR.

## Alternatives

- Multi-region active-active: deferred; deletion, export and tenant fences are complex enough in one region.
- Local-only deployment: unsuitable for SaaS pilot.

## Security/privacy impact

Backups are encrypted and restore must apply deletion tombstones before traffic. Region and subprocessor disclosure is mandatory before onboarding pilot users.

## Rollback

If Azure Southeast Asia is unavailable or terms/data residency review rejects it, choose another single region before production data exists and rerun threat model/disclosure.

