# TradeProof - Import and Accounting Contract

- **Document ID:** `TP-ACC`
- **Document version:** 1.0.0
- **Trang thai:** `FROZEN_MVP_V1`
- **Phien ban contract:** `binance_spot_trade_history_csv_v1`
- **Cap nhat:** 2026-08-27
- **Pham vi:** Binance Spot, mot workspace, mot trading account, long-only, import bang file CSV

Tai lieu nay la nguon su that cho importer, reconciliation, plan matching, `TradeEpisode` va cac metric tai chinh cua MVP. Khi code, test hoac UI mau thuan voi tai lieu nay, tai lieu nay duoc uu tien cho den khi co phien ban contract moi.

Tat ca code identifier, enum value, error code va ten cot trong tai lieu la ASCII va phan biet hoa-thuong dung nhu da ghi. Moi so tien va so luong duoc tinh bang decimal; cam dung binary floating point.

## 1. Quyet dinh MVP da dong bang

1. Venue duy nhat la `BINANCE`; product type duy nhat la `SPOT`.
2. Chi ho tro vi the `LONG`. Khong co short, margin, leverage, borrow cost, funding hoac perpetual.
3. Moi workspace co dung mot `TradingAccount`; account do co `venue = BINANCE`, `product_type = SPOT` va `reporting_currency = USDT`.
4. Moi bo ba `(workspace_id, trading_account_id, instrument_id)` co toi da mot `TradeEpisode` o trang thai `OPEN`.
5. Nguon fill duy nhat la file Binance Spot Trade History CSV theo contract v1 o muc 3. Khong co API key, private API, live sync, read-only sync hoac generic CSV mapper.
6. `NormalizedFill` la su kien bat bien. `TradeEpisode` va accounting ledger la projection co version, co the replay tu fill.
7. P&L, fee conversion, episode grouping va metric do deterministic code tinh. LLM khong duoc tinh, sua hoac dien gia tri thieu.
8. Moi timestamp duoc luu UTC. Workspace luu timezone IANA da user xac nhan, default de de xuat la `Asia/Ho_Chi_Minh`; UI/report dung timezone do nhung khong ghi de timestamp goc.
9. Moi instrument MVP bat buoc co `quote_asset = USDT`. Import pair co quote asset khac USDT bi quarantine; khong co cross-currency P&L trong MVP.

### 1.1. Owned version identifiers

TP-ACC so huu cac identifier sau; code, migration, export, fixture va TP-PLAN phai dung dung literal, khong tao alias nhu `fill-v1`, `episode-v1` hoac `accounting-v1`:

| Domain | Exact identifier | Noi persist bat buoc |
|---|---|---|
| Import adapter/header | `binance_spot_trade_history_csv_v1` | `ImportBatch.contract_version`, canonical signature va export manifest |
| Import preview schema | `import_preview_v1` | `ImportPreview.preview_schema_version` va preview-summary hash basis |
| Staged fill schema | `staged_fill_v1` | `StagedFill.staged_fill_schema_version` |
| Normalized fill schema | `normalized_fill_v1` | `NormalizedFill.fill_schema_version` |
| Setup preset schema | `setup_preset_v1` | `SetupPresetRevision.schema_version` va frozen plan reference |
| Setup label key | `setup_label_key_v1` | `SetupPresetRevision.label_normalizer_version` |
| Plan checklist schema | `plan_checklist_v1` | `SetupPresetRevision` va `TradePlanRevision.checklist_schema_version` |
| Episode grouping/projection | `episode_projection_v1` | `TradeEpisodeProjection.projection_algorithm_version` |
| Plan proof | `plan_proof_v1` | `TradeEpisodeProjection.plan_proof_rule_version` va moi `PlanMatchResolution` |
| Fee conversion | `fee_conversion_v1` | `FeeConversion.algorithm_version` |
| Ledger/accounting | `wac_episode_v1` | `AccountingLedgerEntry.algorithm_version` va projection totals |
| Metric dictionary | `metrics_v1` | moi financial/adherence `MetricSnapshot.algorithm_version` |
| North-star | `verified_review_week_rate_v1` | north-star `MetricSnapshot.metric_version` |

`projection_version`, `revision_no` va `conversion_version` la sequence cua record/projection, khong phai algorithm identifier. Moi API/export tra artifact phai tra ca record version va exact algorithm/schema identifier lien quan.

## 2. Ngoai pham vi

- Binance Futures, margin, lending, staking, funding, interest va liquidation.
- Nhieu account trong mot workspace, transfer giua account, deposit/withdrawal va wallet balance reconciliation.
- Tax lot, tax report, FIFO/LIFO cho thue va realized gain cua tai san dung de tra fee.
- Order lifecycle, canceled order, open order va intent suy ra tu order.
- Sua file CSV tren server, tu do map cot hoac doan venue/symbol.
- Gia fee tu venue khac, gia hien tai, gia do nguoi dung tu nhap ma khong co audit event.
- Tach mot fill SELL vuot vi the thanh phan close va phan short.
- API import du lieu giao dich. Market bar cong khai, neu co, chi la input da luu cho fee conversion/context; no khong phai account sync.
- Opening balance, inventory co truoc import, deposit/withdrawal dung de bu position va transfer asset. Tap CSV phai chua du BUY lich su de replay tu zero inventory; neu khong, SELL dau tien bi quarantine va MVP yeu cau upload them lich su.
- `MFE`, `MAE`, volatility-normalized return va intratrade/account-equity drawdown.

## 3. Binance Spot Trade History CSV v1

### 3.1. File envelope

Importer chi chap nhan file thoa tat ca dieu kien:

| Thuoc tinh | Contract |
|---|---|
| Encoding | UTF-8, co the co mot BOM `EF BB BF` o dau file |
| Delimiter | Dau phay `,` |
| Quote | Dau nhay kep theo RFC 4180 |
| Line ending | `LF` hoac `CRLF` |
| Header | Dung mot dong, dung thu tu tai muc 3.2 |
| Blank line | Duoc bo qua neu tat ca cell rong; khong tinh vao denominator |
| Gioi han MVP | Maximum inclusive: `file_size_bytes <= 20 * 1024 * 1024` va `data_rows <= 100,000`; byte/row dau tien vuot limit reject `FILE_TOO_LARGE`/`TOO_MANY_ROWS` truoc durable row admission |

Parser MUST dung CSV parser tuan RFC 4180, khong `split(',')`. Sau khi parser bo quote va importer bo BOM o cell dau, danh sach header phai bang chinh xac danh sach ben duoi. Khong trim, doi case, chap nhan alias hay chap nhan cot thua. Header sai lam Upload `REJECTED` tai preview, khong tao ImportPreview/ImportBatch va khong row nao duoc import.

### 3.2. Accepted header

```csv
Date(UTC),Pair,Side,Price,Executed,Amount,Fee
```

Binance xac nhan nguoi dung co the export spot trade history, nhung tai lieu cong khai khong dong bang schema cot cua file web export. Vi vay ten `binance_spot_trade_history_csv_v1` la adapter contract do TradeProof so huu, da doi chieu voi approved fixture; no khong phai cam ket rang moi bien the export Binance deu co schema nay. Neu Binance thay header, importer fail `HEADER_MISMATCH` va mot contract version moi phai duoc review. Khong mo rong v1 bang heuristic.

| Cot | Quy tac v1 | Vi du |
|---|---|---|
| `Date(UTC)` | `yyyy-MM-dd HH:mm:ss` hoac `yyyy-MM-dd HH:mm:ss.SSS`; chuoi khong co offset va luon duoc hieu la UTC | `2026-08-27 09:15:03.127` |
| `Pair` | Binance symbol viet hoa, khong separator; phai resolve dung mot import-supported historical window trong pinned `InstrumentCatalogVersion` | `BTCUSDT` |
| `Side` | Chi `BUY` hoac `SELL` | `BUY` |
| `Price` | Decimal duong, quote asset tren mot base asset | `117250.12000000` |
| `Executed` | Decimal duong noi lien asset code; asset phai bang base asset | `0.00120000BTC` |
| `Amount` | Decimal duong noi lien asset code; asset phai bang quote asset | `140.70014400USDT` |
| `Fee` | Decimal khong am noi lien asset code; asset co the la base, quote hoac third asset | `0.00000300BNB` |

Grammar cho `Price`:

```text
CANONICAL_DECIMAL := ("0" | [1-9][0-9]{0,19})("."[0-9]{1,18})?
```

Grammar cho `Executed`, `Amount`, `Fee`:

```text
ASSET_CODE := [A-Z0-9]{2,20}
ASSET_AMOUNT := CANONICAL_DECIMAL immediately followed by ASSET_CODE
```

`CANONICAL_DECIMAL` la grammar khong am, khong phai validation duong. Sau parse, `Price`, `Executed` va `Amount` bat buoc > 0; `Fee` bat buoc >= 0 va van phai co asset code khi bang 0. Moi CSV numeric field co toi da 20 chu so phan nguyen va 18 chu so phan le, vua `DECIMAL(38,18)`. Khong chap nhan leading zero ngoai literal `0`, so am, dau `+`, scientific notation, space, thousands separator, `NaN` hoac `Infinity`.

Syntax sai la `INVALID_DECIMAL`. Chuoi dung hinh thuc nhung vuot 20 integer digits/18 fractional digits, hoac gia tri derived khong vua target `DECIMAL(38,18)`, la `DECIMAL_OVERFLOW`; cam truncate, wrap hoac silent round source value.

`Amount` la gia tri quote authoritative cho cash flow va cost basis. `Price` la du lieu audit. Importer tinh:

```text
computed_amount_quote = price_quote_per_base * executed_qty_base
amount_tolerance = 10 ^ (-quote_precision)
```

Neu `abs(gross_amount_quote - computed_amount_quote) > amount_tolerance`, row bi quarantine voi `AMOUNT_PRICE_MISMATCH`. Khong tu thay `Amount` bang phep nhan.

### 3.3. Timestamp precision

- Chuoi co `.SSS` tao `source_timestamp_precision = MILLISECOND` va interval `[t, t + 1 ms)`.
- Chuoi khong co phan le tao `source_timestamp_precision = SECOND`, canonical `executed_at = t.000Z` va interval `[t, t + 1 second)`.
- Khong duoc tu them do chinh xac ma source khong co.
- `source_time_start` la dau interval; `source_time_end_exclusive` la cuoi interval.
- Moi phep chung minh "plan co truoc fill" phai so voi `source_time_start`, khong so voi thoi diem import.
- Neu interval cua BUY va SELL giao nhau va thu tu co the lam thay doi episode/accounting, cac row lien quan o `SEQUENCE_AMBIGUOUS` cho den khi co `ImportResolution`.

Quan he source-time bat buoc:

```text
fill_a provably_before fill_b
iff fill_a.source_time_end_exclusive <= fill_b.source_time_start
```

Hai interval giao nhau la incomparable theo source clock. Producer tao mot deterministic linearization nhu sau:

1. Giu moi quan he `provably_before`; manual resolution khong duoc dao quan he nay.
2. Voi tap incomparable ma moi permutation hop le cho cung episode boundary, position, cost basis va gross ledger delta, sap `dedup_key ASC`.
3. Neu co it nhat mot permutation lam doi episode assignment, first/closing fill, position transition, cost basis hoac gross ledger delta, cac proven-new `NormalizedFill` trong ambiguous group giu `ACCOUNTING_PENDING` voi `SEQUENCE_AMBIGUOUS` va chua co active allocation. Moi fill den sau tren cung account/instrument ma allocation phu thuoc state sau group tao mot dependent suffix va cung giu `ACCOUNTING_PENDING`/unallocated; producer khong duoc bo qua gap roi resume projection. Instrument khac khong bi block.
4. `ImportResolution.action = SET_SEQUENCE` phai dung exact payload o muc 5.4 va liet ke moi row/fill trong group dung mot lan. Thu tu do chi resolve cac interval incomparable; sau resolution producer replay tu fill som nhat trong group qua toan dependent suffix, tao allocation/sequence/accounting projection moi va atomically publish neu tat ca invariant pass. Terminal ImportRow/ImportBatch disposition/counters giu nguyen import-time history; current resolution/projection phan anh ket qua moi. Projection active cu, neu co, giu nguyen cho den luc publish thanh cong.

`source_row_number` chi dung truy vet, khong duoc coi la timestamp cua san. Import time, worker arrival order va database insertion order khong tham gia linearization.

### 3.4. Instrument resolution

`Instrument` la public stable identity, persisted truoc hoac trong cung transaction voi lan publish dau:

```text
instrument_id
venue = BINANCE
product_type = SPOT
venue_symbol
base_asset
quote_asset
created_at
```

`instrument_id` la opaque ID; unique `(venue, product_type, venue_symbol)`. Mapping symbol/assets bat bien. `created_at` la trusted publish-transaction timestamp va bang `recorded_at` cua InstrumentCatalogPublishEvent dau tien tham chieu instrument; no khong duoc regenerate khi catalog doi.

`InstrumentCatalogVersion` la snapshot versioned cua cac Binance Spot symbol duoc MVP ho tro. Moi row co exact schema:

```text
instrument_id
catalog_version
venue = BINANCE
product_type = SPOT
venue_symbol
base_asset
quote_asset
base_precision
quote_precision
valid_from
valid_to_exclusive nullable
import_supported
plan_enabled
source = BINANCE_PUBLIC_SPOT_METADATA
source_retrieved_at
content_sha256
published_at
```

`MarketConversionCatalogVersion` row co exact schema:

```text
catalog_version
venue_symbol
base_asset
quote_asset
purpose = FEE_CONVERSION_ONLY
valid_from
valid_to_exclusive nullable
conversion_supported
source = BINANCE_PUBLIC_SPOT_METADATA
source_retrieved_at
content_sha256
published_at
```

Moi catalog family co durable append-only publish stream rieng. `InstrumentCatalogPublishEvent` va `MarketConversionCatalogPublishEvent` cung exact shape:

```text
catalog_publish_event_id
event_sequence                 positive integer
catalog_version
event_type = PUBLISH
recorded_at
content_sha256
```

Sequence bat dau 1, contiguous trong tung family, allocate duoi family lock; `(event_sequence)` va `(catalog_version)` unique trong family, `recorded_at` nondecreasing. Publish atomically inserts the complete immutable version row set, any first-seen Instrument headers, and one event; moi row `published_at = event.recorded_at`. Current catalog tai as-of `T` la event co greatest `event_sequence` trong cac event `recorded_at <= T`, khong dung opaque ID tie-break.

Row `content_sha256` la lowercase SHA-256 cua RFC 8785 exact object. Instrument row basis la `{ "baseAsset": str, "basePrecision": int, "catalogVersion": str, "importSupported": bool, "instrumentId": id, "planEnabled": bool, "productType": "SPOT", "quoteAsset": "USDT", "quotePrecision": int, "source": "BINANCE_PUBLIC_SPOT_METADATA", "sourceRetrievedAt": canonical-rfc3339-ms, "validFrom": canonical-rfc3339-ms, "validToExclusive": canonical-rfc3339-ms-or-null, "venue": "BINANCE", "venueSymbol": str }`. Market-conversion row basis la `{ "baseAsset": str, "catalogVersion": str, "conversionSupported": bool, "purpose": "FEE_CONVERSION_ONLY", "quoteAsset": str, "source": "BINANCE_PUBLIC_SPOT_METADATA", "sourceRetrievedAt": canonical-rfc3339-ms, "validFrom": canonical-rfc3339-ms, "validToExclusive": canonical-rfc3339-ms-or-null, "venueSymbol": str }`. `published_at` va hash field khong nam trong row basis.

Publish-event `content_sha256` hash exact object `{ "catalogFamily": "INSTRUMENT"|"MARKET_CONVERSION", "catalogVersion": str, "rows": [{ "contentSha256": hash, "recordKey": object }] }`; rows sort theo export/source key `(catalog_version, venue_symbol, valid_from)` va recordKey co exact member names do. Event ID, sequence va timestamp khong nam trong content basis. Empty version, duplicate key/hash, partial row set hoac reuse version voi bytes khac bi reject.

Moi record trong trading/import `InstrumentCatalogVersion` bat buoc co `quote_asset = USDT`; validator cua family nay tu choi record quote khac. Auxiliary `MarketConversionCatalogVersion` theo rule rieng o muc 3.4: exactly one side la USDT, nen inverse row co `base_asset = USDT` va quote khac USDT la hop le. Import `Pair` phai match chinh xac mot Instrument `venue_symbol`; khong tach symbol bang suffix heuristic vi cac asset code co the mo ho. Importer chon dung mot row thoa:

```text
row.import_supported = true
and row.valid_from <= fill.source_time_start
and fill.source_time_end_exclusive <= coalesce(row.valid_to_exclusive, +infinity)
```

Khong co symbol thi `UNKNOWN_INSTRUMENT`; co symbol nhung khong import-supported thi `INSTRUMENT_NOT_IMPORT_SUPPORTED`; source interval khong nam tron trong dung mot non-overlapping validity window thi `INSTRUMENT_VALIDITY_AMBIGUOUS`. Symbol resolve duoc nhung quote khac USDT bi quarantine `UNSUPPORTED_QUOTE_ASSET`. `plan_enabled` chi dieu khien plan revision moi; no khong ngan historical import trong validity window.

Read-only auxiliary `MarketConversionCatalogVersion` la catalog rieng cho official Binance Spot public market bars va khong phai import/trading `InstrumentCatalogVersion` cua plan/fill/episode. No chi chap nhan pair co mot ben la USDT, gom direct `fee_asset + USDT` hoac inverse `USDT + fee_asset`; record co `purpose = FEE_CONVERSION_ONLY` va khong the duoc import thanh user trade. Vi du `USDTTRY` trong inverse fixture khong noi long invariant `NormalizedFill.quote_asset = USDT`.

Moi catalog version va moi row trong version la immutable; cam UPDATE/DELETE. Trong mot version, `(venue_symbol, valid_from)` unique va validity windows cua cung symbol khong duoc overlap. Stable `Instrument` identity la unique `(BINANCE, SPOT, venue_symbol)`; moi catalog version/window cua cung symbol bat buoc reuse cung `instrument_id`, `base_asset` va `quote_asset`. Catalog publish fail `INSTRUMENT_IDENTITY_CONFLICT` neu doi mapping; MVP khong split identity/episode key am tham khi symbol duoc reuse.

Active pointer chi chuyen bang publish event sequence tren. Rename, precision/validity change, delist hay them symbol phai tao version moi; version cu duoc retain cho replay/export. Active version moi phai carry forward moi reliable historical import window; delist dat `plan_enabled = false` nhung giu `import_supported = true` cho window cu, nen trade cu van import duoc. Chi verified metadata correction moi duoc tao version moi voi `import_supported = false` va reason audit. MVP khong suy source-time validity tu current listing status.

Moi `ImportBatch` pin active `instrument_catalog_version` mot lan truoc row parsing va moi row trong batch resolve bang version do. Historical fill/plan dung version da persist, khong re-resolve bang active pointer hien tai. Reprocess explicit cung bytes duoi catalog version moi la batch moi, co the resolve row tung `UNKNOWN` o version cu, va khong duoc alias exact-file ve batch version cu.

`MarketConversionCatalogVersion` cung immutable va co active pointer rieng. Moi pair row co `valid_from`, `valid_to_exclusive nullable` va `conversion_supported`; moi active version moi phai carry forward reliable historical windows, va delist khong xoa/disable window cu. Fee worker pin active conversion catalog version tai luc tao conversion, nhung pair eligibility duoc xet theo historical bar interval, khong theo current listing status. Fee conversion retain exact version/pair metadata da dung; market pair bi delist sau do khong invalidate conversion/projection cu. Day la public metadata snapshot lifecycle, khong phai trading-account API sync.

## 4. Import pipeline va idempotency

### 4.1. Cac buoc bat buoc

CSV import co hai pha tach biet. `UPLOAD_VALIDATE` la pha preview duy nhat; no khong duoc tao `ImportBatch`, `ImportRow`, `StagedFill`, `NormalizedFill`, allocation, episode, ledger, ContextSnapshot hay MetricSnapshot.

Pha preview chay theo thu tu:

1. Lock exact TP-SEC Upload/lease, recheck Workspace `ACTIVE`, captured deletion generation, `upload_kind = CSV`, exact object version/hash/size va raw-read deadline.
2. Pin active `instrument_catalog_version`, validate size, UTF-8, CSV syntax va exact header, roi streaming preflight moi non-blank data row bang adapter da pin.
3. Tinh read-only canonical signature/duplicate estimate trong bounded memory; khong reserve dedup key va khong ghi row/business projection.
4. Neu file-level validation fail, atomically append Upload `REJECT`, terminalize `UPLOAD_VALIDATE` voi `UPLOAD_REJECTED`, tao khong `ImportPreview`/`ImportBatch`, va bat dau raw purge. Neu pass, atomically append Upload `ACCEPT`, tao immutable `ImportPreview` + sequence-1 CREATE event, emit first-party `import_previewed` theo TP-LAB va terminalize cung fence voi `UPLOAD_ACCEPTED`. ProductAnalyticsEvent nay la minimized validation instrumentation duy nhat duoc phep; no khong phai TP-ACC trading/weekly metric va khong duoc chua/join raw row values.

`ImportPreview` chi la sanitized command artifact. `preview_summary_json` co exact RFC 8785 shape, cam unknown/missing member:

```json
{
  "adapterContractVersion": "binance_spot_trade_history_csv_v1",
  "dataRows": 500,
  "duplicateEstimateRows": 3,
  "errorRows": [{
    "columnName": "Price",
    "errorCode": "INVALID_DECIMAL",
    "sourceRowNumber": 17
  }],
  "errorsTruncated": false,
  "instrumentCatalogVersion": "...",
  "invalidRows": 1,
  "sourceTimeEndExclusive": "2026-08-27T10:00:01.000Z",
  "sourceTimeStart": "2026-08-27T09:00:00.000Z",
  "symbols": ["BTCUSDT"],
  "validRows": 499
}
```

`dataRows = validRows + invalidRows`, `0 <= duplicateEstimateRows <= validRows`, va moi counter la exact nonnegative integer. `symbols` la sorted unique uppercase symbols cua rows parse duoc; hai source-time field cung null iff khong co row co valid timestamp, con lai la min lower bound va max exclusive upper bound. `errorRows` chua toi da 100 invalid rows dau theo `sourceRowNumber`; moi row chi co first failure theo order column `Date(UTC), Pair, Side, Price, Executed, Amount, Fee`, roi cross-field rules o muc 3.2. `columnName` la exact offending column hoac null cho row rule; `errorCode` la closed safe code o muc 11. `errorsTruncated = (invalidRows > count(errorRows))`. Khong member nao chua raw cell, row bytes, filename, exception text, user ID hay provider locator. Duplicate estimate la advisory snapshot tai `created_at`; IMPORT phai recompute dedup duoi account lock.

`preview_summary_sha256 = SHA256(UTF8("tradeproof_import_preview_v1\u0000") || RFC8785(preview_summary_json))`, lowercase hex. Candidate expiry la `min(created_at + 60 minutes, Upload.forced_purge_at - 120 minutes)`; `UPLOAD_VALIDATE` chi duoc ACCEPT neu candidate lon hon `created_at`, nguoc lai REJECT `RAW_UPLOAD_RETENTION_DEADLINE`. Deadline bat bien, khong retry/refresh nao gia han. Pha preview co zero business effect ngay ca khi worker retry/crash.

Pha confirmed import bat dau duy nhat boi `ConfirmImport(importPreviewId, previewSummarySha256, idempotencyKey)`. Transaction lock Workspace, preview, Upload/lease va account; recheck `ACTIVE`/current deletion generation, preview summary hash, derived status `READY`, trusted time `< expires_at`, Upload `ACCEPTED`, lease readable va exact hash/size/version. No atomically append preview CONFIRM event, tao mot `ImportBatch` status `UPLOADED`, va tao exact IMPORT `TenantControlJob` + fence + ENQUEUE theo TP-SEC. Khong `ImportRow`, fill, episode, ledger, context hay metric duoc ghi trong transaction confirm. Retry cung key va exact command tra cung preview event, batch va control job; cung key khac byte fail `IMPORT_CONFIRM_IDEMPOTENCY_CONFLICT`; key khac cho preview da confirmed tra existing batch ma khong allocate work sequence.

Sau confirm, IMPORT worker:

1. Recheck exact immutable upload bytes, preview adapter/catalog/hash va batch binding.
2. Tao durable `ImportRow` metadata/hash cho moi non-blank data row, ke ca row loi. Raw file/cell chi nam trong private upload/quarantine object tam thoi.
3. Parse va validate tung field; resolve instrument, tao canonical signature va phan loai duplicate duoi account lock.
4. Tao immutable `NormalizedFill` cho source fact da chung minh la moi, hoac `StagedFill` chi cho multiplicity ambiguous chua biet new/duplicate.
5. Resolve timestamp ordering, replay projection theo instrument, match plan tai opening fill, tao ledger va tinh reconciliation.
6. Commit row hop le theo idempotent transaction. Row quarantine khong sinh `StagedFill` hay `NormalizedFill`.

Loi mot data row khong rollback row hop le khac. File invalid khong tao preview/batch. Mot confirmed batch chi co the `REJECTED` truoc row admission khi exact source binding/read bi mat hoac deterministic revalidation khong khop preview; no phai co stable safe file error va zero row/business effect. Raw upload/quarantine object va raw cell material phai bi purge trong toi da 24 gio theo TP-SEC; database nghiep vu khong retain raw CSV/cell sau do.

### 4.2. Canonical signature

Importer serialize JSON UTF-8 voi key order co dinh sau; decimal duoc canonical hoa bang cach bo zero vo nghia, khong dung exponent, va zero luon la `"0"`:

```json
{
  "contract":"binance_spot_trade_history_csv_v1",
  "trading_account_id":"<uuid>",
  "venue_symbol":"BTCUSDT",
  "source_time":"2026-08-27T09:15:03.127Z",
  "source_timestamp_precision":"MILLISECOND",
  "side":"BUY",
  "price_quote_per_base":"117250.12",
  "executed_qty_base":"0.0012",
  "gross_amount_quote":"140.700144",
  "fee_qty":"0.000003",
  "fee_asset":"BNB"
}
```

```text
canonical_signature = SHA256(canonical_json_bytes)
occurrence_index = vi tri 1-based trong nhom row co cung canonical_signature,
                   theo source_row_number trong file
dedup_key = SHA256(canonical_signature + ":" + decimal(occurrence_index))
```

`trading_account_id` nam trong signature de khong dedup cheo account. `workspace_id` khong can nam trong signature vi MVP rang buoc mot account chi thuoc mot workspace.

### 4.3. Quy tac duplicate

- Exact-file key la `(workspace_id, trading_account_id, contract_version, instrument_catalog_version, file_sha256)`, khong bao gio chi la hash. Cung bytes nhung khac pinned catalog version la explicit reprocess batch moi, khong alias; row tung `UNKNOWN` co the resolve, con row da import van qua row-level dedup. Chi mot original batch co `duplicate_file_of_batch_id = null` lam target. Distinct request tao alias shell ngay; neu original con processing, alias cho ket qua original va khong start parser/accounting worker thu hai.
- File khac, signature chi xuat hien mot lan trong ca tap existing va incoming: row incoming la `DUPLICATE` neu signature da ton tai; neu chua thi `IMPORTED_NEW`.
- Neu signature chua co trong existing data, chap nhan tat ca occurrence slot incoming la fill moi.
- Neu existing va incoming deu co dung mot occurrence, row incoming la `DUPLICATE`.
- Neu existing va incoming co cung multiplicity lon hon mot, map theo `occurrence_index`; moi slot incoming la `DUPLICATE`.
- Neu existing da co signature va multiplicity existing khac multiplicity incoming, toan bo nhom incoming la `DUPLICATE_MULTIPLICITY_AMBIGUOUS`. Khong duoc tu suy ra slot nao duplicate hay fill moi vi CSV khong co exchange trade id.
- `ImportResolution.action = ACCEPT_AS_NEW` tao `dedup_key` moi tu `resolution_id` theo exact framing o muc 5.4; `MARK_DUPLICATE` lien ket row voi exact fill existing. Ca hai la audit event bat bien.

`IMPORTED_NEW` va `DUPLICATE_MULTIPLICITY_AMBIGUOUS` la intermediate dedup decision/reason, khong phai final `ImportRow.status`. Mapping bat buoc:

- `IMPORTED_NEW` tao immutable `NormalizedFill`; final status la `RECONCILED` neu fill duoc allocate va own ledger complete, hoac `ACCOUNTING_PENDING` neu fee/sequence/replay cua chinh fill chua complete.
- `DUPLICATE_MULTIPLICITY_AMBIGUOUS` tao immutable `StagedFill` khong co dedup key va map final import-time `ACCOUNTING_PENDING` voi `error_code` cung ten. Audited resolution tao current disposition nhung khong viet lai terminal ImportRow/batch counters.
- Proven duplicate resolve den canonical target row/fill. Target `RECONCILED` cho incoming `DUPLICATE`; target `ACCOUNTING_PENDING` cho incoming `ACCOUNTING_PENDING` voi cung pending reason; target `QUARANTINED` cho incoming `QUARANTINED` voi cung sanitized error. Khong suy disposition tu episode quality chung.

Quy tac tren uu tien khong lam mat fill that. V1 khong hua dedup hoan hao cho hai giao dich co moi field va timestamp giong nhau, vi source CSV khong co unique trade id.

### 4.4. Backfill va replay

Fill duoc replay theo thoi gian su kien, khong theo thoi gian upload. Them fill cu hon fill da co se tao projection version moi cho instrument.

- Neu vung bi anh huong chua co `Review` hoac manual episode correction, system replay tu fill accepted som nhat va supersede projection cu.
- Neu replay co the doi boundary hoac plan link cua episode da co `Review`, row moi o `HISTORICAL_REPLAY_CONFLICT`; khong publish projection moi cho den khi user xac nhan `CONFIRM_REPLAY`.
- Projection cu khong bi xoa. UI va metric chi doc projection version active.
- `episode_id` duoc resolve tu durable `TradeEpisode` header theo exact UUIDv5 rule tai muc 5.4. Projection moi khong tu tao identity. Episode khong bi anh huong giu nguyen ID; replay doi opening fill tuan theo quy tac SAME_ID/SPLIT/MERGE tai muc 5.4.

Truoc `CONFIRM_REPLAY`, server tao immutable tenant-owned `ReplayConflictPreview`:

```text
replay_conflict_id
workspace_id
based_on_active_projection_refs_json
proposed_projection_refs_json
source_input_digest
episode_mapping_json
impact_json
created_at
expires_at
```

`based_on_active_projection_refs_json` la sorted unique array exact TP-EXP record keys `{ "episode_id": id, "projection_version": int }`. `proposed_projection_refs_json` la sorted unique array theo same key order, moi entry co exact shape:

```json
{
  "accountingQuality": "COMPLETE",
  "associatedPlanRecordKey": null,
  "associatedPlanRevisionRecordKey": null,
  "averageCostQuotePerBase": null,
  "closedFillRecordKey": null,
  "feeConversionRecordKeys": [],
  "fillRecordKeys": [{ "fill_id": "..." }],
  "firstFillRecordKey": { "fill_id": "..." },
  "frozenPlanRevisionRecordKey": null,
  "grossRealizedPnlQuote": "0",
  "knownFeeQuote": "0",
  "ledgerAlgorithmVersion": "wac_episode_v1",
  "netRealizedPnlQuote": "0",
  "openCostBasisQuote": "100",
  "planProofReasonCode": "NO_ELIGIBLE_CANDIDATE",
  "planCandidateRecordKeys": [],
  "planProofBasisSha256": "...",
  "planProofRuleVersion": "plan_proof_v1",
  "planProofStatus": "UNMATCHED",
  "positionQtyBase": "1",
  "proposalDigestSha256": "...",
  "projectionAlgorithmVersion": "episode_projection_v1",
  "recordKey": { "episode_id": "...", "projection_version": 2 },
  "state": "OPEN"
}
```

Enums/null coupling va decimal canonical strings equal would-be TradeEpisodeProjection. `fillRecordKeys` are exact ordered allocation sequence, nonempty/unique; first key equals element 1, CLOSED requires non-null closing key equal final close-to-zero fill and OPEN requires null. Fee-conversion keys are sorted by `(fill_id, conversion_version, fee_conversion_id)` and equal every conversion version selected by the proposed ledger. Plan candidate keys are sorted TradePlan record keys; `planProofBasisSha256` hashes exact would-be `plan_proof_basis_json`. Algorithm/version literals equal the proposed projection. Plan keys/proof obey section 6. Each `proposalDigestSha256` hashes RFC 8785 of the same entry with only that member omitted. These are preview-local proposed keys, not foreign keys to an already-published TradeEpisodeProjection; every embedded fill/conversion/plan key must resolve same-workspace at preview creation/export.

`episode_mapping_json` is an array of exact `{ "mappingOrdinal": int, "newProposalRecordKeys": [recordKey...], "oldProjectionRecordKeys": [recordKey...], "relation": enum }`. Entries sort by unsigned RFC 8785 bytes of first old key or empty sentinel, then first new key or empty sentinel, then relation; `mappingOrdinal` is contiguous 1..N after sorting. Cardinality is exact: SAME_ID_CHANGED 1/1 with same episode ID; SPLIT 1/2+; MERGE 2+/1; REMOVED 1/0; ADDED 0/1. Old arrays partition based-on refs exactly once and new arrays partition proposed refs exactly once.

`impact_json` has exact member set:

```json
{
  "eligibilityImpacts": [{
    "newProjectionRecordKey": {},
    "priorEligibilityEventRecordKeys": [{ "episode_metric_eligibility_event_id": "..." }],
    "requiresDecision": true
  }],
  "planImpacts": [{
    "change": "CHANGED",
    "newPlanRevisionRecordKey": null,
    "newProjectionRecordKey": {},
    "oldPlanRevisionRecordKey": null,
    "oldProjectionRecordKey": {}
  }],
  "reviewImpacts": [{
    "outcome": "RECONFIRM_REQUIRED",
    "reviewRecordKey": { "review_id": "..." },
    "reviewRevisionRecordKey": { "review_revision_id": "..." },
    "sourceProjectionRecordKey": {},
    "targetProjectionRecordKey": {}
  }],
  "sourceFillRecordKeys": [{ "fill_id": "..." }],
  "triggerImportRowRecordKeys": [{ "import_row_id": "..." }]
}
```

All arrays sort by their primary record-key RFC 8785 bytes and contain no duplicate. Eligibility has exactly one entry per proposed projection, `priorEligibilityEventRecordKeys` sorted by event sequence and possibly empty; confirmation therefore makes an explicit EXCLUDE/RESTORE decision for every proposed projection. Plan `change` is `UNCHANGED | CHANGED | REMOVED | ADDED` with old/new nullability matching the label. Review `outcome` is `RECONFIRM_REQUIRED` only for same episode ID/new version and requires target key; `HISTORICAL_ONLY` for split/merge/removed identity and requires target null. Review keys/revisions and every old/source/import/eligibility/plan/fill key are exact same-workspace canonical keys and must close; proposed/target keys close to the embedded proposed array until publish.

`source_input_digest` is lowercase SHA-256 of RFC 8785 exact object `{ "basedOnActiveProjectionRefs": based_on_active_projection_refs_json, "episodeMappings": episode_mapping_json, "impact": impact_json, "proposedProjections": proposed_projection_refs_json }`. `created_at` is trusted creation commit time and `expires_at = created_at + 24 hours`. Raw CSV/cell content is forbidden.

`CONFIRM_REPLAY` ImportResolution has exact `payload_json` `{ "eligibilityDecisions": [{ "action": "EXCLUDE"|"RESTORE", "projectionRecordKey": {} }], "previewSourceInputDigest": hash, "replayConflictId": id }`; decisions sort by proposed key and partition every proposed key once, with no extra/missing key. Outer ImportResolution stores actor/reason/idempotency. Command locks affected account/instrument, rejects expired preview, recomputes current active refs, proposal entries/mappings/impacts/digest and requires byte equality before publish; stale mismatch fails `STALE_REPLAY_PREVIEW`. Final published keys/boundary/plan/accounting summary must equal proposal entries, and eligibility events are appended atomically from decisions. Retry same key/payload returns same resolution; changed payload conflicts.

UI bat buoc hien mapping, old/new opening/closing fills, plan proof/link, Review outcome va eligibility decision warning. Khi replay publish:

- Review cua old/superseded episode history khong bi xoa hay auto-copy.
- Neu active projection moi giu cung `episode_id` nhung doi `projection_version`, existing Review atomically thanh `RECONFIRM_REQUIRED`; latest ReviewRevision van tro old projection va current review/adherence metric loai no cho den `ReviseEpisodeReview` tren new version.
- Neu split/merge/opening-fill change tao `episode_id` moi, new active episode khong co Review. User phai complete Review moi; old Review chi duoc resolve trong historical report qua old episode/projection ref.
- Historical MetricSnapshot/WeeklyReport giu exact old projection + ReviewRevision pair; current artifact khong reuse old revision ngam dinh.

## 5. Data entities

Moi append-only state/decision stream trong TP-ACC co field `event_sequence` phai bat dau tai 1, contiguous khong gap theo aggregate scope duoc neu, va duoc allocate cung transaction duoi aggregate lock. Database unique aggregate scope + sequence; `recorded_at` la trusted millisecond timestamp va phai nondecreasing theo sequence. Current/as-of replay loc `recorded_at <= T` roi chon greatest `event_sequence`; opaque event ID chi la identity/sort sau khi state da resolve, khong bao gio tie-break semantic state.

### 5.1. Ownership entities

| Entity | Field bat buoc | Invariant |
|---|---|---|
| `Workspace` | `workspace_id`, `owner_user_id`, `lifecycle_state`, `deletion_guard_generation`, `deletion_id`, `timezone`, `created_at`, `deleting_at`, `deleted_at` | Exact identity/lifecycle/null/generation contract thuoc `TP-SEC:workspace_deletion_v1`; MVP mot owner; `timezone` la IANA ID da user xac nhan; moi query bat buoc scope theo `workspace_id` |
| `WorkspaceOwnerProfile` | `owner_user_id`, `workspace_id`, `created_at` | 1:1 voi Workspace/owner; stable identity header, khong chua auth credential |
| `WorkspaceOwnerProfileRevision` | `owner_profile_revision_id`, `owner_user_id`, `workspace_id`, `revision_no`, `display_name`, `locale`, `recorded_at`, `idempotency_key` | Append-only current product profile; `(workspace_id,revision_no)` va `(workspace_id,idempotency_key)` unique |
| `TradingAccount` | `trading_account_id`, `workspace_id`, `venue`, `product_type`, `reporting_currency`, `account_label`, `created_at` | Unique `workspace_id`; `venue = BINANCE`; `product_type = SPOT`; `reporting_currency = USDT` |
| `Instrument` | `instrument_id`, `venue`, `product_type`, `venue_symbol`, `base_asset`, `quote_asset`, `created_at` | Unique `(venue, product_type, venue_symbol)`; identity/assets immutable va reused qua moi catalog version |
| `InstrumentCatalogVersion` | cac field tai muc 3.4 | `(catalog_version, venue_symbol, valid_from)` unique; windows cung symbol khong overlap |
| `MarketConversionCatalogVersion` | `catalog_version`, `venue_symbol`, `base_asset`, `quote_asset`, `purpose`, `valid_from`, `valid_to_exclusive`, `conversion_supported` | Immutable read-only official market-pair metadata; windows cung symbol khong overlap; `purpose = FEE_CONVERSION_ONLY`; dung mot ben la USDT; khong duoc tham chieu boi plan/fill/episode |

Workspace bootstrap inserts Workspace, WorkspaceOwnerProfile va profile revision 1 atomically. `display_name` nullable; neu non-null thi trim 1-80 Unicode scalars, plain text. Initial MVP `locale = vi-VN`; future locale phai la supported canonical BCP-47 tag. Profile edit appends next revision under workspace lock; current at as-of `T` is greatest `revision_no` with `recorded_at <= T`. `TP-EXP` exports the stable header, every visible immutable WorkspaceOwnerProfileRevision and a current-profile pointer; any convenience current projection uses exact `{ owner_user_id, workspace_id, display_name, locale, created_at = header.created_at, updated_at = selected_revision.recorded_at }`. Thus retry at a pinned cutoff and round-trip retain revision IDs/numbers/idempotency/history. Identity-provider claims, email, token, session, password/recovery material never enter either entity or projection.

### 5.2. Import entities

`ImportPreview` la immutable sanitized artifact cua CSV `UPLOAD_VALIDATE`, khong phai mot partial batch:

```text
ImportPreview
import_preview_id
workspace_id
trading_account_id
source_upload_id
preview_schema_version          import_preview_v1
adapter_contract_version        binance_spot_trade_history_csv_v1
instrument_catalog_version
source_sha256
file_size_bytes
safe_display_filename
preview_summary_json
preview_summary_sha256
created_at
expires_at

ImportPreviewStateEvent
import_preview_state_event_id
workspace_id
import_preview_id
event_sequence                  1..2 contiguous
event_type                      CREATE | CONFIRM | ABANDON
recorded_at
actor_type                      SYSTEM | USER
actor_user_id                   nullable; non-null iff USER
idempotency_key
command_payload_sha256
import_batch_id                 nullable; non-null iff CONFIRM
```

Header va CREATE event sequence 1 commit atomically voi exact Upload ACCEPT va `UPLOAD_VALIDATE` terminal marker; `created_at = CREATE.recorded_at = Upload.accepted_at`. Header values copy the locked same-workspace TradingAccount, Upload hash/size and pinned catalog/adapter. `(workspace_id,source_upload_id)` va `(workspace_id,import_preview_id)` unique; a CSV Upload has at most one preview. MVP deliberately sets `safe_display_filename = "trade-history.csv"`; it never persists the client path/name, and this exact literal is copied to legacy `ImportBatch.original_filename`. Preview JSON/hash and expiry obey section 4.1 and never mutate.

`command_payload_sha256` is lowercase SHA-256 of RFC 8785 `{ "importPreviewRecordKey": { "import_preview_id": id }, "operation": "CREATE"|"CONFIRM"|"ABANDON", "previewSummarySha256": hash, "sourceUploadRecordKey": { "upload_id": id } }`. CREATE `idempotency_key` exactly copies UPLOAD_VALIDATE operation idempotency; CONFIRM/ABANDON copy the user command key. `(workspace_id,idempotency_key)` is unique in this stream table. Retry requires identical event type and digest or fails `IMPORT_PREVIEW_IDEMPOTENCY_CONFLICT`.

Logical status is derived, not updated on the header: `CONFIRMED` iff the sole sequence-2 event is CONFIRM; `ABANDONED` iff it is ABANDON; otherwise `READY` iff trusted now is before `expires_at` and Upload remains `ACCEPTED` with readable ACTIVE lease, else `EXPIRED`. Sequence 2 is optional and mutually exclusive. `AbandonImportPreview(importPreviewId,previewSummarySha256,idempotencyKey)` locks the same rows, requires READY and appends ABANDON; retry same exact payload returns it, changed payload conflicts. ABANDON or derived EXPIRED starts the existing UPLOAD_PURGE chain immediately. Confirm and abandon after expiry, after raw read denial/PURGE, cross-workspace, or with a stale hash fail with zero batch/work/business writes. Sanitized preview header/events are non-exported temporary command metadata and are deleted child-first no later than 30 days after CONFIRMED/ABANDONED/EXPIRED; a confirmed preview is eligible only after its ImportBatch copied proof is validated and the IMPORT chain is terminal. This local terminal-row expiration does not create a new tenant work type.

`ImportBatch`:

```text
import_batch_id
workspace_id
trading_account_id
source_upload_id
upload_idempotency_key
source_import_preview_id
source_preview_schema_version     import_preview_v1
source_preview_summary_sha256
confirmed_at
contract_version = binance_spot_trade_history_csv_v1
instrument_catalog_version
original_filename
file_sha256
file_size_bytes
uploaded_at
started_at
finished_at
status
file_error_code nullable
data_rows nullable
reconciled_rows
duplicate_rows
accounting_pending_rows
quarantined_rows
reconciliation_rate nullable
duplicate_file_of_batch_id nullable
```

`source_upload_id` bat buoc composite FK den exact TP-SEC Upload cung workspace, `upload_kind = CSV`, co immutable ACCEPT/ImportPreview CREATE proof, `source_sha256 = file_sha256` va `byte_size = file_size_bytes`; mismatch fail truoc parser/business write. `source_import_preview_id`, schema/hash, contract/catalog, file hash/size va safe filename copy exact confirmed preview; `uploaded_at = Upload.created_at = RECEIVE.recorded_at`, while `confirmed_at = CONFIRM.recorded_at`. The copied preview-proof fields are immutable provenance, not a foreign key to temporary ImportPreview rows, and remain sufficient after those rows expire. Provider object key/lease khong nam trong ImportBatch. Legacy field name `upload_idempotency_key` stores exactly the `ConfirmImport.idempotencyKey`; database enforces unique `(workspace_id, upload_idempotency_key)`, unique `(workspace_id,source_import_preview_id)`, and partial unique exact-file key on an original batch with `duplicate_file_of_batch_id IS NULL`. While preview rows exist, CONFIRM is the authoritative creation-time link; afterward the batch's copied proof is authoritative and MUST match the IMPORT payload/terminal evidence. Retry same confirm key/payload returns exact batch/control job; changed payload conflicts. Mot distinct confirmed preview tao toi da mot alias batch cua request do. `duplicate_file_of_batch_id` phai FK den original batch co cung workspace, trading account, adapter contract, pinned catalog version va file hash; cross-tenant/account alias bi cam. Alias chi finalize counters/rows sau khi original terminal.

`finished_at` null truoc terminal va duoc gan bang trusted server commit timestamp trong cung transaction ghi terminal status, final counters/rate va durable row dispositions. Vi vay product-metric visibility cua mot admitted batch la exact `finished_at`; worker start, upload time hoac source trade time khong thay the field nay.

Alias batch khong reparse raw file va khong tao `StagedFill`, `NormalizedFill`, allocation, conversion hay ledger. Trong mot transaction, no clone mot durable `ImportRow` metadata record cho moi original `ImportRow`: same `source_row_number`, `raw_row_sha256`, sanitized diagnostic/provenance; `normalized_fill_id = staged_fill_id = null`. Original `RECONCILED` hoac `DUPLICATE` map alias row thanh `DUPLICATE` voi `duplicate_of_fill_id` tro canonical fill; original `ACCOUNTING_PENDING` map cung status/reason va canonical duplicate ref neu co, con unresolved multiplicity candidate clone safe pending reason voi ca ba fill ref null; original `QUARANTINED` map cung status/sanitized error va no fill. Alias `data_rows` bang original row count, counters/rate tinh tu cloned statuses va thoa field-level balance. Neu original la pre-admission `REJECTED`, alias clone stable file error, zero row/counter, `data_rows`/rate null.

`ImportRow`:

```text
import_row_id
workspace_id
import_batch_id
source_row_number
raw_row_sha256
status
error_code nullable
error_detail_json nullable
staged_fill_id nullable
normalized_fill_id nullable
duplicate_of_fill_id nullable
created_at
```

Field-level balance chi ap dung sau khi batch pass file envelope, encoding, CSV syntax va exact header, va row materialization bat dau. Khi do `data_rows = count(ImportRow)` non-null; moi `ImportRow` dai dien dung mot non-blank data row, con header/blank row khong duoc tinh. Batch post-validation phai thoa exact balance:

```text
data_rows = reconciled_rows
            + duplicate_rows
            + accounting_pending_rows
            + quarantined_rows
```

Oversize, invalid UTF-8, CSV syntax hoac header fail tai `UPLOAD_VALIDATE`, tao Upload REJECT nhung zero ImportPreview/ImportBatch/ImportRow/fill/business row. Mot confirmed `ImportBatch` `REJECTED` truoc admission chi danh cho stale/missing exact source binding hoac deterministic revalidation mismatch; no cung co stable safe file error, zero `ImportRow`, `StagedFill`, `NormalizedFill`, allocation va ledger, bon disposition counter bang 0, `data_rows = reconciliation_rate = null`. Exact balance tren khong ap dung cho pre-admission `REJECTED` batch.

`raw_row_sha256` tinh tren exact source-row bytes khi private object con ton tai. Durable `ImportRow` khong co raw cell/value field. `error_code` va `error_detail_json` cung null cho `RECONCILED | DUPLICATE`; ca hai non-null cho `ACCOUNTING_PENDING | QUARANTINED`. Error detail la exact `import_row_error_detail_v1` object, khong admit extra/missing member:

```json
{
  "columnIndex": 4,
  "columnName": "Price",
  "diagnosticSha256": "...",
  "expectedCode": "POSITIVE_DECIMAL",
  "observedCountCapped": null,
  "observedLengthCapped": 32769,
  "ruleCode": "DECIMAL_OVERFLOW",
  "truncated": true
}
```

`columnName` is null for a row/group/accounting rule, otherwise exactly `Date(UTC) | Pair | Side | Price | Executed | Amount | Fee` and `columnIndex` is its matching 1-based position `1..7`; the two fields are jointly null/non-null. `ruleCode` equals the row's exact non-null `error_code`. `expectedCode` is one of `EXACT_COLUMN_COUNT | UTC_TIMESTAMP | KNOWN_INSTRUMENT | BUY_OR_SELL | POSITIVE_DECIMAL | NONNEGATIVE_DECIMAL | MATCHING_ASSET | PRICE_AMOUNT_TOLERANCE | BASE_FEE_NOT_EXCEED_EXECUTED | UNIQUE_MULTIPLICITY | PARTIAL_ORDER | LONG_ONLY_POSITION | AVAILABLE_FEE_CONVERSION | REPLAY_CONFIRMATION | LEDGER_INVARIANT`; null is forbidden. Mapping is exact:

| `ruleCode` | `columnName` | `expectedCode` |
|---|---|---|
| `COLUMN_COUNT_MISMATCH` | null | `EXACT_COLUMN_COUNT` |
| `INVALID_TIMESTAMP` | `Date(UTC)` | `UTC_TIMESTAMP` |
| `INVALID_DECIMAL`, `DECIMAL_OVERFLOW` | exact offending numeric column | `NONNEGATIVE_DECIMAL` only for `Fee`, otherwise `POSITIVE_DECIMAL` |
| `INVALID_SIDE` | `Side` | `BUY_OR_SELL` |
| `UNKNOWN_INSTRUMENT`, `INSTRUMENT_NOT_IMPORT_SUPPORTED`, `INSTRUMENT_VALIDITY_AMBIGUOUS`, `UNSUPPORTED_QUOTE_ASSET` | `Pair` | `KNOWN_INSTRUMENT` |
| `EXECUTED_ASSET_MISMATCH` | `Executed` | `MATCHING_ASSET` |
| `AMOUNT_ASSET_MISMATCH` | `Amount` | `MATCHING_ASSET` |
| `AMOUNT_PRICE_MISMATCH` | `Amount` | `PRICE_AMOUNT_TOLERANCE` |
| `BUY_BASE_FEE_EXCEEDS_EXECUTED` | `Fee` | `BASE_FEE_NOT_EXCEED_EXECUTED` |
| `DUPLICATE_MULTIPLICITY_AMBIGUOUS` | null | `UNIQUE_MULTIPLICITY` |
| `SEQUENCE_AMBIGUOUS` | null | `PARTIAL_ORDER` |
| `SELL_WITHOUT_OPEN_POSITION`, `SELL_EXCEEDS_POSITION` | null | `LONG_ONLY_POSITION` |
| `FEE_CONVERSION_UNAVAILABLE` | `Fee` | `AVAILABLE_FEE_CONVERSION` |
| `HISTORICAL_REPLAY_CONFLICT` | null | `REPLAY_CONFIRMATION` |
| `LEDGER_INVARIANT_FAILED` when materialized on an affected pending row | null | `LEDGER_INVARIANT` |

No other error code may be persisted on `ImportRow`; file/catalog/API/review/command errors in the global table are not row diagnostics.

The two observed fields are safe magnitudes only, never values. `observedLengthCapped` is null when length is irrelevant, else exact Unicode-scalar/byte length required by the named rule capped to integer `32769`, where `32769` means `>= 32769`. `observedCountCapped` is null when count is irrelevant, else exact row/group/count magnitude capped to integer `100001`, where `100001` means `>= 100001`. At least one may be null and both may be null. `truncated = true` iff either underlying magnitude exceeded its cap; otherwise false. It never means a raw diagnostic string was retained.

`diagnosticSha256` is lowercase SHA-256 of RFC 8785 bytes of the same seven safe members with `diagnosticSha256` omitted, plus domain-separation framing: `SHA256(UTF8("tradeproof_import_error_detail_v1\u0000") || RFC8785(object_without_hash))`. It is not a hash of a cell, row, filename, user text or exception message. Parser/library exceptions are mapped to the closed rule/expected codes before persistence; unknown exception text is discarded. Source bytes remain immutable in private storage only during the TP-SEC retention window, then are purged; row hash, row number, closed diagnostic, disposition and normalized/staged references remain for audit.

Import-time fill-reference coupling is closed and becomes immutable with terminal batch publication. `RECONCILED` requires only `normalized_fill_id`; `DUPLICATE` requires only `duplicate_of_fill_id`; `QUARANTINED` requires all three null. `ACCOUNTING_PENDING/DUPLICATE_MULTIPLICITY_AMBIGUOUS` on an original batch requires only `staged_fill_id`, even after later disposition; a pending row for its own canonical source fact requires only `normalized_fill_id`; a proven duplicate mirroring a pending canonical target requires only `duplicate_of_fill_id`; and an exact-file alias of a multiplicity row requires all three null and is identified by non-null batch `duplicate_file_of_batch_id`. No other terminal status/error/ref combination is valid. `normalized_fill_id` and `duplicate_of_fill_id` composite-reference exact same-workspace NormalizedFill rows; `staged_fill_id` composite-references the exact same-workspace StagedFill whose originating row is this row. Current resolution fate is read from StagedFillDisposition/ImportResolution; queue membership is `disposition absent`, never inferred by mutating the row.

`StagedFill` exists only while multiplicity ambiguity means the row is not yet proven new or duplicate:

```text
StagedFill
staged_fill_id
workspace_id
trading_account_id
import_batch_id
import_row_id
source_row_number
import_contract_version          binance_spot_trade_history_csv_v1
staged_fill_schema_version       staged_fill_v1
instrument_catalog_version
venue                            BINANCE
product_type                     SPOT
instrument_id
venue_symbol
base_asset
quote_asset                      USDT
side
executed_at
source_timestamp_precision
source_time_start
source_time_end_exclusive
price_quote_per_base
executed_qty_base
gross_amount_quote
fee_qty
fee_asset
canonical_signature
occurrence_index
created_at

StagedFillDisposition
staged_fill_disposition_id
workspace_id
staged_fill_id
resolution_id
outcome                          ADMITTED_AS_NEW | DISCARDED_AS_DUPLICATE
normalized_fill_id               nullable; non-null iff ADMITTED_AS_NEW
duplicate_of_fill_id             nullable; non-null iff DISCARDED_AS_DUPLICATE
recorded_at
```

Every StagedFill field is immutable and copied from the one parsed candidate; it has unique `(workspace_id,import_row_id)` and intentionally has no `dedup_key` or mutable admission flag. At most one disposition exists by unique `(workspace_id,staged_fill_id)` and unique `(workspace_id,resolution_id)`. ADMITTED_AS_NEW atomically creates one immutable NormalizedFill by exact field copy plus the resolution-derived dedup key; DISCARDED_AS_DUPLICATE creates no NormalizedFill and pins the canonical target. The same transaction inserts ImportResolution + disposition but never changes terminal ImportRow refs/status/error or ImportBatch counters/rate/status. A crash exposes all resolution effects or none. StagedFill and its optional disposition are retained/exported with the Workspace so an unresolved queue and either resolution fate round-trip without raw CSV.

`NormalizedFill`:

```text
fill_id
workspace_id
trading_account_id
import_batch_id
import_row_id
source_row_number
import_contract_version = binance_spot_trade_history_csv_v1
fill_schema_version = normalized_fill_v1
instrument_catalog_version
venue = BINANCE
product_type = SPOT
instrument_id
venue_symbol
base_asset
quote_asset
side
executed_at
source_timestamp_precision
source_time_start
source_time_end_exclusive
price_quote_per_base
executed_qty_base
gross_amount_quote
fee_qty
fee_asset
canonical_signature
occurrence_index
dedup_key
created_at
```

Invariant:

- `dedup_key` unique trong `trading_account_id`.
- NormalizedFill chi duoc tao khi source fact da proven new; no immutable ngay khi insert va khong co mutable admission state. Sequence/replay/fee pending la allocation/accounting state cua canonical fill, khong doi fill thanh staged candidate.
- `quote_asset` bat buoc bang `USDT` va bang `TradingAccount.reporting_currency`.
- `instrument_catalog_version` bat buoc bang pinned version cua `ImportBatch`; `instrument_id`, symbol, assets va precision khop immutable row trong version do.
- Moi fill tro ve dung mot originating `ImportRow` co status `RECONCILED` hoac `ACCOUNTING_PENDING`; alias/`DUPLICATE` row khong tao fill moi. Source bytes khong bi sua khi private object con duoc retain, nhung khong duoc persist vinh vien trong business database.
- Asset, symbol, product, dedup va moi numeric/time field khong duoc doi sau insert.
- Fill khong bi hard-delete rieng le. Xoa workspace xoa toan bo theo privacy contract; truoc do audit van truy vet duoc.

### 5.3. Plan entities

`SetupPreset` la stable aggregate do Planning/TP-ACC so huu:

```text
setup_id
workspace_id
preset_kind                  USER_DEFINED | SYSTEM_OTHER
created_at
```

`SetupPresetRevision`:

```text
setup_revision_id
workspace_id
setup_id
revision_no
schema_version               setup_preset_v1
label
label_key
label_normalizer_version     setup_label_key_v1
checklist_schema_version     plan_checklist_v1
checklist_json
recorded_at
recorded_by_user_id
content_sha256
```

`SetupPresetStateEvent`:

```text
setup_state_event_id
workspace_id
setup_id
event_sequence                positive integer
setup_revision_id            nullable cho ARCHIVE/REACTIVATE
event_type                   CREATE | REVISE | ARCHIVE | REACTIVATE
recorded_at
actor_user_id
idempotency_key
```

Moi row tenant-owned co direct immutable `workspace_id`; child dung composite FK `(workspace_id, setup_id)` va `(workspace_id, setup_revision_id)`. `(workspace_id, setup_id, revision_no)`, `(workspace_id, setup_revision_id)`, `(workspace_id, setup_id, event_sequence)` va `(workspace_id, idempotency_key)` unique. Aggregate/revision/event append-only; current revision va `ACTIVE | ARCHIVED` la projection tu greatest visible `event_sequence` trong setup stream.

`label` trim xong dai 1-60 Unicode scalar values va giu exact user text. `label_key` la ket qua trim Unicode White_Space, NFC, roi full default case-fold theo Unicode 15.1; version/Unicode data dong bang boi `setup_label_key_v1`. Moi current `ACTIVE` preset trong workspace unique `label_key`; archived preset khong giu slot, nhung REACTIVATE fail `SETUP_LABEL_CONFLICT` neu key da duoc dung. UI render escaped text; label khong duoc parse thanh instruction hay taxonomy.

`checklist_json` la array 0-10 item theo exact order, moi item co shape `{ "item_id": "<opaque-stable-id>", "order": <1-based-integer>, "label": "...", "required": true|false }`. `order` phai la exact set `1..N`; `item_id` unique trong aggregate history, label trim dai 1-120 Unicode scalar values. Revision tiep theo giu `item_id` neu item van cung semantic; item da remove khong duoc tai su dung ID cho item moi. Hash cover exact schema/normalizer versions, label/key va ordered checklist.

`SetupPresetRevision.content_sha256` la lowercase SHA-256 cua RFC 8785 exact object sau; timestamp la canonical UTC RFC 3339 milliseconds va khong member nao duoc omit/null:

```json
{
  "checklist_json": [],
  "checklist_schema_version": "plan_checklist_v1",
  "label": "...",
  "label_key": "...",
  "label_normalizer_version": "setup_label_key_v1",
  "recorded_at": "...",
  "recorded_by_user_id": "...",
  "revision_no": 1,
  "schema_version": "setup_preset_v1",
  "setup_id": "...",
  "setup_revision_id": "...",
  "workspace_id": "..."
}
```

Workspace bootstrap atomically tao dung mot `SYSTEM_OTHER` aggregate co stable opaque ID, revision 1 label `Khác`, checklist rong va state ACTIVE. `SYSTEM_OTHER` khong duoc revise, archive hoac delete rieng; no luon ton tai va API expose semantic code `OTHER`. V1 cho toi da 50 current ACTIVE `USER_DEFINED` preset moi workspace; vuot limit fail `SETUP_LIMIT_REACHED` truoc write.

Commands `CreateSetupPreset`, `ReviseSetupPreset`, `ArchiveSetupPreset` va `ReactivateSetupPreset` lay authenticated actor, expected current revision/state va idempotency key. Create/revise atomically insert revision + event; archive/reactivate insert event. Stale base fail `STALE_SETUP_REVISION`, conflict label fail `SETUP_LABEL_CONFLICT`, retry cung key/payload tra cung effect, cung key payload khac fail `IDEMPOTENCY_CONFLICT`. Plan arm chi nhan current ACTIVE revision cung workspace tai transaction time; no freeze exact `setup_id`, `setup_revision_id`, label va checklist. Rename/archive sau do khong mutate plan/Review/report cu.

`TradePlan`:

```text
trade_plan_id
workspace_id
trading_account_id
instrument_id
direction = LONG
state
created_at
expires_at
consumed_by_episode_id nullable
```

`TradePlanRevision`:

```text
trade_plan_revision_id
workspace_id
trade_plan_id
revision_no
based_on_revision_id nullable
recorded_at
recorded_by_user_id
instrument_catalog_version
setup_id
setup_revision_id
setup_label_snapshot
checklist_schema_version
thesis
entry_zone_low
entry_zone_high
initial_stop_price
planned_risk_quote
planned_risk_asset
confidence_score
checklist_json
content_sha256
```

`setup_revision_id` tham chieu mot immutable current ACTIVE setup revision thuoc cung workspace; `setup_label_snapshot`, `checklist_schema_version` va `checklist_json` copy exact revision do tai `recorded_at` va khong thay doi khi preset duoc rename/archive. `setup_id`, `setup_revision_id`, snapshot label va schema version bat buoc non-null khi arm; system preset `OTHER` cung co stable ID/revision. Historical metric co the group theo stable `setup_id`, nhung display/export label phai lay snapshot tu frozen plan revision, khong join label hien tai.

`TradePlanRevision.content_sha256` la lowercase SHA-256 cua RFC 8785 exact object sau. Decimal fields are canonical decimal strings, checklist is the exact ordered array, and every member is required:

```json
{
  "checklist_json": [],
  "checklist_schema_version": "plan_checklist_v1",
  "confidence_score": 3,
  "entry_zone_high": "101",
  "entry_zone_low": "99",
  "initial_stop_price": "95",
  "instrument_catalog_version": "...",
  "planned_risk_asset": "USDT",
  "planned_risk_quote": "100",
  "based_on_revision_id": null,
  "recorded_at": "...",
  "recorded_by_user_id": "...",
  "revision_no": 1,
  "setup_id": "...",
  "setup_label_snapshot": "...",
  "setup_revision_id": "...",
  "thesis": "...",
  "trade_plan_id": "...",
  "trade_plan_revision_id": "...",
  "workspace_id": "..."
}
```

`entry_zone_low`, `entry_zone_high` va `initial_stop_price` chap nhan exact `CANONICAL_DECIMAL` grammar o muc 3.2, phai > 0, toi da 20 chu so phan nguyen/18 chu so phan le va persist bang `DECIMAL(38,18)` ma khong round. Canonical plan/API/export string la plain decimal value sau khi bo insignificant fractional trailing zero va dau cham neu fraction rong; zero duy nhat la `"0"`. Vi du `"101.00"` duoc chap nhan nhung persist/hash/return thanh `"101"`; day la lexical canonicalization, khong doi numeric value. Leading zero, sign, exponent, space, thousands separator, `NaN`/`Infinity`, syntax sai hoac zero fail `PLAN_VALIDATION_FAILED`; value dung syntax nhung vuot range fail `DECIMAL_OVERFLOW`. Cross-field arm validation van bat buoc `entry_zone_low <= entry_zone_high` va `initial_stop_price < entry_zone_low`. Quote precision chi la presentation; no khong quantize plan source value.

`thesis` la JSON null khi client bo trong hoac input chi gom Unicode White_Space. Neu non-null, server bo Unicode 15.1 White_Space o hai dau, giu byte sequence UTF-8 con lai khong NFC/case-fold, va bat buoc 1..1,000 Unicode scalar values; unpaired surrogate/invalid UTF-8, NUL hoac C0/C1 control ngoai TAB/LF bi reject `PLAN_VALIDATION_FAILED`. Persist/hash dung exact trimmed string va khong truncate. Moi create/revise/AI confirmation deu dung cung rule nay.

`based_on_revision_id` null exactly for revision 1 created by ARM; revision N>1 points to the same-plan current revision N-1 and is validated under the plan lock. A gap, stale/cross-plan base or noncontiguous number fails `STALE_PLAN_REVISION` with zero effect.

`TradePlanRevision.instrument_catalog_version` la active catalog version tai trusted `recorded_at`. Exact instrument row phai co `plan_enabled = true` va `valid_from <= recorded_at < coalesce(valid_to_exclusive, +infinity)` de arm; later active-pointer move/delist khong mutate revision, proof hay historical metric. Revision/plan moi sau delist bi reject cho den khi `plan_enabled = true` trong mot catalog version moi.

API contract cua TP-PRD dung `planned_risk_usdt`; mapping duy nhat la:

```text
request/response planned_risk_usdt == TradePlanRevision.planned_risk_quote
TradePlanRevision.planned_risk_asset == USDT
```

`planned_risk_usdt` dung grammar rieng `("0" | [1-9][0-9]{0,7})("."[0-9]{1,8})?`, sau parse bat buoc > 0: toi da 8 chu so phan nguyen va 8 chu so phan le theo TP-PRD, persisted bang `DECIMAL(16,8)`. No dung cung plain-decimal canonicalization bo insignificant fractional trailing zero nhu ba plan price; khong round numeric value de vua range, va overflow tra `DECIMAL_OVERFLOW`. API khong nhan asset rieng va khong convert currency. Persistence/canonical export dung cap `planned_risk_quote` + `planned_risk_asset`, convenience API dung `planned_risk_usdt`. Neu asset persisted khac USDT hoac hai value khong bang nhau, revision khong duoc arm.

`PlanStateEvent`:

```text
plan_state_event_id
workspace_id
trade_plan_id
event_sequence
event_type
armed_revision_id nullable
consumed_by_episode_id nullable
recorded_at
actor_type
actor_user_id nullable
idempotency_key
```

Every direct authenticated `ArmPlan | RevisePlan | CancelPlan` command owns one non-exported plan-command idempotency receipt:

```text
PlanCommandReceipt
plan_command_receipt_id
workspace_id
trade_plan_id
command_type                    ARM | REVISE | CANCEL
idempotency_key
request_sha256
result_revision_id              nullable
result_state_event_id           nullable
recorded_at
```

`(workspace_id,idempotency_key)` is unique across all three direct plan commands. `trade_plan_id` and every non-null result have composite same-workspace ownership. Result coupling is closed: ARM has both result IDs; REVISE has only `result_revision_id`; CANCEL has only `result_state_event_id`. ARM request hash is lowercase SHA-256 of RFC 8785 `{ "accountRecordKey": { "trading_account_id": id }, "commandType": "ARM", "confidenceScore": int, "entryZoneHigh": canonical-decimal, "entryZoneLow": canonical-decimal, "expiryDurationSeconds": normalized-int, "initialStopPrice": canonical-decimal, "instrumentId": id, "plannedRiskUsdt": canonical-decimal, "setupRevisionId": id, "thesis": string-or-null }`. `normalized-int` is the supplied validated value or default `86400`; client null is never hashed. REVISE uses exact `{ "basedOnRevisionId": id, "commandType": "REVISE", "confidenceScore": int, "entryZoneHigh": canonical-decimal, "entryZoneLow": canonical-decimal, "initialStopPrice": canonical-decimal, "plannedRiskUsdt": canonical-decimal, "setupRevisionId": id, "thesis": string-or-null, "tradePlanId": id }`. CANCEL uses `{ "commandType": "CANCEL", "expectedEventSequence": int, "tradePlanId": id }`. Unknown/extra members reject before receipt creation. A transcript confirmation may append a TradePlanRevision through TP-SEC's full-replacement validator, but that authenticated mutation is idempotently owned only by its `AiConfirmationCommandIntent`/`AiConfirmationCommandReceipt`; it creates no second `PlanCommandReceipt` and cannot use ARM/REVISE/CANCEL namespace or bypass current-base/lifecycle validation.

V1 has no persisted TradePlan draft/header before ARM. `ArmPlan` receives the complete request represented by the exact ARM hash basis above, not a draft ID. ARM locks Workspace/account/instrument, materializes expired predecessors, normalizes and validates every request field, and atomically creates the TradePlan header already ARMED, revision 1, ARM event sequence 1 and receipt. `TradePlan.created_at = TradePlanRevision.recorded_at = ARM.recorded_at = receipt.recorded_at`; `armed_revision_id` equals revision 1 and `expires_at` uses that same time. REVISE requires effective ARMED plus exact current `basedOnRevisionId`, appends the next revision and receipt at one trusted time, and never changes `expires_at` or writes a state event. CANCEL requires effective ARMED plus exact event-sequence base, then appends CANCEL+receipt atomically. Same idempotency key and byte-identical normalized request returns the recorded plan/revision/event; any changed byte or command type fails `PLAN_COMMAND_IDEMPOTENCY_CONFLICT`. A failure writes none of header/revision/event/receipt, so abandoned/invalid client form state leaves no TradePlan row.

Moi revision va state event append-only. Cam UPDATE/DELETE. Database unique `(workspace_id, trade_plan_id, event_sequence)`; moi command, ke ca system CONSUME trong projection transaction, allocate next contiguous sequence duoi plan lock. `armed_revision_id` non-null iff `event_type = ARM` and is a composite same-workspace FK to that plan's revision 1; other events require null. `consumed_by_episode_id` non-null iff `event_type = CONSUME`, va la composite same-workspace FK den durable `TradeEpisode` header; voi `ARM | CANCEL | EXPIRE` no bat buoc null. CONSUME event va first projection/header cua episode co the insert trong cung transaction, nhung khong duoc tro toi proposed/non-persisted ID. Administrative event replay dung greatest visible sequence, con automatic expiry co semantic effective time duy nhat la immutable `TradePlan.expires_at` theo rule ben duoi; `EXPIRE.recorded_at` chi la trusted materialization commit time va khong duoc backdate. `TradePlan.consumed_by_episode_id` bang exact field cua CONSUME event khi effective state `CONSUMED`, null trong moi state khac. `relation_to_first_fill` la gia tri derived trong episode projection/API, khong phai field mutable cua revision. `recorded_at` do server gan tu trusted UTC clock; client timestamp chi co the luu rieng lam metadata va khong tham gia matching.

API field `submittedAt` cua revision bang chinh xac canonical `recorded_at`; khong co client-submitted timestamp thay the. `checklist_json` copy exact ordered array `plan_checklist_v1` tu SetupPresetRevision, gom toi da 10 object `{ "item_id": "<stable-id>", "order": <1-based-integer>, "label": "...", "required": true|false }`; `item_id` unique trong aggregate history va khong duoc tai su dung cho noi dung khac.

`TradePlan.state`/API effective state chi co `ARMED`, `CONSUMED`, `CANCELLED`, `EXPIRED`; `DRAFT` is not a persisted/API plan state in v1. Event terminal/CONSUME co truoc quyet dinh state; neu latest administrative state la ARMED thi effective state la ARMED only while trusted as-of `T < expires_at`, va la EXPIRED tai `T >= expires_at` du `EXPIRE` event chua materialize. `PlanStateEvent.event_type` chi co `ARM`, `CONSUME`, `CANCEL`, `EXPIRE`; `actor_type` chi co `USER`, `SYSTEM`, va system event co `actor_user_id = null`. `idempotency_key` unique theo plan. `consumed_by_episode_id` la denormalized projection tu event `CONSUME`, khong phai source duoc update doc lap.

V1 khong co background PLAN_EXPIRE job. Moi read/state projection derive expiry tu header. Truoc bat ky arm/revise/cancel/match/export-pointer write nao tren cung account/instrument, transaction lock cac plan, va voi moi row con materialized ARMED nhung trusted `now >= expires_at`, insert toi da mot SYSTEM EXPIRE event voi exact idempotency key `plan-expire:<trade_plan_id>:<canonical-expires_at>` va `recorded_at = now`, roi cap nhat materialized state truoc tiep tuc. Retry tra cung event; expiry equality luon thang CANCEL/CONSUME/ARM moi. Vi semantic time la `expires_at`, lazy delay khong doi historical proof, effective state hoac one-armed-plan uniqueness; asynchronous sweeper/outbox cho EXPIRE bi cam.

`PlanEpisodeAssociation` chi dung cho late association:

```text
plan_episode_association_id
workspace_id
episode_id
based_on_projection_version
trade_plan_id
trade_plan_revision_id
association_type = LATE
actor_user_id
reason
idempotency_key
request_sha256
recorded_at
result_projection_version
```

Association append-only, bat buoc do user chu dong tao sau first-fill interval va khong sua plan proof thanh verified. `(workspace_id,idempotency_key)` and `(workspace_id,episode_id,based_on_projection_version)` are unique; the selected revision belongs to the selected plan and all IDs are same-workspace. The request digest is lowercase SHA-256 of RFC 8785 `{ "basedOnProjectionVersion": int, "episodeId": id, "reason": trimmed-string, "tradePlanId": id, "tradePlanRevisionId": id }`. Command locks the active projection and plan, requires exact based-on version with proof UNMATCHED, validates nonempty reason <=500 Unicode scalars and `recorded_at >= E`, then atomically inserts the association, optional idempotent CONSUME and next projection with `result_projection_version = based_on_projection_version + 1`, LATE proof and this association ID. Retry same key/payload returns the same association/projection; changed payload fails `PLAN_ASSOCIATION_IDEMPOTENCY_CONFLICT`, stale base fails `STALE_PLAN_ASSOCIATION`, and every failure has zero effect.

`PlanMatchResolution` ghi nhan user confirmation cho proof `AMBIGUOUS`; no khong thay the `PlanEpisodeAssociation` dung cho `LATE`:

```text
plan_match_resolution_id
workspace_id
episode_id
based_on_projection_version
action
selected_trade_plan_id nullable
selected_trade_plan_revision_id nullable
old_association_json
new_association_json
actor_user_id
reason
idempotency_key
plan_proof_rule_version = plan_proof_v1
recorded_at
```

Record append-only, `recorded_at` do trusted server clock gan va `(workspace_id, idempotency_key)` unique. `old_association_json` va `new_association_json` deu co exact shape `{ "trade_plan_id": <uuid|null>, "trade_plan_revision_id": <uuid|null> }`; server tu projection truoc/sau tao hai field nay, client khong duoc tu khai bao. Moi resolution replay/publish projection version moi voi full allocation/ledger materialization cung deterministic fill order, supersede nhung giu version cu de audit; no khong tao fill/business cash flow moi. Existing Review tu dong resolve `RECONFIRM_REQUIRED` cho den revision moi tham chieu projection version moi.

`Review` va `ReviewRevision` toi thieu:

```text
review_id
episode_id
workspace_id
state
created_at
completed_at

review_revision_id
workspace_id
review_id
revision_no
episode_projection_version
recorded_at
recorded_by_user_id
idempotency_key
exit_reason
exit_reason_taxonomy_version
exit_reason_other_text nullable
rule_breach
breach_taxonomy_version
breach_type_ids
breach_other_text nullable
stop_moved_away
risk_exceeded
required_checklist_results_json
emotion nullable
emotion_taxonomy_version nullable
lesson nullable
content_sha256
```

MVP khong persist Review draft; UI draft chi o client/ephemeral store va khong phai business record. `Review.state` v1 chi co `COMPLETED` va `RECONFIRM_REQUIRED`; state sau chi do projection replay tao, khong phai draft. State la projection derived tu latest ReviewRevision vs active episode projection, khong phai mutable source field doc lap. Database enforce unique `(workspace_id, episode_id)`, unique `(review_id, revision_no)` va unique `(workspace_id, ReviewRevision.idempotency_key)` qua ownership join.

`CompleteEpisodeReview` bat buoc authenticated actor thuoc workspace, target active projection `CLOSED`, request `expected_episode_projection_version` bang active version, chua co Review, payload dat tat ca validation ben duoi va optional screenshot da scan. Mot transaction gan trusted server `created_at = completed_at`, tao `Review(state=COMPLETED)`, `ReviewRevision(revision_no=1, episode_projection_version=expected version, recorded_at=completed_at)` va attachment join. Retry cung idempotency key tra cung Review/revision; command khac sau khi Review ton tai fail `REVIEW_ALREADY_COMPLETED`. Khong co partial Review neu transaction fail.

`ReviseEpisodeReview` bat buoc Review cung workspace, target episode co active `CLOSED` projection, `expected_episode_projection_version` bang active version, `expected_revision_no` bang current max revision, full replacement payload hop le va idempotency key. Mot transaction insert revision `current + 1` voi active projection version va dat state `COMPLETED`; stale revision/projection fail `STALE_REVIEW_REVISION`/`STALE_EPISODE_PROJECTION`. `Review.completed_at` luon la first completion trusted time va khong doi khi revise; revision `recorded_at` la edit time.

Current metric chi coi Review completed khi `Review.state = COMPLETED` va latest revision co `episode_projection_version` bang active episode projection. Historical metric/report dung revision + projection version da snapshot tai `reporting_as_of_at`; replay khong rewrite pair cu.

`ReviewRevisionAttachment`:

```text
review_revision_id
workspace_id
attachment_id
role = SCREENSHOT
ordinal = 1
attachment_content_sha256
created_at
```

Moi revision co 0 hoac 1 screenshot. Attachment phai cung workspace, `state = ACTIVE`, `scan_status = PASSED` va la supported image theo TP-SEC tai transaction time. Join va digest immutable; revise/carry-forward/remove screenshot luon tao ReviewRevision moi voi exact attachment set moi. Retention/deletion co the tombstone binary theo TP-SEC nhung khong xoa historical join/digest; export ghi ro attachment available hay tombstoned. `breach_type_ids` persist sorted by frozen taxonomy `(item_order,item_id)`, not client order.

`ReviewRevision.content_sha256` la lowercase SHA-256 cua RFC 8785 exact object below. Every nullable member is present as JSON null; booleans are JSON booleans; `required_checklist_results_json` is the exact boolean map; `attachments` is empty or one exact immutable join summary:

```json
{
  "attachments": [{
    "attachment_content_sha256": "...",
    "attachment_id": "...",
    "ordinal": 1,
    "role": "SCREENSHOT"
  }],
  "breach_other_text": null,
  "breach_taxonomy_version": "breach_type_v1",
  "breach_type_ids": [],
  "emotion": null,
  "emotion_taxonomy_version": null,
  "episode_projection_version": 1,
  "exit_reason": "TARGET_REACHED",
  "exit_reason_other_text": null,
  "exit_reason_taxonomy_version": "exit_reason_v1",
  "idempotency_key": "...",
  "lesson": null,
  "recorded_at": "...",
  "recorded_by_user_id": "...",
  "required_checklist_results_json": {},
  "review_id": "...",
  "review_revision_id": "...",
  "revision_no": 1,
  "risk_exceeded": false,
  "rule_breach": false,
  "stop_moved_away": false,
  "workspace_id": "..."
}
```

Timestamp is canonical UTC RFC 3339 milliseconds. Hash and attachment joins are written in the same Review transaction; deleting binary later does not change either. Unknown/missing member, client-order breach IDs or a join/hash mismatch fails validation rather than being re-canonicalized silently.

- `breach_taxonomy_version` bat buoc non-null tren completed revision. `breach_type_ids` la canonical API, storage va export field; no la array cac ID unique ton tai trong dung frozen version, khong co alias `breach_type_ids_json`. Neu `rule_breach = false`, array phai rong va `breach_other_text = null`. Neu `rule_breach = true`, array phai co it nhat mot ID; ID `OTHER` trong cung version bat buoc co `breach_other_text` non-blank toi da 500 ky tu. Rename/archive taxonomy khong duoc sua immutable ID/label mapping cua version da tham chieu trong historical export/report.
- `emotion_taxonomy_version` non-null khi va chi khi `emotion` non-null; emotion ID phai ton tai trong frozen version do. Historical report resolve label tu version da persist, khong tu taxonomy active hien tai.
- `stop_moved_away = true` chi khi stop duoc doi xa invalidation theo huong lam tang risk cua long trade; doi stop gan entry/hoa von khong tinh la true.
- `required_checklist_results_json` la object map chinh xac moi required `item_id` trong frozen revision den boolean: `true` nghia la da tuan thu item, `false` nghia la khong tuan thu. Completed Review khong chap nhan missing, null, duplicate hoac extra item ID. Episode khong co verified frozen revision phai dung object rong; checklist result khong duoc dung de nang plan proof.

Cross-field invariant:

- `exit_reason = OTHER` khi va chi khi `exit_reason_other_text` non-blank toi da 500 ky tu; reason khac bat buoc text null.
- `lesson` nullable; neu co thi trim xong phai non-blank va toi da 2,000 ky tu. Validation dem Unicode scalar value, khong dem UTF-16 code unit; vuot gioi han fail `REVIEW_VALIDATION_FAILED`, khong truncate.
- `stop_moved_away = true` khi va chi khi `STOP_MOVED_AWAY` co trong `breach_type_ids`; `risk_exceeded = true` khi va chi khi `RISK_EXCEEDED` co trong IDs.
- Co it nhat mot required checklist result `false` khi va chi khi `CHECKLIST_MISSED` co trong IDs. Episode khong co verified frozen revision co checklist object rong va khong duoc dung ID nay.
- `UNPLANNED_ENTRY` chi hop le khi proof khac `VERIFIED`; no khong tu dong nang/doi proof.
- Bat ky boolean/derived breach condition tren la true thi `rule_breach = true`. Khi `rule_breach = false`, hai boolean phai false, moi required checklist result phai true va breach IDs rong.

Review taxonomy la public immutable source owned by TP-ACC, khong phai bang label hard-code rieng trong exporter. Durable entities:

```text
ReviewTaxonomyVersion
taxonomy_version
taxonomy_type                 EXIT_REASON | BREACH_TYPE | EMOTION
content_sha256
published_at

ReviewTaxonomyItem
taxonomy_version
taxonomy_type                 EXIT_REASON | BREACH_TYPE | EMOTION
item_id
label_vi
item_order                    positive contiguous integer

ReviewTaxonomyPublishEvent
taxonomy_publish_event_id
taxonomy_type                 EXIT_REASON | BREACH_TYPE | EMOTION
event_sequence                positive contiguous integer per taxonomy_type
taxonomy_version
recorded_at
content_sha256
```

Version key `taxonomy_version` unique globally and type immutable; item key `(taxonomy_version, item_id)` unique, `(taxonomy_version, item_order)` unique, order exact `1..N`. Version, complete item set va publish event insert atomically; `published_at = event.recorded_at`. Current version per type la greatest visible event sequence. Version `content_sha256` la lowercase SHA-256 cua RFC 8785 exact object `{ "items": [{ "itemId": id, "itemOrder": int, "labelVi": str }], "taxonomyType": type, "taxonomyVersion": version }`, items sorted by `(item_order,item_id)`. Publish-event hash equals version hash. Event/version reuse with other bytes, empty item set, duplicate ID/order or timestamp-decreasing sequence fails closed.

Review taxonomy initial versions do TP-ACC so huu va dong bang. `item_order` bat dau 1 va tang theo row order hien thi trong moi taxonomy version:

| Taxonomy version | ID | Frozen `label_vi` |
|---|---|---|
| `exit_reason_v1` | `TARGET_REACHED` | `Dat muc tieu` |
| `exit_reason_v1` | `STOP_HIT` | `Cham stop` |
| `exit_reason_v1` | `THESIS_INVALIDATED` | `Luan diem khong con dung` |
| `exit_reason_v1` | `RISK_REDUCTION` | `Giam rui ro` |
| `exit_reason_v1` | `TIME_EXIT` | `Thoat theo thoi gian` |
| `exit_reason_v1` | `OTHER` | `Khac` |
| `breach_type_v1` | `ENTRY_OUTSIDE_ZONE` | `Vao ngoai entry zone` |
| `breach_type_v1` | `STOP_MOVED_AWAY` | `Doi stop xa invalidation` |
| `breach_type_v1` | `RISK_EXCEEDED` | `Vuot planned risk` |
| `breach_type_v1` | `CHECKLIST_MISSED` | `Khong tuan thu checklist` |
| `breach_type_v1` | `UNPLANNED_ENTRY` | `Vao lenh khong co verified plan` |
| `breach_type_v1` | `OTHER` | `Khac` |
| `emotion_v1` | `CALM` | `Binh tinh` |
| `emotion_v1` | `FOCUSED` | `Tap trung` |
| `emotion_v1` | `ANXIOUS` | `Lo lang` |
| `emotion_v1` | `IMPULSIVE` | `Boc dong` |
| `emotion_v1` | `FRUSTRATED` | `That vong` |

Completed Review bat buoc co `exit_reason_taxonomy_version = exit_reason_v1` va `breach_taxonomy_version = breach_type_v1` trong initial release; emotion null thi version null, emotion non-null thi `emotion_taxonomy_version = emotion_v1`. Moi taxonomy version record va ID/label mapping trong version la immutable. Them ID, retire ID hoac doi label tao version moi va audited publish event/sequence; historical ReviewRevision giu IDs + versions cu, validation/replay khong join active taxonomy. Export bat buoc kem exact version va moi item duoc referenced ID/type validation can; no copy fields tu durable entities tren. Behavioral experiment taxonomy khong thuoc TP-ACC.

### 5.4. Episode va ledger entities

`TradeEpisode` la durable identity header, khong phai projection tam:

```text
episode_id
workspace_id
trading_account_id
instrument_id
opening_fill_id
opening_fill_dedup_key
created_at
```

Identity v1 dung UUIDv5 theo RFC 9562 voi namespace literal `7d9478a1-70f4-5e45-8ae7-c0c3a9ba2514`. Name bytes la UTF-8 cua exact string `tradeproof_episode_v1\u0000<trading_account_id>\u0000<instrument_id>\u0000<opening_fill_dedup_key>`, trong do hai ID o lowercase canonical UUID text va dedup key o lowercase 64-character hex; khong co whitespace, BOM hay terminating NUL. `episode_id` la lowercase canonical UUID text cua ket qua. Producer khac namespace, framing, case hoac encoding la contract violation.

`opening_fill_id` composite-FK toi exact same-workspace immutable `NormalizedFill` co cung account/instrument, `side = BUY` va `dedup_key = opening_fill_dedup_key`. Database enforce unique `(workspace_id, episode_id)` va unique `(trading_account_id, instrument_id, opening_fill_dedup_key)`; tuple sau phai recompute dung `episode_id` tren. Header duoc insert atomically voi projection version dau tien. `created_at` la trusted server commit timestamp cua lan insert do, bang `TradeEpisodeProjection.created_at` cua version dau tien, bat bien va khong duoc suy tu fill event time, import time khi export, hay recompute time.

Replay/publish phai resolve header truoc khi gan projection version:

- Neu opening fill khong doi, reuse exact header/`episode_id`; publish `max(projection_version) + 1`. Khong update `opening_fill_*` hoac `created_at`.
- Neu SPLIT, moi resultant episode dung opening BUY fill cua chinh segment. Segment giu old opening fill reuse old header; segment co opening fill khac insert hoac reuse header lich su cua exact tuple.
- Neu MERGE, resultant episode dung earliest opening BUY fill theo deterministic linearization cua merged segment va resolve header cua tuple do. Cac header bi hap thu van ton tai de resolve historical projection/Review/export.
- Neu episode bi REMOVED khoi active replay, khong xoa header hay projection cu. Neu exact tuple xuat hien lai ve sau, reuse header va tang projection version; cam insert identity/header thu hai.
- Mot header chi visible tai as-of `T` khi `created_at <= T`. Export/replay khong duoc fabricate header timestamp tu projection dang active; moi projection phai FK toi mot header da visible khong muon hon chinh `created_at` cua projection.

`TradeEpisodeProjection`:

```text
episode_id
projection_version
projection_algorithm_version = episode_projection_v1
ledger_algorithm_version = wac_episode_v1
workspace_id
trading_account_id
instrument_id
quote_asset = USDT
state
first_fill_id
first_fill_at
first_fill_time_end_exclusive
first_fill_timestamp_precision
closed_fill_id nullable
closed_at nullable
closed_time_end_exclusive nullable
closed_timestamp_precision nullable
associated_plan_id nullable
associated_plan_revision_id nullable
frozen_plan_revision_id nullable
plan_proof_status
plan_proof_reason_code
plan_proof_rule_version = plan_proof_v1
plan_candidate_ids_json
plan_proof_basis_json
plan_proof_resolved_at
late_association_id nullable
plan_match_resolution_id nullable
position_qty_base
open_cost_basis_quote
average_cost_quote_per_base nullable
gross_realized_pnl_quote
known_fee_quote
net_realized_pnl_quote nullable
accounting_quality
created_at
superseded_at nullable
```

Projection time la copy bat bien tu fill identity, khong phai worker/import time:

```text
first_fill_at                     = first_fill.source_time_start
first_fill_time_end_exclusive     = first_fill.source_time_end_exclusive
first_fill_timestamp_precision    = first_fill.source_timestamp_precision
closed_at                         = closed_fill.source_time_start
closed_time_end_exclusive         = closed_fill.source_time_end_exclusive
closed_timestamp_precision        = closed_fill.source_timestamp_precision
```

Ba field `closed_*` cung null khi projection `OPEN` va cung non-null khi `CLOSED`. Reporting, user-week assignment va sort theo episode dung lower bound `first_fill_at`/`closed_at`; interval end-exclusive va precision luon duoc tra kem de UI/context khong trinh bay false precision.

`created_at` la trusted server commit timestamp duoc gan trong cung transaction publish projection, allocations, ledger va active pointer. Khi version moi publish, version cu nhan `superseded_at = new.created_at` trong cung transaction; `created_at < superseded_at` va cac active interval khong overlap. Projection active tai as-of `T` khi `created_at <= T` va (`superseded_at` null hoac `T < superseded_at`). Tai exact supersede timestamp, version moi active va version cu khong con active. Worker/import time nay chi dung cho lifecycle visibility; no khong thay `first_fill_at`/`closed_at` trong reporting window.

`EpisodeFillAllocation`:

```text
episode_id
workspace_id
projection_version
fill_id
event_sequence
position_qty_before
position_qty_delta
position_qty_after
cost_basis_before
cost_basis_delta
cost_basis_after
gross_realized_delta_quote
fee_expense_delta_quote nullable
```

`event_sequence` do episode projection producer gan chi sau khi sequence ambiguity da resolve. Database enforce unique `(episode_id, projection_version, event_sequence)` va unique `(episode_id, projection_version, fill_id)`; trong moi projection, sequence bat dau tai 1, contiguous den N, khong gap va tang theo deterministic linearization o muc 3.3. Allocation va `event_sequence` bat bien trong projection da publish. Replay tao `projection_version` moi, tinh va gan lai sequence tu 1..N trong version moi; cam update sequence cua version cu.

`AccountingLedgerEntry`:

```text
ledger_entry_id
workspace_id
episode_id
projection_version
fill_id
entry_sequence
entry_type
occurred_at
asset
asset_qty_delta
quote_asset = USDT
quote_value_delta nullable
position_qty_delta_base
cost_basis_delta_quote
gross_realized_delta_quote
fee_expense_delta_quote nullable
fee_conversion_id nullable
algorithm_version = wac_episode_v1
created_at
```

`entry_type` chi co `TRADE | FEE`. Moi `EpisodeFillAllocation`/fill trong mot projection tao chinh xac hai ledger entry, khong split/merge them entry:

```text
TRADE.entry_sequence = 2 * allocation.event_sequence - 1
FEE.entry_sequence   = 2 * allocation.event_sequence
```

Database enforce unique `(workspace_id, episode_id, projection_version, entry_sequence)`, unique `(workspace_id, episode_id, projection_version, fill_id, entry_type)`, va exact contiguous set `1..2N` khi projection co N allocations. `occurred_at = fill.source_time_start`; `created_at = projection.created_at`. `ledger_entry_id` la RFC 9562 UUIDv5 voi namespace literal `1fa78c73-95b1-5d92-b0a8-24af63a91c22`; name bytes la UTF-8 cua `tradeproof_ledger_entry_v1\u0000<episode_id>\u0000<projection_version-base10>\u0000<entry_sequence-base10>`, lowercase canonical UUID, unsigned base-10 integer khong leading zero, khong whitespace/BOM/terminating NUL.

Dat `q = fill.executed_qty_base`, `A = fill.gross_amount_quote`, `v = selected FeeConversion.fee_value_quote`, va voi SELL dat `c = cost_removed_quote` theo muc 9.3. Moi delta la canonical signed decimal; zero encode `0`, khong `-0` hay leading plus. Exact mapping:

| Field | BUY `TRADE` | SELL `TRADE` | BUY `FEE` | SELL `FEE` |
|---|---:|---:|---:|---:|
| `asset` | `fill.base_asset` | `fill.base_asset` | `fill.fee_asset` | `fill.fee_asset` |
| `asset_qty_delta` | `+q` | `-q` | `-fill.fee_qty` | `-fill.fee_qty` |
| `quote_value_delta` | `-A` | `+A` | `-v`, hoac null neu unavailable | `-v`, hoac null neu unavailable |
| `position_qty_delta_base` | `+q` | `-q` | `-fill.fee_qty` iff fee asset la base, else `0` | `0` |
| `cost_basis_delta_quote` | `+A` | `-c` | `-v` iff fee asset la base, else `0` | `0` |
| `gross_realized_delta_quote` | `0` | `A - c` | `0` | `0` |
| `fee_expense_delta_quote` | `0` | `0` | `v`, hoac null neu unavailable | `v`, hoac null neu unavailable |
| `fee_conversion_id` | null | null | selected conversion ID, non-null | selected conversion ID, non-null |

Trong bang, dau `+` chi mo ta sign; canonical persisted positive decimal khong co ky tu plus. BUY base-fee luon co `FILL_RATE` nen `v` non-null. SELL base fee khong giam analytical episode position/cost vi duoc tra tu wallet balance ngoai executed quantity theo muc 9.3. Zero fee van tao FEE entry voi `asset_qty_delta = 0`, `quote_value_delta = 0`, cac analytical delta bang `0`, va reference exact zero-fee conversion.

Moi FEE entry composite-FK den exact same-workspace `FeeConversion` cua cung fill; TRADE entry cam co conversion ref. Projection publisher locks every allocated fill/conversion chain and chooses the unique version active at exact `TradeEpisodeProjection.created_at` by `conversion.created_at <= projection.created_at < coalesce(conversion.superseded_at,+infinity)`. If conversion is first created or refreshed in the same publish transaction, its `created_at = projection.created_at`, old version's `superseded_at` equals that instant, and the new version wins the half-open boundary; otherwise the pre-existing active version must cover the instant. Zero or multiple active versions abort publication. Every FEE row pins this selected ID; replay never substitutes the later current version. Voi moi allocation, tong hai entry phai bang exact `position_qty_delta`, `cost_basis_delta`, `gross_realized_delta_quote` va nullable `fee_expense_delta_quote` cua allocation. Recurrence `before + delta = after` phai dung cho position va basis tai moi sequence. Reader recompute moi row tu immutable fill, prior allocation state va conversion; fabricated, missing, duplicate, reordered hoac split entry la `LEDGER_INVARIANT_FAILED`/export schema violation.

`FeeConversion`:

```text
fee_conversion_id
workspace_id
fill_id
conversion_version
fee_asset
quote_asset
fee_qty
status
method nullable
rate_quote_per_fee_asset nullable
fee_value_quote nullable
as_of_at nullable
market_bar_ids_json nullable
market_bar_source_observation_ids_json nullable
market_conversion_catalog_version nullable
conversion_path_json nullable
algorithm_version = fee_conversion_v1
created_at
superseded_at nullable
```

Moi `FeeConversion.quote_asset` bat buoc bang `USDT`. `market_conversion_catalog_version` non-null cho `DIRECT_1M_CLOSE`/`INVERSE_1M_CLOSE`, null cho `NATIVE_QUOTE`, `FILL_RATE` va zero-fee `EXACT`. Voi market-bar conversion, `market_bar_ids_json` va `market_bar_source_observation_ids_json` la hai mang non-null, cung do dai va cung thu tu; moi observation ID phai tham chieu chinh xac bar revision ID tai cung index. Selector persist observation cu the da dung trong conversion transaction va cam chon lai observation khac khi replay, export hoac verify. Voi `NATIVE_QUOTE`, `FILL_RATE`, zero-fee `EXACT` va `UNAVAILABLE`, ca hai mang phai null. `conversion_path_json` phai retain immutable venue symbol, base/quote asset, direct/inverse side, catalog version, bar revision IDs va aligned selected source-observation IDs. Market-bar conversion chi quy doi third fee asset truc tiep hoac nghich dao sang USDT.

`conversion_version` bat dau 1 va contiguous theo fill; `(workspace_id, fill_id, conversion_version)` va `(workspace_id, fee_conversion_id)` unique. `created_at` la trusted commit time. Khi version moi insert, version active cu co `superseded_at = new.created_at` trong cung transaction; active-as-of dung half-open interval giong projection va chi co toi da mot version active per fill.

Null/value coupling exact:

| Case | `status` | `method` | `rate_quote_per_fee_asset` | `fee_value_quote` | `as_of_at` | bar arrays/catalog/path |
|---|---|---|---|---|---|---|
| `fee_qty = 0` | `EXACT` | null | null | `0` | null | all null |
| nonzero quote fee | `EXACT` | `NATIVE_QUOTE` | `1` | `fee_qty` | null | all null |
| nonzero base fee | `EXACT` | `FILL_RATE` | rounded fill rate | rounded value | null | all null |
| eligible direct bar | `DERIVED` | `DIRECT_1M_CLOSE` | bar close | rounded value | bar end | exact non-null values below |
| eligible inverse bar | `DERIVED` | `INVERSE_1M_CLOSE` | rounded reciprocal | rounded value | bar end | exact non-null values below |
| no eligible path | `UNAVAILABLE` | null | null | null | null | all null |

For either market-bar method, each ID array has length exactly one and exact shapes:

```json
market_bar_ids_json = [{ "revisionId": "..." }]
market_bar_source_observation_ids_json = [{ "sourceObservationId": "..." }]
```

`conversion_path_json` has this exact member set; nullable catalog member is always present:

```json
{
  "bar": {
    "barEndExclusiveEpochMs": 0,
    "close": "300",
    "openAtEpochMs": 0,
    "recordKey": { "revisionId": "..." },
    "resolutionRecordKey": null,
    "selectedObservationRecordKey": { "sourceObservationId": "..." },
    "timeframe": "1m"
  },
  "catalogPair": {
    "baseAsset": "BNB",
    "catalogVersion": "...",
    "conversionSupported": true,
    "quoteAsset": "USDT",
    "recordKey": {
      "catalog_version": "...",
      "valid_from": "...",
      "venue_symbol": "BNBUSDT"
    },
    "validFrom": "...",
    "validToExclusive": null,
    "venueSymbol": "BNBUSDT"
  },
  "direction": "DIRECT",
  "productType": "SPOT",
  "venue": "BINANCE"
}
```

`direction` is `DIRECT | INVERSE` and must match method. Every copied catalog field/key equals one exact row in `market_conversion_catalog_version`; the version also equals top-level `market_conversion_catalog_version`. Bar record equals `market_bar_ids_json[0]`; selected observation equals `market_bar_source_observation_ids_json[0]` and its `marketBarRevisionId` equals that bar. `resolutionRecordKey` is null for a sole visible revision and exact `{ "marketBarResolutionId": id }` for a resolved conflict; its candidate set/selected revision must pass TP-MCE `market_bar_as_of_v1` at `FeeConversion.created_at`. Copied venue/product/symbol/timeframe/open/close and `barEndExclusiveEpochMs = openAtEpochMs + 60000` equal immutable bar data; `as_of_at` is the canonical RFC 3339 millisecond timestamp for that end. Direct requires pair `(baseAsset, quoteAsset) = (fee_asset, USDT)` and rate equals close. Inverse requires `(USDT, fee_asset)` and rate equals `round_half_even(1 / close, 18)`. Both require `conversionSupported = true`, bar interval inside pair validity, eligibility at fill time, and direct precedence. Unknown/extra member, array length other than one, wrong index/alignment, stale resolution or current-bar/catalog substitution rejects publication/export.

`ImportResolution`:

```text
resolution_id
workspace_id
import_row_id nullable
replay_conflict_id nullable
action
payload_json
reason
actor_user_id
idempotency_key
recorded_at
```

Moi tenant-owned business row/entity/child/join/resolution trong TP-ACC bat buoc co direct non-null immutable `workspace_id`, ke ca `ImportRow`, revision/event, allocation, ledger, fee conversion va attachment join. Moi child dung composite FK `(workspace_id, parent_id)` den parent co unique `(workspace_id, parent_id)`; cross-workspace FK/association bi database reject, khong chi application check. Authorization scope `workspace_id` truoc moi read/write/export/delete/replay. Chi global public `Instrument`/catalog version rows, TP-ACC product taxonomy versions, public market bars va provider provenance khong co workspace; khi duoc business row tham chieu, referenced public ID/version van phai duoc persist.

`ImportResolution.action` chi co:

- `ACCEPT_AS_NEW`: xac nhan occurrence ambiguous la fill moi;
- `MARK_DUPLICATE`: lien ket occurrence ambiguous den fill existing;
- `SET_SEQUENCE`: payload la danh sach `import_row_id` co thu tu cho mot ambiguous time group;
- `CONFIRM_REPLAY`: chap nhan projection boundary/plan-link thay doi do backfill;

`payload_json` la RFC 8785 object voi exact shape theo action; moi nested object cung cam unknown/missing member:

```json
ACCEPT_AS_NEW  -> {}
MARK_DUPLICATE -> {
  "targetFillRecordKey": { "fill_id": "..." }
}
SET_SEQUENCE   -> {
  "ambiguousGroupDigestSha256": "...",
  "orderedMembers": [{
    "fillRecordKey": { "fill_id": "..." },
    "importRowRecordKey": { "import_row_id": "..." }
  }]
}
CONFIRM_REPLAY -> {
  "eligibilityDecisions": [{
    "action": "EXCLUDE",
    "projectionRecordKey": { "episode_id": "...", "projection_version": 1 }
  }],
  "previewSourceInputDigest": "...",
  "replayConflictId": "..."
}
```

`ACCEPT_AS_NEW` chi hop le cho mot `DUPLICATE_MULTIPLICITY_AMBIGUOUS` row co exact unresolved StagedFill. Cung transaction tao ImportResolution, `ADMITTED_AS_NEW` disposition va mot NormalizedFill moi copy exact candidate values/canonical signature, voi `dedup_key = SHA256(UTF8("tradeproof_accept_as_new_v1\u0000" + lowercase-canonical-UUID(resolution_id)))`, encoded thanh lowercase 64-character hex. Khong co whitespace, BOM hoac terminating NUL ngoai delimiter literal. NormalizedFill keeps the originating `import_row_id`, while the terminal row retains its import-time staged ref/status/error; current accounting and queue use the disposition/result fill. Retry cung resolution khong tao disposition/fill/key thu hai.

`MARK_DUPLICATE.targetFillRecordKey` phai resolve mot `NormalizedFill` khac row, cung workspace, trading account, instrument va `canonical_signature`; target khong duoc la alias-only row, bi xoa, hay tro nguoc ve source row dang resolve. Cung transaction tao ImportResolution + `DISCARDED_AS_DUPLICATE` disposition va tao khong NormalizedFill cho candidate; the disposition pins target while the terminal row retains its import-time staged ref/status/error. Scalar ID, cross-workspace target, signature/account/instrument mismatch va duplicate chain bi reject.

Voi `SET_SEQUENCE`, ambiguous group la maximal connected component trong interval-overlap graph cua unresolved canonical NormalizedFill rows cung workspace/account/instrument: moi edge noi hai interval incomparable, va component chi duoc mo resolution khi co it nhat hai topological order ton trong `provably_before` cho ket qua episode/boundary/position/cost/gross khac nhau. `orderedMembers` nonempty, unique theo ca hai key, chua exact component va moi pair row/fill phai la authoritative `ImportRow.normalized_fill_id` link. Array la thu tu user chon va phai ton trong moi `provably_before` edge; dependent suffix khong nam trong array.

`ambiguousGroupDigestSha256` la lowercase SHA-256 cua RFC 8785 bytes cua exact object sau, trong do `members` sorted theo unsigned RFC 8785 bytes cua `importRowRecordKey`, khong theo thu tu user chon:

```json
{
  "instrumentRecordKey": { "instrument_id": "..." },
  "members": [{
    "fillRecordKey": { "fill_id": "..." },
    "importRowRecordKey": { "import_row_id": "..." }
  }],
  "schemaId": "import_sequence_ambiguous_group_v1",
  "tradingAccountRecordKey": { "trading_account_id": "..." },
  "workspaceId": "..."
}
```

Server lock account/instrument, rebuild component and digest from immutable row/fill records, then require exact equality before applying order. Outer `import_row_id` cua `SET_SEQUENCE` bang `import_row_id` co record-key RFC 8785 bytes nho nhat trong digest-ordered `members`; voi `ACCEPT_AS_NEW`/`MARK_DUPLICATE` no la row duy nhat dang resolve. Ba row action bat buoc `import_row_id` non-null va `replay_conflict_id` null. `CONFIRM_REPLAY` bat buoc nguoc lai; conflict ID xuat hien ca outer field va payload phai bang nhau, va payload tuan exact preview/decision partition o muc 4.4.

`(workspace_id, idempotency_key)` unique; resolution append-only. Retry chi tra record cu khi action, outer refs va RFC 8785 payload bytes bang nhau; bat ky thay doi nao fail `IDEMPOTENCY_CONFLICT`. Resolution chi doc immutable StagedFill/NormalizedFill values, `raw_row_sha256`, row/fill IDs va sanitized diagnostics; `payload_json` cam chua raw CSV/cell value. `ACCEPT_AS_NEW`/`MARK_DUPLICATE` chi hop le cho row co unresolved StagedFill; `SET_SEQUENCE` chi hop le cho row co canonical NormalizedFill va exact pending group. V1 khong co action sua timestamp, side, price, quantity, amount, fee hay symbol; invalid source phai duoc sua tai file va upload batch moi.

User metric exclusion khong dung `ImportResolution`. `EpisodeMetricEligibilityEvent` append-only:

```text
episode_metric_eligibility_event_id
workspace_id
episode_id
event_sequence
based_on_projection_version
action = EXCLUDE | RESTORE
reason
actor_user_id
idempotency_key
recorded_at
```

Database unique `(workspace_id, idempotency_key)`, `(workspace_id, episode_id, event_sequence)` va composite FK den exact episode projection. Sequence la contiguous tren episode qua moi projection version; replay/version change khong reset no. Command chi nhan active `based_on_projection_version`; stale version fail `STALE_EPISODE_ELIGIBILITY`. Tai `reporting_as_of_at`, loc event visible va lay greatest `event_sequence` cho exact active projection: khong event hoac latest `RESTORE` la included, latest `EXCLUDE` la user-excluded. Event khong xoa fill, ledger hay historical metric.

Eligibility event khong silently carry qua projection version. Neu replay doi version cua episode co event, `ReplayConflictPreview` danh dau impact va `CONFIRM_REPLAY` bat buoc chon `EXCLUDE` hoac `RESTORE` cho moi affected new projection; transaction publish append event moi based on new version. Split/merge/new episode ID khong auto-copy old event. Defensive version mismatch khong co explicit event moi bi loai current metric voi reason `ELIGIBILITY_VERSION_UNRESOLVED` cho den audited decision.

## 6. Plan-to-first-fill matching

### 6.1. Plan state machine

```text
          +-> CONSUMED
ARMED ----+-> CANCELLED
          +-> EXPIRED
```

- `ARMED` la state dau tien va chi duoc tao boi atomic ARM header + revision 1 + event 1 + receipt. Exact decimal/text rules o muc 5.4 phai pass, `planned_risk_quote > 0`, `planned_risk_asset` bang instrument quote asset, `initial_stop_price > 0`, `entry_zone_low <= entry_zone_high`, `initial_stop_price < entry_zone_low`, va `confidence_score` la integer 1..5. Arm command co nullable `expiry_duration_seconds`: null thanh exact default `86400`, non-null la integer `900..604800`; `armed_at = ARM.recorded_at` va `expires_at = armed_at + duration` trong cung transaction.
- Toi da mot effective `ARMED` plan cho moi `(workspace_id, trading_account_id, instrument_id)` tai mot trusted as-of. Arm transaction materialize moi expired predecessor theo exact lazy rule truoc khi enforce unique current slot; tai `now = expires_at`, old plan da EXPIRED va slot duoc giai phong.
- `CONSUMED`, `CANCELLED`, `EXPIRED` la terminal state. Edit noi dung luon tao revision; khong dua plan terminal ve `ARMED`.

### 6.2. Plan proof status

Dat `S = first_fill.source_time_start` va `E = first_fill.source_time_end_exclusive`. `plan_proof_status` chi co bon value:

| Status | Contract |
|---|---|
| `VERIFIED` | Dung mot plan/revision da submit va arm bang server timestamp strict `< S`, va plan hop le trong toan interval `[S, E)` |
| `AMBIGUOUS` | Co candidate plan nhung `ARM`, selected revision submission, expiry hoac terminal state timestamp nam trong `[S, E)`, hoac co nhieu candidate |
| `LATE` | Chi duoc tao boi audited `PlanEpisodeAssociation` sau first-fill interval; khong phai auto-match |
| `UNMATCHED` | Khong co candidate tu dong va chua co late association |

Chi `VERIFIED` co `frozen_plan_revision_id`, `is_planned = true` va duoc tinh `planned_initial_risk_quote`/`r_multiple`. `AMBIGUOUS`, `LATE`, `UNMATCHED` luon co `frozen_plan_revision_id = null`, `is_planned = false` va R-multiple null. UI hien lower-case label tuong ung, nhung persisted enum dung upper-case.

### 6.3. Deterministic auto-match

Auto-match chi chay khi accepted BUY mo episode moi; BUY them vao episode dang OPEN khong chay lai proof. Engine dung server event timeline va `plan_proof_v1` theo thu tu:

1. Lay plan cung workspace, account, instrument, `direction = LONG`, co `ARM.recorded_at < E`, `expires_at >= S`, khong bi cancel strict `< S`, va chua consume boi episode khac. Automatic expiry timing chi doc authoritative `expires_at`; EXPIRE audit materialization time khong tham gia candidate timing.
2. Voi moi plan, chon revision co `(recorded_at, revision_no)` lon nhat nhung `recorded_at < E`. Revision khong co trong khoang nay lam plan khong phai candidate.
3. Ghi tat ca candidate IDs va cac timestamp da so sanh vao `plan_candidate_ids_json`/`plan_proof_basis_json`; khong chi luu winner.
4. Neu khong co candidate, publish `UNMATCHED` voi `associated_plan_id = null` va reason `NO_ELIGIBLE_CANDIDATE`.
5. Neu co nhieu candidate do data/replay conflict, publish `AMBIGUOUS`, reason `MULTIPLE_CANDIDATES`, khong auto-consume plan nao va yeu cau data repair.
6. Neu dung mot candidate, no la `VERIFIED` chi khi `ARM.recorded_at < S`, selected revision `recorded_at < S`, `expires_at >= E`, va khong co `CANCEL.recorded_at < E`. Khi do `associated_plan_revision_id` va `frozen_plan_revision_id` cung tro den selected revision.
7. Neu dung mot candidate nhung `ARM.recorded_at`, selected revision `recorded_at`, authoritative `expires_at` hoac `CANCEL.recorded_at` nam trong `[S, E)`, publish `AMBIGUOUS`, reason code tu field dau tien theo thu tu vua liet ke; luu candidate vao `associated_plan_id`/`associated_plan_revision_id` nhung frozen revision van null. `EXPIRE.recorded_at` khong bao gio la timing input.

`plan_candidate_ids_json` is not a scalar-ID bag. It is a sorted unique array of exact `{ "planRecordKey": { "trade_plan_id": id }, "revisionRecordKey": { "trade_plan_revision_id": id } }`, ordered by unsigned RFC 8785 bytes of plan key then revision key. It equals exactly the evaluations whose exclusion reason is NONE.

`plan_proof_basis_json` has this exact member set; nullable members are always present:

```json
{
  "evaluatedAt": "...",
  "evaluatedPlans": [{
    "armEventRecordKey": { "plan_state_event_id": "..." },
    "armRecordedAt": "...",
    "candidate": true,
    "consumeEventRecordKey": null,
    "consumeRecordedAt": null,
    "consumedByEpisodeRecordKey": null,
    "eventSequenceWatermark": 1,
    "exclusionReason": "NONE",
    "expiresAt": "...",
    "planRecordKey": { "trade_plan_id": "..." },
    "revisionNoWatermark": 1,
    "revisionRecordKey": { "trade_plan_revision_id": "..." },
    "revisionRecordedAt": "...",
    "terminalEventRecordKey": null,
    "terminalEventType": null,
    "terminalRecordedAt": null
  }],
  "firstFillInterval": {
    "endExclusive": "...",
    "precision": "MILLISECOND",
    "start": "..."
  },
  "firstFillRecordKey": { "fill_id": "..." },
  "selectedCandidate": null
}
```

`evaluatedAt = plan_proof_resolved_at`. Under ordered locks for every in-scope plan, producer captures `eventSequenceWatermark = max(existing PlanStateEvent.event_sequence, 0)` and `revisionNoWatermark = max(existing TradePlanRevision.revision_no, 0)` before inserting any projection-side event. `evaluatedPlans` contains every plan header with `created_at <= evaluatedAt` in the same workspace/account/instrument/LONG scope, sorted by unsigned RFC 8785 bytes of `planRecordKey`; selectors below may read only sequence/revision numbers at or below those persisted watermarks and records with `recorded_at <= evaluatedAt`. A CONSUME appended by the current publication necessarily has sequence above the watermark and cannot feed back into its own proof. A pre-existing same-episode CONSUME at/below the watermark remains visible evidence.

Selection is exact:

1. ARM selector is the sole ARM event visible under the watermark; if none exists, both ARM fields are null. An ARM at/after `E` is retained as evidence, not nulled.
2. Revision selector first chooses greatest `(recorded_at, revision_no)` with `recorded_at < E`. If none, it chooses the earliest visible `(recorded_at, revision_no)` with `E <= recorded_at <= evaluatedAt` as boundary evidence. If no visible revision exists, both revision fields are null.
3. Terminal selector chooses the lowest `event_sequence` visible CANCEL/EXPIRE audit event. State-machine validation permits at most one; all three terminal fields are null iff none exists. For CANCEL, `terminalRecordedAt` is semantic terminal time; for EXPIRE it is only materialization time and all expiry decisions instead use adjacent authoritative `expiresAt`.
4. Consume selector chooses the sole visible CONSUME event. `consumeEventRecordKey`, `consumeRecordedAt` and `consumedByEpisodeRecordKey` are all non-null and equal that event/episode iff it exists; otherwise all three are null. Multiple terminal or CONSUME events are invalid source history, not a tie to resolve.

Watermarks are nonnegative safe integers and equal the greatest source sequence/revision observed, including records irrelevant to the time predicate; a smaller/larger/fabricated watermark rejects. Every copied timestamp/key must equal the selected immutable record. Apply the first matching exclusion in this exact table:

| Precedence | `exclusionReason` | Exact condition |
|---:|---|---|
| 1 | `NO_ARM_BEFORE_END` | ARM selector is null or `armRecordedAt >= E` |
| 2 | `NO_REVISION_BEFORE_END` | no revision exists with `recorded_at < E`; boundary-evidence revision may be non-null |
| 3 | `EXPIRED_BEFORE_INTERVAL` | `expiresAt < S` |
| 4 | `TERMINAL_BEFORE_INTERVAL` | terminal selector type is CANCEL and `terminalRecordedAt < S`; EXPIRE uses precedence 3 and `expiresAt` only |
| 5 | `CONSUMED_BY_OTHER_EPISODE` | consume selector non-null and consumed episode differs from the episode being projected |
| 6 | `NONE` | none of the above |

`candidate` is true iff reason `NONE`; `plan_candidate_ids_json` is the exact typed plan/revision projection of those rows. A `NONE` row therefore always has non-null ARM and revision refs. For every key/timestamp pair, both members are null or both non-null; `terminalEventType` is null with terminal key/time and otherwise equals referenced `CANCEL | EXPIRE`. `expiresAt` always equals non-null `TradePlan.expires_at`. These rules, rather than the exclusion reason alone, close every nullable member.

`selectedCandidate` is one exact object with the same member set as an element of `plan_candidate_ids_json`, and its RFC 8785 bytes must equal that element, only for single-candidate VERIFIED or timestamp-AMBIGUOUS auto-match. It is null for zero/multiple candidates and remains the original frozen value after manual resolution or LATE association. First-fill key/interval equals immutable fill identity. Every nested plan/revision/event/fill/episode key resolves in the same workspace; unknown/extra member, duplicate, wrong order, timestamp mismatch, candidate substitution or cross-workspace key rejects projection publication.

Timestamp bang `S` nam trong interval va la ambiguous; timestamp bang `E` khong nam trong interval va khong the lam bang chung pre-fill. Engine khong duoc dung import time, client clock, source row order hoac timestamp da lam tron de nang proof.

Voi dung mot candidate `VERIFIED` hoac `AMBIGUOUS`, auto-match luon luu association. Neu current plan state van `ARMED`, cung transaction publish projection tao `PlanStateEvent.event_type = CONSUME` va `consumed_by_episode_id = episode_id`. Neu plan da `CANCELLED`/`EXPIRED` sau `S`, association/proof van duoc luu nhung khong tao transition tu terminal state; neu da `CONSUMED` boi chinh episode thi replay la no-op. Event co idempotency key `(trade_plan_id, episode_id, CONSUME)`; replay khong tao event thu hai. System-generated consume event nay khong duoc dua nguoc vao phep tinh proof cua chinh episode. `UNMATCHED` va multi-candidate ambiguous khong auto-consume.

Reason code v1 la `VERIFIED_BEFORE_INTERVAL`, `ARM_INSIDE_INTERVAL`, `REVISION_INSIDE_INTERVAL`, `EXPIRY_INSIDE_INTERVAL`, `CANCEL_INSIDE_INTERVAL`, `MULTIPLE_CANDIDATES`, `NO_ELIGIBLE_CANDIDATE` hoac `USER_ASSOCIATED_AFTER_FILL`. Status `LATE` bat buoc dung reason cuoi; khong nhan free-text thay cho reason code. `EXPIRE_EVENT_INSIDE_INTERVAL` bi cam vi audit materialization time khong phai semantic expiry input.

Moi projection bat buoc persist `plan_proof_status`, `plan_proof_reason_code`, `plan_proof_rule_version`, `plan_candidate_ids_json`, `plan_proof_basis_json` va `plan_proof_resolved_at`, ke ca khi unmatched. Cac nullable link phai thoa:

```text
VERIFIED  -> associated_plan_id != null, associated_plan_revision_id != null,
             frozen_plan_revision_id == associated_plan_revision_id,
             late_association_id == null, plan_match_resolution_id == null
AMBIGUOUS -> frozen_plan_revision_id == null, late_association_id == null;
             associated links co the null hoac non-null theo auto-match/resolution
LATE      -> associated_plan_id != null, associated_plan_revision_id != null,
             frozen_plan_revision_id == null, late_association_id != null,
             plan_match_resolution_id == null
UNMATCHED -> associated_plan_id == null, associated_plan_revision_id == null,
             frozen_plan_revision_id == null, late_association_id == null,
             plan_match_resolution_id == null
```

### 6.4. Audited ambiguous match resolution

`PlanMatchResolution.action` chi co:

| Action | Precondition | New association |
|---|---|---|
| `CONFIRM_ASSOCIATION` | Active projection la `AMBIGUOUS` va dang co dung mot associated plan/revision | Giu nguyen associated plan/revision; ghi audit user confirmation |
| `SELECT_CANDIDATE` | Active projection la `AMBIGUOUS`; selected plan/revision nam trong exact candidate/basis da persist khi proof duoc tao | Dat associated links thanh selected plan/revision |
| `REMOVE_ASSOCIATION` | Active projection la `AMBIGUOUS` va dang co associated plan/revision | Dat ca hai associated links ve null |

Single-candidate timestamp ambiguity duoc auto-associate theo muc 6.3; user dung `CONFIRM_ASSOCIATION` de xac nhan hoac `REMOVE_ASSOCIATION` de bo lien ket. Multi-candidate ambiguity ban dau khong co association; user phai dung `SELECT_CANDIDATE`, sau do co the confirm hoac remove. `SELECT_CANDIDATE` khong duoc chon plan/revision ngoai `plan_candidate_ids_json` va `plan_proof_basis_json` da dong bang.

`CONFIRM_ASSOCIATION` va `SELECT_CANDIDATE` bat buoc co ca hai selected IDs; voi confirm, IDs phai bang current associated links. `REMOVE_ASSOCIATION` bat buoc hai selected fields null. Sai shape hoac candidate membership fail `INVALID_PLAN_MATCH_CANDIDATE`.

Moi command bat buoc gui `based_on_projection_version`, `idempotency_key`, action, selected IDs theo action va `reason` khong rong toi da 500 ky tu. Server reject `STALE_PLAN_MATCH_RESOLUTION` neu based-on version khong con active; retry cung `(workspace_id, idempotency_key)` tra dung resolution/projection da tao, khong tao effect thu hai. Server persist actor, trusted `recorded_at`, old/new association va dat `plan_match_resolution_id` cua projection moi den record vua tao.

Moi resolution giu nguyen `plan_proof_status = AMBIGUOUS`, `plan_proof_reason_code`, `plan_proof_rule_version`, candidates, proof basis va proof resolved time; `frozen_plan_revision_id = null`, `is_planned = false`, planned risk va R van null. No khong duoc tao `VERIFIED` hoac `LATE`.

Voi `SELECT_CANDIDATE`, neu selected plan van `ARMED`, chua consume va khong bi episode khac claim, cung transaction tao idempotent `CONSUME` nhu muc 6.3. Neu plan da `CANCELLED`/`EXPIRED` sau source interval, association van duoc ghi ma khong viet lai state. Neu plan da consume boi episode khac, command fail `PLAN_ALREADY_CONSUMED`. `CONFIRM_ASSOCIATION` khong tao consume thu hai. `REMOVE_ASSOCIATION` chi bo display/projection link: no khong delete/dao nguoc historical `CONSUME`, khong re-arm plan terminal va khong sua event history. Neu can intent moi, user tao plan moi.

### 6.5. Late association va tinh bat bien

User co the chuyen `UNMATCHED -> LATE` bang exact idempotent `PlanEpisodeAssociation` command o muc 5.4 co actor, selected revision, active based-on projection, server `recorded_at >= E` va reason. Neu plan van effective ARMED va chua consume, cung transaction tao idempotent `CONSUME`; neu plan da terminal, association van duoc luu nhung khong sua state lich su. `PlanMatchResolution` co the doi associated links cua `AMBIGUOUS` nhung khong co manual transition tu `AMBIGUOUS` sang `LATE` hoac `VERIFIED`.

Sau khi mot episode projection dau tien da publish `AMBIGUOUS` hoac `LATE`, status do khong bao gio duoc nang thanh `VERIFIED`, ke ca khi user khai bao, upload file precision cao hon hoac revise plan. Revision/post-fill note khong thay frozen snapshot, planned status hay R. Backfill lam doi opening fill/boundary phai qua `HISTORICAL_REPLAY_CONFLICT`; no khong am tham viet lai proof da dung trong Review/Weekly Lab.

## 7. TradeEpisode state machine

### 7.1. Lifecycle

```text
NO_POSITION --accepted BUY--> OPEN
OPEN --accepted BUY--> OPEN
OPEN --accepted partial SELL--> OPEN
OPEN --accepted SELL to zero--> CLOSED
CLOSED --next accepted BUY--> new OPEN episode
```

Supersession la lifecycle ky thuat chi duoc bieu dien boi `superseded_at` va active interval. Immutable `state` cua ca current va historical TradeEpisodeProjection luon la `OPEN` hoac `CLOSED`; replay khong doi no thanh `SUPERSEDED`.

Quy tac:

- SELL khi khong co episode `OPEN` bi quarantine `SELL_WITHOUT_OPEN_POSITION`.
- SELL co `executed_qty_base > position_qty_base` bi quarantine `SELL_EXCEEDS_POSITION`; khong split row va khong tao short.
- BUY khong tao episode moi neu instrument da co episode `OPEN`; no tang vi the va cap nhat weighted average.
- Episode dong chi khi decimal `position_qty_base == 0`. Khong dung epsilon.
- Database enforce unique partial index cho mot projection active `OPEN` tren `(workspace_id, trading_account_id, instrument_id)`.
- Fill da quarantine khong duoc anh huong position, ledger hay metric.

### 7.2. Event sequence va episode identity

Voi active projection co N allocation:

- `event_sequence` phai la exact set `{1, 2, ..., N}` theo deterministic linearization; sequence 1 luon la opening BUY.
- `first_fill_id` phai bang `fill_id` cua allocation sequence 1. `first_fill_at`, end-exclusive va precision phai bang source interval cua cung fill.
- Neu `state = CLOSED`, allocation sequence N la fill dau tien dua `position_qty_after` ve exact 0; `closed_fill_id` phai bang fill do, va `closed_at`, end-exclusive, precision phai bang source interval cua cung fill. Khong allocation nao sau no thuoc episode nay.
- Neu `state = OPEN`, moi `position_qty_after` sau sequence 1 deu > 0 va toan bo `closed_*` null.
- Entry context identity, plan proof identity va episode UUID deu dung `first_fill_id`; exit context identity dung `closed_fill_id`. Khong module nao duoc tu chon fill som/tre hon bang row number, upload time hoac timestamp lower bound khi identity da persist.
- Projection khong duoc publish neu con `SEQUENCE_AMBIGUOUS`; source interval va audited `SET_SEQUENCE` la hai input ordering duy nhat.

### 7.3. Decimal va rounding

- Parse va luu source numeric exact bang `DECIMAL(38,18)`; source co toi da 20 integer digits va 18 fractional digits, khong round luc parse.
- Intermediate multiplication/division dung arbitrary-precision decimal. Neu platform bat buoc fixed precision, dung toi thieu 76 significant digits, toi thieu 36 fractional digits va it nhat 18 guard digits truoc final quantization.
- Moi gia tri quote ghi vao projection/ledger quantize den scale 18 bang `ROUND_HALF_EVEN`, sau do verify con toi da 20 integer digits.
- Neu source, intermediate required target hoac final quantized value vuot declared range, source row bi `DECIMAL_OVERFLOW`; neu overflow chi xuat hien khi replay/accounting, projection khong publish, batch `NEEDS_ATTENTION` voi cung code. Cam silent truncation, saturation, wrap va binary floating point.
- Presentation rounding theo `quote_precision`, chi o API/UI; khong ghi de ledger.
- Khi SELL dung toan bo `position_qty_base`, `cost_removed_quote` phai bang chinh xac `open_cost_basis_quote` con lai. Quy tac nay ngan dust do phep chia.

## 8. Fee conversion

### 8.1. Conversion precedence

Voi moi fill, resolver chay theo thu tu co dinh:

1. `fee_asset == quote_asset`: `method = NATIVE_QUOTE`, rate `1`, value bang `fee_qty`.
2. `fee_asset == base_asset`: `method = FILL_RATE`, rate bang `gross_amount_quote / executed_qty_base`.
3. Third asset: dung market bar Binance Spot da luu theo muc 8.2.
4. Khong co path hop le: `status = UNAVAILABLE`; khong thay bang 0.

Stored rate/value are deterministic: `NATIVE_QUOTE` rate is exact `1`; `FILL_RATE = round_half_even(gross_amount_quote / executed_qty_base, 18)`; direct rate is the exact canonical bar close; inverse rate is `round_half_even(1 / bar_close, 18)`. For every available nonzero conversion, `fee_value_quote = round_half_even(fee_qty * stored rate_quote_per_fee_asset, 18)`. Implementations do not multiply by an unpersisted higher-precision reciprocal.

`FeeConversion.status` chi co:

- `EXACT`: zero fee, `NATIVE_QUOTE` hoac `FILL_RATE`;
- `DERIVED`: direct hoac inverse market-bar path da resolve;
- `UNAVAILABLE`: khong co path hop le.

`fee_qty == 0` co `status = EXACT`, `fee_value_quote = 0` va khong can market data, bat ke fee asset.

### 8.2. Point-in-time third-asset conversion

Fee worker pin mot active `MarketConversionCatalogVersion` cho conversion attempt. Voi moi Binance 1m bar, engine tinh `bar_end_exclusive = open_at + 60 seconds`. Resolver chi dung bar cua `BINANCE/SPOT` thoa `bar_end_exclusive <= fill.source_time_start`; khong dung Binance `close_time` de kiem tra eligibility vi field do co the bieu dien millisecond inclusive. Pair row trong pinned conversion catalog phai co `conversion_supported = true`, exact symbol/assets, va historical window chua tron bar interval:

```text
pair.valid_from <= bar.open_at
and bar_end_exclusive <= coalesce(pair.valid_to_exclusive, +infinity)
```

Current pair listing status khong tham gia phep tinh. Voi moi path, chon logical bar co `bar_end_exclusive` lon nhat, nhung `fill.source_time_start - bar_end_exclusive <= 5 minutes`; exact revision/resolution/observation for that logical key MUST come from TP-MCE `market_bar_as_of_v1` at conversion `created_at`. Zero revision or unresolved conflict makes that path unavailable; the fee worker cannot choose a consumer-local revision. Bar partial, unreliable, sai timeframe/venue, hoac nam ngoai pinned pair validity khong duoc dung.

Thu tu path:

1. Direct pair `fee_asset + USDT`: rate bang bar close USDT tren fee asset; `method = DIRECT_1M_CLOSE`.
2. Inverse pair `USDT + fee_asset`: rate bang `1 / bar_close`; `method = INVERSE_1M_CLOSE`.

Direct luon duoc uu tien neu ca hai pair ton tai. Khong co bridge, graph path, stablecoin parity, current price hoac venue khac. Moi market-bar conversion luu bar revision ID, aligned selected `MarketBarSourceObservation.sourceObservationId`, `open_at`, `bar_end_exclusive`, path, rate, `as_of_at = bar_end_exclusive` va `algorithm_version = fee_conversion_v1`. Observation do dong chuoi provenance bat bien toi exact `MarketDataSourceRequest` va `MarketDataIngestionBatch`; implementation khong duoc suy dien observation tu bar ID tai thoi diem doc.

```text
fee_value_quote = round_half_even(fee_qty * rate_quote_per_fee_asset, 18)
```

Neu direct va inverse bar deu thieu, stale, zero hoac unreliable, conversion la `UNAVAILABLE`. Khi market data bo sung, system co the tao `FeeConversion` version moi va replay ledger; record cu bi supersede, khong sua tai cho.

### 8.3. Accounting quality khi thieu conversion

- `gross_realized_pnl_quote` van co the tinh neu fee la third asset.
- `known_fee_quote` chi cong fee da convert.
- `net_realized_pnl_quote = null` neu bat ky fee conversion cua episode `UNAVAILABLE`.
- `accounting_quality = FEE_CONVERSION_MISSING` va row disposition la `ACCOUNTING_PENDING`.
- UI MUST hien fee asset/quantity thieu; cam hien known net nhu net day du.
- Episode co accounting khong complete bi loai khoi metric net P&L, R, expectancy, win rate, payoff ratio, profit factor va `closed_episode_*` drawdown; sample report phai hien so episode bi loai.

## 9. Weighted-average episode accounting

### 9.1. Bien trang thai

Truoc moi fill:

```text
Q = position_qty_base
B = open_cost_basis_quote
G = cumulative gross_realized_pnl_quote
F = cumulative fee_expense_quote
```

Neu `Q > 0`, persisted `average_cost_quote_per_base = round_scale18_half_even(B / Q)`; neu `Q == 0`, average cost la `null`. Field nay la derived display/query value voi signed DECIMAL(38,18) range and canonical zero/trailing-zero rules cua muc 7.3; overflow fails projection publication with `DECIMAL_OVERFLOW`. Accounting recurrence MUST NOT feed this rounded field back into cost removal.

### 9.2. BUY

Dat:

```text
q = executed_qty_base
A = gross_amount_quote
v = fee_value_quote
```

Neu fee asset bang base asset, Binance da tru fee tren asset nhan ve trong analytical episode:

```text
require fee_qty < q
net_acquired_qty = q - fee_qty
net_acquired_basis = A - v
Q' = Q + net_acquired_qty
B' = B + net_acquired_basis
gross_realized_delta = 0
fee_expense_delta = v
```

Neu fee asset khac base asset:

```text
Q' = Q + q
B' = B + A
gross_realized_delta = 0
fee_expense_delta = v
```

`BUY_BASE_FEE_EXCEEDS_EXECUTED` duoc quarantine neu `fee_qty >= q`; ten error code duoc giu ngan, nhung equality cung invalid vi khong duoc tao episode co net acquired quantity bang 0. Voi base fee hop le, `v` luon co tu `FILL_RATE`, nen `A - v` duong.

### 9.3. SELL

Voi moi SELL, fee khong thay doi executed position quantity. Neu fee asset la base, no duoc ghi la fee expense tra tu account balance ngoai executed quantity; MVP khong tu suy ra wallet lot.

```text
require q <= Q
if q == Q:
    cost_removed = B
else:
    cost_removed = round_half_even((B / Q) * q, 18)

Q' = Q - q
B' = B - cost_removed
gross_realized_delta = A - cost_removed
fee_expense_delta = v
```

Neu `Q' == 0`, bat buoc set `B' = 0` va close episode. Khong cho `B' < 0`.

For partial SELL, `(B / Q) * q` is one exact rational expression from pre-fill B/Q and exact q, rounded only once at `cost_removed`; it never uses persisted `average_cost_quote_per_base` and never rounds B/Q first. This rule is intentionally distinct from the derived average field and prevents cumulative WAC drift.

### 9.4. Episode totals va invariants

```text
gross_realized_pnl_quote = sum(gross_realized_delta)
known_fee_quote = sum(fee_expense_delta where conversion is available)
net_realized_pnl_quote = gross_realized_pnl_quote - sum(all fee_expense_delta)
```

`net_realized_pnl_quote` chi non-null khi tat ca fee conversion available.

Moi field co suffix `_quote` trong episode, allocation va ledger v1 deu co don vi USDT. Khong co implicit currency field hoac conversion sang currency khac.

Moi projection publish phai qua cac invariant:

1. `position_qty_base >= 0` va `open_cost_basis_quote >= 0`.
2. Tong `position_qty_delta_base` bang ending quantity.
3. Tong `cost_basis_delta_quote` bang ending basis.
4. Tong gross va fee ledger bang episode totals.
5. `(episode_id, projection_version, event_sequence)` unique va allocation sequence la contiguous `1..N`, khong co gap/duplicate.
6. `first_fill_id`/`closed_fill_id` va sau field time/precision cua chung khop allocation identity theo muc 7.2.
7. Episode `CLOSED` co quantity va basis bang 0, co day du `closed_fill_id`, `closed_at`, `closed_time_end_exclusive`, `closed_timestamp_precision`.
8. Episode `OPEN` co quantity > 0 va tat ca `closed_*` null.
9. Moi immutable NormalizedFill hoac duoc allocate dung mot active episode projection, hoac co exact `ACCOUNTING_PENDING` reason `SEQUENCE_AMBIGUOUS | HISTORICAL_REPLAY_CONFLICT` va chua co active allocation. Multiplicity StagedFill khong phai NormalizedFill va khong co allocation; fee-conversion pending NormalizedFill van co allocation va gross ledger hop le.
10. Moi episode co dung mot opening BUY fill.
11. Projection persist exact `episode_projection_v1` va `wac_episode_v1`; moi allocation/ledger entry cung projection version.
12. Neu bat ky invariant fail, projection khong publish va batch la `NEEDS_ATTENTION` voi `LEDGER_INVARIANT_FAILED`.

Day la analytical trading P&L, khong phai wallet accounting hay tax accounting. Dac biet, fee base tren SELL va fee third asset duoc quy doi thanh expense nhung MVP khong theo doi cost basis cua asset dung tra fee.

Algorithm version cua ledger la `wac_episode_v1`; metric dictionary nay la `metrics_v1`. Hai value phai duoc persist va tra trong export/API, khong lay ngam dinh tu application build version.

## 10. Reconciliation va data quality

### 10.1. Row disposition

Moi non-blank data row phai co dung mot disposition terminal hoac pending:

| Disposition | Y nghia | Tinh numerator |
|---|---|---|
| `RECONCILED` | Fill moi da normalize, allocate, ledger complete | Co |
| `DUPLICATE` | Doi chieu duoc fill existing da reconciled, khong tao double count | Co |
| `ACCOUNTING_PENDING` | Canonical fill co fee/sequence/replay chua complete, hoac multiplicity StagedFill chua co disposition tai batch terminal | Khong |
| `QUARANTINED` | Parse, validation hoac position rule fail | Khong |

Bon value tren la final `ImportRow.status`; intermediate parser/dedup decision khong duoc ghi vao field nay. Disposition la per source row/fill, khong phai blanket episode status:

- Neu mot fill co fee conversion unavailable, chi row cua fill do la `ACCOUNTING_PENDING`. Fill khac trong cung episode van `RECONCILED` neu own normalization, admission, allocation va ledger entries complete; episode van co `accounting_quality = FEE_CONVERSION_MISSING` va bi loai khoi net family.
- Voi `SEQUENCE_AMBIGUOUS`, moi row trong ambiguous group va dependent suffix theo muc 3.3 la `ACCOUNTING_PENDING`. Prefix da publish truoc gap khong bi downgrade; producer khong reconcile row nao sau gap cho den `SET_SEQUENCE` va replay thanh cong.
- Voi replay conflict, row cua canonical fill moi gay conflict la `ACCOUNTING_PENDING`; row cua projection active cu giu disposition da co cho den atomic publish projection moi.
- Proven duplicate ke thua canonical target row/fill disposition theo muc 4.3. Chi target `RECONCILED` tao incoming `DUPLICATE` va vao numerator; pending target khong duoc bien thanh numerator chi vi episode co gross projection.

Header row va blank row khong nam trong denominator. Data row loi van nam trong denominator sau file-level validation; cam bo row loi de lam dep ty le.

```text
reconciliation_denominator = ImportBatch.data_rows
reconciliation_numerator = count(RECONCILED) + count(DUPLICATE)
reconciliation_rate = round_scale18_half_even(numerator / denominator)
```

For positive denominator, `reconciliation_rate` is persisted/exported as canonical decimal after exactly one scale-18 ROUND_HALF_EVEN division, stripping trailing zeros and normalizing zero; no binary float. File khong co data row co `reconciliation_rate = null`, batch `NEEDS_ATTENTION`, error `EMPTY_FILE`.

Moi counter trong cong thuc phai bang count exact immutable terminal `ImportRow.status` cua batch; field-level balance o muc 5.2 va reconciliation denominator phai bang nhau. Sau khi batch terminal, row disposition, counters, rate, status va `finished_at` khong doi. Neu canonical target pending sau nay reconcile qua audited resolution/replay, incoming alias van giu import-time `ACCOUNTING_PENDING`; current episode/ledger projection va resolution record phan anh recovery, khong viet lai lich su batch hoac numerator. Muon co import result moi, user chay explicit reprocess/import batch moi duoi pinned contract; v1 cam background alias-propagation mutation.

### 10.2. Batch status va quality

`ImportBatch.status`:

```text
UPLOADED -> PROCESSING -> COMPLETE | PARTIAL | NEEDS_ATTENTION | REJECTED
```

| Final status | Dieu kien |
|---|---|
| `REJECTED` | Confirmed source binding/read lost or deterministic revalidation mismatch before row admission; invalid file itself already stopped at Upload REJECT/no batch |
| `COMPLETE` | Rate bang 1 va khong pending/quarantine |
| `PARTIAL` | Rate > 0.98 va < 1, khong co blocking projection conflict |
| `NEEDS_ATTENTION` | Rate <= 0.98, rate null, hoac co blocking projection conflict |

Status is derived from exact integers, never the rounded persisted rate: COMPLETE requires `numerator = denominator` and no pending/quarantine; PARTIAL requires `98 * denominator < 100 * numerator < 100 * denominator` and no blocking projection conflict; NEEDS_ATTENTION requires null rate, `100 * numerator <= 98 * denominator`, or a blocking conflict. Thus acceptance "tren 98%" is strict and the exact 98% boundary is NEEDS_ATTENTION. Cross-products use arbitrary-precision integers. Moi batch UI phai hien denominator, numerator va danh sach tung row khong o numerator.

`TradeEpisodeProjection.accounting_quality`:

| Value | Dieu kien |
|---|---|
| `COMPLETE` | Ledger invariant pass va tat ca fee converted |
| `FEE_CONVERSION_MISSING` | It nhat mot fee `UNAVAILABLE` |
| `SEQUENCE_PENDING` | Thu tu fill chua duoc chung minh/resolved |
| `REPLAY_PENDING` | Backfill dang cho xac nhan |
| `INVALID` | Ledger invariant fail; projection khong active |

## 11. Error code va quarantine contract

Error code v1 toi thieu:

| Error code | Cap | Cach xu ly |
|---|---|---|
| `FILE_TOO_LARGE` | File | Reject batch |
| `TOO_MANY_ROWS` | File | Reject batch |
| `INVALID_UTF8` | File | Reject batch |
| `CSV_PARSE_ERROR` | File | Reject batch |
| `HEADER_MISMATCH` | File | Reject batch |
| `EMPTY_FILE` | Batch | Needs attention |
| `COLUMN_COUNT_MISMATCH` | Row | Quarantine |
| `INVALID_TIMESTAMP` | Row | Quarantine |
| `INVALID_DECIMAL` | Row | Quarantine |
| `DECIMAL_OVERFLOW` | Row/projection/API | Row quarantine, hoac projection khong publish/API validation fail; khong silent round |
| `INVALID_SIDE` | Row | Quarantine |
| `UNKNOWN_INSTRUMENT` | Row | Quarantine |
| `INSTRUMENT_NOT_IMPORT_SUPPORTED` | Row | Quarantine |
| `INSTRUMENT_VALIDITY_AMBIGUOUS` | Row | Quarantine |
| `INSTRUMENT_IDENTITY_CONFLICT` | Catalog publish | Reject version; khong move active pointer |
| `UNSUPPORTED_QUOTE_ASSET` | Row | Quarantine |
| `EXECUTED_ASSET_MISMATCH` | Row | Quarantine |
| `AMOUNT_ASSET_MISMATCH` | Row | Quarantine |
| `AMOUNT_PRICE_MISMATCH` | Row | Quarantine |
| `BUY_BASE_FEE_EXCEEDS_EXECUTED` | Row | Quarantine |
| `DUPLICATE_MULTIPLICITY_AMBIGUOUS` | Row | Accounting pending |
| `SEQUENCE_AMBIGUOUS` | Row group | Accounting pending |
| `SELL_WITHOUT_OPEN_POSITION` | Row | Quarantine |
| `SELL_EXCEEDS_POSITION` | Row | Quarantine |
| `FEE_CONVERSION_UNAVAILABLE` | Row | Accounting pending |
| `HISTORICAL_REPLAY_CONFLICT` | Row group | Accounting pending |
| `STALE_REPLAY_PREVIEW` | Replay resolution | Reject changed based-on projection/input digest |
| `LEDGER_INVARIANT_FAILED` | Projection | Needs attention; khong publish |
| `INVALID_SEQUENCE_RESOLUTION` | Resolution command | Reject; khong doi row/projection |
| `STALE_PLAN_MATCH_RESOLUTION` | Plan-match command | Reject stale based-on version; khong mutate |
| `INVALID_PLAN_MATCH_CANDIDATE` | Plan-match command | Reject ID/revision ngoai frozen candidates/basis |
| `PLAN_ALREADY_CONSUMED` | Plan-match command | Reject plan da consume boi episode khac |
| `REVIEW_EPISODE_NOT_CLOSED` | Review command | Reject episode khong co active CLOSED projection |
| `REVIEW_ALREADY_COMPLETED` | Complete review command | Reject command moi; idempotent retry tra record cu |
| `STALE_REVIEW_REVISION` | Revise review command | Reject expected revision mismatch |
| `STALE_EPISODE_PROJECTION` | Review command | Reject expected active projection mismatch |
| `REVIEW_VALIDATION_FAILED` | Review command | Reject missing/invalid structured field hoac taxonomy/checklist mismatch |
| `REVIEW_ATTACHMENT_NOT_READY` | Review command | Reject attachment sai workspace, chua scan/pass hoac khong ACTIVE |
| `STALE_EPISODE_ELIGIBILITY` | Metric eligibility command | Reject based-on projection khong active |
| `WEEK_NOT_ENDED` | Weekly completion command | Reject completion truoc authoritative `WeeklyCohort.cohort_end_at_utc` |
| `WEEKLY_REVIEW_PRECONDITION_FAILED` | Weekly completion command | Reject report/experiment ownership, state hoac user-week mismatch |

`error_detail_json` chi duoc co column name/index, rule, expected type/range, safe length/count va hash/truncated-redacted diagnostic; cam durable raw cell, raw row, note hoac noi dung workspace khac. Sua file va upload lai khong xoa row loi cu; batch cu van la audit record, nhung private raw upload/quarantine object van bi purge trong 24 gio.

## 12. Metric dictionary

### 12.1. Eligibility chung

Voi ad-hoc/non-weekly metric request, reporting interval duoc tao tu requested local boundary trong timezone IANA snapshot, sau do moi doi sang UTC:

```text
reporting_start_at_utc = to_utc(reporting_start_local_inclusive, workspace_timezone)
reporting_end_at_utc = to_utc(reporting_end_local_exclusive, workspace_timezone)
included iff reporting_start_at_utc <= closed_at < reporting_end_at_utc
```

`closed_at` trong phep tren la lower bound `closed_fill.source_time_start`; khong dung interval end-exclusive, import time hoac projection publish time de gan reporting interval. Ad-hoc metric artifact phai snapshot timezone, local/UTC boundaries va TZDB version.

Weekly Lab va north-star la ngoai le producer-boundary: chung bat buoc consume exact immutable `WeeklyCohort.cohort_start_at_utc`/`cohort_end_at_utc`, timezone, TZDB va `cohort_type` do TP-LAB tao theo muc 12.6. TP-ACC khong goi `to_utc` de reconstruct weekly boundary va khong ap alternate timezone-change algorithm. Voi moi loai interval, boundary la half-open; khong dung `closed_at <= reporting_end_at_utc`.

Dat `closed_base_eligible = true` khi episode:

- projection active;
- `state = CLOSED`;
- `closed_at` nam trong resolved ad-hoc interval hoac exact WeeklyCohort interval;
- latest `EpisodeMetricEligibilityEvent` cho exact projection tai `reporting_as_of_at` khong phai `EXCLUDE`, va khong co unresolved replay-version mismatch.

Khong co boolean `eligible` dung chung. Family eligibility la:

```text
gross_eligible = closed_base_eligible
                 and accounting_quality in {COMPLETE, FEE_CONVERSION_MISSING}
                 and gross_realized_pnl_quote is not null
                 and gross ledger invariants pass

net_eligible = closed_base_eligible
               and accounting_quality = COMPLETE
               and net_realized_pnl_quote is not null
               and all ledger invariants pass
```

`episode_gross_pnl_quote` va moi sum/mean gross-only dung `gross_eligible`; episode `FEE_CONVERSION_MISSING` vi the van duoc emit trong gross aggregate. `episode_fee_quote`, net P&L, R/expectancy, win/breakeven, payoff, profit factor, fee ratios, `closed_episode_*`, setup, confidence va adherence dung `net_eligible`. `accounting_completeness_rate` la ngoai le dung `closed_base_eligible` lam denominator. Context aggregate ap dung family eligibility cua metric goc truoc, sau do ap context eligibility o muc 12.5.

Moi response metric phai kem family/policy ID, `eligible_episode_count`, `excluded_episode_count`, exclusion reason counts, filter, reporting interval va exact `algorithm_version = metrics_v1`. Evidence label is `INSUFFICIENT` for N < 2, `EXPLORATORY` for 2 <= N < 30 and `ESTIMATED` for N >= 30; cam mo ta la edge da duoc chung minh.

### 12.2. Financial metrics

MVP co dung mot currency partition `USDT` vi moi instrument, planned risk va account reporting currency deu bat buoc USDT. Implementation van phai partition theo `quote_asset` nhu defensive invariant: neu du lieu quote khac xuat hien do corruption/migration, khong cong, average hoac lap equity curve chung; loai no voi reason `UNSUPPORTED_QUOTE_ASSET` thay vi ngam convert. MVP khong co reporting-currency conversion.

`planned_risk_quote` phai co `planned_risk_asset = USDT`; neu khong, plan revision khong hop le de arm.

| Metric | Cong thuc | Edge case |
|---|---|---|
| `episode_gross_pnl_quote` | Episode `gross_realized_pnl_quote`; family `gross_eligible` | Chi final khi episode closed; van available voi `FEE_CONVERSION_MISSING` neu gross invariants pass |
| `episode_fee_quote` | Tong fee converted cua episode | Null neu co conversion missing |
| `episode_net_pnl_quote` | `gross_pnl - fee_quote` | Null neu co conversion missing |
| `planned_initial_risk_quote` | `planned_risk_quote` tu frozen `VERIFIED` revision | Null neu proof khac VERIFIED hoac risk invalid |
| `r_multiple` | `episode_net_pnl_quote / planned_initial_risk_quote` | Null neu proof khac VERIFIED, risk null hoac <= 0 |
| `mean_expectancy_r` | Arithmetic mean cua non-null `r_multiple` | Null neu N = 0 |
| `median_expectancy_r` | Median cua non-null `r_multiple`; N chan lay mean hai gia tri giua | Null neu N = 0 |
| `mean_r_ci_95` | Khoang doi xung theo exact numeric profile `mean_r_ci_95_v1` ngay duoi bang | Null voi reason `INSUFFICIENT_SAMPLE` neu N < 2; khong goi quantile library tai runtime |
| `win_rate` | `count(net_pnl > 0) / N` | Breakeven nam trong N nhung khong la win |
| `breakeven_rate` | `count(net_pnl == 0) / N` | Exact ledger decimal zero |
| `payoff_ratio` | `mean(net_pnl of wins) / abs(mean(net_pnl of losses))` | Null voi reason `NO_WINS` hoac `NO_LOSSES` |
| `profit_factor` | `sum(net_pnl of wins) / abs(sum(net_pnl of losses))` | Neu co loss: numeric value va `display_state = NORMAL`; neu khong co loss: `value = null`, `null_reason = NO_LOSSES`, `display_state = POSITIVE_INFINITY` khi gross profit > 0, nguoc lai `display_state = UNDEFINED`; API khong bao gio serialize numeric Infinity |
| `fee_drag_pct_of_gross_profit` | `sum(episode_fee_quote) / sum(max(episode_gross_pnl_quote, 0)) * 100` | Null voi reason `NO_GROSS_PROFIT` neu denominator = 0 |
| `fee_pct_of_gross_turnover` | `sum(fee_quote) / sum(gross_amount_quote of fills allocated to the same net_eligible episode cohort) * 100` | Null reason `NO_GROSS_TURNOVER` neu turnover = 0; khong cong fill account-wide ngoai cohort/report interval |
| `accounting_completeness_rate` | `closed_base_eligible` episodes co `accounting_quality = COMPLETE` / tat ca `closed_base_eligible` episodes | Null neu khong co closed episode; report count theo tung exclusion reason |

#### 12.2.1. Closed numeric profile `metrics_decimal_v1`

Every `metrics_v1` DECIMAL/INTERVAL producer uses `metrics_decimal_v1`; there is no implementation-specific math context. Persisted ledger/quote inputs are their exact already-quantized scale-18 decimals. Counts are exact integers. All subsequent operands are treated as mathematical rationals, addition/multiplication/comparison is exact and binary floating point is forbidden.

The one intentional per-episode division boundary is:

```text
r_multiple18 = round_scale18_half_even(
    episode_net_pnl_quote / planned_initial_risk_quote)
```

It exists only for VERIFIED proof with positive exact risk. This canonical decimal is the `r_multiple` exposed in episode drill-down/counterexample payloads and is the only R input consumed by `mean_expectancy_r`, `median_expectancy_r` and `mean_r_ci_95`; downstream code MUST NOT recompute an unrounded net/risk quotient. It is derived deterministically from pinned projection/plan inputs even when not stored as a separate projection column.

Every other division is rounded exactly once at the final MetricSnapshot value boundary to scale 18 with ROUND_HALF_EVEN, then trailing zeros are stripped and negative zero becomes `0`. Exact definitions before that final round are:

```text
mean_expectancy_r   = sum(r_multiple18) / N
median_expectancy_r = middle r_multiple18, or
                       (middle_left + middle_right) / 2 for even N
count_rate          = numerator_count / denominator_count
payoff_ratio        = (sum_win_quote * loss_count) /
                      (abs(sum_loss_quote) * win_count)
profit_factor       = sum_win_quote / abs(sum_loss_quote)
fee_drag_pct        = (sum_fee_quote * 100) / sum_positive_gross_quote
fee_turnover_pct    = (sum_fee_quote * 100) / sum_gross_turnover_quote
```

Complement behavior rates use exact counts `(denominator_count - source_numerator_count) / denominator_count`; they never subtract a rounded source rate from `1`. Sum operands are accumulated exactly from scale-18 source decimals before multiplication/division. Mean/median never average already rounded aggregate means. Sign/win/breakeven comparisons use exact `episode_net_pnl_quote` before any division. Count-rate values are fractions in `[0,1]` with unit `RATIO`; the deterministic renderer multiplies by 100 only for display. Fee percentage values already include `*100` and use unit `PERCENT`. R values use `R`; money uses `USDT`; counts use `EPISODE_COUNT`; duration uses `MILLISECOND`.

For rate/ratio snapshots, persisted numerator/denominator are not optional conventions: count rates copy integer counts as canonical decimal strings; payoff copies `sum_win_quote * loss_count` and `abs(sum_loss_quote) * win_count`; profit factor copies positive/negative absolute sums; fee percentage snapshots copy `sum_fee_quote * 100` and their exact denominator. Mean, median, CI, count and OBJECT metrics have both fields null. The final value must recompute from those fields under this profile.

Final DECIMAL values and each INTERVAL bound must fit signed DECIMAL(38,18), at most 20 integer digits after rounding. Intermediate arithmetic is unbounded; overflow is checked only at an authoritative persisted source boundary or final metric boundary. Final overflow fails `METRIC_DECIMAL_OVERFLOW` and publishes no MetricSnapshot/report; there is no truncation, saturation, Infinity or fallback zero. Null/denominator-zero branches skip division and retain the authoritative reason/display state.

`mean_r_ci_95_v1` la numeric profile dong, technology-neutral layered on `metrics_decimal_v1`. Dau vao la exact canonical decimal `r_multiple18` da quantize cua N episode trong family/segment, theo thu tu episode da cong bo; thu tu khong duoc anh huong ket qua. Cam recompute unrounded net/risk, binary floating point va thay critical value bang mot thu vien thong ke, ke ca khi thu vien do tra gia tri gan hon.

Voi `N >= 2`, coi moi input decimal la mot so huu ty exact va tinh khong round:

```text
mean = sum(r_i) / N
sample_variance = sum((r_i - mean)^2) / (N - 1)
standard_error_squared = sample_variance / N
standard_error = sqrt36_half_even(standard_error_squared)
margin = critical95(N - 1) * standard_error
lower = round_scale18_half_even(mean - margin)
upper = round_scale18_half_even(mean + margin)
```

`sqrt36_half_even(x)` la boi so khong am `k / 10^36` gan `sqrt(x)` nhat; so sanh khoang cach duoc thuc hien bang so huu ty exact sau khi binh phuong cac moc nua don vi, va truong hop cach deu chon `k` chan. Dinh nghia nay la authoritative; Newton, integer square root hoac cach khac chi la implementation neu cho cung `k`. `round_scale18_half_even` chon boi so `k / 10^18` gan gia tri huu ty nhat, tie chon `k` chan. Chi hai bound duoc quantize scale 18; sau do strip trailing zero va normalize negative zero theo canonical decimal grammar. `sample_variance` dung mau so `N - 1`, khong dung population variance. Zero variance tao `[mean, mean]`. Bound co the am va khong clamp.

`critical95(df)` la exact decimal constant trong bang dong sau; `df >= 30` dung literal normal-approximation v1. Day la policy cua metric v1, khong phai lookup bang locale/config:

| `df` | `critical95` | `df` | `critical95` | `df` | `critical95` |
|---:|---:|---:|---:|---:|---:|
| 1 | 12.706204736432095 | 11 | 2.200985160082949 | 21 | 2.079613844727662 |
| 2 | 4.302652729911275 | 12 | 2.178812829663418 | 22 | 2.0738730679040147 |
| 3 | 3.182446305284264 | 13 | 2.1603686564610127 | 23 | 2.0686576104190406 |
| 4 | 2.7764451051977987 | 14 | 2.1447866879169273 | 24 | 2.0638985616280205 |
| 5 | 2.570581835636305 | 15 | 2.131449545559323 | 25 | 2.059538552753294 |
| 6 | 2.4469118511449692 | 16 | 2.1199052992210112 | 26 | 2.055529438642871 |
| 7 | 2.3646242510102993 | 17 | 2.1098155778331806 | 27 | 2.0518305164802833 |
| 8 | 2.306004135204166 | 18 | 2.10092204024096 | 28 | 2.048407141795244 |
| 9 | 2.2621571628540993 | 19 | 2.093024054408263 | 29 | 2.045229642132703 |
| 10 | 2.2281388519649385 | 20 | 2.0859634472658364 | `>= 30` | 1.959963984540054 |

Output cua metric nay map vao `MetricSnapshot.value_type = INTERVAL`, `value_interval_json = { "lowerDecimal": lower, "upperDecimal": upper }`, `unit = R`; moi typed-value field khac null theo `TP-LAB`. `N < 2` cho INTERVAL unavailable voi moi value field null, `null_reason = INSUFFICIENT_SAMPLE`, khong tinh square root hay critical value.

Hai fee ratio phai dung cung exact `net_eligible` episode projection refs, reporting interval va exclusion policy cho ca fee numerator, gross-profit/turnover denominator; khong duoc quet moi fill cua account. Khong co funding/borrow metric trong MVP. Field hoac UI nao ghi "fee/funding" phai hien chi `fee` trong scope nay.

### 12.3. Closed-episode drawdown

Chi tinh tren `net_eligible` closed episodes, khong noi suy mark-to-market drawdown giua episode. Sap episode trong USDT partition theo `(closed_at ASC, episode_id ASC)`. Dat `closed_episode_equity_quote_0 = 0`, `closed_episode_peak_quote_0 = 0` va `closed_episode_peak_at_0 = reporting_start_at_utc`; sau episode i:

```text
closed_episode_equity_quote_i = closed_episode_equity_quote_(i-1)
                                + episode_net_pnl_quote_i
closed_episode_peak_quote_i = max(0, closed_episode_equity_quote_1 ...
                                     closed_episode_equity_quote_i)
closed_episode_drawdown_quote_i = closed_episode_peak_quote_i
                                  - closed_episode_equity_quote_i
closed_episode_maximum_drawdown_quote = max(closed_episode_drawdown_quote_i)
```

Cap nhat peak time deterministic: neu `equity_i >= peak_(i-1)`, dat `peak_i = equity_i` va `peak_at_i = closed_at_i`; equality chon latest equal peak. Neu `equity_i < peak_(i-1)`, giu ca peak va peak_at truoc do. Mot underwater spell bat dau tai `peak_at` dang co khi episode dau tien dua equity xuong duoi peak, va ket thuc tai `closed_at` cua episode dau tien sau do co equity `>=` spell peak. Neu episode dau tien trong interval bi lo, spell bat dau tai initial `reporting_start_at_utc`; neu chua hoi phuc, ket thuc tai `reporting_end_at_utc` va `is_open = true`.

`closed_episode_time_under_water` la duration lon nhat cua cac spell theo quy tac tren. N = 0 tra null; N > 0 va khong drawdown tra duration 0. UI phai ghi ro day la closed-episode curve, khong phai intratrade/account-equity drawdown.

### 12.4. Plan va adherence metrics

Trong muc nay, `closed eligible` va `planned reviewed eligible` deu bat dau tu `net_eligible`; episode chi co gross hop le khong nam trong denominator.

| Metric | Dinh nghia exact |
|---|---|
| `is_planned` | `plan_proof_status = VERIFIED` va `frozen_plan_revision_id` non-null |
| `planned_trade_rate` | Planned closed eligible episodes / all closed eligible episodes |
| `review_coverage_rate` | Closed eligible episodes co `Review.state = COMPLETED` va latest revision match active projection / all closed eligible episodes |
| `is_adherent` | Verified planned, current-projection Review `COMPLETED`, moi value trong exact `required_checklist_results_json` la `true`, `stop_moved_away = false`, `risk_exceeded = false`, `rule_breach = false` |
| `plan_adherence_rate` | Count adherent / planned closed eligible episodes co current-projection completed Review |
| `stop_moved_away_rate` | Count Review co `stop_moved_away = true` / planned reviewed eligible episodes |
| `risk_exceeded_rate` | Count Review co `risk_exceeded = true` / planned reviewed eligible episodes |
| `rule_breach_rate` | Count Review co `rule_breach = true` / planned reviewed eligible episodes |

Missing Review khong duoc tu coi la non-adherent; no bi loai khoi adherence denominator va duoc phan anh qua `review_coverage_rate`.

`is_reentry_after_loss = true` khi episode truoc do gan nhat cua cung account va instrument da CLOSED voi net P&L < 0, va episode hien tai mo trong `(previous.closed_at, previous.closed_at + 24 hours]`. Khong co episode truoc thi false; episode truoc co accounting incomplete thi null voi reason `PREVIOUS_ACCOUNTING_INCOMPLETE`. Day la nhan mo ta thoi gian, khong khang dinh episode sau bi gay ra boi loss.

### 12.5. Confidence va segmentation

- `confidence_score` v1 la integer 1..5 tu frozen revision.
- Confidence report gom `count`, `mean_expectancy_r`, `median_expectancy_r`, `win_rate` theo tung score; khong dinh nghia alias `mean_r`/`median_r` va khong goi day la probability calibration.
- Performance theo `setup_id` dung `net_eligible`; archived/renamed setup van dung frozen stable ID/revision va khong bi doi thanh unknown. Chi legacy/corrupt record thieu setup identity vao taxonomy bucket `UNKNOWN`; bucket nay van nam trong aggregate setup va co exclusion/quality count rieng.
- Context/regime `UNKNOWN` khong phai taxonomy bucket. Missing ContextSnapshot, missing context label hoac context label `UNKNOWN` chi tang `context_unknown_count` va exclusion reason `CONTEXT_UNKNOWN`; no khong bao gio nam trong numerator, denominator hay bucket cua context-dependent aggregate.
- Context-dependent aggregate chi nhan `ContextSnapshot.quality = COMPLETE`, `aggregationEligible = true`, cung `phase`, `timeframe`, `algorithmVersion` va `parameterSetId`. `ENTRY` va `EXIT`, version hoac parameter set khac nhau la population rieng, cam tron. Snapshot `PARTIAL`, `UNRELIABLE`, unknown/missing va version mismatch chi tang coverage/exclusion counters tuong ung.
- Bat ky so sanh segment nao cung phai hien N; N < 2 la `INSUFFICIENT`, 2..29 la `EXPLORATORY`, va comparative copy bi suppress cho `INSUFFICIENT`.
- `uncertainty_status = INSUFFICIENT` neu N < 2, `EXPLORATORY` neu 2 <= N < 30, va `ESTIMATED` neu N >= 30. `ESTIMATED` khong dong nghia edge hay quan he nhan qua.

### 12.6. North-star `verified_review_week_rate_v1`

Exact metric ID la `verified_review_week_rate`; exact metric version la `verified_review_week_rate_v1`; engine version van la `metrics_v1`. MVP co mot owner/user trong workspace, nen `user_id` cua user-week bang `Workspace.owner_user_id`.

TP-LAB so huu immutable `WeeklyCohort`; TP-ACC khong tu reconstruct week boundary. North-star consume cac field authoritative `weekly_cohort_id`, `cohort_type`, `workspace_id`, `user_id`, `workspace_timezone`, `tzdb_version`, local boundaries, `cohort_start_at_utc`, `cohort_end_at_utc` va predecessor/successor chain.

`REGULAR` cohort snapshot IANA timezone/TZDB tai luc lock va co natural boundary:

```text
cohort_start_local = Monday 00:00:00 inclusive
cohort_end_local   = next Monday 00:00:00 exclusive
cohort_start_at_utc = to_utc(cohort_start_local, workspace_timezone, tzdb_version)
cohort_end_at_utc   = to_utc(cohort_end_local, workspace_timezone, tzdb_version)
completion_deadline_at_utc = cohort_end_at_utc + 72 hours
```

Doi workspace timezone duoc schedule tai exact UTC end cua REGULAR cohort cu. TP-LAB tao mot `TRANSITION` cohort tu boundary do den Monday 00:00 tiep theo trong timezone moi, roi tao REGULAR cohorts theo timezone moi. Cohort chain bat buoc `previous.cohort_end_at_utc = next.cohort_start_at_utc`, khong overlap/gap; moi closed episode thuoc dung mot cohort cho reporting. `TRANSITION` cohort van render/report episode nhung luon bi loai khoi north-star numerator/denominator voi reason `TRANSITION_COHORT`.

Boundary la half-open. Episode duoc gan khi `cohort_start_at_utc <= episode.closed_at < cohort_end_at_utc`; `closed_at` la lower bound cua closing fill theo muc 5.4. Khong dung `closed_time_end_exclusive`, Review time, import time hoac report time. `north_star_episode_eligible = true` khi va chi khi cohort la `REGULAR`, projection active, `state = CLOSED`, `accounting_quality = COMPLETE`, `net_realized_pnl_quote` non-null, ledger invariants pass, latest exact-projection eligibility event khong `EXCLUDE`, va `closed_at` nam trong cohort. Pending ReplayConflictPreview khong thay source active; chi confirmed atomic publish thay projection tai as-of sau. Context missing, `UNKNOWN`, `PARTIAL` hay `UNRELIABLE` khong anh huong north-star eligibility. Episode proof `AMBIGUOUS`, `LATE` hoac `UNMATCHED` van nam trong eligible denominator nhung khong nam trong verified numerator.

`WeeklyReviewCompletion` la source event rieng, append-only va toi thieu co:

```text
weekly_review_completion_id
workspace_id
user_id
weekly_cohort_id
cohort_type
weekly_report_revision_id
behavioral_experiment_revision_id
cohort_start_local
cohort_end_local
workspace_timezone
tzdb_version
cohort_start_at_utc
cohort_end_at_utc
completed_at
actor_user_id
idempotency_key
recorded_at
```

TP-LAB so huu schema `WeeklyCohort`, `WeeklyReportRevision` va `BehavioralExperimentRevision`; TP-ACC chi so huu completion event/FK va metric timing. Completion command bat buoc ba FK `weekly_cohort_id`, `weekly_report_revision_id` va `behavioral_experiment_revision_id` non-null, cung workspace/user/source cohort; cohort nguon bat buoc la `REGULAR` va `completion_eligible_cohort = true`. Report revision phai la current `PUBLISHED` revision cua cohort nguon. Experiment revision phai do user confirm, tham chieu chinh report revision do va target next chronological `REGULAR` cohort, bo qua moi `TRANSITION`; target chua bi thay the tai command time. Copied timezone/boundaries phai exact bang immutable cohort. Kiem tra ownership/status va insert completion la mot transaction; sai precondition fail `WEEKLY_REVIEW_PRECONDITION_FAILED`.

`completed_at = recorded_at` do trusted server UTC clock gan; client timestamp khong duoc nhan. `(workspace_id, idempotency_key)` va `(workspace_id, user_id, weekly_cohort_id)` unique. Retry cung idempotency key hoac completion thu hai cho cung cohort tra event da co, khong tao effect moi. Command truoc `cohort_end_at_utc` bi reject `WEEK_NOT_ENDED`; command cho `TRANSITION` bi reject `WEEKLY_REVIEW_PRECONDITION_FAILED`; completion muon cua cohort `REGULAR` van duoc persist de audit nhung khong dat deadline. Report/experiment sua sau do tao TP-LAB revision moi, khong mutate completion event da tham chieu exact revision cu.

Mot user-week chi final/evaluation-ready khi `reporting_as_of_at >= completion_deadline_at_utc`. Dat:

```text
eligible_episode_count = count(north_star_episode_eligible)
eligible_user_week = cohort_type = REGULAR
                     and eligible_episode_count >= 3

verified_episode_count = count(eligible episode where
    plan_proof_status = VERIFIED
    and frozen_plan_revision_id is not null)

verified_coverage_pass = eligible_user_week
                         and 5 * verified_episode_count
                             >= 4 * eligible_episode_count

completion_pass = exists WeeklyReviewCompletion where
                  completion.weekly_cohort_id = cohort.weekly_cohort_id
                  and cohort_end_at_utc <= completed_at
                  and completed_at < completion_deadline_at_utc

qualifying_user_week = eligible_user_week
                       and verified_coverage_pass
                       and completion_pass

verified_review_week_rate = count(qualifying_user_week)
                            / count(eligible_user_week)
```

Phep so sanh coverage dung integer cross-multiplication o tren de boundary 0.8 exact. `TRANSITION` cohort va REGULAR cohort co duoi 3 eligible episode khong nam trong numerator hoac denominator. Neu denominator bang 0, metric value la `null` voi reason `NO_ELIGIBLE_USER_WEEK`, khong phai 0. Completion missing/late hoac verified coverage duoi 0.8 lam eligible REGULAR cohort nam trong denominator nhung khong nam trong numerator.

North-star `MetricSnapshot` bat buoc persist:

```text
metric_snapshot_id
metric_id = verified_review_week_rate
metric_version = verified_review_week_rate_v1
algorithm_version = metrics_v1
workspace_id
user_id
reporting_as_of_at
cohort_range_start_sequence
cohort_range_end_sequence_exclusive
cohort_range_refs_json
numerator
denominator
value nullable
null_reason nullable
numerator_weekly_cohort_ids_json
denominator_weekly_cohort_ids_json
user_week_drilldown_json
input_event_digest
created_at
```

Opaque cohort IDs are never compared. Both range bounds are positive integers, start is strictly less than end, and the end sequence need not have a header. Resolve headers with `created_at <= reporting_as_of_at`, same workspace/user and `start <= cohort_sequence < end`; replay each state stream at the as-of, exclude `SUPERSEDED`, and require every retained header to be `LOCKED` with its LOCK event visible and `reporting_as_of_at >= cohort_end_at_utc + 72 hours`. `cohort_range_refs_json` is the nonempty array of exact `{ "weekly_cohort_id": id }` for all retained headers, sorted by `cohort_sequence`; its first sequence equals start and end equals greatest retained sequence plus one. Any skipped numeric sequence must resolve only to a visible SUPERSEDED header. Retained refs must form one predecessor chain with exact adjacent UTC boundaries. Thus a latest final range uses `last_sequence + 1` without inventing an end ID; a gap, duplicate sequence, foreign user/workspace, unlocked/not-final cohort or non-superseded omitted header rejects the snapshot.

`numerator_weekly_cohort_ids_json` and `denominator_weekly_cohort_ids_json` are sorted filtered arrays of the same exact typed cohort keys, not scalar IDs. Numerator contains exactly entries with `qualifyingUserWeek = true`; denominator exactly entries with `eligibleUserWeek = true`. Their lengths equal integer `numerator`/`denominator`. `value = ratio18(numerator, denominator)`: exact integer division rounded once to scale 18 with `ROUND_HALF_EVEN`, then strip trailing zeros and normalize negative zero. Denominator zero requires `value = null`, `null_reason = NO_ELIGIBLE_USER_WEEK`; otherwise `null_reason = null`.

`user_week_drilldown_json` is an array sorted by cohort sequence. Every element has this exact member set; all nullable members are present:

```json
{
  "behavioralExperimentRevisionRecordKey": null,
  "candidateEpisodes": [{
    "eligibilityEventRecordKey": null,
    "projectionRecordKey": { "episode_id": "...", "projection_version": 1 },
    "result": "ELIGIBLE",
    "resultReason": "ELIGIBLE"
  }],
  "cohort": {
    "cohortEndAtUtc": "...",
    "cohortEndLocalExclusive": "...",
    "cohortRecordKey": { "weekly_cohort_id": "..." },
    "cohortSequence": 1,
    "cohortStartAtUtc": "...",
    "cohortStartLocal": "...",
    "cohortType": "REGULAR",
    "completionDeadlineAtUtc": "...",
    "endBoundaryResolution": "EXACT",
    "lockEventRecordKey": { "weekly_cohort_state_event_id": "..." },
    "previousCohortRecordKey": null,
    "startBoundaryResolution": "EXACT",
    "timezoneChangeScheduleRecordKey": null,
    "tzdbVersion": "...",
    "workspaceTimezone": "Asia/Ho_Chi_Minh"
  },
  "completionPass": false,
  "coverageComparison": {
    "leftOperand": 0,
    "operator": ">=",
    "rightOperand": 0
  },
  "eligibleEpisodeCount": 0,
  "eligibleEpisodeProjectionRecordKeys": [],
  "eligibleUserWeek": false,
  "excludedEpisodeProjections": [],
  "qualifyingUserWeek": false,
  "userWeekExclusionReasons": ["FEWER_THAN_3_ELIGIBLE_EPISODES"],
  "verifiedCoveragePass": false,
  "verifiedEpisodeCount": 0,
  "verifiedEpisodeProjectionRecordKeys": [],
  "weeklyReportRevisionRecordKey": null,
  "weeklyReviewCompletionRecordKey": null
}
```

The cohort object copies its exact immutable header fields and selected LOCK event; `completionDeadlineAtUtc = cohortEndAtUtc + 72 hours`. `previousCohortRecordKey` and `timezoneChangeScheduleRecordKey` equal nullable header FKs. Unknown/missing member or copied-value mismatch rejects. The drill-down has exactly one element per `cohort_range_refs_json` key.

For each retained cohort, `candidateEpisodes` is every TradeEpisodeProjection active at `reporting_as_of_at`, `state = CLOSED`, whose `closed_at` is inside the cohort half-open UTC interval, sorted by `(closed_at, episode_id, projection_version)`. `eligibilityEventRecordKey` is the greatest visible event sequence whose `based_on_projection_version` equals that exact active projection, or null. A pending ReplayConflictPreview does not replace or exclude its based-on active projection; only atomic `CONFIRM_REPLAY` publication changes the active projection/event evidence at a later as-of.

`resultReason` is the first matching value below; `result = ELIGIBLE` iff reason `ELIGIBLE`, else `EXCLUDED`:

1. `TRANSITION_COHORT` when cohort type is TRANSITION;
2. `LEDGER_INVARIANT_FAILED` when projection/ledger invariants fail;
3. `ACCOUNTING_QUALITY_INCOMPLETE` when accounting quality is not COMPLETE;
4. `NET_PNL_UNAVAILABLE` when net realized P&L is null;
5. `USER_EXCLUDED` when latest eligibility event is EXCLUDE;
6. otherwise `ELIGIBLE`.

`eligibleEpisodeProjectionRecordKeys` is the exact candidate-key projection for ELIGIBLE rows. `excludedEpisodeProjections` is the exact filtered array `{ "projectionRecordKey": key, "reasonCode": resultReason }` for EXCLUDED rows. Both preserve candidate order and partition it once. Verified keys are the eligible subset whose projection has `plan_proof_status = VERIFIED` and non-null frozen plan revision. Counts equal array lengths. `coverageComparison` is exact `{ leftOperand: 5 * verifiedEpisodeCount, operator: ">=", rightOperand: 4 * eligibleEpisodeCount }`; operands are safe nonnegative integers.

For REGULAR, `eligibleUserWeek = eligibleEpisodeCount >= 3`, exclusion reasons are `[]` or exact `["FEWER_THAN_3_ELIGIBLE_EPISODES"]`, and `verifiedCoveragePass = eligibleUserWeek && leftOperand >= rightOperand`. For TRANSITION, eligible keys/count are empty/zero, every candidate is excluded first by `TRANSITION_COHORT`, eligible/coverage/qualifying are false and user-week reasons equal `["TRANSITION_COHORT"]`.

At the as-of, select the unique same-cohort WeeklyReviewCompletion with `recorded_at <= reporting_as_of_at`. If absent, completion/report/experiment refs are all null and completionPass false. If present, all three refs are non-null: report/experiment keys equal its immutable FKs, and `completionPass` is exact `cohortEndAtUtc <= completed_at < completionDeadlineAtUtc`. `qualifyingUserWeek = eligibleUserWeek && verifiedCoveragePass && completionPass`. Late completion remains referenced but fails. No completion is valid for TRANSITION.

`input_event_digest` is lowercase SHA-256 of RFC 8785 bytes of this exact object:

```json
{
  "algorithmVersion": "metrics_v1",
  "cohortRange": {
    "endSequenceExclusive": 2,
    "resolvedCohortRecordKeys": [{ "weekly_cohort_id": "..." }],
    "startSequence": 1
  },
  "denominatorCohortRecordKeys": [],
  "metricId": "verified_review_week_rate",
  "metricVersion": "verified_review_week_rate_v1",
  "numeratorCohortRecordKeys": [],
  "reportingAsOfAt": "...",
  "userId": "...",
  "userWeekDrilldown": [],
  "workspaceId": "..."
}
```

Every array is the exact persisted array above. All embedded cohort/event/projection/completion/report/experiment keys are same-workspace typed FKs and reference records visible at the as-of; every public/shared exception is forbidden here. Snapshot is immutable and idempotent unique on `(workspace_id, user_id, metric_version, cohort_range_start_sequence, cohort_range_end_sequence_exclusive, reporting_as_of_at, input_event_digest)`. Replay with the same source records produces identical digest/output; an event committed after the as-of is absent until a new snapshot.

### 12.7. Metric ngoai MVP

`MFE`, `MAE` va volatility-normalized return nam ngoai MVP. Accounting engine/API khong khai bao field, placeholder hay reason code cho cac metric nay. Client khong duoc suy ra chung tu fill `Price`; them cac metric nay sau MVP yeu cau contract version moi va market-path methodology rieng.

## 13. Golden fixture matrix

Fixture test phai dong bang instrument catalog, trusted server timestamps va market bars; khong goi network. Moi fixture phai assert row disposition, episode boundary, ledger deltas, quality va metric lien quan.

| ID | Input cot loi | Expected |
|---|---|---|
| `F01_quote_fee_round_trip` | BUY 1 BTC, Amount 100 USDT, fee 0.1 USDT; SELL 1, Amount 120, fee 0.12 USDT | 1 CLOSED episode; gross 20; fee 0.22; net 19.78 |
| `F02_partial_wac` | BUY 1@100 fee .1 USDT; BUY 1@120 fee .12; SELL 1.5@130 Amount 195 fee .195 | OPEN Q .5, B 55, avg 110; gross 30; known fee .415; net-to-date 29.585 |
| `F03_partial_wac_close` | F02 + SELL .5 Amount 45 fee .045 | CLOSED; gross 20; fee .46; net 19.54; ending basis 0 |
| `F04_buy_base_fee` | BUY 1 BTC Amount 100 fee .01 BTC; SELL .99 Amount 118.8 fee .1188 USDT | BUY opens Q .99, B 99; CLOSED gross 19.8; fee 1.1188; net 18.6812 |
| `F05_third_fee_direct` | Round trip gross 20; BUY fee .01 BNB voi eligible BNBUSDT bar close 300; SELL fee .01 BNB voi eligible bar close 320; moi bar revision co hai source observation hop le | fee 6.2; net 13.8; moi bar co `bar_end_exclusive <= source_time_start`; conversion retain exact bar ID va aligned observation ID da chon, replay/export khong doi sang observation con lai |
| `F06_third_fee_inverse` | Quote USDT, third fee 4 TRY; khong co TRYUSDT; eligible USDTTRY bar close 40 TRY/USDT | Rate .025 USDT/TRY; fee .1 USDT; `INVERSE_1M_CLOSE`; retain conversion catalog version, immutable pair metadata, bar ID/end-exclusive va aligned selected source-observation ID |
| `F07_third_fee_missing` | Episode nhieu fill, mot fill co third-asset fee khong co valid bar voi `bar_end_exclusive <= source_time_start` | Chi row cua fill thieu conversion la `ACCOUNTING_PENDING`; cac fill own-ledger complete la `RECONCILED`; episode `FEE_CONVERSION_MISSING`, gross eligible, net null va net excluded |
| `F08_exact_file_reimport` | Upload cung bytes hai lan cung workspace/account/adapter/catalog; retry preview/confirm; subcase catalog version moi va canonical row reconciled/pending/quarantined | Moi upload co one zero-business preview; confirm retry co one batch/IMPORT chain; cung exact key tao alias khong reparse, clone dung durable row number/hash/sanitized disposition refs va counters nhung zero new fill/ledger effect; catalog version moi la reprocess batch khong alias; incoming row theo canonical target la `DUPLICATE`/`ACCOUNTING_PENDING`/`QUARANTINED`, chi reconciled target vao numerator |
| `F09_overlap_unique` | File 2 lap mot unique signature va them mot signature moi | Mot `DUPLICATE`, mot `RECONCILED`; khong double count |
| `F10_duplicate_multiplicity` | Hai row y het trong file 1; file 2 chi co mot row cung signature; resolve ACCEPT va MARK voi crash/retry | Ambiguous row co immutable StagedFill/no dedup key; ACCEPT tao one NormalizedFill + ADMITTED disposition, MARK tao zero candidate fill + DISCARDED disposition/target; moi candidate co exactly one fate |
| `F11_header_extra_column` | Header them `OrderId`, retry UPLOAD_VALIDATE va thu ConfirmImport bang fabricated/stale preview ID | Upload `REJECTED`/`HEADER_MISMATCH`; zero ImportPreview/ImportBatch/ImportRow/fill/business row; confirm fail zero work-sequence allocation |
| `F12_asset_mismatch` | Pair BTCUSDT nhung Executed ket thuc `ETH` | Row `QUARANTINED`, denominator tang mot |
| `F13_sell_without_buy` | SELL khi Q = 0 | `SELL_WITHOUT_OPEN_POSITION`; khong episode |
| `F14_oversell` | Q = 1, SELL 1.1 | `SELL_EXCEEDS_POSITION`; Q van 1 |
| `F15_episode_boundary` | BUY, partial SELL, BUY, SELL ve zero, BUY | Hai episode; BUY giua luc OPEN thuoc episode 1; BUY cuoi mo episode 2 |
| `F16_plan_before_fill` | Plan ARMED/revision luc 09:00:00.000Z; first fill interval bat dau 09:01:00.000Z | `VERIFIED`; auto-match/consume; freeze revision; sua sau fill khong doi R |
| `F17_plan_same_second` | Plan ARM hoac selected revision luc 09:01:00.500Z; source fill interval SECOND `[09:01:00,09:01:01)`; chay confirm/remove, va multi-candidate select subcase | Initial `AMBIGUOUS`; single candidate auto-associate/consume; moi resolution tao projection moi nhung frozen revision null, `is_planned=false`, R null; multi-candidate chi select ID trong frozen candidates; khong action nao nang thanh VERIFIED |
| `F18_sequence_ambiguous` | BUY va SELL co interval timestamp giao nhau, order lam doi WAC/boundary, roi co later fills cung instrument; sau do co audited `SET_SEQUENCE` | Truoc resolution group va dependent suffix `ACCOUNTING_PENDING`, khong resume allocation/event sequence sau gap; sau resolution replay ca suffix, sequence unique contiguous 1..N, ton trong source partial order, first/closing identity va interval fields khop allocation |
| `F19_backfill_review_conflict` | Import fill cu lam doi episode da co Review/eligibility event, cover same-ID va split/merge, sau do `CONFIRM_REPLAY` exact preview | Projection cu active den confirm; preview map old/new refs + review/plan/eligibility impact; same ID -> `RECONFIRM_REQUIRED`, new IDs khong auto-copy Review/event; explicit new eligibility decision; sequence moi 1..N, old projection/revision/sequence bat bien |
| `F20_final_basis_rounding` | Nhieu partial SELL tao repeating decimal, SELL cuoi dung full Q | CLOSED co Q = 0 va B = 0 exact |
| `F21_rate_99_of_100` | 99 reconciled/duplicate, 1 quarantined | Rate .99, batch `PARTIAL`, dat gate > .98 |
| `F22_rate_98_of_100` | 98 reconciled/duplicate, 2 quarantined | Rate .98, batch `NEEDS_ATTENTION`, khong dat gate |
| `F23_zero_fee` | Fee `0BNB`, khong co BNB bar | Conversion `EXACT`, value 0, accounting complete |
| `F24_price_amount_tolerance` | Mot row trong tolerance, mot row vuot tolerance | Row dau admitted dung Amount; row sau `AMOUNT_PRICE_MISMATCH` |
| `F25_episode_identity_replay` | Publish episode lan dau, replay SAME_ID, sau do confirm SPLIT/MERGE/REMOVED va tai xuat exact opening tuple | UUIDv5 bytes khop literal namespace/framing; first header/projection `created_at` bang nhau; SAME_ID giu header/timestamp va tang version; split/merge resolve theo opening BUY; removed header van export as-of va exact tuple reuse cung ID, khong duplicate header |
| `F26_same_millisecond_state_order` | CREATE/ARCHIVE/REACTIVATE setup, ARM/CONSUME plan va EXCLUDE/RESTORE episode dung cung millisecond voi opaque IDs co lexical order nguoc commit order | Moi stream co sequence contiguous theo commit/validated order; source replay, as-of API va export pointer deu chon greatest visible sequence, khong doi state khi doi opaque ID |
| `F27_public_catalog_taxonomy_bytes` | Hai producer doc cung instrument/conversion metadata va frozen Review taxonomy; publish hai catalog versions cung millisecond voi reversed IDs | Exact row/version/event hashes va SHARED_PUBLIC export bytes identical; current catalog theo greatest publish sequence; taxonomy item order/type/hash khop; reuse version/label/symbol mapping voi bytes khac bi reject |
| `F28_replay_preview_contract` | SAME_ID/SPLIT/MERGE/REMOVED/ADDED preview; mutate mapping order/cardinality, proposal hash, fill/plan/review/eligibility key, decision partition, expiry va active source before confirm | Exact nested bytes/digest and typed closure pass only valid preview; every mutation/stale/expired/cross-tenant case rejects with zero publish; valid CONFIRM creates proposal-equal projections and one explicit eligibility decision each, retry idempotent |
| `F29_revision_hash_bases` | Fixed SetupPresetRevision, TradePlanRevision and ReviewRevision with null/non-null thesis/screenshot/OTHER/checklist variants; plan decimal pairs `101`/`101.0`/`101.00` and risk `5`/`5.00000000`; leading-zero/sign/exponent/space/overflow variants; thesis trim/Unicode/1,000-scalar boundaries; mutate every semantic field/order/null once | Exact RFC 8785 bytes/content SHA-256 match across two producers; every trailing-zero lexical equivalent is accepted and normalizes to identical persisted decimal/API/export/hash bytes, while leading zero, sign, exponent, whitespace and overflow reject; after normalization each semantic field/order/null mutation changes hash or rejects; deleted screenshot bytes do not rewrite Review hash |
| `F30_plan_proof_basis_closure` | Zero/single/multiple candidate sets, every exclusion reason, expiry `-1ms`/equality/`+1ms`, delayed/duplicate lazy EXPIRE materialization, new-arm slot reuse, current-transaction CONSUME vs pre-existing same-episode CONSUME; mutate watermark/order/key/timestamp/exclusion/selected candidate | Exact effective state and `plan_proof_basis_json` bytes/status/reason are identical regardless materialization delay; equality is expired, EXPIRE `recorded_at` never changes proof, current CONSUME is above watermark while prior CONSUME is pinned; only exact single candidate can be selected and every duplicate/stale/cross-workspace/mutation rejects |
| `F31_import_resolution_payloads` | All four actions with golden payload bytes; mutate unknown/missing member, target key, group membership/order/digest, outer anchor, partial-order violation, tenant and idempotent retry payload | Valid resolutions produce the exact dedup/duplicate/sequence/replay effect once; every malformed, stale, dangling, cross-workspace, partial-set, partial-order or changed-retry case rejects with zero business mutation |
| `F32_ledger_entry_contract` | Quote/base/third-asset fee, zero/unavailable fee, partial/final SELL, replay and old/new conversion at exact publish boundary; delete/duplicate/reorder/split an entry or mutate every sign/null/conversion ref | Exactly two deterministic `TRADE`/`FEE` entries per allocation, contiguous sequence and UUIDv5 bytes; unique half-open conversion selector, row formulas, allocation recurrence and projection sums agree; every cardinality/sign/null/FK/order mutation rejects |
| `F33_fee_conversion_path_bytes` | Direct/inverse bars with sole/resolved/unresolved revisions and multiple observations plus native/fill-rate/zero/unavailable; mutate path/index/catalog/bar/resolution/observation/current substitution | Shared `market_bar_as_of_v1`, exact null table, aligned keys/path bytes, stored rate/value and request provenance agree with MCE; stale candidate resolution, wrong observation sequence and every mutation reject without reselection |
| `F34_north_star_snapshot_closure` | Latest-range sentinel, REGULAR/TRANSITION and superseded sequence gaps, DST/timezone chain, zero/one/many user-weeks, late/missing completion, pending/confirmed replay and every episode exclusion; mutate one nested ref/value/order/bound/event | Exact range partition, drill-down, typed closure, ratio18 and digest bytes agree across producers; pending preview preserves current source, confirmed replay changes later as-of; duplicate/gap/foreign/unlocked/dangling/order/as-of and one-field mutations reject |
| `F35_mean_r_ci_numeric_profile` | N=0/1; N=2 `[1,3]`; N=3 `[-1.25,0.5,2.75]`; zero variance `[2,2,2]`; N=31 with thirty `0` values and one `31`; run through independent decimal implementations | N<2 is `INSUFFICIENT_SAMPLE`; bounds are respectively `[-10.706204736432095,14.706204736432095]`, `[-4.314530171362140456,5.647863504695473789]`, `[2,2]`, and `[-0.959963984540054,2.959963984540054]`; exact INTERVAL bytes match and any population-variance, binary-float, early-rounding, alternate critical or bound mutation rejects |
| `F36_import_error_detail_privacy` | Every row error family, column and row-level errors, exact/capped length/count boundaries; inject raw cell, filename, exception text, unknown/extra member, wrong column pair, hash and status/null mutation | Only exact eight-member `import_row_error_detail_v1` persists; capped magnitudes/hash bytes match across producers, raw content is absent, status/error/null coupling holds and every malformed/privacy-leaking mutation rejects before durable row commit/export |
| `F37_metrics_decimal_profile` | Exact quote/risk vectors `1/3`, `2/3`, `-1/6`; odd/even R samples, count rate `1/3`, payoff wins `[1,2]`/losses `[-1,-3,-5]`, profit factor and fee percent `1/3`; complement, zero denominator, final half-even tie and overflow | Episode R values are `0.333333333333333333`, `0.666666666666666667`, `-0.166666666666666667`; downstream values consume those decimals, count/profit `1/3` is `0.333333333333333333`, payoff is `0.5`, fee percent is `33.333333333333333333`; exact numerator/denominator/unit/null bytes match and early aggregate rounding, unrounded-R reuse, binary float, alternate scale or overflow publication rejects |
| `F38_average_cost_rounding` | Open states `B/Q = 1/3`, exact half-even ties `1/(2*10^18)` and `3/(2*10^18)`, partial SELL where rounded average reuse changes a later digit, full close and final average overflow | Persisted averages are `0.333333333333333333`, `0`, `0.000000000000000002`; partial cost removes from exact `(B/Q)*q` rounded once, full close removes exact remaining B and leaves Q/B zero+average null; alternate scale/rounded-average reuse rejects and overflow publishes no projection |
| `F39_reconciliation_rate_boundary` | Numerator/denominator `1/3`, `98/100`, `981/1000`, `99/100`, `0/0`, plus huge integer cross-products and binary-float/rounded-status mutations | Persisted rates are `0.333333333333333333`, `0.98`, `0.981`, `0.99`, null; exact 98% is NEEDS_ATTENTION, values strictly above and below 1 are PARTIAL absent blocking conflict, null is NEEDS_ATTENTION; status uses integer cross-multiplication and all alternate bytes/overflow paths reject |

Gia fixture phai duoc viet thanh decimal string; assertion khong dung floating-point tolerance cho ledger result.

## 14. Implementation gates

Importer/accounting chi duoc coi la san sang khi:

1. Tat ca fixture F01-F39 pass deterministic o it nhat hai lan replay tu database rong.
2. Cung tap fill duoc upload theo thu tu file khac nhau tao cung active projection, tru cac case duoc contract yeu cau pending.
3. Preview retry tao mot immutable summary/hash/expiry va zero batch/business row; ConfirmImport retry atomically tra mot batch/IMPORT chain. Exact file re-import materialize alias `ImportRow`/counters theo muc 5.2 nhung khong thay doi fill count, ledger totals hoac episode IDs; overlap import cung khong double-count business effect.
4. Invalid file REJECT tai UPLOAD_VALIDATE va zero preview/batch/business row. Confirmed batch sau file-level validation co `data_rows = reconciled_rows + duplicate_rows + accounting_pending_rows + quarantined_rows`; denominator khop non-blank `ImportRow` count. Rare confirmed pre-admission reject co zero business rows/counters va nullable count/rate, khong fabricate disposition.
5. Moi net metric loai episode thieu fee conversion va tra excluded count; gross-only metric van include no neu gross invariants pass.
6. Plan same-time/after-fill khong bao gio duoc gan nhan pre-fill.
7. Proof fixture assert day du `VERIFIED`, `AMBIGUOUS`, `LATE`, `UNMATCHED`; chi VERIFIED co frozen revision/is_planned/R va ambiguous/late khong the upgrade.
8. Moi canonical NormalizedFill va instrument co quote USDT; inverse fee fixture khong dung bridge hoac current price.
9. Database constraint ngan hai active OPEN episode tren cung key va ngan duplicate `dedup_key`.
10. Property tests tao chuoi long-only BUY/SELL hop le luon giu `Q >= 0`, `B >= 0`; khi close thi Q va B bang 0.
11. Export co provenance hash/row number da sanitize, normalized fill, pinned catalog versions, fee conversion path, active episode projection, frozen plan revision va metric algorithm version; khong export raw cell da purge.
12. Log/telemetry khong ghi raw CSV row hoac noi dung plan; chi ghi IDs, status va error code.
13. Database/property test assert `event_sequence` unique contiguous theo projection, chi duoc tao sau sequence resolution; replay khong mutate sequence cu; first/closing IDs va lower-bound/end-exclusive/precision khop exact source fill.
14. `PlanMatchResolution` fixture assert single-candidate confirm/remove va multi-candidate select, stale-version rejection va idempotent retry; moi output van `AMBIGUOUS`, frozen revision/R null.
15. North-star replay test consume immutable WeeklyCohort, cover Monday/DST, scheduled timezone change + no-gap/overlap TRANSITION exclusion, exact 0.8, 72-hour strict deadline, duoi 3 episode, denominator zero/null va context missing; replay khop included/excluded projection refs va input digest.
16. Context aggregate test loai unknown/missing/non-COMPLETE/version-mismatch vao counters; setup `UNKNOWN` van la taxonomy bucket cua setup aggregate.
17. Contract/API test assert exact version identifiers, `planned_risk_usdt` mapping, immutable setup revision/label va frozen breach/emotion taxonomy versions trong export/replay.
18. Decimal boundary test cover exact 20+18 CSV, 8+8 planned risk, positive/nonnegative rules, wide intermediate, half-even tie va `DECIMAL_OVERFLOW`; khong silent round/wrap.
19. Row-disposition test cover one missing-fee fill trong multi-fill episode, pending dependent suffix, duplicate target o ca ba state va multiplicity ACCEPT/MARK; StagedFill co one immutable disposition/fate, terminal ImportRow/batch counters giu nguyen va numerator deterministic.
20. Closed-episode drawdown test cover first episode loss, consecutive equal peaks va recovery exactly at peak; `peak_at`, duration va reporting boundary deterministic.
21. Catalog replay test doi active version/deactivate pair sau import/arm; fill, plan, conversion va replay van dung pinned immutable versions/pair metadata.
22. Retention test purge private raw upload/quarantine object trong 24 gio, trong khi durable row hash, sanitized error va normalized provenance van replay/export duoc.
23. Review taxonomy test validate exact v1 ID/label/version pairs, exit/breach `OTHER` text rules, boolean/checklist cross-field invariants va export moi referenced taxonomy version.
24. Review command test assert active-CLOSED exact projection precondition, atomic/idempotent first completion, stale revision/projection conflict, first `completed_at` preservation, replay `RECONFIRM_REQUIRED` va immutable scanned screenshot join/tombstone export.
25. Weekly completion test assert cohort/report/experiment same-scope FKs, published/confirmed preconditions, trusted timestamp, idempotency va successor-cohort linkage.
26. Exact-file/catalog test assert same-tenant scoped key/FK, catalog-v2 explicit reprocess, stable `instrument_id` across windows va historical conversion pair validity after delist.
27. Tenant schema/authorization test assert direct immutable `workspace_id` va composite same-workspace FK tren moi business child/join/event/resolution; cross-workspace read/write/link fail o database va service.
28. Replay fixture assert immutable affected-set preview/digest, SAME_ID/SPLIT/MERGE mapping, no Review auto-copy, historical projection+revision refs va stale confirmation rejection.
29. Episode metric eligibility test assert append-only EXCLUDE/RESTORE as-of resolution, idempotency, stale projection rejection va explicit decision khi replay/version thay doi.
30. Moi market-bar `FeeConversion` resolve duoc chuoi exact bar revision -> selected observation -> source request -> ingestion batch; replay/export khong reselection.
31. Episode identity test recompute UUIDv5 tu exact namespace/name bytes, assert header insert atomically voi first projection, immutable `created_at`, SAME_ID reuse va SPLIT/MERGE/REMOVED khong tao duplicate hoac dangling header.
32. State-stream concurrency test force same-millisecond timestamps/reversed lexical IDs cho setup, plan va eligibility; contiguous sequence, idempotent retry va source/export as-of state phai identical.
33. Public-data conformance test tao Instrument/catalog publish/version va Review taxonomy version/items tu hai producer, assert exact RFC 8785 hash/bytes, complete atomic row set, contiguous publish sequence va current-pointer result.
34. Replay-preview conformance test recompute proposal/mapping/impact/source digest va exact CONFIRM payload, validate every embedded typed key/cardinality/order/expiry, then assert atomic proposal-equal publish or zero effect.
35. Revision-hash conformance test independently serializes every exact Setup/Plan/Review basis, checks golden bytes/SHA-256 and one-field/null/order mutations; plan trailing-zero variants normalize to identical persisted/hash bytes while leading-zero/sign/exponent/space/overflow reject.

Bat ky thay doi accepted header, dedup signature, accounting formula, fee path, plan matching hoac metric denominator deu la breaking contract change va phai tang contract/algorithm version; khong sua am tham duoi ten v1.

## 15. Nguon doi chieu

- [Binance Academy - How to Get Account Trade History via API](https://academy.binance.com/en/articles/how-to-get-account-trade-history-via-api): xac nhan website cho phep export historical spot trades va mo ta field cap trade; khong duoc dung de ngam dinh CSV header bat bien.
- [Binance Spot REST API - General Information](https://developers.binance.com/en/docs/products/spot/rest-api): doi chieu venue terminology, timestamp va ranh gioi public/private API. MVP nay khong goi private API.
