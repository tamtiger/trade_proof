# PRD - Phase 5: review, metrics and dashboard

## Outcome

Hoàn tất Week 5 trong local harness: người dùng review episode đã đóng, đính kèm screenshot đã sanitize, hệ thống publish metric snapshot deterministic và dashboard/API/UI có đủ drill-down để xem chất lượng dữ liệu.

## Scope

In scope có Review/ReviewRevision append-only, public review taxonomy v1, screenshot attachment lifecycle tối thiểu, METRICS work type, metric snapshot envelope tối thiểu, dashboard episode detail và UI smoke. Out of scope là Weekly Lab report/cohort/export/deletion/product analytics/AI/voice branch.

### AC `AC-001`
Review contract có exact taxonomy versions `exit_reason_v1`, `breach_type_v1`, `emotion_v1`, immutable taxonomy item/order/content hashes; `CompleteEpisodeReview` và `ReviseEpisodeReview` chỉ nhận active CLOSED exact projection, expected projection/revision, full replacement payload, idempotency conflict-safe, append-only revision, first `completed_at` preserved, stale projection/revision rejected, và dashboard/current metric chọn latest revision matching active projection.

### AC `AC-002`
Screenshot attachment core saga dùng reservation/finalizer preallocated attachment ID, supported image validation, sanitized ACTIVE/PASSED Attachment, immutable ReviewRevisionAttachment join/hash, `ATTACHMENT_DELETE` state transition giữ historical join và tombstone/absence evidence; invalid/stale/cross-workspace/not-ready attachment không tạo partial Review hoặc orphan.

### AC `AC-003`
Metric engine có registered `METRICS` work type và immutable `metric_snapshot_v1` records cho accounting/review/adherence/context coverage tối thiểu; mỗi snapshot lưu candidate/included/excluded episode refs, closed reason counts, source review/context IDs, `metrics_v1`, `metrics_decimal_v1`, `INSUFFICIENT|EXPLORATORY|ESTIMATED` sample label, và không dùng binary floating point hoặc directional/edge claim.

### AC `AC-004`
API/UI/dashboard hiển thị episode detail/accounting breakdown, fee conversion provenance, review completion/revision state, screenshot attach/delete controls, metric quality/exclusion banner và drill-down episode list; UI không thêm AI, exchange key/private sync/live sync/generic browser hoặc trading-signal language.

### AC `AC-005`
Phase 5 migration/tests/verifier/local CI/CHANGELOG/CI workflow được cập nhật; Phase 0/1/2/3/4 checks vẫn xanh, secret/bin-obj guard không báo lỗi, và core accessibility smoke static checks pass.