# PRD - Phase 6: Weekly Lab and data rights

## Outcome

Triển khai Week 6 trên local harness để TradeProof có một vòng deterministic sau review: khóa Weekly Lab, publish report, đề xuất behavior experiment, ghi completion, tạo product analytics snapshot tối thiểu, export reference-closed và chạy data-rights deletion flow có FENCE/drain/tombstone.

## Scope

- WeeklyCohort, WeeklyCohortInputRevision, WeeklyReportRevision và renderer payload `weekly_lab_v1`/`weekly_lab_renderer_v1`.
- BehavioralExperimentRevision và WeeklyReviewCompletion với append-only/idempotency/stale guards.
- Product analytics local records và metric snapshots không chứa raw content.
- Export request/job/archive manifest, round-trip validation, STANDARD/OVERSIZE classification và expiry evidence.
- Workspace deletion request/FENCE/target/drain/tombstone local records.
- API/UI/migration/tests/verifier/local CI/changelog cho Phase 6.

## Acceptance Criteria

### AC `AC-001`

Weekly Lab tạo `WeeklyCohort`, input revision và report revision deterministic theo workspace timezone, Monday-local half-open interval, locked as-of, homogeneous dependency tuple, chọn MetricSnapshot/Review/Context refs immutable và renderer output không tính metric phía client.

### AC `AC-002`

Behavioral experiment và weekly completion có taxonomy `behavioral_experiment_v1`, propose/confirm/cancel append-only, target next regular cohort, uniqueness/idempotency/stale guard và completion precondition gắn đúng report/experiment/cohort.

### AC `AC-003`

Product analytics local harness có first-party `product_analytics_event_v1`, workspace product metric snapshots, internal aggregate privacy threshold, external projection/purge records tối thiểu, không chứa raw business content và không vào workspace export ngoài owner-owned first-party source.

### AC `AC-004`

Export/data-rights có `tradeproof_export_v1` request/job/revision/manifest deterministic, cutoff/reference closure, spreadsheet-safe CSV convenience entries, STANDARD/OVERSIZE service-class boundary, round-trip validator và `EXPORT_EXPIRY` revocation/delete evidence.

### AC `AC-005`

Workspace deletion local harness có `workspace_deletion_v1` request/FENCE, generation increment, target DAG/drain evidence, cancellation of queued work, export/url revocation, primary local purge/tombstone and re-registration suppression within deterministic records.

### AC `AC-006`

Phase 6 API/UI/migration/tests/verifier/local CI/CHANGELOG/CI workflow được cập nhật; Phase 0-5 checks vẫn xanh, secret/bin-obj guard pass và UI không thêm AI/exchange sync/trading-signal language.

## Non-goals

- AI feature branches vẫn disabled/empty đến Week 7.
- Không gọi storage/network/processor/email thật.
- Không chứng minh full production performance profile; chỉ lưu contract/verifier local.