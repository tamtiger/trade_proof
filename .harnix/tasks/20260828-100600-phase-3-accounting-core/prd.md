# Phase 3 PRD: episode and accounting core

## Outcome

Phase 3 bật IMPORT consumer trong local harness để biến confirmed batch thành row dispositions, NormalizedFill, episode projection, WAC ledger, plan proof và progress/UI có thể kiểm chứng, đồng thời giữ nguyên boundary Phase 2 rằng preview/confirm chưa tạo business row.

### AC `AC-001`

Domain/application contracts có exact version literals Phase 3 (`normalized_fill_v1`, `episode_projection_v1`, `plan_proof_v1`, `fee_conversion_v1`, `wac_episode_v1`) và DTO/records cho ImportRow, NormalizedFill, FeeConversion, TradeEpisodeProjection, EpisodeFillAllocation, AccountingLedgerEntry, plan proof và accounting progress mà vẫn giữ Phase 2 preview/confirm zero-row boundary.

### AC `AC-002`

`IMPORT` consumer local xử lý confirmed ImportBatch idempotent: parse upload bytes, tạo đúng một ImportRow cho mỗi non-blank data row, admit/dedup NormalizedFill bằng canonical signature/dedup key, quarantine row lỗi hoặc long-only violation với safe reason, terminalize IMPORT fence và cập nhật counters sao cho bốn disposition cân bằng `data_rows`.

### AC `AC-003`

Episode state machine và WAC ledger xử lý BUY, BUY thêm, partial SELL và SELL-to-zero; quote/base fee conversion tạo FeeConversion exact, third-asset fee thiếu path làm row `ACCOUNTING_PENDING` và episode `FEE_CONVERSION_MISSING`, không tạo position âm hoặc hai OPEN episode active cho cùng account/symbol.

### AC `AC-004`

Plan-to-first-fill auto proof tạo `VERIFIED`, `AMBIGUOUS` hoặc `UNMATCHED` theo source interval/server timestamp, consume plan idempotent khi auto-associated, freeze revision chỉ cho VERIFIED, và không bao giờ nâng ambiguous/late/unmatched thành verified trong Phase 3 surface.

### AC `AC-005`

API/UI import progress sau confirm có thể chạy worker, hiển thị counters, batch status, safe row errors và episode/accounting summary bằng tiếng Việt, không thêm exchange API key/private sync/live sync/generic mapper hoặc copy khuyến nghị giao dịch.

### AC `AC-006`

Phase 3 migration contract, tests, verifier, local CI và CHANGELOG được cập nhật; Phase 0/1/2 checks vẫn xanh, secret/bin-obj guard không báo lỗi và repo sẵn sàng commit `feat: complete phase 3 accounting core`.
