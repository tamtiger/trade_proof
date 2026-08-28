# PRD: Phase 7 core hardening

Outcome: hoàn tất bước hardening core release cho local harness bằng cách chứng minh profile AI disabled, release readiness evidence và regression guard mà không bật extension AI.

In scope:

- Core AI-disabled profile giữ `voice_transcription_enabled`, `ai_taxonomy_enabled` và `ai_weekly_summary_enabled` bằng `false`.
- Release readiness report gom security, accessibility, performance và reliability smoke evidence deterministic trong local harness.
- API/UI hiển thị trạng thái readiness và disabled AI profile nhưng không tạo AI consent/run/output endpoint hoặc control giả hoạt động.
- Test, verifier, local CI và CHANGELOG cho Phase 7; Phase 0-6 vẫn pass.

Out of scope:

- Không triển khai voice transcription, taxonomy suggestion, AI weekly summary, AI gateway, processor credential hoặc network/outbound call.
- Không thay thế production readiness review, on-call dashboard thật, backup restore exercise pilot-equivalent hoặc data processor disclosure pháp lý của Week 8.
- Không thay đổi schema/behavior tài chính đã hoàn tất ở Phase 0-6.

### AC `AC-001`
Core AI-disabled profile là explicit contract: ba feature flags đều `false`, không có AI processor/credential/outbound route, không có `AI_RUN`, `AI_CANCEL`, `AI_OUTPUT_DELETE` callable endpoint/control và không có UI control giả hoạt động cho extension.

### AC `AC-002`
Release hardening evidence record có schema version immutable, trạng thái P0/P1 defect bằng zero, security/a11y/performance/reliability smoke pass, giữ AI outage/dependency blocked mà core flow vẫn complete.

### AC `AC-003`
API/UI Phase 7 expose readiness theo cách deterministic: `/healthz` và OpenAPI lên `phase-7`, dashboard có readiness data, UI có panel readiness không dùng từ ngữ signal/AI bật và không thêm route AI callable.

### AC `AC-004`
Migration, tests, verifier, local CI và CHANGELOG được cập nhật cho Phase 7; Phase 0-6 runners/verifiers vẫn xanh và guard secret/bin-obj vẫn pass.