# Plan - Phase 7: core hardening

## Implementation Checklist

- [x] `S1-CONTRACT-FLAGS` — Add ReleaseReadiness contracts, AI-disabled profile constants and hardening evidence records.
- [x] `S2-APP-API-UI` — Add deterministic readiness app, dashboard/API surface and UI panel without AI extension controls.
- [x] `S3-TESTS-CI` — Add migration, Phase 7 tests, verifier, local CI and changelog/CI wiring.
- [ ] `S4-VERIFY-FINISH` — Run snapshot-bound checks, finish Harnix task and commit Phase 7.

<!-- harnix:execution-notes:begin -->
slice:S1-CONTRACT-FLAGS=passed
slice:S2-APP-API-UI=passed
slice:S3-TESTS-CI=passed
slice:S4-VERIFY-FINISH=pending
check:phase7-scope-review=pending
check:phase7-app-tests=pending
check:phase7-artifact-review=pending
check:phase7-local-ci=pending
<!-- harnix:execution-notes:end -->

### Slice `S1-CONTRACT-FLAGS`
Criteria: `AC-001`, `AC-002`
Checks: `phase7-scope-review`, `phase7-app-tests`, `phase7-artifact-review`
Paths: `src/TradeProof.Domain/Foundation/ReleaseReadinessContracts.cs`, `src/TradeProof.Domain/Foundation/IngestionContracts.cs`, `docs/IMPLEMENTATION_PLAN.md`, `docs/ACCEPTANCE_TESTS.md`, `docs/SECURITY_PRIVACY_AI.md`, `docs/adr/0007-ai-processor.md`

Work: add versioned contract constants and immutable records for AI disabled profile, release hardening evidence, readiness report and registered hardening work labels without enabling AI work.

### Slice `S2-APP-API-UI`
Criteria: `AC-001`, `AC-002`, `AC-003`
Checks: `phase7-app-tests`, `phase7-artifact-review`
Paths: `src/TradeProof.Application/Foundation/ReleaseReadinessApp.cs`, `src/TradeProof.Application/Foundation/TradeProofApp.cs`, `src/TradeProof.Api/Program.cs`, `src/TradeProof.Api/wwwroot/index.html`, `src/TradeProof.Api/wwwroot/quick-plan.js`, `src/TradeProof.Api/wwwroot/styles.css`

Work: create deterministic readiness publication in the local app, expose it through dashboard/API, and render a core-readiness panel that reports disabled flags and gate state without extension controls.

### Slice `S3-TESTS-CI`
Criteria: `AC-001`, `AC-002`, `AC-003`, `AC-004`
Checks: `phase7-app-tests`, `phase7-artifact-review`, `phase7-local-ci`
Paths: `src/TradeProof.Infrastructure/Migrations/007_phase7_core_hardening.sql`, `tests/TradeProof.App.Tests/Phase7Tests.cs`, `tests/TradeProof.App.Tests/TestProgram.cs`, `tools/verify-phase7.ps1`, `tools/test-phase7.ps1`, `.github/workflows/ci.yml`, `CHANGELOG.md`

Work: add forward-only migration contract, focused Phase 7 tests, artifact verifier, local CI wrapper and changelog/CI wiring while keeping previous phase checks compatible with `phase-7`.

### Slice `S4-VERIFY-FINISH`
Criteria: `AC-001`, `AC-002`, `AC-003`, `AC-004`
Checks: `phase7-scope-review`, `phase7-app-tests`, `phase7-artifact-review`, `phase7-local-ci`
Paths: `.harnix/tasks`, `.harnix/workspace/TamNT167/journal/2026-08-28.jsonl`, `tools/test-phase7.ps1`

Work: run snapshot-bound verification for every required check, finish the Harnix task only after completion audit passes, then commit Phase 7 with the authorized per-phase commit policy.