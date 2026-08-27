# Phase 1 Plan: tenant foundation and Quick Plan

## Implementation Checklist

- [x] `S1-TENANT-AUTH-SCHEMA` — tạo domain/application foundation, managed-development identity boundary và schema SQL contract.
- [x] `S2-WORK-FOUNDATION` — triển khai shared tenant-work primitives, sequence/fence/marker/idempotency/provider lookup.
- [x] `S3-MEASUREMENT` — triển khai product_measurement_run_v1 và registered PRODUCT_MEASUREMENT_TIMEOUT.
- [x] `S4-SETUP-PLAN` — triển khai setup presets và Quick Plan arm/revise/cancel/expire no-draft.
- [x] `S5-API-UI` — map Minimal API endpoints và responsive Quick Plan UI đầu tiên.
- [x] `S6-CI-CHANGELOG` — thêm Phase 1 verifier/local CI và CHANGELOG entry ở đầu file.

### Slice `S1-TENANT-AUTH-SCHEMA`

Criteria: `AC-001`, `AC-002`
Checks: `phase1-scope-review`, `phase1-contract-tests`, `phase1-artifact-review`, `phase1-local-ci`
Paths: `src/TradeProof.Domain/Foundation/TradeProofContracts.cs`, `src/TradeProof.Application/Foundation/TradeProofApp.cs`, `src/TradeProof.Infrastructure/Migrations/001_phase1_foundation.sql`, `src/TradeProof.Api/Program.cs`, `tests/TradeProof.App.Tests/Phase1Tests.cs`

Managed-development auth boundary không có password, issuer/subject byte-exact, bootstrap one User/Workspace/TradingAccount/system OTHER preset, PRE_AUTH/POST_AUTH audit và SQL contract tenant-owned.

### Slice `S2-WORK-FOUNDATION`

Criteria: `AC-003`
Checks: `phase1-artifact-review`, `phase1-local-ci`
Paths: `src/TradeProof.Domain/Foundation/TradeProofContracts.cs`, `src/TradeProof.Application/Foundation/TradeProofApp.cs`, `src/TradeProof.Infrastructure/Migrations/001_phase1_foundation.sql`, `tests/TradeProof.App.Tests/Phase1Tests.cs`

Triển khai shared durable primitive trong local harness: TenantControlJob, TenantWorkItemFence, contiguous sequence, optional lease schema, terminal marker, payload digest, semantic idempotency cả sau compaction và deterministic provider lookup.

### Slice `S3-MEASUREMENT`

Criteria: `AC-004`
Checks: `phase1-artifact-review`, `phase1-local-ci`
Paths: `src/TradeProof.Domain/Foundation/TradeProofContracts.cs`, `src/TradeProof.Application/Foundation/TradeProofApp.cs`, `tests/TradeProof.App.Tests/Phase1Tests.cs`, `src/TradeProof.Infrastructure/Migrations/001_phase1_foundation.sql`

Implement product_measurement_run_v1 theo TP-LAB G23: START, deadline 30 phút, QUICK_PLAN practice 1..3 before MEASURED, timeout control và terminal marker không external lease.

### Slice `S4-SETUP-PLAN`

Criteria: `AC-005`
Checks: `phase1-artifact-review`, `phase1-local-ci`
Paths: `src/TradeProof.Domain/Foundation/TradeProofContracts.cs`, `src/TradeProof.Application/Foundation/TradeProofApp.cs`, `tests/TradeProof.App.Tests/Phase1Tests.cs`

Implement setup_preset_v1, setup_label_key_v1, plan_checklist_v1 và ArmPlan no-draft/idempotent/append-only với decimal canonicalization, one armed plan per account/symbol, revise/cancel/expire server timestamp.

### Slice `S5-API-UI`

Criteria: `AC-001`, `AC-005`, `AC-006`
Checks: `phase1-artifact-review`, `phase1-local-ci`
Paths: `src/TradeProof.Api/Program.cs`, `src/TradeProof.Api/wwwroot/index.html`, `src/TradeProof.Api/wwwroot/quick-plan.js`, `src/TradeProof.Api/wwwroot/styles.css`, `.github/workflows/ci.yml`

Map Minimal API endpoints và serve responsive Quick Plan UI làm first screen, dùng dev identity headers, tiếng Việt, không AI/exchange key surface.

### Slice `S6-CI-CHANGELOG`

Criteria: `AC-007`, `AC-001`, `AC-002`, `AC-003`, `AC-004`, `AC-005`, `AC-006`
Checks: `phase1-artifact-review`, `phase1-local-ci`
Paths: `CHANGELOG.md`, `.github/workflows/ci.yml`, `tools/verify-phase0.ps1`, `tools/test-phase0.ps1`, `tools/verify-phase1.ps1`, `tools/test-phase1.ps1`

Phase 1 verifier/local CI phải chạy không cần production secret, kiểm CHANGELOG entry mới nhất ở đầu file, và bảo đảm không track bin/obj.