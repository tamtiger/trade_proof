# Plan - Phase 5: review, metrics and dashboard

## Implementation Checklist

- [x] `S1-REVIEW-CONTRACTS` — Freeze Review taxonomy/contracts and RED tests.
- [x] `S2-ATTACHMENT-SAGA` — Implement screenshot attachment reservation, activation, join and delete/tombstone flow.
- [x] `S3-METRICS-ENGINE` — Implement METRICS work type and deterministic MetricSnapshot publication.
- [x] `S4-DASHBOARD-API-UI` — Add API/UI/dashboard review, attachment, metric quality and drill-down surfaces.
- [x] `S5-VERIFY-FINISH` — Add migration/verifier/local CI/CHANGELOG and complete Harnix verification.

### Slice `S1-REVIEW-CONTRACTS`
Criteria: `AC-001`, `AC-003`, `AC-005`
Checks: `phase5-scope-review`, `phase5-app-tests`, `phase5-artifact-review`, `phase5-local-ci`
Paths: `src/TradeProof.Domain/Foundation`, `src/TradeProof.Application/Foundation`, `tests/TradeProof.App.Tests/Phase5Tests.cs`, `docs/IMPORT_AND_ACCOUNTING.md`

Add Phase 5 tests first, then add Review taxonomy/version records, review commands, validation invariants, content hash and append-only state.

### Slice `S2-ATTACHMENT-SAGA`
Criteria: `AC-002`, `AC-004`, `AC-005`
Checks: `phase5-app-tests`, `phase5-artifact-review`, `phase5-local-ci`
Paths: `src/TradeProof.Domain/Foundation/IngestionContracts.cs`, `src/TradeProof.Application/Foundation/IngestionApp.cs`, `src/TradeProof.Application/Foundation`, `src/TradeProof.Infrastructure/Migrations`, `tests/TradeProof.App.Tests/Phase5Tests.cs`

Reuse the existing reservation/finalizer foundation to preallocate screenshot attachment IDs, validate supported local image bytes, activate one sanitized attachment, join it to review revisions, and delete/tombstone without mutating historical joins.

### Slice `S3-METRICS-ENGINE`
Criteria: `AC-003`, `AC-004`, `AC-005`
Checks: `phase5-app-tests`, `phase5-artifact-review`, `phase5-local-ci`
Paths: `src/TradeProof.Domain/Foundation`, `src/TradeProof.Application/Foundation`, `tests/TradeProof.App.Tests/Phase5Tests.cs`, `docs/WEEKLY_LAB.md`, `docs/IMPORT_AND_ACCOUNTING.md`

Register `METRICS`, enqueue metric work after projection publication, compute immutable metric snapshots from active closed projections, exact review/context sources, exclusion reasons and sample labels.

### Slice `S4-DASHBOARD-API-UI`
Criteria: `AC-004`, `AC-005`
Checks: `phase5-app-tests`, `phase5-artifact-review`, `phase5-local-ci`
Paths: `src/TradeProof.Api/Program.cs`, `src/TradeProof.Api/wwwroot/index.html`, `src/TradeProof.Api/wwwroot/quick-plan.js`, `src/TradeProof.Api/wwwroot/styles.css`, `src/TradeProof.Application/Foundation/TradeProofApp.cs`

Expose review, attachment and metric commands through API; extend dashboard response and UI with operational controls and data-quality/drill-down views without client-side metric recomputation.

### Slice `S5-VERIFY-FINISH`
Criteria: `AC-001`, `AC-002`, `AC-003`, `AC-004`, `AC-005`
Checks: `phase5-scope-review`, `phase5-app-tests`, `phase5-artifact-review`, `phase5-local-ci`
Paths: `.github/workflows/ci.yml`, `CHANGELOG.md`, `tools/test-phase5.ps1`, `tools/verify-phase5.ps1`, `src/TradeProof.Infrastructure/Migrations`, `.harnix/tasks`

Update migration, verifier, CI workflow and changelog; run snapshot-backed required checks and finish only after all acceptance criteria are met.