# Changelog

## 2026-08-27 - Phase 1: tenant foundation and Quick Plan

- Added managed-development identity bootstrap with tenant-scoped User, Workspace, TradingAccount, system OTHER setup preset, idempotency receipts and PRE_AUTH/POST_AUTH audit boundaries.
- Added Phase 1 schema contract, in-memory tenant-work foundation, product measurement timeout flow and Quick Plan command lifecycle.
- Added responsive local Quick Plan UI, Phase 1 tests, artifact verifier and CI script.

## 2026-08-27 - Phase 0: technical ADR and fixture freeze

- Added Week 0 ADR baseline covering runtime, identity, database, queue, object storage/scanner, market data, AI, deployment, observability and Binance terms boundaries.
- Added fixture intake documentation with explicit real-sample consent slots and no fabricated Binance sample data.
- Added .NET 10 solution skeleton, artifact verification project, local Phase 0 verifier and CI workflow.
- Recorded Binance market-data boundary as public market-data-only usage with no raw redistribution until fresh terms review.
