# PRD: Phase 8 pilot readiness

Outcome: hoàn tất release-candidate package cho pilot-readiness trong repo bằng operations docs, evidence bundle, support diagnostics và Phase 8 regression gates, không tự deploy hoặc bật paid pilot/AI.

In scope:

- Production readiness review cho local release candidate, gồm no P0/P1 defect và non-waivable gates.
- Alert dashboard/on-call ownership table và runbook exercise cho incident, backup/restore, deletion và processor dependency.
- Pilot onboarding/support docs với known limitations, disabled flags và no-workspace-access support boundary.
- Data processor disclosure draft và release evidence bundle theo `TP-AT` Section 8.
- Read-only support diagnostics script, Phase 8 tests, verifier, CI wiring và changelog.

Out of scope:

- Không deploy production, không tạo externally visible release hoặc paid pilot thật.
- Không bật AI extensions, paid feature flag, processor credential, network call hoặc workspace data export.
- Không ký legal contract, không xác nhận on-call lịch thật hoặc backup restore pilot-equivalent ngoài repo.

### AC `AC-001`
Operations package có readiness review, alert dashboard/on-call ownership, incident/backup/deletion/processor runbook exercise, pilot onboarding/support flow, known limitations và data processor disclosure; docs nói rõ local candidate không tự deploy và không cần workspace user-data access.

### AC `AC-002`
Release evidence bundle liệt kê build/commit capture policy, migration version tới Phase 7, requirements-to-tests matrix, test reports, security/secret scan, performance/usability/accessibility evidence, disabled flags, known limitations, no P0/P1 defect và version list cho các contract chính.

### AC `AC-003`
Support diagnostics script chỉ đọc repo metadata, docs, git status/log và Harnix public status; script không yêu cầu WorkspaceId, token, secret, DB credential, object store credential hoặc workspace export.

### AC `AC-004`
API/CI/test wiring được cập nhật cho Phase 8: health/openapi lên `phase-8`, Phase8Tests và verify-phase8 pass, test-phase8 chạy Phase 0-8 runners/verifiers, các verifier Phase 2-7 vẫn tương thích.
