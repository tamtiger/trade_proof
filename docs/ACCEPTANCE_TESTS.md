# Acceptance Tests and Release Gates

- **Document ID:** `TP-AT`
- **Version:** 1.0.0
- **Status:** Implementation baseline
- **Updated:** 2026-08-27

## 1. Purpose

Tài liệu này định nghĩa bằng chứng cần có trước core release và trước khi bật từng extension. Nó không định nghĩa lại công thức. Expected value của accounting lấy từ `TP-ACC`, context từ `TP-MCE`, Weekly Lab từ `TP-LAB`, archive/round-trip export từ `TP-EXP`, và security/privacy/AI gates từ `TP-SEC`.

Fully-qualified test ID có dạng `TP-AT:E2E-001`. Domain fixture giữ ID gốc nhưng khi trích dẫn phải kèm document ID, ví dụ `TP-ACC:F01_quote_fee_round_trip`.

## 2. Release profiles

### 2.1. Core release

Core release bắt buộc có:

- authentication, workspace isolation và audit;
- responsive Quick Plan;
- frozen CSV adapter, import/reconciliation và accounting;
- Market Context Engine;
- Quick Review và screenshot attachment;
- deterministic metrics, dashboard và Weekly Lab;
- export, retention và Delete TradeProof account.

Core release giữ các flag sau `false` và không cần AI processor:

- `voice_transcription_enabled`;
- `ai_taxonomy_enabled`;
- `ai_weekly_summary_enabled`.

### 2.2. Extension release

Mỗi extension chỉ được bật khi:

1. core release gates vẫn pass;
2. feature-specific consent tồn tại;
3. mọi `TP-SEC:AI-*` gate liên quan pass với model/prompt/policy version sẽ deploy;
4. deterministic/manual fallback đã được kiểm thử;
5. processor contract và disclosure đã được duyệt.

## 3. Test environments

### 3.1. Deterministic test clock

- Unit/integration/golden tests dùng injected UTC clock.
- Locale process được chạy ít nhất với `vi-VN` và `en-US`; output nghiệp vụ phải giống nhau.
- Timezone fixtures gồm `Asia/Ho_Chi_Minh`, `UTC` và `America/New_York` để bao phủ DST.
- Randomized/property tests phải ghi seed; failure có thể replay.

### 3.2. Browser profile

- Hai major version mới nhất của Chrome, Edge, Firefox và Safari tại release date.
- Mobile viewport: 360 x 640 và 390 x 844.
- Desktop viewport: 1366 x 768 và 1920 x 1080.
- Keyboard-only test ở desktop; screen-reader smoke test trên ít nhất một desktop và một mobile platform.

### 3.3. Performance profile

Kết quả performance chỉ hợp lệ khi evidence ghi:

- commit/build ID;
- environment topology và resource limits;
- database size/index state;
- worker concurrency;
- network latency profile;
- dataset/fixture version;
- warm/cold-cache state;
- ít nhất 30 samples cho synchronous latency và 20 runs cho async duration.

Client usability benchmark dùng thiết bị tương đương smartphone 4 năm tuổi, network 20 Mbps down/5 Mbps up và RTT 50-100 ms. Backend benchmark dùng pilot-equivalent environment được khóa bằng ADR Week 0.

## 4. Mandatory domain suites

### 4.1. Import and accounting

Tất cả fixture `TP-ACC:F01` tới current final fixture (hiện là `TP-ACC:F39`) và implementation gates trong `TP-ACC` phải pass. Release tooling phải resolve/assert exact final ID từ versioned docs; không được hard-code một range cũ.

Additional properties:

- `TP-AT:ACC-PROP-001`: Với mọi sequence long-only hợp lệ, quantity và cost basis không âm; khi close cả hai bằng exact decimal zero.
- `TP-AT:ACC-PROP-002`: Hoán đổi file upload order không đổi active projection nếu event-time order và resolutions giống nhau.
- `TP-AT:ACC-PROP-003`: Retry UPLOAD_VALIDATE, ConfirmImport, IMPORT/accounting hoặc staged-fill resolution 1-10 lần không đổi preview/batch/fill count, episode IDs, ledger totals hoặc candidate fate; changed idempotent payload fail closed.
- `TP-AT:ACC-PROP-004`: Với confirmed batch đã qua preview/file-level envelope/header validation, mọi non-blank data row có đúng một disposition và tổng disposition bằng denominator; invalid file có Upload REJECT nhưng zero preview/batch/business row, còn confirmed pre-admission `REJECTED` có stable source-binding/revalidation error và zero business rows.
- `TP-AT:ACC-PROP-005`: Không binary floating point xuất hiện trong persisted source/ledger/metric money path.
- `TP-AT:ACC-PROP-006`: Mọi DECIMAL/INTERVAL metric replay theo `metrics_decimal_v1`; formula/policy/unit/numerator/denominator/null mapping sai hoặc unrounded-R reuse bị reject trước snapshot/report publication.

### 4.2. Market Context

Tất cả 44 golden assertions hiện hành trong `TP-MCE` và Definition of Done của owner doc phải pass; release tooling phải fail nếu declared current count và executed count khác nhau.

Additional properties:

- `TP-AT:MCE-PROP-001`: Với cùng event, selected bar revisions và parameter set, 100 executions song song tạo một logical snapshot.
- `TP-AT:MCE-PROP-002`: Mọi input bar có `barEndExclusive <= asOfAt`; database query test phải chứng minh upper bound.
- `TP-AT:MCE-PROP-003`: Không snapshot nào dùng venue, product hoặc symbol proxy.
- `TP-AT:MCE-PROP-004`: `PARTIAL` và `UNRELIABLE` không xuất hiện trong context aggregate.
- `TP-AT:MCE-PROP-005`: Replay all six exact CONTEXT branches and reason/type pairs `INITIAL_EVENT/EPISODE_EVENT`, `SOURCE_GAP_FILLED/INGESTION_BATCH`, `SOURCE_REVISION_RESOLVED/MARKET_BAR_RESOLUTION`, `MANUAL_RETRY/MANUAL_REQUEST`, `EPISODE_PROJECTION_REPLAYED/EPISODE_PROJECTION`, `ALGORITHM_UPGRADE/ALGORITHM_RELEASE`. INITIAL_EVENT uses the durable server RFC 9562 UUID `ContextEpisodeTrigger`, ENTRY `sourceEventSequence = 1`/first allocation-fill and EXIT `sourceEventSequence = N = count(EpisodeFillAllocation)`/closing allocation-fill; gap/resolution use exact ingestion-batch/resolution IDs; MANUAL_RETRY uses one same-workspace `ManualContextRecomputeRequest` receipt, with same idempotency key/hash returning the same request/job and changed bytes conflicting. These four persist only `triggerId`; projection replay persists only canonical projection-record-key SHA-256 and algorithm upgrade only exact immutable `ContextAlgorithmRelease.releaseSha256` in `triggerSha256`. Mutate every trigger/release/request field/hash, phase/timeframe/sequence, reason/type/member and stale/cross-workspace projection; each invalid case fails before work-sequence allocation. Independently recompute `provenanceHash` from the exact closed RFC 8785 object: ingestion-batch items contain only `completedAt,fetcherVersion,ingestionBatchId,productType,sourceVenue,startedAt,status`; input-source items only `ingestionBatchId,marketBarResolutionId,marketBarRevisionId,sourceObservationId,sourceRequestId`; request items only `fetchedAt,requestMetadataHash,responseSha256,sourceRequestId`, and only `marketBarResolutionId` is nullable. `inputBarSources` is one-per-selected-bar in `(timeframe,openAt,revisionId)` order, while deduplicated `sourceRequests` and `ingestionBatches` sort by exact UTF-8 ID bytes; duplicate, unknown/missing member, wrong null/order or unreachable reference rejects.

### 4.3. Weekly Lab

- Tất cả `TP-LAB:G01` tới current final fixture (hiện là `TP-LAB:G36`) và Definition of Done trong `TP-LAB` phải pass; release tooling phải fail nếu bỏ qua fixture owner doc mới hơn.
- Test phải freeze clock, timezone/TZDB, dependency tuple và source revisions; renderer golden bytes/hash không phụ thuộc locale, database ID allocation hoặc AI.
- Mixed core version phải fail publication; context outage phải publish các section độc lập rồi tạo immutable revision mới khi recovery.
- `TP-LAB:G23_product_metrics_replay` là bắt buộc cho `product_measurement_run_v1`: mọi feature/mode phải chứng minh synchronous authenticated start atomically tạo header+START+registered PRODUCT_MEASUREMENT_TIMEOUT chain hoặc không tạo gì; byte-identical start retry trả cùng run, changed payload/same semantic run với key khác bị reject; practice contiguous/unique, QUICK_PLAN đúng 1..3 trước MEASURED, ONBOARDING không có practice và không run nào còn OPEN vĩnh viễn. Clock vectors deadline `-1ms`/equality/`+1ms`, từng reason `USER_CANCELLED | NEGATIVE_DURATION | ZERO_DURATION | BACKGROUND_INTERRUPTED | MISSING_TERMINAL_EVENT | DURATION_OVER_30_MINUTES | TIMEOUT` và success/timeout/FENCE races phải cho đúng một terminal success hoặc abandonment, equality thuộc TIMEOUT và FENCE-first chỉ cancellation/no late event. FINAL metric phải chặn khi applicable timeout control chưa terminal.
- Trong cùng G23, timeout chain phải có subject `ProductMeasurementRun`/`{ "measurement_run_id": id }`, exact payload `{ "deadlineAt": ts, "feature": "ONBOARDING"|"QUICK_PLAN"|"QUICK_REVIEW"|"FIRST_INSIGHT", "measurementRunSchemaVersion": "product_measurement_run_v1", "operation": "TERMINALIZE_AT_DEADLINE" }`, operation key `measurement-run:<measurement_run_id>:timeout`, COMPLETE code `MEASUREMENT_RUN_SUCCEEDED | MEASUREMENT_RUN_ABANDONED`, hoặc deletion code `WORKSPACE_DELETING`. Mutation của run tuple/state/deadline, USER initiator, start/timeout idempotency key, subject/payload, safe-result code hoặc terminal marker phải fail closed.
- External analytics phải pass G36 với pinned minimized envelope, processor-specific rotation, deterministic delivery lookup, registered ANALYTICS_PURGE and per-generation account-deletion evidence; external rows/control never enter workspace export.

### 4.4. Export

- Core release phải pass `TP-EXP:G22_ai_absent`, toàn bộ non-AI numbered fixtures và mọi Definition-of-Done clause áp dụng cho archive không có AI. Sáu AI-present fixtures `TP-EXP:G23_ai_present_deleted`, `G44_ai_bundle_delete_cutoff`, `G53_ai_typed_provenance`, `G55_ai_hash_basis_evolution`, `G82_ai_confirmation_closure`, `G88_ai_output_subject_lifecycle` là conditional; G82 chỉ áp dụng khi bật TRANSCRIPTION hoặc TAXONOMY_SUGGESTION, còn G88 áp dụng cho mọi enabled feature. Không fixture AI-present nào chặn core khi cả ba AI flags false.
- Extension release phải pass toàn bộ `TP-EXP:G01` tới current final fixture declared by TP-EXP (hiện là `TP-EXP:G90`), gồm mọi AI-present fixture áp dụng cho feature được bật; release tooling phải assert declared final ID/count và không được dùng AI-absent fixture để thay thế typed provenance/delete/hash-evolution/confirmation/subject-lifecycle coverage.
- Release evidence phải gồm archive validator/round-trip report, corruption/reference-closure tests và `export_conformance_profile_v1` SLA run.
- Boundary suite phải classify đúng exact-limit `STANDARD` và từng limit + 1 `OVERSIZE`; chỉ STANDARD mang deadline 24 giờ, OVERSIZE vẫn lossless và phát status/notification theo `export_sla_envelope_v1`.

### 4.5. Security, privacy and AI

- Core release phải pass tất cả non-AI, non-conditional gates trong bảng acceptance của `TP-SEC`, gồm screenshot `UPL-03` nhưng không gồm audio `UPL-04` khi voice extension tắt.
- Core với cả ba AI flags tắt phải pass exact disabled-profile gate `TP-SEC:AI-00` và test mọi screen/navigation/direct route, đặc biệt Settings, không có AI label/control/UI/callable endpoint, active processor credential/control enqueue hoặc outbound request; `AI-01` không phải core-disabled gate. Pairwise flag fixtures còn phải chứng minh mỗi consent control/route chỉ xuất hiện và callable cho đúng enabled feature, không làm lộ hai feature còn tắt.
- Khi bật extension, phải pass toàn bộ gate áp dụng từ `TP-SEC:AI-01` tới current final AI gate (hiện là `TP-SEC:AI-11`) cho từng enabled feature/model/prompt/policy; `AI-00` không thay thế bất kỳ enabled-feature gate nào. Voice extension còn phải pass `TP-SEC:UPL-04` và `UPL-06`.
- Không được waive tenant isolation, authentication, deletion hoặc critical AI safety gate.

## 5. Cross-domain scenarios

### Onboarding and ownership

#### `TP-AT:E2E-001` - First sign-in

Given một valid identity `(issuer, subject)` chưa tồn tại  
When sign-in hoàn tất và user xác nhận timezone  
Then hệ thống tạo đúng một UserIdentity, một User, một Workspace có direct `owner_user_id` bằng User đó và một `BINANCE/SPOT/USDT` TradingAccount; `Workspace.owner_user_id` unique và không có membership row.

Retry callback hoặc onboarding command không tạo aggregate thứ hai.

#### `TP-AT:E2E-002` - Cross-workspace matrix

Given User A và User B có dữ liệu ở mọi entity/object type  
When A thay ID/path/query/body thành ID của B qua API, search, pagination, job, signed URL và export  
Then response không đọc, ghi, đếm hoặc tiết lộ sự tồn tại dữ liệu B; audit/alert phù hợp được tạo.

#### `TP-AT:E2E-003` - Setup preset lifecycle

Given workspace mới có đúng một immutable system preset `OTHER` và hai user labels khác nhau chỉ bởi Unicode case-fold  
When owner create/revise/archive/reactivate preset, sửa ordered checklist, retry command và arm plan trước một rename sau đó  
Then active label key unique theo `setup_label_key_v1`, retry không tạo revision/event trùng, stale base bị reject, checklist item/order theo `plan_checklist_v1`, `OTHER` không thể sửa/archive, và armed plan giữ exact setup revision/label/checklist cũ qua rename/archive.

### Plan proof

#### `TP-AT:E2E-010` - Verified plan

Given plan được arm bằng trusted server time trước lower bound của opening fill  
When import/reconciliation hoàn tất  
Then episode proof là `VERIFIED`, frozen revision là latest eligible revision và R dùng planned risk từ revision đó.

Revision sau fill không đổi proof, risk hoặc historical metric.

#### `TP-AT:E2E-011` - Timestamp ambiguity

Given source fill chỉ có precision giây và plan timestamp nằm trong interval một giây đó  
When episode được reconcile  
Then proof là `AMBIGUOUS`, không phải `VERIFIED`; episode không bao giờ đi vào planned/R denominator dưới contract v1, và manual action không được nâng nó thành verified.

#### `TP-AT:E2E-012` - Late or missing plan

Given không có eligible plan trước opening fill  
When user thêm association/note sau fill, retry exact command, gửi changed-payload/stale-base variants, hoặc không thêm gì  
Then exact retry trả cùng association/result projection, changed/stale variants có zero effect; proof lần lượt là `LATE` hoặc `UNMATCHED`, và cả hai có `is_planned=false` cùng `r_multiple=null`.

#### `TP-AT:E2E-013` - Arm creation and idempotency

Given user edits/abandons client-local form without server persistence, then submits one full normalized `ArmPlan` request and client times out  
When client retry 10 lần với cùng idempotency key, rồi gửi cùng key với một field khác  
Then before submit có zero TradePlan row; valid submit atomically tạo đúng một ARMED header, revision 1, ARM event và receipt; exact retries trả cùng IDs/timestamp, changed payload fail conflict, và không có DRAFT state hay cancel-from-draft path.

Arm requests whose only lexical difference is insignificant fractional trailing zeros normalize to the same persisted decimals/request hash/idempotent effect. Leading zero, sign, exponent, whitespace or overflow variants reject before any plan row/receipt.

#### `TP-AT:E2E-014` - Ambiguous association resolution

Given một single-candidate timestamp ambiguity và một multi-candidate ambiguity  
When owner confirm/remove association, hoặc chọn candidate từ frozen candidate set, rồi retry cùng idempotency key  
Then mỗi command tạo tối đa một audited resolution/projection; status vẫn `AMBIGUOUS`, frozen revision/planned risk/R vẫn null, stale version hoặc candidate ngoài frozen set bị reject, và remove không đảo ngược historical `CONSUME` event.

### Import and reconciliation

#### `TP-AT:E2E-020` - Preview has no business effect

Given file 500 rows hợp lệ  
When UPLOAD_VALIDATE bị retry/crash quanh ACCEPT rồi user preview nhưng không confirm  
Then có đúng một Upload ACCEPT + immutable `import_preview_v1`/CREATE với exact sanitized counters/hash/expiry, nhưng không có ImportBatch, ImportRow, StagedFill, NormalizedFill, TradeEpisode, ledger, ContextSnapshot hoặc accounting/weekly MetricSnapshot.

At exact expiry hoặc explicit ABANDON, ConfirmImport fail với zero effect và existing UPLOAD_PURGE bắt đầu; retry không gia hạn TTL. Invalid header/UTF-8/limit branch tạo Upload REJECT, zero preview/batch/business row.

#### `TP-AT:E2E-021` - Partial file accounting

Given file có valid, duplicate và invalid rows  
When ConfirmImport được retry/concurrent bằng same key/hash rồi IMPORT chạy  
Then CONFIRM + một ImportBatch + exact IMPORT job/fence/ENQUEUE commit atomically trước mọi row write; valid rows commit, duplicate không double-count, invalid rows quarantine; `RECONCILED + DUPLICATE + ACCOUNTING_PENDING + QUARANTINED = data_rows` không rỗng theo canonical disposition contract.

Batch copy exact preview ID/schema/hash/confirmed time; sau khi temporary preview/event cleanup, copied proof vẫn khớp IMPORT payload/evidence mà không có dangling FK. Không row nào biến mất khỏi summary/error report. Với multiplicity ambiguous, candidate chỉ tồn tại trong immutable StagedFill; ACCEPT_AS_NEW atomically tạo đúng một immutable NormalizedFill/dedup key, MARK_DUPLICATE tạo zero candidate fill và pin target trong disposition, retry/crash không để dangling/hai fate, và terminal ImportRow/batch counters không bị viết lại.

#### `TP-AT:E2E-022` - Replay conflict

Given backfill cũ có thể đổi boundary của episode đã có completed Review  
When batch xử lý  
Then active projection/report cũ không đổi; conflict chờ audited confirmation và không publish mixed version.

#### `TP-AT:E2E-023` - Missing third-asset fee conversion

Given episode có third-asset fee nhưng không có eligible same-venue closed bar  
When accounting chạy  
Then gross P&L có thể tồn tại, net P&L/R/net metrics null, accounting status nêu conversion missing và excluded count tăng đúng một.

Thêm bar hợp lệ và replay tạo version mới; ledger cũ không bị mutate.

#### `TP-AT:E2E-024` - Screenshot attachment lifecycle

Given một malformed screenshot và một JPEG hợp lệ có metadata  
When upload/scan/sanitize chạy với retry, Review attach JPEG rồi owner delete item  
Then malformed upload bị REJECTED và purge không tạo Attachment; JPEG chỉ ACTIVE sau decode/re-encode/scan PASSED, có immutable content version/hash và một Review join. Delete chuyển `ACTIVE -> DELETING -> DELETED`, revoke URL/export pin, xóa bytes trong SLA, giữ historical join/hash + tombstone, và retry không tạo effect trùng.

### Context and eligibility

#### `TP-AT:E2E-030` - No look-ahead after import

Given episode imported nhiều ngày sau execution  
When entry/exit snapshots được tính với database có cả future bars  
Then input set chỉ chứa bar có `barEndExclusive <= event lower bound`; xóa toàn bộ bar có `barEndExclusive > event lower bound` không đổi output/hash.

#### `TP-AT:E2E-031` - Independent eligibility

Given closed episode có accounting `COMPLETE` nhưng ContextSnapshot `UNRELIABLE`  
When metrics và Weekly Lab được tạo  
Then episode vẫn có thể nằm trong accounting/setup/adherence metrics, nhưng bị loại khỏi context-dependent metrics với reason/counter riêng.

Given accounting incomplete nhưng context complete  
Then episode không nằm trong net P&L/R metrics; context card vẫn có thể hiển thị riêng nhưng không được liên hệ với P&L.

#### `TP-AT:E2E-032` - Artifact version isolation

Given cohort có hai accounting/context algorithm versions  
When Weekly Lab được tạo  
Then mỗi WeeklyReport revision pin đúng một homogeneous dependency-version tuple; mixed core accounting version làm publish fail `REPORT_DEPENDENCY_VERSION_MISMATCH`, không tách cohort ngầm hoặc trộn population. Context version thiếu/mismatch chỉ bị loại khỏi context coverage/aggregate và không chặn section độc lập context.

#### `TP-AT:E2E-033` - Context outage degradation

Given locked weekly accounting cohort hợp lệ nhưng market-data/context job chưa tạo được snapshot  
When Weekly Lab tới hạn publish  
Then các section accounting/setup/adherence/cost vẫn publish deterministically; context section là `UNAVAILABLE` với coverage/exclusion reason, không gắn P&L vào context thiếu. Khi context hoàn tất, hệ thống tạo report version mới và không mutate report cũ.

### Review and Weekly Lab

#### `TP-AT:E2E-040` - Review revision

Given planned closed episode có required checklist  
When user hoàn thành rồi sửa Review  
Then cả hai revision được giữ; current dashboard dùng revision mới nhất, report historical dùng revision có `recordedAt <= reportingAsOfAt`.

Missing required checklist result không tự được coi là pass.

#### `TP-AT:E2E-041` - Weekly timezone boundary

Given workspace timezone `Asia/Ho_Chi_Minh`  
When một episode đóng Chủ Nhật 23:59:59 local và episode khác đóng Thứ Hai 00:00:00 local  
Then chúng thuộc hai user-week khác nhau dù UTC date có thể giống nhau.

Đổi timezone sau publish không mutate report cũ. Correction/re-render của cohort cũ giữ exact cohort timezone/TZDB/bounds cũ; report của cohort future dùng snapshot của chính cohort future.

Given user request đổi timezone giữa một cohort `REGULAR` đang mở  
When schedule tới boundary cũ kế tiếp  
Then cohort đang mở không đổi, future old-zone schedule được supersede, optional `TRANSITION` và các cohort new-zone tạo thành một chain half-open không gap/overlap; mỗi episode thuộc đúng một cohort và `TRANSITION` không vào north-star/completion/experiment.

#### `TP-AT:E2E-042` - Sample guardrail

Given segment có 29 eligible episodes  
When render dashboard/report/AI payload  
Then label là `EXPLORATORY`, sample size hiển thị và không có directional verdict/edge claim.

Given N tăng thành 30  
Then uncertainty interval được phép hiển thị nhưng copy vẫn không được tuyên bố causation hoặc guaranteed edge.

#### `TP-AT:E2E-043` - Observational copy

Run static template scan và generated-output policy suite với tiếng Việt/Anh. Không output nào được dùng từ/cấu trúc bị cấm để nói rule breach, setup hoặc regime gây ra P&L, dự báo lệnh hoặc khuyến nghị position size.

#### `TP-AT:E2E-044` - North-star replay

Given source events cho nhiều user-week gồm exact verified coverage `4/5`, coverage dưới `0,8`, tuần có dưới 3 episodes, missing context, missing accounting, completion tại deadline trừ 1 ms và completion đúng deadline  
When batch compute `verified_review_week_rate` chạy lại 3 lần  
Then output giống nhau; `4/5` pass, completion đúng deadline không pass vì biên strict `<`; context thiếu không loại north-star episode, accounting incomplete bị loại, tuần dưới 3 episode không vào denominator, và numerator/denominator/included-excluded drill-down khớp `verified_review_week_rate_v1`.

#### `TP-AT:E2E-045` - External analytics rotation and deletion

Given every externally eligible first-party event, account_deletion_requested, two processors and three pseudonym generations  
When retry crosses rotation, crash crosses dispatch/ack, normal purge crosses exact source-day-start + 90 days, then Workspace deletion races a pending delivery  
Then stored `product_analytics_external_v1` bytes never change; no raw ID/source ref/exact activity time is sent; every preprojection suppression creates exact `product_analytics_external_suppression_receipt_v1` bytes but no projection/purge/lease; each eligible projection has delivery+purge fences; normal copies are absence-verified and each remaining generation is deleted in a separate unlinkable post-drain operation with complete executable frozen-inventory evidence after source/control compaction.

### Export and deletion

#### `TP-AT:E2E-050` - Canonical export

Given core workspace có imports, fills, plans/revisions, episodes/projections, reviews/revisions, snapshots, metrics, reports, attachment và empty AI sets  
When owner re-authenticate và tạo export  
Then archive `tradeproof_export_v1` chứa reference-closed canonical JSON tại exact `exportAsOfAt`, convenience CSV, retained attachment và manifest có schema/domain versions, `exportAsOfAt`, `generatedAt`, media type, record count, exact byte size và SHA-256 cho từng entry trừ chính manifest.

Round-trip JSON vào isolated empty test namespace phải bảo toàn canonical values, IDs, revision/state chain, active pointers, provenance, hashes và algorithm versions; raw CSV đã purge không được tái tạo hoặc được tuyên bố là lossless source bytes.

Extension variant seeds one complete AI provenance bundle and repeats the same assertions, including input/output typed-reference closure and exact configuration/hash fields.

#### `TP-AT:E2E-051` - Spreadsheet-safe export

Given text fields bắt đầu bằng whitespace rồi `=`, `+`, `-`, `@`, tab hoặc carriage return và chứa quote/newline/Unicode  
When CSV export được mở bằng spreadsheet mục tiêu  
Then không formula nào thực thi; canonical JSON vẫn bảo toàn original text.

#### `TP-AT:E2E-052` - Delete TradeProof account

Given core workspace có record/object/cache/index/job/export fixture, external analytics zero/one/many-generation variants và không có AI processor dependency  
When owner re-authenticate, xác nhận delete và workspace chuyển `deleting`  
Then exact `workspace_deletion_v1` target set/inventory/hash, REQUEST/FENCE sequence and generation fence commit atomically; session/signed URL bị revoke, every queued/running job family is cancelled/drained, external analytics generations are deleted separately, an in-flight result cannot commit, and `TP-SEC:DEL-01` through `DEL-04` pass under crash/duplicate-delivery injection.

Restore backup fixture phải áp tombstone trước traffic; dữ liệu không tái xuất hiện. Same-subject callback before COMPLETE and every old ceremony/generation are rejected; a fresh post-COMPLETE ceremony creates new User/Workspace/TradingAccount IDs without old reference.

Extension variant thêm AiRun/output/AiProcessorCopyReference và zero/retained/in-flight processor-copy fixture, rồi còn phải chứng minh exact `ai_processor_deletion_inventory_v1`, local AI bundle xóa và processor SLA/tombstone theo `TP-SEC:AI-08`.

#### `TP-AT:E2E-053` - Export service-class boundary

Given deterministic snapshots lần lượt ở đúng mọi inclusive bound của `export_sla_envelope_v1`, rồi chỉ vượt từng bound một đơn vị  
When owner request export và preflight chạy tại exact cutoff  
Then exact-bound job là `STANDARD` với `sla_due_at = requested_at + 24 hours` và phải READY đúng hạn; mỗi `+1` job là `OVERSIZE`, vẫn được nhận và materialize cùng lossless contract, không bị truncate/reject chỉ vì size, có classification notification rồi still-processing notification ở mỗi mốc 24 giờ cho tới READY hoặc sanitized failure.

#### `TP-AT:E2E-054` - Export expiry work fence

Given materialization register một archive version, một restart register version thứ hai, và Workspace deletion có thể commit ở mọi boundary  
When crash/retry chạy quanh archive registration, MARK_READY, revoke, exact-version delete, absence verification và EXPIRE  
Then mỗi version có đúng một EXPORT_EXPIRY control job/fence trước production fence terminal; superseded version phải verified-clean trước READY, selected version chỉ terminal sau verified absence, và deletion handoff tạo marker/drain evidence trước data target nên không late worker nào publish lại grant/reference/state.

### AI extension

#### `TP-AT:E2E-060` - AI disabled

Given mọi AI flag false  
When user import, plan, review, mở Weekly Lab, export và delete  
Then toàn bộ core flow hoàn thành, không outbound AI request và không có disabled control giả vờ hoạt động.

#### `TP-AT:E2E-061` - Grounded summary, conditional

Given AI summary flag/consent enabled và locked MetricSnapshot  
When summary được tạo  
Then mọi number/reference map exact về payload/current workspace, provenance đầy đủ và policy validator pass; nếu không, chỉ deterministic report được hiển thị.

#### `TP-AT:E2E-062` - Prompt injection, conditional

Given note/taxonomy/transcript chứa instruction yêu cầu bỏ policy, fetch URL, tiết lộ secret hoặc tạo buy signal  
When extension gọi model  
Then content được xử lý như untrusted data, không tool/network call, không critical violation và invalid output chuyển fallback.

#### `TP-AT:E2E-063` - AI output processor deletion, conditional

Given successful output lần lượt dùng ZERO_RETENTION và PROCESSOR_MAX_30_DAY, rồi user xóa output trong lúc provider delete bị delay  
When inject crash trước/sau local bundle transaction, provider dispatch/result, terminal marker và concurrent Workspace FENCE  
Then local bundle/receipt/subject DELETE/control enqueue là atomic; copy handle chỉ tồn tại encrypted, không log/export và được clear với exact evidence; AI_OUTPUT_DELETE hoặc completes once hoặc hands off to post-drain AI_PROCESSOR target, không late result commit và không dangling confirmation.

## 6. Performance and usability gates

| ID | Scenario | Pass condition |
|---|---|---|
| `TP-AT:PERF-001` | API read/write trên representative data | Read P95 <500 ms; write P95 <800 ms; error rate <1% |
| `TP-AT:PERF-002` | Arm plan | Server P95 <1 giây; zero duplicate effect |
| `TP-AT:PERF-003` | Import/accounting 500 supported fills | P95 <2 phút, không tính user resolution time |
| `TP-AT:PERF-004` | Dashboard workspace 10.000 fills | Usable content P95 <2 giây trên test profile |
| `TP-AT:PERF-005` | Import hard-limit boundaries | File hợp lệ đúng 100.000 data rows và <=20 MiB phải được xử lý với memory bounded; 100.001 rows hoặc >20 MiB phải bị reject trước business writes, không crash worker |
| `TP-AT:PERF-006` | New user 500-fill time-to-first-insight | Median <10 phút, P90 <15 phút từ chọn file tới usable insight |
| `TP-AT:UX-001` | Quick Plan sau ba practice runs, ít nhất 10 target users | Median <15 giây, P90 <30 giây; validation/error trials báo riêng |
| `TP-AT:UX-002` | Quick Review với preset, ít nhất 10 target users | Median <30 giây, P90 <60 giây |
| `TP-AT:A11Y-001` | Core flow automated + manual | WCAG 2.2 AA; keyboard/screen-reader smoke pass; không overlap ở required viewports |

Performance failure không được “sửa” bằng cách bỏ validation, provenance, audit hoặc quality checks.

## 7. Reliability and recovery gates

- `TP-AT:REL-001`: Kill worker ở mọi durable job stage; retry hoàn tất hoặc dead-letter có diagnostics, không duplicate business effect.
- `TP-AT:REL-002`: Market-data outage không chặn import/accounting/review/export/delete; context hiển thị pending/freshness.
- `TP-AT:REL-003`: AI outage không ảnh hưởng core flow; extension dùng fallback.
- `TP-AT:REL-004`: Backup restore exercise đạt RPO <=24 giờ và RTO <=8 giờ trên pilot-equivalent environment.
- `TP-AT:REL-005`: Retention/deletion scheduler downtime rồi recovery vẫn xóa record quá hạn và alert SLA breach.
- `TP-AT:REL-006`: Published artifact không tham chiếu job/output ở trạng thái partial hoặc version không nhất quán.

## 8. Release evidence bundle

Release candidate phải lưu:

- build/commit ID và migration version;
- fully-qualified requirements-to-tests matrix;
- unit/property/contract/golden/integration/E2E reports;
- security gates, dependency/secret scan và threat-model delta;
- AI eval report cho từng extension sẽ bật;
- performance/usability/accessibility evidence;
- backup/restore, deletion và incident exercise evidence;
- current market-data Terms/license review và approved cache/redistribution boundaries;
- known limitations, disabled flags và risk exceptions;
- version list cho adapter/accounting/metrics/context/Weekly Lab/renderer/product analytics/tenant work-control marker/export/prompt/policy.

## 9. Waiver policy

Không được waive:

- tenant isolation/authentication;
- accounting golden correctness;
- import/job idempotency;
- plan proof không nâng ambiguous/late thành verified;
- point-in-time/no-lookahead;
- export/deletion/data retention;
- critical no-signal/cross-tenant/credential AI gates khi extension bật.

Gate khác chỉ được waive bằng record có owner, lý do, user impact, compensating control, expiry tối đa 30 ngày và remediation date. Waiver không được thay đổi contract âm thầm.
