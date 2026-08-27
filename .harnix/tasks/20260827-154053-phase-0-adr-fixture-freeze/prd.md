# Phase 0 PRD: technical ADR and fixture freeze

## Outcome

Phase 0 làm cho repository đủ điều kiện bắt đầu Phase 1 bằng cách chốt các quyết định kỹ thuật, policy dùng Binance market data, scanner/upload boundary, fixture evidence model, và skeleton kiểm thử/CI không cần production secret.

### AC `AC-001`

Repo có bộ ADR Week 0 bao phủ đủ 10 technical ADR bắt buộc trong `TP-PLAN` và mỗi ADR ghi context, decision, alternatives, security/privacy impact, rollback và owner.

### AC `AC-002`

Binance Product Terms/market-data review được ghi bằng nguồn chính thức hiện hành, nêu rõ allowed use, cache/retention/redistribution boundary và follow-up trigger trước pilot.

### AC `AC-003`

Fixture baseline ghi rõ consent/license inventory, ít nhất 5 Binance CSV sample thật đã anonymize hoặc trạng thái blocker không giả mạo khi sample thật chưa có; synthetic fixtures nếu có phải bị phân loại riêng.

### AC `AC-004`

Malware scanner v1 được chốt là self-hosted/stateless/no-egress/no-retained-copy với pinned release evidence file và fail-closed conformance expectation.

### AC `AC-005`

Repo có CI/test/lint/secret-scan skeleton chạy được trong môi trường hiện tại và không cần secret production.

### AC `AC-006`

`CHANGELOG.md` tồn tại và mục Phase 0 mới nhất nằm ở đầu file, ghi nhận thay đổi của task.