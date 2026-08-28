# Adversarial Read-Only Review Prompt

Prompt chuẩn để thực hiện adversarial, read-only review toàn bộ repo `trade_proof`.
Dùng làm input trực tiếp cho agent hoặc Principal Engineer.

---

Bạn là Principal Engineer thực hiện adversarial, read-only review toàn bộ repo
`C:\FPT\MyProject\trade_proof`.

## Mục tiêu

- Xác định chính xác Phase 0-8 đã implement đến đâu.
- Đánh giá mức sẵn sàng cho pilot/production.
- Bằng chứng hợp lệ: code có trong source, migration tồn tại, test assert behavior cụ thể.
- Bằng chứng KHÔNG hợp lệ: tài liệu, test tên đẹp, string assertion,
  trạng thái PASS tự khai báo, comment, `task.json` `completedAt`.

## Quy trình

### Bước 1 — Đọc context theo thứ tự

```
a. .harnix/workflow.md, .harnix/config.yaml
b. .harnix/tasks/*/prd.md và plan.md (Phase 0-8)
c. src/**/*.cs  — domain logic, persistence, API controllers
d. tests/**/*.cs — tìm assertion thực, không chỉ tên test
e. .github/workflows/ — CI config
```

### Bước 2 — Lập inventory

- **Source files** thực sự tồn tại (không chỉ được referenced trong docs).
- **Migrations**: số lượng, schema coverage.
- **Tests**: tổng số, loại (unit / integration / e2e), assertion quality.
- **CI**: stages, secrets, deployment targets.

### Bước 3 — Trace từng acceptance criterion

Với mỗi acceptance criterion trong `prd.md`, trace toàn bộ chain:

```
HTTP handler → auth/authz middleware → domain logic →
DB query → background job (nếu có) → automated test assertion
```

Báo cáo gap ở **bất kỳ node nào** trong chain.

### Bước 4 — Static analysis cho attack surfaces (read-only)

Tìm code paths cho từng attack surface sau.
Trace từ HTTP boundary đến actual guard.
Báo **missing guard**, **bypassable guard**, hoặc **guard chỉ tồn tại trong test doubles**.

| Attack surface | Gì cần tìm |
|---|---|
| Identity spoofing | Auth token validation, claim extraction |
| Cross-tenant access | Tenant filter trong mọi DB query |
| Idempotency | Duplicate request handling |
| Mixed valid/invalid import | Partial failure rollback |
| Malicious uploads | File validation, path traversal check |
| Export round-trip | Data completeness |
| Service-only endpoints | Authorization check hiện diện và enforced |

### Bước 5 — Verifier quality check

Với mỗi test trong passing state, verify tất cả:

- Có assertion xác nhận HTTP status code **hoặc** response body value.
- Có assertion verify DB/state sau call (không chỉ no-throw).
- Không chỉ assert string `"success"` hoặc kiểm tra exception không xảy ra.

Flag là **"shallow test"** nếu không đạt tiêu chí trên.

### Bước 6 — Chạy build và test độc lập

```powershell
dotnet build
dotnet test --no-build --logger "console;verbosity=detailed"
```

Paste raw exit code và failure summary vào báo cáo.

### Bước 7 — Ràng buộc

Không sửa file, không commit, không push, không thay đổi `.harnix` task state.

---

## Severity

| Level | Định nghĩa |
|---|---|
| **P0** | Security boundary bị broken hoặc data loss tại runtime |
| **P1** | Acceptance criterion không có implementation hoặc test |
| **P2** | Implementation present nhưng không đúng contract |
| **P3** | Quality issue, không ảnh hưởng correctness |

---

## Định dạng đầu ra

### 1. FINDINGS

Sắp theo P0 → P3. Mỗi finding:

```
[Pn] <title>
  File: <file>:<line>
  Evidence: <quote hoặc mô tả cụ thể>
  Impact: <hậu quả>
  Fix: <hướng sửa>
```

### 2. TEST MATRIX

| Acceptance criterion | Impl? | Test? | Evidence |
|---|---|---|---|

### 3. CHECKS RUN

Commands đã chạy, exit codes, output tóm tắt.

### 4. MISSING CHECKS

Những gì không chạy được và lý do cụ thể.

### 5. RESIDUAL RISKS

Risks không thể verify statically.

### 6. VERDICT

Chọn đúng một trong ba, kèm lý do ngắn:

| Verdict | Điều kiện |
|---|---|
| **not-ready** | Có P0, **hoặc** ≥ 3 P1 không có fix path rõ ràng |
| **conditional** | Không có P0; P1 còn lại có fix rõ ràng và low-risk |
| **ready** | Không có P0, không có P1 |
