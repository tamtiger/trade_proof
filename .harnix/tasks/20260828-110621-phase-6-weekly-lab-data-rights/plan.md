# Plan - Phase 6: Weekly Lab and data rights

## Implementation Checklist

- [ ] `S1-WEEKLY-LAB-REPORT` — Add WeeklyCohort/input/report contracts, RED tests and deterministic report publication.
- [ ] `S2-EXPERIMENT-COMPLETION` — Add behavioral experiment lifecycle and weekly completion preconditions.
- [ ] `S3-PRODUCT-ANALYTICS` — Add first-party product analytics events, workspace/internal metric snapshots and external projection/purge records.
- [ ] `S4-EXPORT-DATA-RIGHTS` — Add export request/archive/manifest/round-trip/expiry and workspace deletion FENCE/drain/tombstone flow.
- [ ] `S5-API-UI-MIGRATION` — Expose Phase 6 APIs/UI controls and migration schema literals.
- [ ] `S6-VERIFY-FINISH` — Add verifier/local CI/changelog/CI workflow and complete Harnix verification.

### Slice `S1-WEEKLY-LAB-REPORT`
Criteria: `AC-001`, `AC-006`
Checks: `phase6-scope-review`, `phase6-app-tests`, `phase6-artifact-review`, `phase6-local-ci`
Paths: `src/TradeProof.Domain/Foundation`, `src/TradeProof.Application/Foundation`, `tests/TradeProof.App.Tests/Phase6Tests.cs`, `docs/WEEKLY_LAB.md`

### Slice `S2-EXPERIMENT-COMPLETION`
Criteria: `AC-002`, `AC-006`
Checks: `phase6-app-tests`, `phase6-artifact-review`, `phase6-local-ci`
Paths: `src/TradeProof.Domain/Foundation`, `src/TradeProof.Application/Foundation`, `tests/TradeProof.App.Tests/Phase6Tests.cs`, `docs/WEEKLY_LAB.md`

### Slice `S3-PRODUCT-ANALYTICS`
Criteria: `AC-003`, `AC-006`
Checks: `phase6-app-tests`, `phase6-artifact-review`, `phase6-local-ci`
Paths: `src/TradeProof.Domain/Foundation`, `src/TradeProof.Application/Foundation`, `tests/TradeProof.App.Tests/Phase6Tests.cs`, `docs/WEEKLY_LAB.md`, `docs/SECURITY_PRIVACY_AI.md`

### Slice `S4-EXPORT-DATA-RIGHTS`
Criteria: `AC-004`, `AC-005`, `AC-006`
Checks: `phase6-app-tests`, `phase6-artifact-review`, `phase6-local-ci`
Paths: `src/TradeProof.Domain/Foundation`, `src/TradeProof.Application/Foundation`, `src/TradeProof.Infrastructure/Migrations`, `tests/TradeProof.App.Tests/Phase6Tests.cs`, `docs/EXPORT_CONTRACT.md`, `docs/SECURITY_PRIVACY_AI.md`

### Slice `S5-API-UI-MIGRATION`
Criteria: `AC-001`, `AC-002`, `AC-003`, `AC-004`, `AC-005`, `AC-006`
Checks: `phase6-app-tests`, `phase6-artifact-review`, `phase6-local-ci`
Paths: `src/TradeProof.Api/Program.cs`, `src/TradeProof.Api/wwwroot`, `src/TradeProof.Infrastructure/Migrations`, `tools`

### Slice `S6-VERIFY-FINISH`
Criteria: `AC-001`, `AC-002`, `AC-003`, `AC-004`, `AC-005`, `AC-006`
Checks: `phase6-scope-review`, `phase6-app-tests`, `phase6-artifact-review`, `phase6-local-ci`
Paths: `.github/workflows/ci.yml`, `CHANGELOG.md`, `tools/test-phase6.ps1`, `tools/verify-phase6.ps1`, `.harnix/tasks`