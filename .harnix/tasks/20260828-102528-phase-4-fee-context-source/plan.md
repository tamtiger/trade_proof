# Plan - Phase 4: fee conversion and context source

## Implementation Checklist

- [x] `S1-CONTRACTS` — freeze Phase 4 contracts and RED tests.
- [x] `S2-MARKET-FEE` — implement public market source and third-asset fee conversion.
- [x] `S3-CONTEXT-SNAPSHOT` — implement context control and snapshot publisher.
- [x] `S4-API-CI` — add API/UI, migration, verifier, CI and changelog.
- [x] `S5-VERIFY-FINISH` — run required evidence and finish task.

### Slice `S1-CONTRACTS`
Criteria: `AC-001`, `AC-002`, `AC-003`, `AC-004`, `AC-005`, `AC-006`
Checks: `phase4-scope-review`, `phase4-app-tests`
Paths: `docs/IMPLEMENTATION_PLAN.md`, `docs/MARKET_CONTEXT_ENGINE.md`, `docs/IMPORT_AND_ACCOUNTING.md`, `tests/TradeProof.App.Tests/Phase4Tests.cs`, `tests/TradeProof.App.Tests/TestProgram.cs`, `src/TradeProof.Domain/Foundation`

Review Week 4/TP-MCE/TP-ACC sections, then add Phase 4 RED tests for direct/inverse fee conversion, no-lookahead bars, context trigger sequences, manual retry conflict and immutable snapshot behavior. RED must fail for missing Phase 4 contracts/APIs.

### Slice `S2-MARKET-FEE`
Criteria: `AC-001`, `AC-002`, `AC-003`
Checks: `phase4-app-tests`, `phase4-artifact-review`
Paths: `src/TradeProof.Domain/Foundation/MarketContextContracts.cs`, `src/TradeProof.Domain/Foundation/AccountingContracts.cs`, `src/TradeProof.Application/Foundation/MarketContextApp.cs`, `src/TradeProof.Application/Foundation/AccountingApp.cs`

Add market conversion catalog and public market provenance stores. Extend `FeeConversionRecord` and conversion logic so quote/base/zero retain exact null coupling while third-asset fees resolve via direct/inverse 1m bars with persisted bar/observation/path metadata or remain unavailable.

### Slice `S3-CONTEXT-SNAPSHOT`
Criteria: `AC-001`, `AC-004`, `AC-005`
Checks: `phase4-app-tests`, `phase4-artifact-review`
Paths: `src/TradeProof.Domain/Foundation/MarketContextContracts.cs`, `src/TradeProof.Application/Foundation/MarketContextApp.cs`, `src/TradeProof.Application/Foundation/TradeProofApp.cs`, `tests/TradeProof.App.Tests/Phase4Tests.cs`

Register `CONTEXT`, seed the exact context algorithm release, enqueue ENTRY/EXIT trigger jobs from episode projections, implement manual recompute idempotency, and publish immutable ContextSnapshot records using selected bars bounded by `asOfAt`.

### Slice `S4-API-CI`
Criteria: `AC-001`, `AC-005`, `AC-006`
Checks: `phase4-artifact-review`, `phase4-local-ci`
Paths: `src/TradeProof.Api/Program.cs`, `src/TradeProof.Api/wwwroot`, `src/TradeProof.Infrastructure/Migrations/004_phase4_fee_context_source.sql`, `tools/verify-phase4.ps1`, `tools/test-phase4.ps1`, `.github/workflows/ci.yml`, `CHANGELOG.md`

Expose local Phase 4 controls/status without credential/live-sync language, add schema migration for public market/context tables, update verification scripts/CI and keep earlier phase verifiers compatible with newer top-of-changelog/API version.

### Slice `S5-VERIFY-FINISH`
Criteria: `AC-001`, `AC-002`, `AC-003`, `AC-004`, `AC-005`, `AC-006`
Checks: `phase4-scope-review`, `phase4-app-tests`, `phase4-artifact-review`, `phase4-local-ci`
Paths: `.harnix/tasks/20260828-102528-phase-4-fee-context-source`, `tests/TradeProof.App.Tests`, `tools`, `src`

Capture Harnix pre/post snapshots for every required check, persist passing evidence only on matching digest, run Harnix check/finish, then commit with `feat: complete phase 4 fee context source`.