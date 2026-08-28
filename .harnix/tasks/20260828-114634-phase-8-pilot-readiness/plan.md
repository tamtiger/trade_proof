# Plan - Phase 8: pilot readiness

## Implementation Checklist

- [x] `S1-READINESS-DOCS` — Add operations readiness docs and release evidence bundle.
- [x] `S2-SUPPORT-SCRIPT` — Add read-only pilot support diagnostics script.
- [x] `S3-TESTS-CI` — Add Phase 8 tests, verifier, API marker, CI/changelog and compatibility wiring.
- [ ] `S4-VERIFY-FINISH` — Run snapshot-bound checks, finish Harnix task and commit Phase 8.

<!-- harnix:execution-notes:begin -->
slice:S1-READINESS-DOCS=passed
slice:S2-SUPPORT-SCRIPT=passed
slice:S3-TESTS-CI=passed
slice:S4-VERIFY-FINISH=pending
check:phase8-scope-review=passed
check:phase8-artifact-tests=passed
check:phase8-artifact-review=passed
check:phase8-local-ci=passed
<!-- harnix:execution-notes:end -->

### Slice `S1-READINESS-DOCS`
Criteria: `AC-001`, `AC-002`
Checks: `phase8-scope-review`, `phase8-artifact-tests`, `phase8-artifact-review`, `phase8-local-ci`
Paths: `docs/operations/pilot-readiness-review.md`, `docs/operations/alert-dashboard.md`, `docs/operations/runbook-exercise.md`, `docs/operations/pilot-onboarding-support.md`, `docs/operations/data-processor-disclosure.md`, `docs/operations/release-evidence-bundle.md`, `docs/IMPLEMENTATION_PLAN.md`, `docs/ACCEPTANCE_TESTS.md`

Work: write concrete Week 8 operations docs and a release evidence bundle that records local candidate limits, no self-deploy, no workspace data access, no P0/P1 defects, disabled flags, migration version and requirements-to-tests mapping.

### Slice `S2-SUPPORT-SCRIPT`
Criteria: `AC-001`, `AC-003`
Checks: `phase8-artifact-tests`, `phase8-artifact-review`, `phase8-local-ci`
Paths: `tools/pilot-support-diagnostics.ps1`, `docs/operations/pilot-onboarding-support.md`, `docs/operations/release-evidence-bundle.md`

Work: add a support diagnostics script that gathers only repo-local metadata, selected docs, git status/log and public Harnix status, while rejecting workspace IDs, tokens, secrets, database credentials, object-store credentials and export paths.

### Slice `S3-TESTS-CI`
Criteria: `AC-001`, `AC-002`, `AC-003`, `AC-004`
Checks: `phase8-artifact-tests`, `phase8-artifact-review`, `phase8-local-ci`
Paths: `src/TradeProof.Api/Program.cs`, `tests/TradeProof.App.Tests/Phase8Tests.cs`, `tests/TradeProof.App.Tests/TestProgram.cs`, `tools/verify-phase8.ps1`, `tools/test-phase8.ps1`, `tools/verify-phase2.ps1`, `tools/verify-phase3.ps1`, `tools/verify-phase4.ps1`, `tools/verify-phase5.ps1`, `tools/verify-phase6.ps1`, `tools/verify-phase7.ps1`, `tools/test-phase7.ps1`, `.github/workflows/ci.yml`, `CHANGELOG.md`

Work: add RED Phase 8 tests first, then update API phase marker, verifier/local CI/changelog and previous verifier compatibility so Phase 0-8 checks run together.

### Slice `S4-VERIFY-FINISH`
Criteria: `AC-001`, `AC-002`, `AC-003`, `AC-004`
Checks: `phase8-scope-review`, `phase8-artifact-tests`, `phase8-artifact-review`, `phase8-local-ci`
Paths: `.harnix/tasks`, `.harnix/workspace/TamNT167/journal/2026-08-28.jsonl`, `tools/test-phase8.ps1`

Work: run snapshot-bound verification for every required check, finish the Harnix task only after completion audit passes, then commit Phase 8 with the user-authorized per-phase commit policy.
