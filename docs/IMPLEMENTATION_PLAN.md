# Implementation Plan

- **Document ID:** `TP-PLAN`
- **Version:** 1.0.0
- **Status:** Ready for estimation
- **Updated:** 2026-08-27

## 1. Delivery assumption

Mốc 8 tuần giả định team tối thiểu:

- 2 backend/data engineers;
- 1 frontend engineer;
- 1 product engineer hoặc QA automation dùng chung;
- part-time product/design và security review.

Một engineer làm toàn thời gian nên lập kế hoạch 14-18 tuần. Voice/AI là extension scope; screenshot thuộc core release nhưng core journal không được phụ thuộc vào AI.

## 2. Architecture constraints

Tài liệu không bắt buộc vendor hoặc programming language, nhưng implementation phải có các logical component sau:

| Component | Responsibility |
|---|---|
| Web client | Responsive UI, client validation, progress polling/streaming; không tính financial metric |
| Application API | AuthZ, commands/queries, idempotency, orchestration và audit |
| Relational database | Tenant-owned records, append-only revisions, decimal finance, versioned artifacts |
| Private object storage | CSV quarantine, screenshot/audio, export archive |
| Durable job queue | Import, context fetch, metrics, export, deletion và optional AI jobs |
| Import adapter | Parse frozen Binance CSV contract thành canonical fills |
| Accounting engine | Episode lifecycle, fee conversion, reconciliation và metrics |
| Market-data adapter | Binance public candles, pagination, retry, cache và provenance |
| Context engine | Point-in-time snapshot theo algorithm version |
| Report engine | Deterministic Weekly Lab và optional AI gateway |
| Export engine | Cutoff snapshot, reference closure, canonical archive/manifest và conformance validation |
| Managed identity | OIDC/magic link, session bootstrap và re-authentication |
| Observability stack | Structured redacted logs, metrics, traces, alert và audit storage |

### Mandatory architecture properties

- Tất cả business command nhận `ActorContext` phía server và tự suy ra `workspace_id`.
- Money/quantity/price dùng fixed precision decimal; không dùng binary floating point.
- Background delivery có thể at-least-once; mọi handler phải idempotent.
- Source records, plan/review revisions, snapshots và published reports là append-only.
- Derived artifact luôn mang schema/algorithm version và input digest.
- AI gateway không có database credential, market-data credential hoặc tool access.
- Core flow hoạt động khi AI và market-context dependency tạm thời lỗi.

## 3. Module boundaries

### Identity and Workspace

Owns:

- `User`, `UserIdentity`, `Workspace`, `WorkspaceOwnerProfile`, `SessionMetadata`;
- ownership checks và recent-auth state;
- exact 1:1 bootstrap, identity generation và Workspace guard lifecycle `ACTIVE -> DELETING -> restricted tombstone/row removal` theo `workspace_deletion_v1`.

Không owns TradingAccount data hoặc application role tự mở rộng ngoài MVP.

### Planning

Owns:

- setup taxonomy;
- client-local/ephemeral Quick Plan form; server owns no draft row or DRAFT state;
- immutable armed plan revisions;
- arm/revise/cancel/expire commands;
- timing proof input.

Planning không được cập nhật fill hoặc tự kết luận matching.

### Ingestion

Owns:

- upload/quarantine, immutable `import_preview_v1` header/event/summary hash và expiry;
- `ImportBatch`, source-row fingerprint và adapter version; batch chỉ sinh từ atomic `ConfirmImport`;
- normalized canonical fills;
- quarantine/resolution reasons.

Ingestion không tính P&L hoặc context.

### Accounting

Owns:

- plan-fill matching result;
- episode state machine;
- fee valuation;
- reconciliation state;
- deterministic episode/segment metrics.

Accounting chỉ đọc immutable plan revisions và fills.

### Market Context

Owns:

- source bars/cache/provenance;
- point-in-time computation;
- immutable ContextSnapshot;
- data-quality classification.

Context không thay đổi accounting eligibility ngoài các context-specific aggregate.

### Review and Lab

Owns:

- append-only review revisions theo `TP-ACC`;
- WeeklyCohort/timezone transition theo `TP-LAB`;
- MetricSnapshot reference set và homogeneous dependency tuple;
- deterministic `weekly_lab_v1` report/renderer;
- behavioral experiment revision lifecycle;
- optional AI run/provenance.

### Data Rights

Owns:

- `tradeproof_export_v1` cutoff/reference-closure/archive orchestration theo `TP-EXP`;
- persisted upload/object retention deadlines, absence-verification jobs;
- `workspace_deletion_v1` target/event/outbox/attempt/tombstone orchestration và generation fence cho mọi worker;
- processor deletion status.

## 4. Command and event flow

### Arm plan

```text
Client -> API: ArmPlan(full normalized plan request, idempotencyKey)
API -> Planning: validate invariant
Planning -> DB: atomically create ARMED TradePlan + revision 1 + ARM event + command receipt
Planning -> Audit: PLAN_ARMED
API -> Client: revisionId, submittedAt, expiryAt
```

Một timeout ở client được retry bằng cùng idempotency key; response phải trả cùng revision.

### Import file

```text
Client -> API: request CSV upload(account, adapter)
API -> DB: RESERVE ObjectIngestReservation
API -> DB: in the same RESERVE transaction create OBJECT_INGEST_FINALIZE job/fence/ENQUEUE
API -> Client: issue one bound, single-use write capability
Client -> Storage gateway: conditional-create exactly one immutable object version
Storage gateway -> DB: RECORD_BYTES exact object version/hash/size and consume capability atomically
Finalizer -> DB: TRANSFER reservation -> Upload + RECEIVE/lease/purge + UPLOAD_VALIDATE job/fence/ENQUEUE atomically
Validation worker -> Adapter: stream validation/preflight with zero batch or durable business effect
Validation worker -> DB: valid -> ACCEPT + immutable ImportPreview/CREATE + preview hash + UPLOAD_VALIDATE terminal atomically
Validation worker -> DB: invalid -> REJECT + UPLOAD_VALIDATE terminal atomically, with no ImportPreview
API -> Client: sanitized preview summary, hash, expiresAt
Client -> API: ConfirmImport(previewId, previewHash, idempotencyKey)
API -> DB: CONFIRM + ImportBatch(UPLOADED, copied preview proof) + IMPORT job/fence/ENQUEUE atomically
Worker -> DB: normalize + deduplicate + quarantine
Worker -> Accounting: group/match/reconcile/compute
Worker -> Context queue: eligible entry/exit jobs
Worker -> Metrics queue: eligible accounting jobs
```

Invalid file atomically REJECTs Upload and creates no preview/batch. Preview and confirm are separate commands: preview creates no ImportBatch/ImportRow/fill/episode/ledger/business metric; confirm creates only batch/control work, and retry returns the same effect. ABANDON or exact preview expiry starts the existing UPLOAD_PURGE chain; it does not add an `IMPORT_PREVIEW` work type.

### Publish Weekly Lab

```text
Scheduler -> Lab: create weekly cohort in workspace timezone
Lab -> Accounting/Context: resolve immutable eligible artifact IDs
Lab -> DB: create locked MetricSnapshot
Lab -> Renderer: deterministic report
Lab -> AI gateway: optional grounded summary
Lab -> Policy validator: validate or discard AI output
Lab -> DB: publish report version
```

## 5. Versioning strategy

Các version độc lập:

| Version | Initial value | Bump when |
|---|---|---|
| Import adapter | `binance_spot_trade_history_csv_v1` | Header/parse/normalization behavior đổi |
| Import preview | `import_preview_v1` | Sanitized summary fields/hash, state/event, TTL hoặc confirm binding đổi |
| Staged fill candidate | `staged_fill_v1` | Multiplicity-candidate fields, immutable disposition/fate hoặc resolution closure đổi |
| Canonical fill schema | `normalized_fill_v1` | Field/invariant/semantic đổi |
| Setup/checklist | `setup_preset_v1` + `setup_label_key_v1` + `plan_checklist_v1` | Preset lifecycle, label-key hoặc checklist identity/shape đổi |
| Review taxonomies | `exit_reason_v1` + `breach_type_v1` + `emotion_v1` | Frozen ID/label set hoặc validation rule đổi |
| Episode projection | `episode_projection_v1` | Grouping/replay/boundary rule đổi |
| Plan proof | `plan_proof_v1` | Timing/matching/association rule đổi |
| Fee conversion | `fee_conversion_v1` | Conversion path/as-of/staleness rule đổi |
| Accounting ledger | `wac_episode_v1` | Cost/fee/P&L formula đổi |
| Metric dictionary | `metrics_v1` | Financial/adherence formula hoặc eligibility đổi |
| Metric decimal profile | `metrics_decimal_v1` | Per-episode R boundary, aggregate rounding, numerator/denominator, unit hoặc overflow rule đổi |
| North-star metric | `verified_review_week_rate_v1` | User-week/coverage/completion contract đổi |
| Context algorithm/release registry | `mce-binance-spot-v1.0.0` + `mce-default-v1` | Formula, baseline, threshold, immutable `ContextAlgorithmRelease` allowlist/digest hoặc durable trigger/request contract đổi |
| Weekly report schema | `weekly_lab_v1` | Cohort membership, payload/section/recipe semantic đổi |
| Weekly renderer | `weekly_lab_renderer_v1` | Fixed label, ordering, number format hoặc view-model output đổi |
| Weekly metric envelope | `metric_snapshot_v1` | Typed value, source/exclusion evidence hoặc digest shape đổi |
| Behavioral experiment | `behavioral_experiment_v1` | Taxonomy, proposal/confirmation hoặc target-cohort rule đổi |
| Weekly export projection | `weekly_lab_export_projection_v1` | TP-LAB record membership/reference semantics đổi |
| Product metric dictionary | `product_metrics_v1` | Supporting formula, as-of source mapping, arithmetic hoặc digest rule đổi |
| Product measurement run | `product_measurement_run_v1` | Run header/state, deadline, terminalization hoặc abandonment taxonomy đổi |
| Product analytics | `product_analytics_event_v1` + `product_analytics_external_v1` + `product_analytics_external_suppression_receipt_v1` + `product_analytics_external_deletion_inventory_v1` + `workspace_product_metric_snapshot_v1` + `internal_aggregate_product_metric_snapshot_v1` + `internal_aggregate_cohort_retirement_v1` | Event/envelope/suppression allowlist, pseudonym rotation, delivery/purge/deletion evidence, tenant contribution, cohort retirement hoặc privacy aggregate contract đổi |
| Tenant work control | `tenant_control_job_payload_v1` + `tenant_work_item_terminal_marker_v1` | Work-type subject/payload union, external-operation lease, marker digest/HMAC basis hoặc drain evidence đổi |
| Upload/attachment | `upload_attachment_v1` | Quarantine/scan/object/delete/tombstone state contract đổi |
| Workspace deletion | `workspace_deletion_v1` + `ai_processor_deletion_inventory_v1` | Identity generation, guard, target/inventory set, deadline, evidence, AI-copy partition, restore hoặc re-registration semantic đổi |
| AI consent | `ai_consent_v1` | Feature, disclosure hoặc GRANT/REVOKE replay semantic đổi |
| AI artifact | `ai_artifact_v1` + `transcript_draft_v1` + `taxonomy_suggestion_v1` + `weekly_summary_v1` + `weekly_summary_renderer_v1` | Run lifecycle, config release/hash, structured claim schema, input/output typed-reference schema, input hash/cardinality, delete bundle hoặc feature-output mapping đổi |
| AI prompt/policy | SemVer riêng | Prompt, model input hoặc validator đổi |
| Export schema | `tradeproof_export_v1` | Canonical archive/layout/cutoff/round-trip contract đổi |
| Export request/job | `tradeproof_export_job_v1` | State/event/idempotency/cutoff orchestration đổi |
| Export SLA envelope | `export_sla_envelope_v1` | STANDARD inclusive bound hoặc OVERSIZE behavior đổi |
| Export reader/profile | `tradeproof_export_round_trip_v1` + `export_conformance_profile_v1` | Reader validation hoặc release evidence dataset/environment đổi |

Version phải được lưu trên artifact, không chỉ trong source control. Recompute tạo artifact mới; không rewrite history.

## 6. Database and migration rules

- Primary key là opaque ID không mang business data. Mặc định dùng random ID; riêng `episode_id` dùng deterministic UUIDv5 theo `TP-ACC` để giữ identity qua replay.
- Unique constraint bắt buộc cho idempotency key theo workspace/command type.
- Unique constraint bắt buộc cho normalized-fill `dedup_key` theo trading account; batch/file idempotency và source-row fingerprint dùng scope/version đúng `TP-ACC`.
- Decimal scale phải được định nghĩa theo field; reject overflow thay vì round âm thầm.
- Timestamp lưu UTC cùng source precision/timezone metadata khi liên quan.
- Revision table cấm application update/delete; correction là record mới.
- Foreign key phải ngăn cross-workspace relation bằng composite key hoặc application + database policy tương đương.
- Mọi tenant job persist captured deletion guard generation và recheck ACTIVE/equality ngay trước external call lẫn result commit; deletion worker là ngoại lệ duy nhất với exact deletion ID/generation.
- Migration forward-only trong production; destructive column removal cần ít nhất một compatibility release.
- Migration có backfill phải restartable, bounded và có progress/rollback plan.

## 7. Delivery workstreams

### Week 0: technical ADR and fixture freeze

Deliverables:

- chọn runtime/framework, database, queue, object storage, identity; chọn AI processor hoặc ghi quyết định giữ toàn bộ AI flags disabled;
- threat model/data-flow diagram;
- review Binance Product Terms cho API usage, retention, caching và redistribution; ghi owner/ngày review/allowed use trong ADR;
- thu và ẩn danh tối thiểu 5 Binance export samples;
- kiểm chứng contract `binance_spot_trade_history_csv_v1` đã đóng băng; nếu sample không tương thích, bump adapter contract trước khi viết parser;
- chốt malware scanner v1 là tiến trình self-hosted/stateless trong private validation environment, không network egress, không external scanning API và không retained external copy; pin engine image/signature bundle version cùng hash làm release evidence;
- đóng băng golden fixture expected outputs;
- chọn supported browsers/test device profile;
- dựng CI, secret scan, lint, unit/integration test và ephemeral environment.

Exit gate:

- Không còn technical vendor decision chặn coding.
- Fixture license/consent được ghi nhận.
- Mọi sample khác format đã biết được phân loại version hoặc unsupported.
- ADR scanner chứng minh input chỉ tồn tại tạm thời theo từng object, zero retained copy sau scan transaction và fail closed khi image/signature release không khớp bản pin.

### Week 1: tenant foundation and planning

Deliverables:

- managed auth integration with byte-exact issuer identity, workspace bootstrap và tenant policy;
- base schema, exact PRE_AUTH/POST_AUTH audit events, idempotency middleware;
- trước khi bật bất kỳ registered work type nào, triển khai shared tenant-work foundation: `TenantControlJob`, `TenantWorkItemFence`, contiguous per-Workspace `work_sequence`, `TenantWorkItemFenceEvent`, `TenantExternalOperationLease`, `TenantWorkItemTerminalMarker`, payload-schema/digest-profile validation, semantic idempotency qua live detail lẫn marker, deterministic provider lookup và child-first terminal-detail compaction;
- migration và conformance harness cho foundation phải hoàn tất trước mọi producer/consumer; type ở các tuần sau chỉ đăng ký payload/subject/COMPLETE predicate, activate worker và chạy type-owned crash/retry/FENCE tests, không tạo control primitive riêng;
- `product_measurement_run_v1` header/state lifecycle và synchronous guarded `product_analytics_event_v1` source/client transaction/allowlist, không deferred event-materialization outbox; activate và conformance-test registered `PRODUCT_MEASUREMENT_TIMEOUT` trên shared foundation;
- setup presets, client-local Quick Plan form, atomic arm/revise/cancel/expire server flow with no persisted draft;
- responsive Quick Plan happy path.

Exit gate:

- Tenant isolation suite pass.
- Shared foundation suite chứng minh atomic job/fence/ENQUEUE, contiguous sequence, lease START/END lookup, terminal marker, idempotent replay sau compaction và generation FENCE trước registered-type activation.
- `product_measurement_run_v1` START/terminal race, exact deadline và `PRODUCT_MEASUREMENT_TIMEOUT` retry/FENCE suite pass; retained run không thể ở OPEN vô hạn.
- Plan timestamp/revision suite pass, including no persisted DRAFT and trailing-zero decimal variants producing identical normalized revision/hash bytes while leading-zero/sign/exponent/space/overflow reject.
- Arm retry không tạo revision trùng.

### Week 2: secure ingestion

Deliverables:

- reserve-before-write RAW_UPLOAD ObjectIngestReservation/gateway, atomic `OBJECT_INGEST_FINALIZE` chain at RESERVE, single-use client capability, provider conditional create, RECORD_BYTES, then TRANSFER into Upload + RECEIVE/lease/purge and enqueue its `UPLOAD_VALIDATE` chain before validator preflight;
- exact `import_preview_v1` header/event/hash/TTL, UPLOAD_VALIDATE tagged payload, atomic ConfirmImport-to-IMPORT chain, source-row fingerprint, immutable `staged_fill_v1` candidate/disposition và batch summary;
- activate và conformance-test `OBJECT_INGEST_FINALIZE`, CSV `UPLOAD_VALIDATE` và `UPLOAD_PURGE`; ConfirmImport được phép đăng ký/enqueue `IMPORT`, nhưng IMPORT consumer chỉ activate sau khi Week 3 accounting invariant hoàn tất;
- import progress/error UI;
- malformed/adversarial CSV tests và exact boundary tests tại 20 MiB, 100.000 data rows, 20 MiB + 1 byte, 100.001 rows.

Exit gate:

- Preview có zero batch/row/fill/episode/ledger/business-metric effect; exact retry returns one preview, còn confirm retry returns one batch + IMPORT fence và zero row effect trong command transaction.
- Exact header/encoding/parser/quarantine fixtures pass; invalid file REJECTs Upload with no preview/batch, còn rare confirmed pre-admission rejection có stable source-binding error và zero business rows.
- Multiplicity ACCEPT/MARK crash-retry yields exactly one StagedFillDisposition/fate; terminal ImportRow/batch counters remain immutable.
- File hợp lệ đúng 100.000 data rows và không quá 20 MiB đi qua parser/staging với memory bounded; vượt một trong hai hard limit bị reject trước business writes.
- Reserve/write/transfer crash suite proves no orphan/extra version, abort absence by 1 hour and no false PURGE at the raw deadline.

### Week 3: episode and accounting core

Deliverables:

- episode state machine và one-open-episode invariant;
- plan-fill matching/timing states;
- quote/base fee accounting;
- reconciliation UI và manual resolution audit;
- activate và conformance-test `IMPORT` cùng `ACCOUNTING_REPLAY` trên shared foundation, gồm exact payload, dedup/semantic retry, crash và generation FENCE.

Exit gate:

- 100% supported golden accounting fixtures exact theo decimal contract.
- Unsupported flows không bị auto-reconcile.
- Với mọi fixture qua file-level validation, `RECONCILED + DUPLICATE + ACCOUNTING_PENDING + QUARANTINED = data_rows` không rỗng.
- Re-import, partial-overlap và worker retry không đổi fill count, episode identity hoặc ledger totals.

### Week 4: fee conversion and context source

Deliverables:

- third-asset fee conversion/provenance;
- Binance candle adapter, pagination/cache/rate-limit handling;
- immutable `ContextAlgorithmRelease` registry: row existence là exact v1 allowlist cho `(algorithmVersion,parameterSetId)` và release digest, không có hidden enabled/current state; durable `ContextEpisodeTrigger`/`ManualContextRecomputeRequest` làm typed trigger identity, với authoritative `sourceEventSequence = 1` cho ENTRY và `N = count(EpisodeFillAllocation)` cho EXIT;
- entry/exit snapshot jobs with exact TenantControlJob slot/recompute trigger payload và immutable input digest; activate và conformance-test registered `CONTEXT` cho mọi ID/digest branch;
- internal global tenant-free public market-data service thực hiện Binance public GET/cache mà không tạo `TenantExternalOperationLease`; CONTEXT tenant-result commit vẫn phải resolve fence, lock Workspace và recheck ACTIVE/current generation;
- no-lookahead suite.

Exit gate:

- Fee unavailable state suppress net metrics đúng.
- Future bars không thay đổi published snapshot.
- MCE control golden reject wrong release/trigger/request hash, cross-workspace reference, ENTRY khác 1, EXIT khác N và mọi client/timestamp-derived sequence; tenant-free source result không thể bypass fenced ContextSnapshot commit.

### Week 5: review, metrics and dashboard

Deliverables:

- episode detail/accounting breakdown;
- Quick Review revisions và attachment pipeline; `attachments_enabled=true` là bắt buộc trong core release, flag chỉ là operational kill switch khi có incident;
- activate và conformance-test media branches của `UPLOAD_VALIDATE` cùng `ATTACHMENT_DELETE`; SANITIZED_ATTACHMENT dùng reservation/finalizer riêng bound vào source Upload và exact SCREENSHOT validation hoặc keep-original TranscriptConfirmation intent;
- implement prepare-then-transfer media saga: trusted sanitizer writes/RECORD_BYTES through a server-only capability, SCREENSHOT ACCEPT or final TranscriptConfirmation transaction alone consumes the exact BYTES_PRESENT preallocated reservation, and every failure/expiry/FENCE follows abort delete/absence verification without partial Attachment/revision;
- metric engine/intervals/sample guardrail;
- dashboard data quality/drill-down.

Exit gate:

- Metric golden fixtures pass.
- Mọi exclusion trace được tới episode/reason.
- Media crash/race suite covers late extra object version, 15-minute staging and raw forced-purge equality; object-store write is never assumed atomic with Upload ACCEPT or TranscriptConfirmation, exact retry yields one Attachment or a stable failed intent, and every abandoned/extra version is absence-verified by the one-hour deadline.
- Core screens pass accessibility smoke suite.

### Week 6: Weekly Lab and data rights

Deliverables:

- weekly cohort/timezone handling;
- deterministic report, homogeneous dependency tuple và behavioral experiment theo `TP-LAB`;
- first-party product analytics events, WorkspaceProductMetricSnapshot closed output/null/count matrix, privacy-threshold internal aggregates, external projection 90-day purge and per-generation deletion inventory;
- cutoff-consistent `tradeproof_export_v1` archive, manifest và conformance reader theo `TP-EXP`;
- activate và conformance-test `COHORT_LOCK`, `REPORT`, `PRODUCT_METRIC`, `ANALYTICS_DELIVERY`, `ANALYTICS_PURGE`, `EXPORT` và `EXPORT_EXPIRY` trên shared Week-1 foundation;
- chỉ triển khai phần deletion-specific tại Week 6: `workspace_deletion_v1` target DAG, FENCE/drain tiêu thụ existing terminal markers theo watermark, drain-bound outbox, exact target action pipelines/control TTL và restore tombstone generation-chain procedure; không định nghĩa lại job/fence/lease/marker/idempotency hoặc compaction foundation.

Exit gate:

- Week boundary/DST fixtures pass.
- Toàn bộ `TP-LAB` golden fixtures pass, gồm timezone transition, report revision và renderer.
- `export_conformance_profile_v1`, exact STANDARD/OVERSIZE `export_sla_envelope_v1` boundaries, round-trip/corruption/reference-closure và deletion release gates pass trong test environment.
- Crash/duplicate-delivery suite proves every `workspace_deletion_v1` deadline/pipeline ordinal, deterministic external-operation lookup, consumption of already-versioned markers after detail compaction, EXPORT_EXPIRY drain, post-drain target verification, restore suppression and same-subject generation-chain behavior.

### Week 7: optional AI and hardening

Deliverables:

- consent UI, AI gateway, feature-specific `AiRunInputReference` provenance, non-exported encrypted AiProcessorCopyReference/delete lookup, typed output closure và schema validator;
- transcription under `voice_ingest_profile_v1`, taxonomy/summary theo feature flags, exact Transcript/Taxonomy confirmation records, active-output-or-payload-free-subject/Tombstone closure và derived-digest disclosure;
- activate và conformance-test `AI_RUN`, `AI_CANCEL` và `AI_OUTPUT_DELETE` trên shared foundation;
- deterministic/manual fallback;
- performance, failure recovery, security và accessibility regression.

Exit gate:

- `TP-SEC:AI-01` tới current final AI gate (hiện `TP-SEC:AI-11`) áp dụng cho feature và AI eval critical gates pass trước khi bật extension; core release được phép giữ toàn bộ AI flags disabled.
- AI crash/retry/FENCE suite pass cho `AI_RUN`, `AI_CANCEL`, `AI_OUTPUT_DELETE`, gồm terminal-marker compaction và Workspace deletion drain integration.
- Import/accounting/report vẫn pass khi AI dependency bị chặn.

### Week 8: pilot release

Deliverables:

- production readiness review;
- alert dashboard và on-call ownership;
- incident, backup/restore, deletion và processor runbook exercise;
- pilot onboarding, known limitations và support script không yêu cầu workspace access.

Exit gate:

- Toàn bộ non-waivable acceptance gate pass.
- Không còn P0/P1 defect.
- Data processor contracts/disclosures sẵn sàng.

## 8. Test strategy

| Layer | Focus |
|---|---|
| Pure unit | Decimal formulas, state transitions, timestamp interval logic, percentiles, policy rules |
| Property-based | Idempotency, fill ordering, quantity conservation, no negative inventory, serialization round-trip |
| Adapter contract | CSV headers/encoding/locale/malformed rows và Binance samples |
| Golden fixtures | Accounting, context, Weekly Lab/renderer và export với exact expected artifacts |
| Integration | Database constraints, queue retry, object storage, market API cassette, auth scope |
| End-to-end | Onboarding -> plan -> import -> review -> Weekly Lab -> export/delete |
| Security | Tenant matrix, upload abuse, log redaction, CSRF/session, signed URL, AI isolation |
| Performance | 500/10.000/100.000-row profiles, dashboard và concurrent workers |
| Accessibility | Automated scan cộng keyboard/screen-reader smoke test |

External API tests phải dùng recorded/synthetic cassette trong CI; một scheduled canary kiểm tra live source contract nhưng không làm unit suite flaky.

## 9. Observability and alerts

Required service metrics:

- request rate/error/latency theo endpoint class;
- queue depth, oldest-job age, retry và dead-letter count;
- import row outcomes theo adapter version;
- reconciliation/exclusion rate theo reason;
- market-data freshness/coverage/rate-limit;
- context and metric job duration/failure;
- cross-tenant authorization denials;
- export/deletion/retention SLA age;
- AI request/validation/fallback theo version, không có user content;
- backup age và restore exercise result.

Mọi alert có owner, severity, threshold, runbook link và expected response time.

## 10. Feature flags and cut order

Flags bắt buộc:

- `attachments_enabled` (default `true`; core release gate fail nếu `false`, trừ incident containment tạm thời);
- `voice_transcription_enabled`;
- `ai_taxonomy_enabled`;
- `ai_weekly_summary_enabled`;
- `paid_pilot_enabled`.

Nếu timeline trượt, cắt theo thứ tự:

1. voice transcription;
2. AI taxonomy;
3. AI Weekly Summary;
4. pricing/paywall automation, thay bằng manual pilot enrollment.

Không được cắt tenant isolation, audit, import idempotency, accounting correctness, no-lookahead, deterministic Weekly Lab, export hoặc deletion.

## 11. Pull request definition of done

Mỗi change phải:

- ghi fully-qualified requirement/test IDs, ví dụ `TP-PRD:PLAN-009` hoặc `TP-SEC:AUTH-03`;
- có test đúng mức rủi ro;
- không làm thay đổi contract mà không bump version;
- có migration/backfill plan nếu đổi schema;
- giữ logs/telemetry không chứa user content;
- cập nhật failure/retry behavior;
- pass lint, type/static checks, unit/integration tests và relevant security suite;
- có screenshot/visual verification cho UI thay đổi trên mobile và desktop;
- không auto-commit, push hoặc deploy từ workflow tài liệu này.

## 12. Technical ADRs required in Week 0

Các ADR này chọn implementation mechanism, không được thay đổi product contract:

1. Runtime/backend framework và frontend framework.
2. Managed OIDC/magic-link provider.
3. Relational database hosting và tenant enforcement mechanism.
4. Queue/worker và idempotency storage.
5. Private object storage, signed URL/gateway mechanism và malware scanner v1: self-hosted, stateless, no network egress, no external scanning API, ephemeral per-object input, no retained external copy; pin engine image/signature bundle versions và hashes.
6. Market-data cache/pagination implementation.
7. AI processor hoặc quyết định giữ AI disabled cho pilot.
8. Deployment region, backup, RPO/RTO và processor disclosure.
9. Observability/error-tracking stack với redaction.
10. Binance market-data Terms, cache retention và redistribution boundaries.

ADR phải ghi context, decision, alternatives, security/privacy impact, rollback và owner.
