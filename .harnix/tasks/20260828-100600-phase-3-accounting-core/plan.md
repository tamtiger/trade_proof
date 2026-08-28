# Phase 3 Plan: episode and accounting core

## Implementation Checklist

- [x] `S1-CONTRACTS` — thêm Phase 3 version literals, domain records và application DTO cho ImportRow/fill/episode/ledger/proof/progress.
- [x] `S2-IMPORT-CONSUMER` — triển khai `ProcessImportAsync` idempotent parse confirmed batch, tạo ImportRow/NormalizedFill/dedup/quarantine và terminalize IMPORT fence.
- [x] `S3-EPISODE-LEDGER` — triển khai episode state machine, quote/base fee conversion và WAC allocation/ledger với invariants.
- [x] `S4-PLAN-PROOF` — nối first BUY opening fill với Quick Plan để tạo VERIFIED/AMBIGUOUS/UNMATCHED và consume plan đúng boundary.
- [x] `S5-API-UI` — expose worker/progress endpoint và UI summary cho row dispositions + episode/accounting quality.
- [x] `S6-CI-CHANGELOG` — thêm migration, Phase 3 tests/verifier/local CI/CHANGELOG và giữ Phase 0/1/2 xanh.

### Slice `S1-CONTRACTS`

Criteria: `AC-001`, `AC-006`
Checks: `phase3-scope-review`, `phase3-app-tests`, `phase3-artifact-review`, `phase3-local-ci`
Paths: `src/TradeProof.Domain/Foundation/TradeProofContracts.cs`, `src/TradeProof.Domain/Foundation/IngestionContracts.cs`, `src/TradeProof.Application/Foundation/IngestionApp.cs`, `src/TradeProof.Infrastructure/Migrations/003_phase3_accounting_core.sql`, `tests/TradeProof.App.Tests/Phase3Tests.cs`

Add exact contract constants and immutable records for NormalizedFill, ImportRow, FeeConversion, TradeEpisodeProjection, allocation, ledger and progress summary without changing Phase 2 preview/confirm semantics.

### Slice `S2-IMPORT-CONSUMER`

Criteria: `AC-002`, `AC-006`
Checks: `phase3-scope-review`, `phase3-app-tests`, `phase3-artifact-review`, `phase3-local-ci`
Paths: `docs/IMPORT_AND_ACCOUNTING.md`, `src/TradeProof.Application/Foundation/IngestionApp.cs`, `src/TradeProof.Domain/Foundation/IngestionContracts.cs`, `tests/TradeProof.App.Tests/Phase3Tests.cs`

Process confirmed batch rows from retained provider bytes, create one ImportRow per non-blank row, preserve idempotency, admit new fills by canonical signature, classify duplicates, quarantine safe row errors and update immutable terminal counters.

### Slice `S3-EPISODE-LEDGER`

Criteria: `AC-003`, `AC-006`
Checks: `phase3-scope-review`, `phase3-app-tests`, `phase3-artifact-review`, `phase3-local-ci`
Paths: `docs/IMPORT_AND_ACCOUNTING.md`, `src/TradeProof.Application/Foundation/IngestionApp.cs`, `src/TradeProof.Domain/Foundation/IngestionContracts.cs`, `src/TradeProof.Infrastructure/Migrations/003_phase3_accounting_core.sql`, `tests/TradeProof.App.Tests/Phase3Tests.cs`

Implement long-only episode projection for deterministic source order, quote/base fee conversion, weighted-average cost, allocations and exactly two ledger entries per allocation; third-asset missing conversion marks the source row pending and episode quality missing.

### Slice `S4-PLAN-PROOF`

Criteria: `AC-004`, `AC-006`
Checks: `phase3-scope-review`, `phase3-app-tests`, `phase3-artifact-review`, `phase3-local-ci`
Paths: `docs/IMPORT_AND_ACCOUNTING.md`, `src/TradeProof.Application/Foundation/TradeProofApp.cs`, `src/TradeProof.Application/Foundation/IngestionApp.cs`, `src/TradeProof.Domain/Foundation/TradeProofContracts.cs`, `tests/TradeProof.App.Tests/Phase3Tests.cs`

For opening BUY fills, evaluate armed plan timing against source interval, persist proof status/reason/candidates, freeze only verified revisions and append idempotent CONSUME when associated.

### Slice `S5-API-UI`

Criteria: `AC-005`, `AC-006`
Checks: `phase3-scope-review`, `phase3-artifact-review`, `phase3-local-ci`
Paths: `src/TradeProof.Api/Program.cs`, `src/TradeProof.Api/wwwroot/index.html`, `src/TradeProof.Api/wwwroot/quick-plan.js`, `src/TradeProof.Api/wwwroot/styles.css`, `tools/verify-phase3.ps1`

Expose import processing endpoint and enrich progress response/UI with safe row disposition counters plus episode/accounting summary, while preserving Vietnamese no-signal copy.

### Slice `S6-CI-CHANGELOG`

Criteria: `AC-006`
Checks: `phase3-scope-review`, `phase3-app-tests`, `phase3-artifact-review`, `phase3-local-ci`
Paths: `CHANGELOG.md`, `.github/workflows/ci.yml`, `tools/test-phase3.ps1`, `tools/verify-phase3.ps1`, `tests/TradeProof.App.Tests/TestProgram.cs`

Add Phase 3 verification scripts, CI phase routing and changelog entry, then run full local CI through Phase 3 before finish/commit.
