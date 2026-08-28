# Phase 2 Plan: secure ingestion

## Implementation Checklist

- [x] `S1-INGEST-RESERVATION` — tạo domain/application contracts cho ObjectIngestReservation, write capability và OBJECT_INGEST_FINALIZE chain.
- [x] `S2-UPLOAD-TRANSFER` — triển khai gateway write-once, Upload/RECEIVE/lease/purge deadlines và UPLOAD_VALIDATE/UPLOAD_PURGE chains.
- [x] `S3-CSV-PREVIEW` — triển khai parser RFC4180/UTF-8/header/boundary và import_preview_v1 sanitized summary/hash/TTL.
- [x] `S4-CONFIRM-IMPORT` — triển khai ConfirmImport idempotent tạo ImportBatch + IMPORT fence, zero business rows.
- [x] `S5-STAGING-PROGRESS` — thêm source-row fingerprint, staged_fill_v1 candidate/disposition shell, batch summary/progress/error safe DTO.
- [x] `S6-API-UI` — map Minimal API endpoints và import progress/error UI thật, không API-key/private-sync/generic-mapper surface.
- [x] `S7-CI-CHANGELOG` — thêm Phase 2 tests/verifier/local CI và CHANGELOG entry ở đầu file.

### Slice `S1-INGEST-RESERVATION`

Criteria: `AC-001`
Checks: `phase2-scope-review`, `phase2-app-tests`, `phase2-artifact-review`, `phase2-local-ci`
Paths: `docs/SECURITY_PRIVACY_AI.md`, `docs/PRODUCT_REQUIREMENTS.md`, `src/TradeProof.Domain/Foundation/TradeProofContracts.cs`, `src/TradeProof.Domain/Foundation/IngestionContracts.cs`, `src/TradeProof.Application/Foundation/TradeProofApp.cs`, `src/TradeProof.Application/Foundation/IngestionApp.cs`, `src/TradeProof.Infrastructure/Migrations/002_phase2_secure_ingestion.sql`

Implement reserve-before-write cho RAW_UPLOAD: preallocated upload ID, immutable deadlines, write capability single-use và OBJECT_INGEST_FINALIZE job/fence atomically at RESERVE.

### Slice `S2-UPLOAD-TRANSFER`

Criteria: `AC-002`
Checks: `phase2-scope-review`, `phase2-app-tests`, `phase2-artifact-review`, `phase2-local-ci`
Paths: `docs/SECURITY_PRIVACY_AI.md`, `src/TradeProof.Domain/Foundation/IngestionContracts.cs`, `src/TradeProof.Application/Foundation/IngestionApp.cs`, `src/TradeProof.Infrastructure/Migrations/002_phase2_secure_ingestion.sql`

Implement in-memory provider conditional create, RECORD_BYTES, TRANSFER into Upload/RECEIVE/UploadObjectLease, UPLOAD_VALIDATE and UPLOAD_PURGE chains, exact forced/purge deadlines.

### Slice `S3-CSV-PREVIEW`

Criteria: `AC-003`
Checks: `phase2-scope-review`, `phase2-app-tests`, `phase2-artifact-review`, `phase2-local-ci`
Paths: `docs/IMPORT_AND_ACCOUNTING.md`, `docs/ACCEPTANCE_TESTS.md`, `src/TradeProof.Domain/Foundation/IngestionContracts.cs`, `src/TradeProof.Application/Foundation/IngestionApp.cs`, `tests/TradeProof.App.Tests/Phase2Tests.cs`

Use platform CSV parser support, validate UTF-8/header/row/size hard limits, produce sanitized import_preview_v1 summary/hash/expiry, and reject malformed files with zero preview/batch/business state.

### Slice `S4-CONFIRM-IMPORT`

Criteria: `AC-004`
Checks: `phase2-scope-review`, `phase2-app-tests`, `phase2-artifact-review`, `phase2-local-ci`
Paths: `docs/IMPORT_AND_ACCOUNTING.md`, `docs/SECURITY_PRIVACY_AI.md`, `src/TradeProof.Domain/Foundation/IngestionContracts.cs`, `src/TradeProof.Application/Foundation/IngestionApp.cs`, `tests/TradeProof.App.Tests/Phase2Tests.cs`

Implement ConfirmImport exact hash/idempotency semantics and IMPORT TenantControlJob enqueue without ImportRow/fill/episode/ledger effects.

### Slice `S5-STAGING-PROGRESS`

Criteria: `AC-005`
Checks: `phase2-scope-review`, `phase2-app-tests`, `phase2-artifact-review`, `phase2-local-ci`
Paths: `docs/IMPORT_AND_ACCOUNTING.md`, `src/TradeProof.Domain/Foundation/IngestionContracts.cs`, `src/TradeProof.Application/Foundation/IngestionApp.cs`, `src/TradeProof.Infrastructure/Migrations/002_phase2_secure_ingestion.sql`, `tests/TradeProof.App.Tests/Phase2Tests.cs`

Add minimal immutable staged_fill_v1 shell, source-row fingerprint, safe row error model and batch summary/progress DTO needed before Week 3 accounting activation.

### Slice `S6-API-UI`

Criteria: `AC-006`
Checks: `phase2-scope-review`, `phase2-artifact-review`, `phase2-local-ci`
Paths: `src/TradeProof.Api/Program.cs`, `src/TradeProof.Api/wwwroot/index.html`, `src/TradeProof.Api/wwwroot/quick-plan.js`, `src/TradeProof.Api/wwwroot/styles.css`, `tools/verify-phase2.ps1`

Expose import endpoints and UI states using local API, Vietnamese status/error text, no exchange API key/private sync/generic mapper entry point.

### Slice `S7-CI-CHANGELOG`

Criteria: `AC-007`
Checks: `phase2-scope-review`, `phase2-app-tests`, `phase2-artifact-review`, `phase2-local-ci`
Paths: `CHANGELOG.md`, `.github/workflows/ci.yml`, `tools/test-phase2.ps1`, `tools/verify-phase2.ps1`, `tests/TradeProof.App.Tests/TestProgram.cs`

Add Phase 2 tests/verifier/local CI, update GitHub CI to Phase 2, keep Phase 0/1 checks green and put the Phase 2 changelog entry at the top.