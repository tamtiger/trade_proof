# Phase 1 PRD: tenant foundation and Quick Plan

## Outcome

Phase 1 tạo nền tenant/auth và planning đủ chạy local: mỗi identity managed-development bootstrap một Workspace/TradingAccount, mọi command lấy workspace từ server context, shared tenant-work primitive có registered PRODUCT_MEASUREMENT_TIMEOUT, Quick Plan arm/revise/cancel/expire có idempotency và UI responsive dùng API thật.

### AC `AC-001`

API có managed-identity boundary cho development header mode, giữ issuer/subject byte-exact, bootstrap đúng một User/Workspace/TradingAccount/system OTHER preset cho mỗi identity và ghi PRE_AUTH/POST_AUTH audit event an toàn.

### AC `AC-002`

Base schema contract Phase 1 có tenant-owned tables, non-null workspace_id, composite same-workspace key/FK intent, direct one-owner Workspace, idempotency receipt và exact PRE_AUTH/POST_AUTH audit fields.

### AC `AC-003`

Shared tenant-work foundation triển khai TenantControlJob, TenantWorkItemFence, contiguous per-Workspace work_sequence, fence events, optional external-operation lease, terminal marker, payload schema/digest validation, semantic idempotency và deterministic provider lookup cho registered work type đầu tiên.

### AC `AC-004`

product_measurement_run_v1 hỗ trợ START, terminal success/abandon/timeout, exact deadline, QUICK_PLAN practice 1..3 before MEASURED, PRODUCT_MEASUREMENT_TIMEOUT payload và no-lease terminal marker trên shared foundation.

### AC `AC-005`

Setup preset và Quick Plan command hỗ trợ system OTHER, user preset create/revise/archive/reactivate, no persisted DRAFT, ArmPlan idempotent append-only revision, decimal canonicalization, one armed plan per account/symbol, revise/cancel/expire server timestamp và retry không tạo revision trùng.

### AC `AC-006`

Responsive Quick Plan UI là màn hình đầu tiên của app, dùng API thật trong local mode, không có exchange API key screen, hiển thị trạng thái armed/submittedAt/expiry và validation errors bằng tiếng Việt.

### AC `AC-007`

CHANGELOG.md có entry Phase 1 mới nhất ở đầu file và local CI Phase 1 chạy không cần production secret, không commit bin/obj.