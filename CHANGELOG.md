# Changelog

## 2026-08-28 - Phase 7: core hardening

- Added explicit AI-disabled release profile contracts, deterministic hardening evidence and core release readiness reports for the local harness.
- Added Phase 7 API/UI readiness controls, migration contract, tests, artifact verifier and CI script while keeping Phase 0/1/2/3/4/5/6 checks green.

## 2026-08-28 - Phase 6: Weekly Lab and data rights

- Added deterministic Weekly Lab cohorts, input revisions, report revisions and behavioral experiment/completion lifecycle for the local harness.
- Added first-party product analytics, workspace/internal product metrics, external projection/purge records and reference-closed export/round-trip/expiry controls.
- Added workspace deletion fence/tombstone records, Phase 6 API/UI/migration/tests/verifier/local CI wiring while keeping Phase 0/1/2/3/4/5 checks green.

## 2026-08-28 - Phase 5: review, metrics and dashboard

- Added immutable episode review revisions, pinned review taxonomy versions and sanitized screenshot attachment lifecycle with deletion tombstones.
- Added deterministic metric snapshots for review coverage, plan adherence, behavior flags and context coverage using decimal-only calculations and sample-size evidence labels.
- Added Phase 5 API/UI dashboard controls, migration contract, tests, artifact verifier and CI script while keeping Phase 0/1/2/3/4 checks green.

## 2026-08-28 - Phase 4: fee conversion and context source

- Added deterministic public market-bar source contracts, conversion catalog provenance, context algorithm release, context triggers/manual retry and immutable ContextSnapshot records.
- Added third-asset fee conversion via direct/inverse point-in-time 1m bars with pinned market-bar/observation/path metadata and no future/current substitution.
- Added Phase 4 context API/UI controls, migration contract, tests, verifier and CI script while keeping Phase 0/1/2/3 checks green.

## 2026-08-28 - Phase 3: episode and accounting core

- Added Phase 3 accounting contracts, local IMPORT consumer, ImportRow/NormalizedFill admission, quote/base fee conversion, long-only quarantine and WAC episode ledger projections.
- Added plan-proof resolution for opening fills, Phase 3 import processing API/UI progress, migration contract, tests, verifier and CI script.
- Kept Phase 2 preview/confirm zero-business-row boundary intact while allowing confirmed IMPORT jobs to reconcile rows after an explicit process step.

## 2026-08-27 - Phase 2: secure ingestion

- Added reserve-before-write RAW_UPLOAD ingestion, single-use write capability, conditional provider write, upload transfer, validation and purge chains in the local harness.
- Added Binance Spot CSV preview validation with UTF-8/header/size/row hard limits, sanitized `import_preview_v1` summaries and ConfirmImport-to-IMPORT idempotency.
- Added staging/progress shell, import UI workflow, Phase 2 migration contract, tests, artifact verifier and CI script while keeping Phase 0/1 checks green.

## 2026-08-27 - Phase 1: tenant foundation and Quick Plan

- Added managed-development identity bootstrap with tenant-scoped User, Workspace, TradingAccount, system OTHER setup preset, idempotency receipts and PRE_AUTH/POST_AUTH audit boundaries.
- Added Phase 1 schema contract, in-memory tenant-work foundation, product measurement timeout flow and Quick Plan command lifecycle.
- Added responsive local Quick Plan UI, Phase 1 tests, artifact verifier and CI script.

## 2026-08-27 - Phase 0: technical ADR and fixture freeze

- Added Week 0 ADR baseline covering runtime, identity, database, queue, object storage/scanner, market data, AI, deployment, observability and Binance terms boundaries.
- Added fixture intake documentation with explicit real-sample consent slots and no fabricated Binance sample data.
- Added .NET 10 solution skeleton, artifact verification project, local Phase 0 verifier and CI workflow.
- Recorded Binance market-data boundary as public market-data-only usage with no raw redistribution until fresh terms review.
