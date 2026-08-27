# Product Requirements

- **Document ID:** `TP-PRD`
- **Version:** 1.0.0
- **Status:** Implementation baseline
- **Updated:** 2026-08-27
- **Scope:** TradeProof MVP

## 1. Purpose

Tài liệu này định nghĩa hành vi người dùng và yêu cầu hệ thống của TradeProof MVP. Công thức tài chính/matching thuộc `TP-ACC`, context thuộc `TP-MCE`, cohort/report/experiment/product metrics thuộc `TP-LAB`, archive export thuộc `TP-EXP`, và security/privacy/AI thuộc `TP-SEC`; tài liệu này không được dùng để tự suy ra công thức hoặc schema thay thế.

## 2. Fixed scope

- Web responsive, giao diện tiếng Việt.
- Binance Spot, symbol có quote asset `USDT`.
- Long-only, không margin hoặc borrow.
- Một owner, một workspace, một trading account.
- File import; không exchange API key hoặc live sync.
- Tối đa một armed plan và một open episode trên mỗi account/symbol.
- Timezone mặc định `Asia/Ho_Chi_Minh`; người dùng được đổi timezone IANA.
- Context dùng Binance Spot public 1m/5m candles.

Thay đổi bất kỳ dòng nào ở trên yêu cầu version mới cho PRD, accounting contract, acceptance suite và implementation plan.

## 3. Actors and authorization

| Actor | Quyền MVP |
|---|---|
| `WorkspaceOwner` | Toàn quyền trong workspace của mình; import, plan, review, export, delete |
| `SystemWorker` | Xử lý job theo workspace scope; không có interactive login |
| `SupportOperator` | Chỉ thấy metadata vận hành đã tối thiểu hóa; không có product feature để xem nội dung người dùng |

MVP không có invitation, coach, shared workspace hoặc custom role.

## 4. Canonical terms

| Term | Definition |
|---|---|
| `Plan` | Ý định giao dịch do người dùng tạo trước fill |
| `Armed revision` | Bản plan append-only có server timestamp; ứng viên cho pre-fill proof |
| `Fill` | Một execution row đã normalize từ source CSV |
| `TradeEpisode` | Chuỗi fill long-only trên một account/symbol từ quantity 0 tới quantity 0 |
| `Verified pre-fill` | Trạng thái timing `verified` và plan được match với episode theo accounting contract |
| `Episode Review` | Structured review append-only sau một closed episode |
| `WeeklyReviewCompletion` | Server-timestamped event xác nhận người dùng đã hoàn thành Weekly Lab cho một user-week; không phải Episode Review |
| `ContextSnapshot` | Kết quả context bất biến tại entry hoặc exit, chỉ dùng dữ liệu point-in-time |
| `Accounting eligible` | Closed, reconciled và có net P&L; dùng cho accounting/setup/adherence metrics |
| `Context eligible` | Có ContextSnapshot `COMPLETE` đúng phase/version; dùng cho context metrics |
| `North-star eligible` | Closed, reconciled và có net P&L; context không phải điều kiện |
| `Metric eligible` | Kết quả đánh giá theo dependency của từng metric; không tồn tại một eligibility toàn cục |
| `Observation` | Mô tả thống kê; không phải quan hệ nhân quả hoặc khuyến nghị giao dịch |

## 5. Primary user flows

### 5.1. Onboarding

1. Người dùng đăng nhập qua managed OIDC hoặc email magic link.
2. Hệ thống tạo một workspace do người dùng sở hữu.
3. Người dùng xác nhận timezone IANA và tên hiển thị cho trading account.
4. Hệ thống tạo `TradingAccount` với venue `BINANCE`, product `SPOT`, reporting currency `USDT`.
5. Người dùng tạo ít nhất một setup preset hoặc dùng preset `OTHER`.
6. Onboarding kết thúc tại dashboard có hai action chính: tạo Quick Plan và import CSV.

### 5.2. Quick Plan

1. Người dùng chọn symbol và setup.
2. Người dùng nhập entry zone, initial stop, planned risk USDT và confidence.
3. Người dùng có thể thêm checklist hoặc note; voice note chỉ xuất hiện khi `voice_transcription_enabled=true` và có consent.
4. Người dùng bấm `Arm plan`.
5. Server validate, tạo immutable revision và trả `submittedAt`.
6. UI hiển thị trạng thái armed, expiry và display state `awaiting_fill`; đây không phải `plan_proof_status` persisted vì chưa có episode để đánh giá.

Người dùng được tạo revision mới, cancel hoặc để plan expire. Revision cũ không bị ghi đè.

### 5.3. Import and reconcile

1. Người dùng chọn file CSV.
2. Client thực hiện kiểm tra sơ bộ; server luôn kiểm tra lại.
3. `UPLOAD_VALIDATE` chỉ tạo sanitized immutable preview: format/catalog version, row count, time range, symbols, duplicate estimate, safe row errors, hash và expiry; không tạo batch hay business effect.
4. Người dùng xác nhận preview còn `READY` bằng preview hash + idempotency key; transaction tạo đúng một `ImportBatch` và exact `IMPORT` control chain trước khi worker chạy.
5. Worker normalize rows, deduplicate, group episodes, match plans và tính accounting.
6. UI hiển thị bốn canonical disposition: `RECONCILED`, `DUPLICATE`, `ACCOUNTING_PENDING`, `QUARANTINED`; metric exclusions được báo riêng, không trộn vào row balance.
7. Người dùng xử lý trường hợp mơ hồ trong resolution queue.
8. Khi episode đủ điều kiện, hệ thống enqueue ContextSnapshot và metrics.

### 5.4. Episode review

1. Episode detail phân tách rõ `Before fill`, `Execution`, `After fill` và `Context`.
2. Người dùng chọn exit reason và trạng thái rule breach.
3. Người dùng xác nhận stop moved/risk exceeded, có thể thêm emotion, lesson và screenshot.
4. Hệ thống lưu review revision append-only và cập nhật review completion.

### 5.5. Weekly Lab

1. Sau khi tuần kết thúc, hệ thống khóa cohort episode theo timezone của workspace.
2. Metric engine tạo payload bất biến và traceable.
3. Deterministic renderer luôn tạo được báo cáo cơ bản.
4. Nếu người dùng opt in AI và provider khả dụng, AI được viết phần diễn giải từ payload đó.
5. Người dùng chọn hoặc sửa một behavioral experiment và hoàn thành review.

## 6. Functional requirements

### 6.1. Authentication and workspace

- `AUTH-001`: Mọi business record phải có `workspace_id` không null.
- `AUTH-002`: Mọi read/write phải authorize `actor_id` với `workspace_id`; không tin workspace ID từ client nếu không kiểm tra ownership.
- `AUTH-003`: MVP không có membership entity. Mỗi Workspace có direct non-null `owner_user_id` trỏ tới đúng một User; `Workspace.owner_user_id` là unique nên mỗi User sở hữu đúng một Workspace, và mọi USER actor trong Workspace phải bằng owner này.
- `AUTH-004`: Export và delete account phải yêu cầu recent authentication theo security contract. Nếu managed provider cung cấp đổi email/identity, thao tác đó diễn ra qua provider và không được đổi ownership key `(issuer, subject)` một cách ngầm định.
- `AUTH-005`: System jobs phải nhận workspace scope tường minh và không được query cross-workspace ngoài aggregate vận hành đã khử định danh.
- `AUTH-006`: `(issuer,subject)`, UserIdentity, User, Workspace và TradingAccount tuân exact 1:1 bootstrap/unique/generation contract trong `TP-SEC`; concurrent callback chỉ tạo một ownership tree và callback sau FENCE không tạo session/tree mới.
- `AUTH-007`: Issuer identity key giữ byte-exact pinned metadata string, không normalize path/trailing slash. Sign-in fail trước identity resolution dùng exact PRE_AUTH audit branch không fabricate actor/workspace hoặc retain raw issuer/subject/token.

### 6.2. Onboarding and settings

- `ONB-001`: Timezone phải là IANA timezone hợp lệ; default chỉ được dùng sau khi người dùng xác nhận.
- `ONB-002`: Workspace có đúng một active `TradingAccount` trong MVP.
- `ONB-003`: Trading account cố định `BINANCE/SPOT/USDT`; đổi venue hoặc product bị từ chối.
- `ONB-004`: Người dùng có thể request đổi timezone. Thay đổi có hiệu lực tại boundary `REGULAR` cũ kế tiếp theo `TP-LAB`, không sửa cohort `OPEN`/`LOCKED`; future `SCHEDULED` header cũ được supersede và hệ thống tạo cohort `TRANSITION` nếu cần để không gap/overlap. MetricSnapshot hoặc Weekly Lab đã phát hành không bị mutate.
- `ONB-005`: Setup preset theo `setup_preset_v1`; label dài 1-60 Unicode scalar values và unique theo `setup_label_key_v1` giữa current active presets; system `OTHER` luôn active và không thể rename/archive.
- `ONB-006`: Tối đa 50 active user-defined presets; mỗi revision có 0-10 ordered checklist item theo `plan_checklist_v1`. Create/revise/archive/reactivate append-only, idempotent và plan đã arm giữ exact revision snapshot.

### 6.3. Plan

- `PLAN-001`: Quick Plan form trước submit chỉ tồn tại client-local/ephemeral. `ArmPlan` nhận full account/instrument record keys, setup revision, entry zone low/high, stop, planned risk, confidence, thesis và optional expiry; server không nhận reference tới server-side form, không persist DRAFT, và chỉ tạo TradePlan khi toàn bộ request hợp lệ.
- `PLAN-002`: Symbol phải active, quote asset `USDT` và được source adapter hỗ trợ.
- `PLAN-003`: `entry_zone_low`, `entry_zone_high` và `initial_stop` dùng exact TP-ACC decimal grammar/`DECIMAL(38,18)` không rounding; insignificant fractional trailing zeros được nhận rồi canonicalize trước persist/hash, còn leading zero/sign/exponent/space/overflow bị reject. Luôn có `0 < entry_zone_low <= entry_zone_high` và với long-only `0 < initial_stop < entry_zone_low`.
- `PLAN-004`: API field `planned_risk_usdt` dùng decimal, lớn hơn 0, tối đa 8 chữ số phần nguyên và 8 chữ số thập phân; nó map exact sang `planned_risk_quote` với `planned_risk_asset = USDT` trong `TP-ACC`.
- `PLAN-005`: Confidence là integer 1-5; đây là self-rating, không phải win probability.
- `PLAN-006`: Checklist tối đa 10 item; mỗi item có stable ID, label 1-120 Unicode scalar và boolean `required`; note/thesis là null khi bỏ trống, nếu có thì trim Unicode White_Space, giữ exact text 1-1.000 Unicode scalar và không truncate.
- `PLAN-007`: Arm command nhận nullable integer `expiry_duration_seconds`; null mặc định `86400`, giá trị khác nằm trong `900..604800`, và `expires_at = armed_at + expiry_duration_seconds`. Plan có hiệu lực trên half-open interval `[armed_at, expires_at)` và tự hết hạn đúng tại equality theo TP-ACC, không phụ thuộc scheduler chạy đúng giờ.
- `PLAN-008`: Chỉ một armed plan được tồn tại trên cùng account/symbol. Plan mới yêu cầu cancel hoặc expire plan cũ.
- `PLAN-009`: Arm/revise/cancel dùng server-authoritative timestamp và idempotency key.
- `PLAN-010`: Armed revision là append-only; update hoặc delete vật lý bị cấm.
- `PLAN-011`: Revision sau first fill không được gắn `verified_pre_fill=true`.
- `PLAN-012`: Sau khi opening fill được reconcile, Episode UI phải hiển thị đúng một trong `verified`, `ambiguous`, `late`, `unmatched`; không quy ambiguous thành verified. Trước opening fill, Plan UI chỉ hiện derived state `awaiting_fill`, không persist như proof enum thứ năm.
- `PLAN-013`: Voice transcript là draft; không thay đổi structured field trước khi người dùng xác nhận.

### 6.4. Import

- `IMP-001`: Chỉ chấp nhận adapter/version có trong import contract.
- `IMP-002`: CSV preview pass mới atomically tạo exact `import_preview_v1` cùng Upload ACCEPT/`UPLOAD_VALIDATE` terminal; preview không tạo `ImportBatch`, `ImportRow`, StagedFill, NormalizedFill, Episode, ledger, ContextSnapshot hoặc accounting/weekly MetricSnapshot. Invalid file tạo Upload REJECT nhưng zero preview/batch/business row.
- `IMP-003`: `ConfirmImport` yêu cầu preview ID + exact summary hash + idempotency key, chỉ nhận preview `READY` trước immutable expiry; cùng transaction tạo một `ImportBatch` và `IMPORT` TenantControlJob/fence/ENQUEUE, nhưng zero row/fill/business projection. Batch copy immutable preview ID/schema/hash/confirmed time làm durable provenance không-FK sau preview cleanup. Retry exact trả cùng batch/job, changed payload conflict; abandon/expire không thể confirm.
- `IMP-004`: Upload lại cùng content trong cùng workspace vẫn tạo/confirm preview riêng rồi trả kết quả batch cũ hoặc tạo batch alias; không tạo business record trùng.
- `IMP-005`: Mỗi source row có fingerprint ổn định; dedup hoạt động cả khi file overlap một phần. Multiplicity ambiguous chỉ tạo immutable `staged_fill_v1` không dedup key; audited ACCEPT atomically tạo immutable NormalizedFill, MARK_DUPLICATE atomically discard candidate và pin canonical target.
- `IMP-006`: Invalid row sau confirm đi vào quarantine với code, field và source row number; preview chỉ giữ bounded safe error summary, không raw cell/filename/exception.
- `IMP-007`: Với confirmed batch đã qua preview/file-level envelope/header validation, không bỏ row âm thầm: `RECONCILED + DUPLICATE + ACCOUNTING_PENDING + QUARANTINED = data_rows` không rỗng theo `TP-ACC`. Invalid file dừng ở Upload REJECT không có batch; rare confirmed batch `REJECTED` trước row admission báo stable source-binding/revalidation error và không giả tạo row disposition.
- `IMP-008`: Import không transactional toàn file; immutable NormalizedFill hợp lệ được commit theo accounting state, multiplicity candidate dùng StagedFill riêng, row pending được giữ cho resolution và row quarantined được cô lập; batch summary phản ánh đủ bốn disposition trong `IMP-007`.
- `IMP-009`: Preview, confirm, IMPORT worker và staged-fill resolution retry không thay đổi kết quả nghiệp vụ.
- `IMP-010`: Người dùng có thể tải error report đã sanitize.
- `IMP-011`: Mọi raw CSV/screenshot/voice persist `purge_due_at = RECEIVE + 24 giờ` và bắt đầu forced purge không muộn hơn RECEIVE+20 giờ; CSV abandon/unconfirmed-preview expiry/import terminal có thể trigger sớm hơn. Accepted/rejected/stalled branch đều purge exact object version sau timely absence verification, không gia hạn vì preview/import/transcription/confirmation retry và không fabricate PURGE khi breach.
- `IMP-012`: Mọi raw/sanitized object phải có `ObjectIngestReservation` trước write, bound single-use capability và one-version conditional create; transfer atomically moves locator vào Upload/Attachment lease, còn abort/shell cleanup có absence proof/TTL exact theo `TP-SEC`.

### 6.5. Reconciliation and episode

- `EP-001`: Episode tuân theo state machine trong accounting contract.
- `EP-002`: Không auto-group fill vượt khỏi long-only hoặc one-open-episode invariant.
- `EP-003`: Mọi auto plan match lưu rule version, candidate IDs và reason code.
- `EP-004`: Manual row resolution và plan-match resolution lưu actor, timestamp, old value, new value, reason và idempotency key; không sửa source row. Xác nhận, chọn hoặc gỡ association mơ hồ không được nâng proof `AMBIGUOUS` thành `VERIFIED`.
- `EP-005`: Closed episode phải lưu các eligibility result riêng theo metric family; không lưu một boolean `eligible` toàn cục.
- `EP-006`: Row `ACCOUNTING_PENDING`, accounting quality `FEE_CONVERSION_MISSING|SEQUENCE_PENDING|REPLAY_PENDING|INVALID`, và row `QUARANTINED` phải hiển thị stable reason code; chỉ các metric phụ thuộc bị loại theo eligibility policy, không dùng nhãn trạng thái tự phát ở client.
- `EP-007`: Recompute tạo metric/snapshot version mới; không mutate artifact đã được Weekly Lab tham chiếu.

### 6.6. Context

- `CTX-001`: Tạo entry snapshot từ `projection.first_fill_id` trỏ tới immutable canonical opening NormalizedFill đã có active allocation, và exit snapshot từ `projection.closed_fill_id` trỏ tới final closing fill; cả hai dùng source-time lower bound theo `TP-ACC`/`TP-MCE`.
- `CTX-002`: Chỉ dùng bar có `barEndExclusive <= asOfAt`; không dùng source `close_time` inclusive để kiểm tra eligibility.
- `CTX-003`: Không dùng proxy venue hoặc proxy symbol trong MVP.
- `CTX-004`: Mỗi snapshot lưu algorithm version, source request metadata, input digest, coverage, quality và exact `provenanceHash`. Hash này dùng closed RFC 8785 object chỉ gồm `ingestionBatches`, `inputBarSources`, `sourceRequests`. Item shapes lần lượt là `{ completedAt, fetcherVersion, ingestionBatchId, productType, sourceVenue, startedAt, status }`, `{ ingestionBatchId, marketBarResolutionId, marketBarRevisionId, sourceObservationId, sourceRequestId }` và `{ fetchedAt, requestMetadataHash, responseSha256, sourceRequestId }`; chỉ `marketBarResolutionId` được nullable. `inputBarSources` có đúng một item cho mỗi selected bar và giữ cùng thứ tự `(timeframe,openAt,revisionId)`; hai mảng còn lại deduplicate rồi sort theo exact UTF-8 bytes của `ingestionBatchId` và `sourceRequestId`. Unknown/missing member, duplicate, wrong null/order hoặc unreachable provenance bị reject trước persist.
- `CTX-005`: ContextSnapshot `partial` hoặc `unreliable` đều bị loại khỏi context aggregate; cả hai có thể hiển thị trên context card với quality badge và diagnostics theo `TP-MCE`.
- `CTX-006`: Source/API failure không chặn import hoặc accounting; CONTEXT job có thể retry nhưng mọi trigger phải có durable typed identity. Projection publication atomically tạo/trả cùng một server RFC 9562 UUID `ContextEpisodeTrigger` cho exact `(workspaceId,tradeEpisodeId,episodeProjectionVersion,phase)` trước enqueue; ENTRY dùng `sourceEventSequence = 1` và first allocation/fill, EXIT chỉ có trên CLOSED projection và dùng `sourceEventSequence = N = count(EpisodeFillAllocation)` cùng closing allocation/fill. Mọi branch phải byte-match cùng authoritative phase/sequence mapping này. Manual retry atomically persist same-workspace `ManualContextRecomputeRequest` receipt và exact CONTEXT chain; `(workspaceId,idempotencyKey)` unique, same key/hash trả cùng request/job còn changed bytes conflict. INITIAL_EVENT/SOURCE_GAP_FILLED/SOURCE_REVISION_RESOLVED/MANUAL_RETRY lần lượt pair với `EPISODE_EVENT | INGESTION_BATCH | MARKET_BAR_RESOLUTION | MANUAL_REQUEST` và chỉ dùng durable `triggerId`; EPISODE_PROJECTION_REPLAYED/ALGORITHM_UPGRADE pair với `EPISODE_PROJECTION | ALGORITHM_RELEASE` và chỉ dùng `triggerSha256`, lần lượt là SHA-256 của canonical new projection record key và exact immutable `ContextAlgorithmRelease.releaseSha256`. Release digest là SHA-256 của closed RFC 8785 `{ "algorithmVersion":str, "calculationContractSha256":hash, "calculationContractVersion":str, "implementationArtifactSha256":hash, "parameterPayloadSha256":hash, "parameterSetId":str }`; wrong branch/type/phase/sequence/release/member bị reject trước work-sequence allocation.

### 6.7. Review

- `REV-001`: Review yêu cầu `exit_reason`, `rule_breach`, hai boolean `stop_moved_away`, `risk_exceeded` và kết quả cho từng required checklist item trong frozen plan revision.
- `REV-002`: Nếu `rule_breach=true`, `breach_type_ids` phải có ít nhất một type từ taxonomy versioned hoặc `OTHER` kèm text.
- `REV-003`: Emotion là optional enum versioned; lesson optional tối đa 2.000 ký tự.
- `REV-004`: Screenshot optional, tối đa theo security contract; metadata nhạy cảm phải được cảnh báo trước upload.
- `REV-005`: Review revision append-only; dashboard dùng revision mới nhất nhưng history luôn export được.
- `REV-006`: Edit review không thay đổi plan proof, fill, accounting hoặc historical Weekly Lab đã phát hành.

### 6.8. Metrics and Weekly Lab

- `LAB-001`: Metric engine chỉ nhận normalized immutable inputs và deterministic algorithm version.
- `LAB-002`: Mọi metric artifact lưu included/excluded episode IDs, exclusion reasons và eligibility policy ID; context thiếu chỉ loại context-dependent metric.
- `LAB-003`: Mọi MetricSnapshot dùng `INSUFFICIENT` khi N < 2, `EXPLORATORY` khi 2 <= N < 30 và `ESTIMATED` khi N >= 30; không hiển thị directional verdict hoặc edge claim.
- `LAB-004`: Báo cáo dùng từ `observed`, `associated`, `co-occurred`; cấm `caused`, `led to`, `will win` và bản dịch tương đương.
- `LAB-005`: Numeric claim phải link tới metric detail chứa formula version, sample size và episode IDs.
- `LAB-006`: Weekly report dùng exact immutable `WeeklyCohort` timezone, TZDB và half-open local/UTC bounds do `TP-LAB` khóa; correction/re-render không đọc timezone hiện tại và không đổi cohort membership lịch sử.
- `LAB-007`: Report tạo lại sau data correction là version mới; version cũ giữ trạng thái `superseded`.
- `LAB-008`: Draft experiment có thể persist dưới dạng append-only `PROPOSED` revision nhưng không có active effect; chỉ một `CONFIRMED` experiment current được nhắm tới mỗi cohort `REGULAR`, và confirm bắt buộc là thao tác rõ ràng của người dùng.
- `LAB-009`: Deterministic report phải hoạt động khi AI disabled, timeout hoặc rejected.
- `LAB-010`: Mỗi WeeklyReport revision dùng đúng một homogeneous dependency-version tuple; mixed core accounting version chặn publish, còn context thiếu/mismatch chỉ làm giảm context coverage và không chặn section độc lập context.
- `LAB-011`: Section order, metric selection, null/sample/tie-break, counterexample và renderer output phải theo exact `weekly_lab_v1`/`weekly_lab_renderer_v1`; client không tự merge, rank hoặc tính lại.
- `LAB-012`: Mọi report metric phải khớp closed `metric_id/formula_version/eligibility_policy/dimension/type/unit/null` matrix và `metrics_decimal_v1`; client/worker không recompute unrounded R, alternate denominator hoặc floating-point value.
- `LAB-013`: Mọi WorkspaceProductMetricSnapshot phải khớp closed product metric type/value/count/status/null matrix; PROVISIONAL không publish partial KPI, ratio dùng integer numerator/denominator và one-final round18, adoption `PRE_PERIOD_ZERO` là exception duy nhất theo `TP-LAB`.
- `LAB-014`: External analytics chỉ nhận exact pinned `product_analytics_external_v1` envelope với processor-specific rotating pseudonyms, UTC day và closed scalar payload; eligible delivery/purge đều fenced, preprojection suppression chỉ tạo internal `product_analytics_external_suppression_receipt_v1` và không có provider lease, copy phải absent theo source-day 90-day deadline, và account deletion xóa từng pseudonym generation bằng frozen inventory `product_analytics_external_deletion_inventory_v1`.

Đối với UX benchmark trong `LAB-013`, `StartProductMeasurement` là synchronous authenticated command dưới Workspace/scope lock và ACTIVE/current deletion-generation check. Nó atomically tạo immutable `ProductMeasurementRun`/`product_measurement_run_v1`, sequence-1 START và một registered `PRODUCT_MEASUREMENT_TIMEOUT` TenantControlJob/fence/ENQUEUE, hoặc không tạo gì; retry cùng start key và byte-identical feature/study/mode/index trả cùng run, còn changed bytes hoặc semantic duplicate bị reject. Mỗi retained run có đúng một sequence-2 terminal `SUCCEED | ABANDON`; `deadline_at = started_at + 30 minutes`, success chỉ commit trước deadline và equality thuộc TIMEOUT. ABANDON reason là closed enum `USER_CANCELLED | NEGATIVE_DURATION | ZERO_DURATION | BACKGROUND_INTERRUPTED | MISSING_TERMINAL_EVENT | DURATION_OVER_30_MINUTES | TIMEOUT`. PRACTICE index phải contiguous/unique, không có hơn một OPEN run trong scope, ONBOARDING cấm PRACTICE, MEASURED unique và chỉ bắt đầu sau mọi practice đã terminal; QUICK_PLAN yêu cầu đúng practice 1, 2, 3 và cấm practice sau MEASURED.

Timeout control có subject `ProductMeasurementRun` với key `{ "measurement_run_id": id }`, exact payload `{ "deadlineAt": ts, "feature": "ONBOARDING"|"QUICK_PLAN"|"QUICK_REVIEW"|"FIRST_INSIGHT", "measurementRunSchemaVersion": "product_measurement_run_v1", "operation": "TERMINALIZE_AT_DEADLINE" }` và operation idempotency key `measurement-run:<measurement_run_id>:timeout`. Success/client-abandon/timeout atomically tạo terminal ProductAnalyticsEvent, sequence 2, COMPLETE và marker với `MEASUREMENT_RUN_SUCCEEDED | MEASUREMENT_RUN_ABANDONED`; FENCE thắng chỉ tạo CANCELLED_DELETION/`WORKSPACE_DELETING`, không tạo late event và để PRIMARY_TENANT_DATA xóa toàn bộ run bundle. Không generic scheduler/outbox nào được terminalize run ngoài control item này.

### 6.9. Export and deletion

- `DATA-001`: Export gồm toàn bộ durable canonical workspace state và immutable history tại một cutoff, cộng reference-closed public market provenance được domain artifact tham chiếu; exact allowlist/exclusion nằm trong `TP-EXP` và không gồm raw CSV đã purge.
- `DATA-002`: JSON export theo `tradeproof_export_v1` là lossless đối với durable canonical state tại cutoff; CSV chỉ là bảng tiện dụng, không lossless và phải chống spreadsheet formula execution.
- `DATA-003`: Export tách `exportAsOfAt` khỏi `generatedAt`, ghi schema/domain versions, workspace timezone và checksum manifest theo `TP-EXP`.
- `DATA-004`: Người dùng thấy tiến độ/service class export và nhận signed download URL có thời hạn. Snapshot `STANDARD` theo `export_sla_envelope_v1` có `sla_due_at = requested_at + 24 hours`; `OVERSIZE` vẫn được chấp nhận lossless, không có v1 24-hour guarantee và phải có authenticated in-app status khi classify, mỗi 24 giờ còn xử lý, rồi khi READY hoặc fail an toàn. Các status này commit trong EXPORT-fenced transaction, không có email/webhook worker. Mỗi archive version đã register phải có EXPORT_EXPIRY fence độc lập trước READY và chỉ terminal sau revoke/delete/exact-version absence; không có hidden cleanup sau EXPORT terminal. Envelope không phải storage, episode hoặc pricing quota.
- `DATA-005`: Delete account dùng two-step confirmation và recent authentication.
- `DATA-006`: UI command `Delete TradeProof account` tạo exact `workspace_deletion_v1`; FENCE atomically tăng generation/revoke access và đóng target/deadline/pipeline set. Mọi data target chỉ enqueue sau JobDrainEvidence, kể cả empty store vẫn delete/no-op + post-drain verify; primary/local ≤24h, cache/index ≤72h, processor/backup ≤30 ngày và incomplete action ordinal không được COMPLETE.
- `DATA-007`: Conformance reader phải validate và round-trip archive trong isolated empty namespace, bảo toàn ID, revision chain, active pointer, hash và canonical value; đây không bắt buộc là production restore endpoint.
- `DATA-008`: Restore áp WorkspaceDeletionTombstone trước traffic. Same-subject đăng ký lại chỉ sau COMPLETE bằng fresh auth ceremony, identity generation+1 và ownership IDs mới; không reattach dữ liệu cũ.
- `DATA-009`: Mọi async tenant operation có thể materialize data, dispatch external operation hoặc commit result sau enqueue dùng exact versioned TenantControlJob payload/fence và, chỉ khi có provider dispatch, external-operation lease; terminal compacts thành payload-free marker giữ payload-schema/digest-profile version và contiguous drain sequence khi subject bị xóa. OBJECT_INGEST_FINALIZE, PRODUCT_MEASUREMENT_TIMEOUT, ANALYTICS_PURGE, EXPORT_EXPIRY và AI_OUTPUT_DELETE là registered work types, không phải hidden scheduler/retention outbox; PRODUCT_MEASUREMENT_TIMEOUT không có external lease. Deletion control graph, encrypted processor inventories/locators và identity-generation tombstone tuân exact TTL/chain cleanup của `TP-SEC`.

### 6.10. AI

Các requirement trong mục này là bắt buộc trước khi bật extension flag tương ứng. Core release giữ toàn bộ AI flags tắt, được phép không triển khai UI/endpoint AI và phải pass exact disabled-profile gate `TP-SEC:AI-00`; `AI-01` không thay thế `AI-00`. Khi bất kỳ AI feature nào được bật, release phải pass mọi gate áp dụng từ `TP-SEC:AI-01` đến `TP-SEC:AI-11` cho feature/model/prompt/policy đó; disabled-profile `AI-00` không được dùng thay evidence của extension.

- `AI-001`: AI là extension mặc định tắt; consent append-only theo `ai_consent_v1`, tách riêng cho transcription, taxonomy suggestion và generative summary; không có current GRANT thì không outbound request.
- `AI-002`: AI không nhận raw CSV, exchange identifiers không cần thiết hoặc cross-workspace data.
- `AI-003`: Numeric input chỉ đến từ locked metric payload; output không được thay numeric source of truth.
- `AI-004`: Mọi generation lưu provider/model, prompt version, input digest, output và policy result.
- `AI-005`: Imported text, note và transcript được đặt trong untrusted-data boundary.
- `AI-006`: Output vi phạm no-signal/causal policy bị chặn và thay bằng deterministic fallback.
- `AI-007`: Transcript/taxonomy suggestion chỉ ghi canonical plan/Review thông qua exact immutable user-confirmation command, stale/idempotency/tenant validation và atomic next revision; optional keep-original chỉ giữ sanitized Attachment và không gia hạn raw deadline.
- `AI-008`: Xóa source AiOutput không đổi confirmed revision; confirmation provenance đóng tới active output trước delete hoặc exact Tombstone sau delete và phải round-trip export.
- `AI-009`: DeleteAiOutput transactionally xóa local content bundle nhưng giữ disclosed payload-free subject/state và unsalted integrity digest đến Workspace deletion; digest là Restricted derived personal data, không được gọi anonymous/content-free. Mỗi outbound run có non-exported encrypted opaque processor-copy handle; AI_OUTPUT_DELETE fence xóa/verify copy, clear handle và ngăn late result race với account deletion. Voice chỉ bật sau exact `voice_ingest_profile_v1` conformance.

## 7. Required screens and states

### 7.1. Dashboard

- Current week plan coverage, review status và data-quality exclusions.
- Recent episodes dạng bảng; không dùng leaderboard hoặc celebratory P&L treatment.
- Empty state dẫn tới Quick Plan hoặc Import, không phải marketing page.

### 7.2. Quick Plan

- Preset-first form, mobile viewport hỗ trợ one-hand entry.
- UI có local editing/submitting/validation-error state; persisted/API TradePlan chỉ có armed, consumed, cancelled hoặc expired, còn revised là revision history chứ không phải lifecycle state.
- `submittedAt` và proof badge hiển thị rõ nhưng không cho client tự chỉnh.

### 7.3. Import Center

- Upload, preview, processing progress, batch history và resolution queue.
- Summary phải cân bằng row counts theo `IMP-007`.
- Error state phân biệt invalid format, partial success, source duplicate và system failure.

### 7.4. Episode Detail

- `Before fill`, `Execution`, `After fill`, `Context`, `Data quality`, `Audit history`.
- Accounting breakdown từ gross tới net; fee conversion provenance có thể mở xem.
- Exclusion banner phải nêu metric nào bị ảnh hưởng.

### 7.5. Weekly Lab

- Overview, setup observations, adherence observations, cost, context, counterexamples và experiment.
- Mọi chart/table có sample size, quality và drill-down episode list.
- Cohort `TRANSITION` có badge rõ ràng, vẫn render continuity report nhưng không tham gia north-star, completion hoặc experiment flow.
- AI copy được đánh dấu là generated summary; deterministic numbers không mang nhãn AI.

### 7.6. Settings

- Profile/timezone, setup taxonomy, data export và delete account luôn hiện. AI consent chỉ hiện theo từng feature có server flag bật; transcription, taxonomy suggestion và weekly summary được ẩn/hiện độc lập, và route/action cho feature tắt phải không callable. Khi cả ba flag tắt, Settings không có nhãn, control hoặc route AI nào.
- Không có exchange API key screen trong MVP.

## 8. Non-functional requirements

### Performance

- `NFR-PERF-001`: API read P95 dưới 500 ms, write P95 dưới 800 ms, không tính async jobs.
- `NFR-PERF-002`: Quick Plan arm P95 dưới 1 giây trên server khi dependency nội bộ khỏe.
- `NFR-PERF-003`: Fixture 500 fills hoàn thành import/accounting P95 dưới 2 phút; UI cập nhật progress ít nhất mỗi 5 giây.
- `NFR-PERF-004`: Dashboard cho 10.000 fills tải usable content P95 dưới 2 giây trên test profile.

### Reliability

- `NFR-REL-001`: Monthly service availability target 99,5%, loại planned maintenance được thông báo.
- `NFR-REL-002`: Background jobs at-least-once nhưng business effects phải idempotent.
- `NFR-REL-003`: RPO tối đa 24 giờ, RTO tối đa 8 giờ trong MVP pilot.
- `NFR-REL-004`: Không publish Weekly Lab với mixed/partial core accounting cohort. Context outage không chặn các section không phụ thuộc context: report publish với context section `UNAVAILABLE` và exclusion/coverage counters, rồi chỉ được bổ sung bằng report version mới; job retry an toàn.

### Accessibility and responsive behavior

- `NFR-A11Y-001`: Các flow chính đạt WCAG 2.2 AA trong automated checks và manual keyboard test.
- `NFR-A11Y-002`: Hỗ trợ viewport từ 360 x 640 tới desktop; không có text/control overlap.
- `NFR-A11Y-003`: Mọi status không chỉ truyền đạt bằng màu; icon-only button có accessible name và tooltip khi cần.

### Compatibility

- `NFR-COMP-001`: Hỗ trợ hai major version mới nhất của Chrome, Edge, Safari và Firefox tại release date.
- `NFR-COMP-002`: Decimal và timestamp không phụ thuộc browser locale.

Security/privacy NFR nằm trong `TP-SEC`; các NFR đó có cùng mức bắt buộc.

## 9. Analytics instrumentation

First-party event phải theo append-only `product_analytics_event_v1` trong `TP-LAB`, có stable event ID, direct workspace ownership, trusted server time, idempotency/dedup và exact payload allowlist. Event không được chứa note text, symbol, user-defined setup label, raw filename/CSV, email hoặc screenshot URL. Typed `source_record_key_json` phải bằng exact canonical recordKey, kể cả composite revision key; nó chỉ tồn tại trong first-party store/export của chính workspace. Projection tới external analytics dùng stored `product_analytics_external_v1` bytes: bỏ toàn bộ source/internal ID và exact time, chỉ gửi closed scalar payload, UTC date và processor-specific workspace/actor/event pseudonym trong immutable generation ≤30 ngày. Retry không recompute sau rotation; account-deletion event không ra ngoài và chỉ có exact suppression receipt nội bộ; normal ANALYTICS_PURGE và per-generation deletion inventory phải có exact absence evidence.

Required event types là `onboarding_completed`, `plan_armed`, `plan_proof_resolved`, `import_previewed`, `import_completed`, `episode_closed`, `review_completed`, `file_selected`, `insight_rendered`, `measurement_abandoned`, `weekly_lab_opened`, `weekly_review_completed`, `export_completed` và `account_deletion_requested`. UX benchmark phải đóng tới exact `product_measurement_run_v1` header/state prefix và registered timeout marker theo `LAB-013`: run tuple phân biệt `PRACTICE|MEASURED`, terminal giữ closed abandoned/invalid reason, valid duration dùng một uninterrupted monotonic client clock, TIMEOUT occurrence dùng immutable deadline và visibility dùng later guarded commit; không suy timing từ arrival order hoặc deferred event materializer.

WorkspaceProductMetricSnapshot là tenant-owned và exportable. InternalAggregateProductMetricSnapshot nhiều workspace chỉ chứa aggregate value/count/reason với minimum privacy cohort, không member key/business ID và không đi vào workspace export. Workspace deletion phải atomically tạo `internal_aggregate_cohort_retirement_v1` cho mọi cohort key chứa workspace trước khi xóa member mapping; publish dưới key đã retire bị chặn. North-star vẫn replay độc quyền theo `verified_review_week_rate_v1` trong `TP-ACC`, không lấy external analytics làm source.

## 10. Error and recovery rules

- User-facing error có stable error code, message tiếng Việt và retryability.
- Validation error gắn trực tiếp với field/row; không chỉ dùng toast chung.
- Async job failure giữ last successful artifact và hiển thị freshness.
- Retry không yêu cầu upload lại nếu file còn trong retention window.
- Không hiển thị stack trace, provider response hoặc internal identifier nhạy cảm.
- Support correlation ID được phép hiển thị và không chứa workspace/user ID dạng raw.

## 11. Copy policy

Allowed:

- “Trong 34 episode đã quan sát, setup A có median R cao hơn setup B.”
- “Các episode có rule breach đi cùng net result thấp hơn trong mẫu này.”
- “Dữ liệu còn ít; kết quả chỉ để khám phá.”

Forbidden:

- “Rule breach này gây ra khoản lỗ.”
- “Regime này sẽ giúp setup thắng.”
- “Đây là cơ hội mua.”
- “Bạn nên tăng position size.”

Mọi generated copy phải đi qua cùng policy, không có ngoại lệ cho AI.

## 12. Traceability

Mỗi pull request triển khai phải ghi requirement IDs được tác động. Một requirement chỉ được đánh done khi:

1. implementation tồn tại;
2. acceptance test tương ứng pass;
3. observability cần thiết tồn tại;
4. security/privacy review cần thiết pass;
5. docs và algorithm/schema version được cập nhật nếu contract thay đổi.
