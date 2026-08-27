# TradeProof Documentation Index

- **Document ID:** `TP-DOCS`
- **Document set version:** 1.0.0
- **Status:** Implementation baseline
- **Updated:** 2026-08-27

## 1. Start here

Đọc theo thứ tự:

1. [Product Brief](../Product_Brief.md) - vấn đề, định vị, fixed MVP scope và validation.
2. [Product Requirements](PRODUCT_REQUIREMENTS.md) - user flows, requirement IDs, UI states và NFR.
3. [Import and Accounting](IMPORT_AND_ACCOUNTING.md) - frozen CSV contract, plan proof, episode, ledger, metrics và fixtures.
4. [Market Context Engine](MARKET_CONTEXT_ENGINE.md) - point-in-time source, formulas, quality, lineage và fixtures.
5. [Weekly Lab](WEEKLY_LAB.md) - cohort/timezone, MetricSnapshot, deterministic report, experiment và product metrics.
6. [Security, Privacy and AI](SECURITY_PRIVACY_AI.md) - tenant/auth, data lifecycle, upload, AI policy và security gates.
7. [Export Contract](EXPORT_CONTRACT.md) - snapshot cutoff, canonical archive, manifest, reference closure và round-trip.
8. [Acceptance Tests](ACCEPTANCE_TESTS.md) - cross-domain scenarios, release profiles và evidence.
9. [Implementation Plan](IMPLEMENTATION_PLAN.md) - architecture boundaries, delivery order, cut rules và Definition of Done.

Không còn product decision mở chặn implementation. Technical vendor ADRs được chọn ở Week 0 nhưng không được thay đổi behavior contract.

## 2. Document authority

Không dùng một thứ tự tổng quát để override mọi loại conflict. Authority theo domain:

| Domain | Normative source | Supporting source |
|---|---|---|
| Product scope/positioning | Product Brief | PRD |
| User behavior/UI states | `TP-PRD` | Product Brief |
| Import, plan proof, episode, finance, metrics | `TP-ACC` | PRD |
| Market context/data quality | `TP-MCE` | PRD |
| Weekly cohort/report/experiment/product analytics | `TP-LAB` | PRD, `TP-ACC`, `TP-MCE` |
| Auth, authorization, privacy, retention, AI | `TP-SEC` | PRD |
| Canonical export archive/round-trip | `TP-EXP` | `TP-SEC` và domain projections |
| Release evidence/pass conditions | `TP-AT` | Domain contract suites |
| Delivery architecture/order | `TP-PLAN` | Các contract trên |

`TP-AT` phải kiểm chứng contract, không được âm thầm định nghĩa behavior khác. Nếu acceptance và domain contract không khớp, đó là documentation defect: dừng affected implementation, sửa contract/test và bump version khi behavior thay đổi.

Security/privacy requirement không được hạ thấp bởi tài liệu domain khác. Product Brief không được dùng để thay công thức chi tiết trong specialized contract.

## 3. Fully-qualified identifiers

Identifier chỉ unique trong document. Khi dùng trong issue, PR, test report hoặc ADR, luôn ghi document prefix:

```text
TP-PRD:PLAN-009
TP-ACC:F17_plan_same_second
TP-MCE:14.1.1
TP-LAB:G09_context_outage_recovery
TP-SEC:TEN-01
TP-EXP:G01_deterministic_archive
TP-AT:E2E-031
```

Không trích `AUTH-01` hoặc `AI-001` mà thiếu document ID.

## 4. Decision register

| ID | Decision | Consequence |
|---|---|---|
| `DEC-001` | MVP venue là Binance Spot | Không multi-venue/proxy volume |
| `DEC-002` | Chỉ symbol quote bằng USDT | Không reporting-currency conversion trong MVP |
| `DEC-003` | Long-only, không margin/borrow/short | SELL vượt quantity bị quarantine; perpetual ở Phase 2 |
| `DEC-004` | File import duy nhất; no exchange API key/sync | Giảm secret surface; adapter phụ thuộc frozen fixture |
| `DEC-005` | `binance_spot_trade_history_csv_v1` là TradeProof-owned contract | Header mismatch fail closed; không heuristic alias |
| `DEC-006` | Web responsive là plan surface | Mobile app/extension ngoài MVP |
| `DEC-007` | Một owner/workspace/trading account | Không member/coach/multi-account UI |
| `DEC-008` | Một armed plan và một open episode/account/symbol | Matching deterministic, không tie-break nhiều plan |
| `DEC-009` | Server timestamp + source precision interval | Proof có verified/ambiguous/late/unmatched; không backdate |
| `DEC-010` | Weighted-average analytical episode accounting | Không tax-lot/wallet accounting |
| `DEC-011` | Third-asset fee dùng fully closed same-venue 1m bar | Missing conversion làm net metric unavailable, không thay bằng 0 |
| `DEC-012` | Context chỉ dùng bar có `barEndExclusive <= event lower bound` | Không look-ahead; snapshot bất biến/versioned |
| `DEC-013` | Session VWAP neo 00:00 UTC | Không user-defined Anchored VWAP trong MVP |
| `DEC-014` | Context `PARTIAL/UNRELIABLE` không aggregate | Accounting metrics có eligibility độc lập |
| `DEC-015` | N < 2 là `INSUFFICIENT`, 2..29 là `EXPLORATORY`, N >= 30 là `ESTIMATED` | Nhãn áp dụng mọi MetricSnapshot; N >=30 vẫn không đồng nghĩa proven edge/causation |
| `DEC-016` | Copy luôn observational, no signal | Cấm causal/predictive/trading recommendation ở template và AI |
| `DEC-017` | Deterministic Weekly Lab thuộc core | AI outage/disable không làm mất workflow |
| `DEC-018` | Voice/AI là extension flags mặc định tắt | Chỉ bật sau conditional security/AI gates |
| `DEC-019` | Screenshot thuộc core nhưng optional cho user | Upload/scan/export/deletion bắt buộc được implement |
| `DEC-020` | UI `Delete TradeProof account` xóa Workspace + local identity | Durable `workspace_deletion_v1`: generation fence, target evidence, restore tombstone và fresh generation khi đăng ký lại |
| `DEC-021` | Mọi raw upload forced purge từ RECEIVE+20h, hard absence deadline +24h | PURGE chỉ terminal sau exact object-version absence verification; breach không fabricate compliance; retained media là sanitized Attachment riêng |
| `DEC-022` | Metric math dùng `metrics_decimal_v1` | Per-episode R và final aggregate scale-18 HALF_EVEN; formula/policy/unit mapping đóng |
| `DEC-023` | Object storage dùng reserve-before-write | Bound single-use capability, OBJECT_INGEST_FINALIZE fence, one immutable version, atomic lease transfer và abort/shell absence TTL ngăn orphan bytes/late commit |
| `DEC-024` | Async tenant work dùng versioned control payload/fence/terminal marker | External operation lookup deterministic; payload schema/digest profile survive compaction; subject có thể xóa nhưng drain sequence vẫn contiguous |
| `DEC-025` | Account deletion không short-circuit empty target | Mọi configured/NONE store chạy post-drain delete/no-op + final verification theo frozen deadline/pipeline matrix |
| `DEC-026` | Product metric có closed output/null/count matrix | PROVISIONAL không publish partial KPI; ratio one-final round18; adoption PRE_PERIOD_ZERO là exception duy nhất |
| `DEC-027` | AI deletion digest là Restricted derived personal data | Payload bị xóa; payload-free lifecycle/digest được disclosure và giữ chỉ tới Workspace deletion |
| `DEC-028` | Cleanup có external result là registered work | Mỗi archive version dùng EXPORT_EXPIRY; DeleteAiOutput dùng AI_OUTPUT_DELETE + encrypted processor-copy handle, nên deletion drain không bỏ sót late result |
| `DEC-029` | External analytics là projection giảm dữ liệu, không phải source | Stored envelope/pseudonym generation pin retry; ANALYTICS_PURGE chứng minh 90-day absence và account deletion xóa riêng từng generation |

Decision thay đổi cần ghi amendment trong file này, cập nhật mọi affected contract, fixture và migration plan. Không chỉnh một dòng scope riêng lẻ mà bỏ qua dependency.

## 5. Eligibility matrix

Không có boolean `eligible` toàn cục.

| Family | Minimum eligibility | Context dependency |
|---|---|---|
| Gross accounting | Active closed projection; gross-ledger invariant pass; quality `COMPLETE` hoặc `FEE_CONVERSION_MISSING` | Không |
| Net accounting/setup/adherence | Closed, accounting `COMPLETE`, net P&L khi metric cần | Không |
| R/expectancy | Net eligible + verified frozen plan + planned risk > 0 | Không |
| Context card | Episode event identity hợp lệ + context snapshot bất kỳ quality | Có để hiển thị |
| Context aggregate | Eligibility của metric nền + ContextSnapshot `COMPLETE`, cùng phase/timeframe/version | Có, bắt buộc |
| North-star | Closed/reconciled/net complete; ít nhất 3 episode trong user-week | Không |
| Weekly Lab section | Eligibility của metric được render | Theo từng section |

Mọi MetricSnapshot lưu policy ID, included IDs, excluded IDs và exclusion reason counts.

## 6. Core versus extension

### Core release gates

- Toàn bộ non-AI requirements trong Product Brief/PRD.
- `TP-ACC`, `TP-MCE`, `TP-LAB`, `TP-EXP`, non-AI `TP-SEC` và core `TP-AT` pass.
- Screenshot upload/scan/export/delete hoạt động.
- Deterministic Weekly Lab hoạt động.

### Conditional extension gates

| Flag | Default | Required before enable |
|---|---|---|
| `voice_transcription_enabled` | `false` | Consent, processor, transcription/fallback/eval gates |
| `ai_taxonomy_enabled` | `false` | Separate consent, structured confirmation, injection/eval gates |
| `ai_weekly_summary_enabled` | `false` | Locked metric payload, grounding, policy validator và eval gates |

Disabled extension không được hiện control hoạt động giả hoặc gửi processor request.

## 7. Implementation readiness checklist

Product/docs baseline được coi là ready khi:

- [x] MVP venue/product/channel/ingestion đã khóa.
- [x] Scope in/out và extension flags không mâu thuẫn.
- [x] Plan proof/timestamp precision có deterministic contract.
- [x] CSV header, parser, idempotency và row disposition có contract.
- [x] Episode/accounting/fee/metric edge cases có golden fixtures.
- [x] Context có no-lookahead, source lineage và quality eligibility.
- [x] Weekly cohort/report/experiment/product metrics có deterministic contract và golden fixtures.
- [x] Export có cutoff, canonical archive, reference closure, manifest và round-trip contract.
- [x] Tenant/auth/security/privacy/export delivery/deletion có durable saga, restore/re-registration semantics, SLA và release gates.
- [x] AI allowlist, consent, grounding, explicit confirmation, eval và fallback có contract.
- [x] Cross-domain acceptance và performance profiles tồn tại.
- [x] Delivery plan nêu team assumption, critical path và cut order.
- [ ] Week 0 technical ADRs được duyệt.
- [ ] Tối thiểu 5 real Binance CSV samples được consent/anonymize và contract-tested.
- [ ] Binance market-data Terms/retention/redistribution review được duyệt trước pilot.

Ba item cuối là kickoff/release-input tasks, không phải product requirement còn mơ hồ. Nếu real sample không khớp adapter v1, bump contract trước khi code parser; không thêm heuristic vào v1.

## 8. Change control

### Patch change

Không đổi behavior/output, ví dụ typo, link hoặc diễn giải rõ hơn. Bump patch document-set version khi ảnh hưởng nhiều file.

### Minor change

Thêm backward-compatible field/metric/flow optional. Yêu cầu:

- affected requirement/test IDs;
- schema/algorithm minor version;
- migration/recompute impact;
- security/privacy review.

### Breaking change

Đổi accepted header, parsing, matching, accounting formula, eligibility, context formula/threshold, retention, ownership hoặc no-signal policy. Yêu cầu:

- decision amendment;
- major contract/algorithm/schema version phù hợp;
- old/new fixture set;
- migration/replay strategy;
- export compatibility;
- explicit rollout/rollback.

## 9. Technical ADR queue

Week 0 phải chọn và ghi ADR cho:

1. Backend/frontend runtime.
2. Managed identity provider.
3. Relational database và tenant enforcement.
4. Queue/worker/idempotency mechanism.
5. Object storage, malware scan và signed URL.
6. Market-data cache/fetch implementation.
7. AI processor hoặc quyết định giữ AI disabled.
8. Deployment region, backup và processor disclosure.
9. Observability/error tracking với redaction.
10. Binance market-data Terms, cache retention và redistribution boundaries.

ADR không được làm yếu contract. Ví dụ chọn database không hỗ trợ row-level security vẫn phải có database/application constraints và isolation tests tương đương.
