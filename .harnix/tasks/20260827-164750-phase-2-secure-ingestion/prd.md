# Phase 2 PRD: secure ingestion

## Outcome

Phase 2 tạo luồng upload/import an toàn trong local harness: reserve-before-write RAW_UPLOAD, gateway ghi bytes một lần, transfer thành Upload có purge/validate chain, CSV preview sanitized, ConfirmImport idempotent tạo ImportBatch/IMPORT fence nhưng chưa bật accounting consumer Week 3, và UI import progress/error dùng API thật.

### AC `AC-001`

ObjectIngestReservation RAW_UPLOAD được tạo trước provider write, có write capability single-use, write_expires_at 15 phút, absence_due_at 1 giờ, purpose/generation immutable và atomically enqueue OBJECT_INGEST_FINALIZE TenantControlJob/fence tại RESERVE.

### AC `AC-002`

Upload gateway ghi đúng một immutable provider version bằng conditional create, RECORD_BYTES chỉ một lần, TRANSFER atomically tạo Upload/RECEIVE/UploadObjectLease, đặt forced_purge_at RECEIVE+20h, purge_due_at RECEIVE+24h, và enqueue UPLOAD_VALIDATE plus UPLOAD_PURGE chains.

### AC `AC-003`

CSV UPLOAD_VALIDATE tạo import_preview_v1 sanitized khi UTF-8/RFC4180/header và boundary pass, không tạo ImportBatch/ImportRow/StagedFill/NormalizedFill/episode/ledger/metric; malformed, header mismatch, >20 MiB hoặc >100000 data rows reject trước business writes.

### AC `AC-004`

ConfirmImport yêu cầu preview READY, exact previewSummarySha256 và trusted time trước expires_at; retry exact trả cùng ImportBatch/IMPORT chain, changed retry conflict, key khác trên preview đã confirm trả existing batch và command transaction tạo zero row/fill/business effect.

### AC `AC-005`

Import staging foundation có source-row fingerprint ổn định, immutable staged_fill_v1 candidate/disposition và batch summary/progress/error safe fields cho bốn disposition mà không persist raw CSV/cell/filename/path.

### AC `AC-006`

Import progress/error UI là một workflow dùng API thật trong local mode, hiển thị trạng thái reserve/write/validate/preview/confirm/purge và safe validation errors bằng tiếng Việt, không có exchange API key/private sync/generic CSV mapper screen.

### AC `AC-007`

CHANGELOG.md có entry Phase 2 mới nhất ở đầu file; local CI Phase 2 chạy không cần production secret, không track bin/obj và giữ Phase 0/1 checks xanh.