# Market Context Engine - Dac ta implement MVP

> Tai lieu nay la contract bat buoc cho code, database, API va test cua Market Context Engine (MCE). Khi tai lieu khac mau thuan voi tai lieu nay trong pham vi MVP, tai lieu nay duoc uu tien.

- **Document ID:** `TP-MCE`
- **Document version:** 1.0.0
- **Updated:** 2026-08-27
- **Trang thai:** Ready for implementation
- **Phien ban contract:** 1.0
- **Algorithm version dau tien:** `mce-binance-spot-v1.0.0`
- **Nguon du lieu:** Binance Spot public market data
- **Product:** Spot, long-only
- **Timeframe:** native candle `1m` va `5m`
- **Muc dich:** mo ta boi canh tai thoi diem entry/exit, khong du bao va khong tao tin hieu giao dich

## 1. Quy uoc normative

Tu **MUST**, **MUST NOT**, **SHOULD** va **MAY** trong tai lieu nay mang nghia rang buoc ky thuat.

- Timestamp trong storage, API va phep tinh la Unix epoch milliseconds UTC.
- UI MAY hien thi theo IANA timezone cua Workspace, mac dinh `Asia/Ho_Chi_Minh`, nhung timezone hien thi MUST NOT thay doi session, `hourOfWeek`, baseline hoac ket qua tinh.
- So thap phan tu Binance MUST duoc parse bang decimal, khong parse truc tiep sang binary floating point.
- `ln`, can bac hai va cac phep tinh thong ke dung IEEE-754 binary64. Ket qua output duoc round half-even theo muc 7.8.
- Mot khoang thoi gian viet la `[from, to)` bao gom `from` va khong bao gom `to`.

## 2. Pham vi va non-goals

### 2.1. Trong MVP

- Chi doc candle public cua Binance Spot tu `GET /api/v3/klines`.
- Chi chap nhan instrument co `productType = SPOT`, `venue = BINANCE` va symbol da normalize, vi du `BTCUSDT`.
- Chi tao context cho `TradeEpisode` long-only.
- Tao snapshot o hai phase `ENTRY` va `EXIT`, moi phase co mot snapshot `1m` va mot snapshot `5m`.
- Tinh RVOL, Session VWAP, range/volatility percentile, market regime va Effort-Response tu candle.
- Moi ket qua phai truy nguoc duoc den dung revision cua tung input bar va phien ban thuat toan.

### 2.2. Ngoai MVP

- Linear perpetual, short position, margin, funding va borrow.
- Du lieu tu san khac, volume tong hop nhieu san hoac proxy khac venue.
- User-defined Anchored VWAP. MVP chi co **Session VWAP neo tai 00:00 UTC**.
- Candle tu WebSocket, order book/L2, liquidation, Volume Profile, ML/HMM regime.
- Context realtime tren man hinh lap ke hoach truoc fill, alert co hoi, du bao hoac xep hang instrument.
- Derived taker-buy share hoac taker imbalance. Raw taker fields van duoc luu de bao toan source provenance, nhung khong nam trong output/UI MVP.
- Suy luan y dinh nguoi mua/ban, tich luy, phan phoi, absorption, tiep dien xu huong hoac dao chieu.

## 3. Contract su kien entry va exit

MCE khong tu gom, sap xep lai hoac replay fill. Input authoritative la mot `TradeEpisodeProjection` active, cac `EpisodeFillAllocation` cung `episode_id + projection_version`, va `NormalizedFill` duoc join qua `fill_id`.

### 3.1. Projection active va accounting gate

- Phai co chinh xac mot `TradeEpisodeProjection` active, nghia la `superseded_at IS NULL`, cho `episode_id`.
- Projection, allocation, fill va context job MUST co cung immutable `workspace_id`; worker derive scope tu projection/server event, khong nhan workspace tu client.
- Tat ca allocation MUST co cung `episode_id` va `projection_version` voi projection active.
- Moi allocation MUST resolve den chinh xac mot immutable `NormalizedFill`; existence in this table already means admitted under TP-ACC, and MCE MUST NOT expect a mutable admission-status field.
- `projection.first_fill_at` MUST bang `firstFill.source_time_start`. Voi projection `CLOSED`, `projection.closed_at` MUST bang `closedFill.source_time_start`; voi projection `OPEN`, `closed_fill_id` va `closed_at` MUST deu null.
- `accounting_quality` du de tao context khi va chi khi thuoc `{COMPLETE, FEE_CONVERSION_MISSING}`. Thieu fee conversion khong lam thay doi position sequence, fill time hoac candle context.
- Voi `SEQUENCE_PENDING`, `REPLAY_PENDING`, `INVALID`, khong co projection active duy nhat, hoac join input khong day du, MCE MUST NOT tao `ContextSnapshot`. Orchestrator tra diagnostic `ACCOUNTING_CONTEXT_NOT_READY` va chi retry khi active projection thay doi.
- MCE MUST NOT dung mot projection da co `superseded_at` lam input cho lan tinh/recompute moi. Historical snapshot da tham chieu projection version do van hop le va duoc giu de audit.

### 3.2. Thu tu fill xac dinh

MCE doc allocation theo `event_sequence ASC`; khong tu sap xep bang timestamp, CSV row number hay exchange trade ID.

```text
allocations = ORDER BY event_sequence ASC
```

- `event_sequence` MUST non-null, la integer va unique trong `(episode_id, projection_version)`.
- Day da sort MUST bang chinh xac `1..N`, voi `N = count(allocations)`: allocation dau co `event_sequence = 1`, allocation cuoi co `event_sequence = N`, va moi hai phan tu lien ke thoa `next.event_sequence = current.event_sequence + 1`.
- Allocation dau MUST co `fill_id = projection.first_fill_id`, `position_qty_before = 0` va `position_qty_after > 0`.
- Neu `projection.state = CLOSED`, allocation co `fill_id = projection.closed_fill_id` MUST la allocation cuoi va co `position_qty_after = 0`.
- CSV MVP khong co `exchangeTradeId`; field nay khong duoc yeu cau, khong tham gia ordering va khong tham gia eligibility cua MCE.

### 3.3. Entry

- Episode MUST bat dau khi position quantity chuyen tu `0` sang `> 0`.
- `entryFillId = projection.first_fill_id`; fill nay MUST la `BUY` va allocation cua no tao chuyen trang thai tren.
- `entryAt = entryFill.source_time_start`. Day la lower bound bao thu cua timestamp interval, khong phai import time hay do chinh xac duoc he thong tu them.
- `entryTimeEndExclusive = entryFill.source_time_end_exclusive` va `entryTimestampPrecision = entryFill.source_timestamp_precision` MUST duoc giu trong snapshot provenance.
- `entryReferencePrice = entryFill.price_quote_per_base`, khong phai average entry price va khong phai candle close.
- Cac fill BUY bo sung sau first fill khong tao snapshot entry moi trong MVP.

### 3.4. Exit

- Episode duoc dong khi projection active co `state = CLOSED` va allocation cuoi cua fill `SELL` dua `position_qty_after` ve chinh xac `0`.
- `exitFillId = projection.closed_fill_id`.
- `exitAt = exitFill.source_time_start`.
- `exitTimeEndExclusive = exitFill.source_time_end_exclusive` va `exitTimestampPrecision = exitFill.source_timestamp_precision` MUST duoc giu trong snapshot provenance.
- `exitReferencePrice = exitFill.price_quote_per_base`, khong phai average exit price va khong phai candle close.
- Partial exit khong tao snapshot exit. Episode dang mo khong co snapshot `EXIT`.
- Neu allocation lam position quantity am, integration gate fail; MCE MUST NOT tu sua, tu sap xep lai hoac coi day la short.
- Re-entry sau khi position da ve `0` la mot episode moi.

### 3.5. So snapshot ky vong

- Episode dang mo sau entry: toi da 2 snapshot, `ENTRY x {1m, 5m}`.
- Episode da dong: toi da 4 snapshot, `{ENTRY, EXIT} x {1m, 5m}`.
- Chat luong cua tung timeframe doc lap; loi `1m` MUST NOT lam thay doi ket qua `5m` va nguoc lai.

## 4. Point-in-time va quy tac khong look-ahead

### 4.1. Dinh nghia bar dong hoan toan

Voi timeframe co do dai `D` milliseconds:

```text
barEndExclusive = openAt + D
bar duoc phep dung tai eventAt khi va chi khi barEndExclusive <= eventAt
```

Khong dung `sourceCloseTime <= eventAt` lam dieu kien vi Binance bieu dien close time bang millisecond cuoi cung cua interval. Contract cua he thong luon dung `openAt + D`.

```text
cutoffAt       = floor(eventAt / D) * D
targetOpenAt   = cutoffAt - D
target bar     = bar co openAt = targetOpenAt
```

Vi du:

| Event | Timeframe | Bar moi nhat duoc dung | Bar bi loai |
|---|---:|---:|---:|
| `10:02:30.000Z` | `1m` | `10:01:00Z` | `10:02:00Z` |
| `10:05:00.000Z` | `5m` | `10:00:00Z` | `10:05:00Z` |
| `10:04:59.999Z` | `5m` | `09:55:00Z` | `10:00:00Z` |

Neu khong co bar tai chinh xac `targetOpenAt`, snapshot la `UNRELIABLE`. He thong MUST NOT lui ve mot bar cu hon va van gan no cho event.

### 4.2. As-of cua snapshot

- `asOfAt` bang `entryAt` hoac `exitAt`, tuc `NormalizedFill.source_time_start`; khong bang import time, server receive time hay `computedAt`.
- Projection `first_fill_at/closed_at` theo TP-ACC MUST bang lower bound nay va duoc MCE validate nhu consistency invariant. MCE van derive event interval/precision tu referenced `NormalizedFill`, khong suy ra precision tu scalar projection timestamp.
- Neu source chi co precision `SECOND`, vi du interval `[10:05:00.000, 10:05:01.000)`, MCE dung `10:05:00.000` lam `asOfAt` va giu nguyen `SECOND` cung `eventTimeEndExclusive`. Khong duoc coi lower bound nay la timestamp millisecond chinh xac.
- Moi query, cache lookup va phep chon input MUST co upper bound `barEndExclusive <= asOfAt`.
- Bar duoc ingest sau event van MAY duoc dung neu no da dong truoc event.
- Bar dong sau event MUST NOT duoc dung, ke ca khi no da co san trong database luc recompute.
- Them, sua hoac xoa moi bar co `openAt >= cutoffAt` MUST khong lam thay doi output hay `inputHash` cua snapshot.

## 5. Nguon Binance Spot va ingestion

### 5.1. Endpoint va mapping

Primary base URL la `https://data-api.binance.vision`; deployment MAY dung mot official Binance REST base URL khac khi cau hinh, nhung `sourceVenue` van la `BINANCE` va base URL thuc te MUST duoc luu trong ingestion batch.

Request:

```http
GET /api/v3/klines?symbol={SYMBOL}&interval={1m|5m}&timeZone=0&startTime={from}&endTime={toExclusive-1}&limit=1000
```

Mapping response:

| Index | Field noi bo | Quy tac |
|---:|---|---|
| 0 | `openAt` | epoch ms UTC; unique trong `(venue, symbol, timeframe)` |
| 1 | `open` | decimal `> 0` |
| 2 | `high` | decimal `> 0` |
| 3 | `low` | decimal `> 0` |
| 4 | `close` | decimal `> 0` |
| 5 | `baseVolume` | decimal `>= 0` |
| 6 | `sourceCloseTime` | MUST bang `openAt + D - 1` |
| 7 | `quoteVolume` | decimal `>= 0` |
| 8 | `tradeCount` | integer `>= 0` |
| 9 | `takerBuyBaseVolume` | decimal `>= 0` va `<= baseVolume` |
| 10 | `takerBuyQuoteVolume` | decimal `>= 0` va `<= quoteVolume` |
| 11 | ignored | khong tham gia hash |

Bar hop le khi:

```text
openAt mod D = 0
high >= max(open, close)
low  <= min(open, close)
high >= low
sourceCloseTime = openAt + D - 1
```

Khong uoc tinh OHLCV va khong tu dien gap trong MVP.

### 5.2. Persisted source provenance

Moi network fetch MUST tao cac provenance entity sau. Stable ID khong duoc tai su dung cho retry attempt khac. Request/observation chi duoc tham chieu sau khi attempt da terminal; batch MAY chuyen mot lan tu `RUNNING` sang terminal, sau do moi field provenance la immutable. Mutable scheduler/job state khong nam trong cac entity nay.

`MarketDataIngestionBatch`:

```text
ingestionBatchId
sourceVenue                   BINANCE
productType                   SPOT
sourceBaseUrl
fetcherVersion
startedAt
completedAt                   nullable
status                        RUNNING | COMPLETE | PARTIAL | FAILED
```

`sourceBaseUrl` la exact effective HTTPS origin used by every request in the batch: lowercase scheme/host, optional explicit port, no user-info/path/query/fragment and no trailing slash; primary literal is `https://data-api.binance.vision`. It must be in the immutable deployment allowlist of official Binance REST origins pinned by `fetcherVersion`. Every child `MarketDataSourceRequest.sourceBaseUrl` must equal its batch value byte-for-byte. Failover to another origin starts a new batch; a retry cannot silently change provenance inside one batch. Unknown origin, non-HTTPS URL or request/batch mismatch rejects the request record and cannot produce an observation.

`MarketDataSourceRequest` - moi HTTP attempt la mot record rieng:

```text
sourceRequestId
ingestionBatchId
retryAttempt
sourceBaseUrl
httpMethod                    GET
path                          /api/v3/klines
symbol
timeframe                     1m | 5m
timeZone                      0
startTime
endTime
limit                         1000
requestedAt
fetchedAt                     nullable
httpStatus                    nullable
responseSha256                nullable
responseRowCount              nullable
requestMetadataHash
```

- `sourceBaseUrl` la exact official origin da goi, canonical theo lowercase scheme/host, khong co credentials, query hoac fragment. Query parameters duoc luu trong cac field rieng o tren.
- `fetchedAt` la UTC epoch milliseconds khi response body da duoc nhan day du va `responseSha256` da tinh xong. Failed attempt MAY co `fetchedAt = null` va MUST khong lam source cho bar observation. Contributing request MUST co HTTP `2xx`, `fetchedAt`, `responseSha256` va terminal batch status thuoc `{COMPLETE, PARTIAL}`.
- `requestMetadataHash` la lowercase SHA-256 cua RFC 8785 canonical object gom chinh xac `ingestionBatchId`, `retryAttempt`, `sourceBaseUrl`, `httpMethod`, `path`, `symbol`, `timeframe`, `timeZone`, `startTime`, `endTime`, `limit`, `requestedAt`; response fields khong nam trong hash nay.

`MarketBarSourceObservation` lien ket mot response row voi immutable bar revision:

```text
sourceObservationId
sourceRequestId
marketBarRevisionId
responseRowIndex
observationSequence
```

`observationSequence` starts at 1 and is contiguous per `marketBarRevisionId`, allocated under the revision lock in observation commit order; `(marketBarRevisionId, observationSequence)` and `(sourceRequestId, responseRowIndex)` are unique. Moi `MarketBarRevision` duoc chon cho snapshot MUST co chinh xac mot `MarketBarSourceObservation` duoc chon lam provenance cua lan tinh do. Mot revision MAY co nhieu observation tu cac request khac nhau; authoritative selector o muc 10.1 persists the lowest visible eligible sequence and khong chon lai ngam dinh khi export.

Terminal `MarketDataIngestionBatch`, `MarketDataSourceRequest`, `MarketBarSourceObservation` va `MarketBarRevision` la public-market provenance dung chung, khong chua workspace/user/episode ID. Chung MUST duoc giu bat bien it nhat lau bang bat ky snapshot tham chieu. Xoa mot workspace chi xoa ContextSnapshot/job/reference tenant-owned cua workspace do; khong xoa global source record con duoc workspace khac hoac retention/Terms policy tham chieu. Export workspace chi copy reference-closed public subset ma snapshot/fee conversion cua workspace do su dung.

### 5.3. Pagination

Fetcher MUST bieu dien nhu cau bang cac interval `[from, toExclusive)` da align theo timeframe.

```text
cursor = from
while cursor < toExclusive:
    request startTime = cursor
            endTime   = toExclusive - 1
            limit     = 1000
    validate va sort response theo openAt
    bo record nam ngoai [cursor, toExclusive)
    upsert immutable revision theo logical bar key
    neu response rong: dung va danh dau phan con lai la missing
    next = lastReturned.openAt + D
    neu next <= cursor: fail voi PAGINATION_STALLED
    cursor = next
```

- Page boundary MUST khong tao duplicate output; duplicate cung content hash duoc deduplicate.
- Hai record cung logical key nhung khac content hash tao hai revision va `SOURCE_CONFLICT`; khong overwrite revision cu.
- Mot page ngan hon 1.000 record khong duoc coi la hoan tat neu `lastReturned.openAt + D < toExclusive`; fetcher tiep tuc cho den page rong hoac den `toExclusive`.
- `1m` va `5m` deu fetch native tu Binance. MVP MUST NOT derive `5m` tu `1m`.

### 5.4. Cua so can fetch

Voi moi snapshot/timeframe, planner lay hop cua cac khoang sau va MAY merge cac khoang giao nhau:

1. Current/core: tu `min(sessionStartAt, targetOpenAt - 20D)` den `cutoffAt`.
2. Baseline: voi moi trong 12 tuan truoc, lay hour block cung `hourOfWeek`, cong them `20D` o phia truoc hour block de tinh derived values.

Planner MUST NOT mo rong upper bound qua `cutoffAt`.

### 5.5. Rate limit va retry

- Service MUST doc `REQUEST_WEIGHT` limit hien hanh tu public `exchangeInfo` luc khoi dong va refresh moi 60 phut; khong hard-code tong weight budget.
- Kline request hien duoc scheduler tinh theo route weight do official API cong bo. Scheduler MUST dung token bucket cho tat ca worker chung public IP va chi su dung toi da 80% moi active `REQUEST_WEIGHT` budget.
- Sau moi response, scheduler cap nhat usage tu cac header `X-MBX-USED-WEIGHT-*` neu co.
- Concurrency mac dinh toi da 2 request/IP cho backfill MCE.
- HTTP `429`: dung moi request cho cung IP, ton trong `Retry-After` (so giay theo response Binance); neu header thieu, dung full-jitter trong `[0, min(30s, 2^(attempt-1) seconds)]`. Khong retry qua 5 lan cho mot page.
- HTTP `418`: mo circuit cho cung IP den `Retry-After` (so giay theo response Binance); khong thu lai som.
- HTTP `408`, `5xx` hoac network timeout: toi da 5 lan, full-jitter exponential backoff voi cac cap `1s, 2s, 4s, 8s, 16s`.
- HTTP `4xx` khac: khong retry. Luu status, Binance error code va message da redact.
- Sau khi het retry, snapshot khong duoc tinh nhu du lieu day du; quality pipeline xu ly theo muc 9.

Tai lieu API chuan: [Binance Spot REST API](https://github.com/binance/binance-spot-api-docs/blob/master/rest-api.md).

## 6. Baseline theo mua vu thoi gian

### 6.1. Hour-of-week

`hourOfWeek` duoc tinh tu `targetBar.openAt` trong UTC:

```text
dayIndex: Monday = 0, ..., Sunday = 6
hourOfWeek = dayIndex * 24 + UTC hour
```

Gia tri hop le la `0..167`. Phut trong gio khong tham gia bucket.

### 6.2. Cua so 12 tuan

Dat `W` la Monday `00:00:00.000Z` cua ISO week chua `targetBar.openAt`.

```text
baseline range = [W - 12 * 7 days, W)
baseline bars  = bar trong range va co hourOfWeek bang target hourOfWeek
```

- Baseline chi gom 12 ISO week hoan tat truoc week cua target; khong gom bat ky bar nao trong target week.
- So slot ky vong la `12 * 60 = 720` cho `1m`, va `12 * 12 = 144` cho `5m`.
- Moi candidate cho normalized true range can previous bar lien tuc.
- Moi candidate cho realized volatility 20 bars can du 20 previous bars lien tuc. Cac predecessor nay la input nhung khong lam tang so slot baseline ky vong.
- Baseline khong duoc fallback sang symbol, timeframe, product, venue hoac timezone khac.

## 7. Cong thuc thong ke

Ky hieu `b0` la target bar; `C_i`, `H_i`, `L_i`, `Q_i` lan luot la close, high, low va quote volume. Index `0` la target; index am la bar lien truoc.

### 7.1. Ham dung chung

Median cua day da sort:

- `n` le: phan tu o giua.
- `n` chan: trung binh cong cua hai phan tu giua.

Empirical mid-rank percentile cua `x` tren baseline `B`:

```text
L = count(v in B where v < x)
E = count(v in B where v = x)
percentile(x, B) = 100 * (L + 0.5 * E) / count(B)
```

Robust z-score cua `x`:

```text
m   = median(B)
MAD = median(abs(v - m) for v in B)

if MAD < 1e-12:
    z = 0                  when abs(x - m) < 1e-12
    z = 99 * sign(x - m)  otherwise
else:
    z = clamp((x - m) / (1.4826 * MAD), -99, 99)
```

### 7.2. Relative Volume

MCE dung quote volume vi no so sanh duoc trong cung mot symbol qua cac bar va khop voi Session VWAP.

```text
volumeBaseline = quoteVolume cua baseline bars hop le
medianQuoteVolume = median(volumeBaseline)
rvol = Q_0 / medianQuoteVolume

x_0 = ln(1 + Q_0)
X_B = [ln(1 + Q_b) for b in baseline]
effortPercentile = percentile(x_0, X_B)
volumeRobustZ = robustZ(x_0, X_B)
```

Neu `medianQuoteVolume <= 0`, `rvol` khong kha dung va snapshot la `UNRELIABLE`.

Volume anomaly code:

```text
UNUSUALLY_HIGH_VOLUME khi volumeRobustZ >= 3.5 va effortPercentile >= 97.5
UNUSUALLY_LOW_VOLUME  khi volumeRobustZ <= -3.5 va effortPercentile <= 2.5
NONE                  trong cac truong hop con lai
```

### 7.3. Range va volatility

Previous bar MUST lien tuc: `b[-1].openAt + D = b[0].openAt`.

```text
trueRange_0 = max(
    H_0 - L_0,
    abs(H_0 - C_-1),
    abs(L_0 - C_-1)
)

normalizedTrueRange_0 = trueRange_0 / C_-1
response_0 = ln(1 + normalizedTrueRange_0)
responsePercentile = percentile(response_0, response baseline)
rangeRobustZ = robustZ(response_0, response baseline)
```

Response baseline ap dung cung cong thuc cho tung baseline candidate.

Range anomaly code:

```text
UNUSUALLY_HIGH_RANGE khi rangeRobustZ >= 3.5 va responsePercentile >= 97.5
UNUSUALLY_LOW_RANGE  khi rangeRobustZ <= -3.5 va responsePercentile <= 2.5
NONE                 trong cac truong hop con lai
```

### 7.4. Session VWAP tai 00:00 UTC

```text
sessionStartAt = 00:00:00.000Z cua UTC date chua eventAt
sessionBars = bar co openAt >= sessionStartAt
              va openAt + D <= eventAt

sessionVwap = sum(quoteVolume) / sum(baseVolume)
vwapDistanceBps = (referencePrice / sessionVwap - 1) * 10,000
```

- `referencePrice` la fill price duoc dinh nghia o muc 3.
- Session VWAP chi kha dung khi khong thieu bat ky expected bar nao trong `[sessionStartAt, cutoffAt)` va tong base volume `> 0`.
- Neu event nam truoc khi bar dau tien cua UTC session dong, Session VWAP la `null`, reason `SESSION_HAS_NO_CLOSED_BAR`.
- Dau cua `vwapDistanceBps` chi mo ta reference price nam tren/duoi Session VWAP. No khong phai buy/sell direction hay tin hieu.

### 7.5. Effort-Response

```text
effortHigh   = effortPercentile >= 50
responseHigh = responsePercentile >= 50
```

| Code | Dieu kien | Nhan UI bat buoc |
|---|---|---|
| `E_HIGH_R_HIGH` | high/high | `Khoi luong va bien do deu cao tuong doi` |
| `E_HIGH_R_LOW` | high/low | `Khoi luong cao, bien do thap tuong doi` |
| `E_LOW_R_HIGH` | low/high | `Khoi luong thap, bien do cao tuong doi` |
| `E_LOW_R_LOW` | low/low | `Khoi luong va bien do deu thap tuong doi` |

Khong dung nhan `absorption`, `accumulation`, `distribution`, `thin liquidity` hoac bat ky dien giai ve chu the giao dich.

### 7.6. Market regime xac dinh

Regime dung 21 target/core bars lien tuc, gom target close va 20 previous close.

```text
path20 = sum(abs(C_k - C_(k-1)) for k = -19..0)
displacement20 = abs(C_0 - C_-20)
efficiencyRatio20 = 0                       when path20 = 0
                    displacement20/path20  otherwise

r_k = ln(C_k / C_(k-1)), k = -19..0
realizedVol20 = sqrt(sum(r_k^2) / 20)
realizedVolPercentile = percentile(realizedVol20, baseline RV20 values)
```

Tung baseline RV20 value duoc tinh bang candidate close va 20 previous close lien tuc.

```text
structure = TREND khi efficiencyRatio20 >= 0.35
            RANGE khi efficiencyRatio20 < 0.35

volatility = HIGH_VOL khi realizedVolPercentile > 50
             LOW_VOL  khi realizedVolPercentile <= 50
```

Bon code hop le:

```text
TREND_HIGH_VOL
TREND_LOW_VOL
RANGE_HIGH_VOL
RANGE_LOW_VOL
```

MCE khong gan huong len/xuong cho `TREND`. Threshold la tham so versioned, khong phai tuyen bo pho quat ve thi truong.

### 7.7. Precision va rounding output

| Nhom field | Scale luu tru |
|---|---:|
| `referencePrice`, `sessionVwap` | 12 decimal places |
| `rvol`, `normalizedTrueRange`, `vwapDistanceBps`, robust z-score | 6 decimal places |
| percentile, `efficiencyRatio20`, `realizedVol20` | 6 decimal places |
| `coreCoverage`, `sessionCoverage`, `baselineCoverage` | 6 decimal places |

Tat ca round half-even. Classification MUST dung gia tri full precision truoc rounding; output rounded khong duoc dua nguoc vao threshold.

## 8. Output contract

Mot `ContextSnapshot` toi thieu co:

```text
id
workspaceId
tradeEpisodeId
episodeProjectionVersion
snapshotRevisionNo             positive contiguous integer
phase                         ENTRY | EXIT
eventFillId
eventSequence
eventAt
eventTimeEndExclusive
eventTimestampPrecision       MILLISECOND | SECOND
referencePrice
venue                         BINANCE
productType                   SPOT
symbol
timeframe                     1m | 5m
timezone                      UTC
asOfAt
cutoffAt
targetBarOpenAt
hourOfWeek
sessionStartAt
rvol
effortPercentile
volumeRobustZ
volumeAnomalyCode
normalizedTrueRange
responsePercentile
rangeRobustZ
rangeAnomalyCode
sessionVwap
vwapDistanceBps
effortResponseCode
efficiencyRatio20
realizedVol20
realizedVolPercentile
regimeCode
quality                       COMPLETE | PARTIAL | UNRELIABLE
qualityReasons[]
missingIntervals[]
coreCoverage
sessionCoverage               nullable only when session has 0 expected slot
baselineCoverage
baselineDistinctWeeks
aggregationEligible
algorithmVersion
parameterSetId
inputBarRevisionIds[]
inputBarSourceObservationIds[]
inputBarResolutionIds[]        aligned nullable IDs
sourceRequestIds[]
sourceIngestionBatchIds[]
inputHash
provenanceHash
computedAt
supersedesSnapshotId          nullable
recomputeReason               nullable
```

`workspaceId` derive tu active `TradeEpisodeProjection`, immutable, bat buoc tham gia tenant authorization/constraint va MUST khop episode/fill refs. Metric khong kha dung MUST la `null`, khong dung `0`, empty string hoac gia tri fallback. Coverage va provenance fields la persisted contract, khong phai response-only values.

The three aligned input arrays have equal length. `inputBarRevisionIds[i]` and `inputBarSourceObservationIds[i]` are non-null scalar public IDs; `inputBarResolutionIds[i]` is null iff `market_bar_as_of_v1` saw one visible revision, otherwise it is the exact MarketBarResolution ID whose selected revision equals index `i`. Arrays sort by `(timeframe, openAt, revisionId)` and contain no duplicate revision. Every resolution candidate set and observation must be valid at `computedAt`.

## 9. Data-quality contract

### 9.1. Coverage

`qualityReasons` is a unique array sorted by ASCII code-point order from this closed enum:

```text
BASELINE_COVERAGE_INSUFFICIENT
BASELINE_COVERAGE_PARTIAL
BASELINE_DISTINCT_WEEKS_INSUFFICIENT
BASELINE_DISTINCT_WEEKS_PARTIAL
CORE_GAP
INPUT_HASH_MISMATCH
INVALID_TARGET_OR_CORE_BAR
MISSING_TARGET_BAR
PAGINATION_STALLED
PROVENANCE_HASH_MISMATCH
REQUIRED_METRIC_UNAVAILABLE
SESSION_COVERAGE_INSUFFICIENT
SESSION_COVERAGE_PARTIAL
SESSION_HAS_NO_CLOSED_BAR
SOURCE_INGESTION_BATCH_INVALID
SOURCE_MISMATCH
SOURCE_OBSERVATION_MISSING
SOURCE_REQUEST_INVALID
SOURCE_RESPONSE_INVALID
SOURCE_REVISION_CONFLICT
```

Every applicable reason is retained; no free text/unknown code is allowed. COMPLETE requires `qualityReasons=[]`. PARTIAL requires a nonempty subset of `BASELINE_COVERAGE_PARTIAL | BASELINE_DISTINCT_WEEKS_PARTIAL | SESSION_COVERAGE_PARTIAL | SESSION_HAS_NO_CLOSED_BAR`. UNRELIABLE requires at least one reason and uses INSUFFICIENT rather than PARTIAL for a coverage/distinct-week threshold below the PARTIAL gate. Source conflict maps exactly to `SOURCE_REVISION_CONFLICT`.

`missingIntervals` is an array of exact objects:

```json
{
  "endExclusive": 0,
  "reasonCode": "NO_SOURCE_BAR",
  "scope": "CORE",
  "start": 0
}
```

`scope` is `CORE | SESSION | BASELINE`; reason is `REVISION_CONFLICT | INVALID_SOURCE_BAR | SOURCE_REQUEST_FAILED | PAGINATION_STALLED | NO_SOURCE_BAR`. Bounds are safe UTC epoch milliseconds, aligned to the snapshot timeframe, `start < endExclusive`, and every covered slot is expected in that scope but unusable. Assign each unusable slot the first applicable reason in the order just listed, then coalesce adjacent slots with same scope/reason. Sort final intervals by `(scope order CORE,SESSION,BASELINE; start; endExclusive; reasonCode)`; intervals within a scope do not overlap. COMPLETE requires `[]`; PARTIAL/UNRELIABLE may use `[]` only when their reason has no missing expected slot, such as zero-slot session or hash/provenance failure. Coverage counts must recompute exactly from expected slots minus these intervals and invalid/conflict slots.

```text
coreCoverage = valid contiguous bars trong target + 20 previous / 21
sessionCoverage = valid bars / expected slots trong [sessionStartAt, cutoffAt)
baselineCoverage = candidate co du raw fields va 20 predecessor bars / expected baseline slots
baselineDistinctWeeks = so ISO week co it nhat mot candidate hop le
```

Neu session co 0 expected slot, `sessionCoverage = null` va reason `SESSION_HAS_NO_CLOSED_BAR`.

### 9.2. COMPLETE

Snapshot la `COMPLETE` khi tat ca dieu kien dung:

- target bar ton tai dung `targetOpenAt`;
- `coreCoverage = 1`;
- `sessionCoverage = 1`;
- `baselineCoverage = 1` va `baselineDistinctWeeks = 12`;
- khong co invalid target/core bar, unresolved source conflict, source mismatch hoac estimated field;
- moi input bar revision resolve duoc den persisted source observation, successful source request va ingestion batch;
- tat ca metric bat buoc o muc 8 co the tinh.

`aggregationEligible = true` chi cho `COMPLETE`.

Thus `aggregationEligible = (quality == COMPLETE)`. UNRELIABLE requires every derived field from `rvol` through `regimeCode`, including anomaly/effort-response codes, to be null. COMPLETE requires every mandatory derived field non-null. PARTIAL follows the explicit session/baseline null rules below; null never becomes zero/`NONE`. Unknown quality/reason/interval members or any quality/coverage/reason mismatch rejects persistence/export.

### 9.3. PARTIAL

Snapshot la `PARTIAL` khi khong dat `COMPLETE`, nhung tat ca dieu kien sau dung:

- target bar ton tai va `coreCoverage = 1`;
- `baselineCoverage >= 0.80` va `baselineDistinctWeeks >= 10`;
- `sessionCoverage >= 0.95`, hoac session chua co closed bar;
- khong co unresolved source conflict, source mismatch hoac invalid target/core bar.

Quy tac field:

- Metric baseline MAY duoc tinh tren candidate hop le va MUST kem coverage/reason.
- Session VWAP MUST la `null` neu `sessionCoverage != 1`.
- `aggregationEligible = false` trong MVP. PARTIAL chi duoc hien thi voi badge va diagnostics, khong duoc dua vao Weekly Lab aggregate.

### 9.4. UNRELIABLE

Snapshot la `UNRELIABLE` neu khong dat hai muc tren, hoac co mot trong cac loi:

- thieu target bar;
- core gap;
- unresolved revision conflict;
- sai venue/product/symbol/timeframe;
- invalid OHLCV cua target/core;
- pagination stalled, source response khong parse duoc, hoac input hash khong verify duoc;
- input bar thieu source observation, dangling request/batch reference, `fetchedAt`/response hash thieu tren contributing request, hoac provenance hash khong verify duoc.

Tat ca derived metric va label MUST la `null`; chi provenance va diagnostics duoc tra ve. `aggregationEligible = false`.

### 9.5. Aggregate va sample size

- Aggregate theo context chi duoc dung snapshot co `aggregationEligible = true`.
- Mot aggregate MUST dung mot `algorithmVersion` va `parameterSetId`; khong tron version.
- API aggregate MUST tra `eligibleCount`, `partialExcludedCount` va `unreliableExcludedCount`.
- Snapshot entry va exit la hai population rieng; khong tron phase trong cung metric neu khong ghi ro.
- Context chi la bien phan nhom mo ta. Weekly Lab khong duoc noi regime/anomaly **gay ra** P&L.

## 10. Immutability, lineage va hash

### 10.1. MarketBar revision

Logical key:

```text
(BINANCE, SPOT, symbol, timeframe, openAt)
```

Moi payload duy nhat tao mot immutable `MarketBarRevision` co `contentHash`. Neu Binance tra content khac cho cung logical key, tao revision moi; khong update revision cu. Resolver phai chon ro revision nao va conflict chua resolve lam snapshot `UNRELIABLE`.

`contentHash` la lowercase hex SHA-256 cua UTF-8 RFC 8785 canonical object gom chinh xac cac field sau theo ten: `venue`, `productType`, `symbol`, `timeframe`, `openAt`, `open`, `high`, `low`, `close`, `baseVolume`, `sourceCloseTime`, `quoteVolume`, `tradeCount`, `takerBuyBaseVolume`, `takerBuyQuoteVolume`. Decimal dung canonical form dinh nghia o muc 10.3; field ignored cua Binance khong tham gia hash.

Viec resolve conflict MUST la mot thao tac duoc audit va chon mot `revisionId` cu the. He thong khong tu dong chon revision moi nhat, gia tri lon hon hoac gia tri da duoc dung truoc do.

One public `MarketBarConflict` is created atomically when the second distinct content revision becomes visible for a logical key:

```text
marketBarConflictId
venue                         BINANCE
productType                   SPOT
symbol
timeframe                     1m | 5m
openAt
createdAt
```

The logical key and `marketBarConflictId` are each unique. Resolution is an immutable append-only stream:

```text
marketBarResolutionId
marketBarConflictId
resolutionSequence            positive contiguous integer
candidateRevisionIdsJson
selectedRevisionId
reasonCode                    VERIFIED_SOURCE_SELECTION
actorType                     OPERATOR
idempotencyKey
recordedAt
contentSha256
```

`candidateRevisionIdsJson` is a nonempty sorted unique array of exact `{ "revisionId": id }`, ordered by `(MarketBarRevision.contentHash, revisionId)`; selected ID occurs exactly once. Sequence starts 1 and is allocated under the conflict lock; `(marketBarConflictId,resolutionSequence)` and global operator `idempotencyKey` are unique. Retry with same key and RFC 8785 payload returns the row; changed payload conflicts. `contentSha256` is SHA-256 of RFC 8785 exact object `{ "candidateRevisionRecordKeys", "conflictRecordKey", "reasonCode", "recordedAt", "resolutionSequence", "selectedRevisionRecordKey" }`, using keys `{ "marketBarConflictId": id }`, `{ "revisionId": id }`; ID/hash are outside. Header/resolution rows are immutable and public export contains the full resolution prefix through every referenced selection.

The shared `market_bar_as_of_v1` selector takes logical key and trusted cutoff `T`:

1. A revision is visible iff it has at least one eligible MarketBarSourceObservation whose successful request has `fetchedAt <= T` and terminal batch is COMPLETE/PARTIAL. Group visible observations by distinct revision/content hash.
2. Zero revision returns MISSING. Exactly one distinct revision selects it with `marketBarResolutionId = null`.
3. More than one requires the greatest visible resolution sequence with `recordedAt <= T` whose candidate array equals the entire visible revision set byte-for-byte. If none exists, result is UNRESOLVED_CONFLICT. A later visible revision automatically makes an older candidate-set resolution inapplicable until a new resolution is recorded.
4. For the selected revision, choose the eligible observation with lowest `observationSequence` among observations visible at T. Zero or multiple rows at that sequence is invalid provenance.

MCE uses `T = ContextSnapshot.computedAt`; TP-ACC fee conversion uses `T = FeeConversion.created_at`. Both persist selected revision, nullable resolution and observation. Historical verification replays with the same cutoff; mutable latest/current source lookup, lexical ID and consumer-specific fallback are forbidden.

### 10.2. Input set

`inputBarRevisionIds` la tap da deduplicate cua moi bar revision thuc su duoc doc de tinh output, bao gom:

- target/core bars;
- session bars;
- baseline candidates;
- predecessor bars dung cho true range va RV20.

Danh sach duoc sort theo `(timeframe, openAt, revisionId)`. `inputBarSourceObservationIds` va `inputBarResolutionIds` co cung cardinality/thu tu; observation tai index `i` MUST tro den revision do, con nullable resolution tuan exact shared selector o muc 10.1.

`sourceRequestIds` la tap sorted unique request ID tu cac selected observation. `sourceIngestionBatchIds` la tap sorted unique batch ID cua cac request do. Moi reference MUST resolve duoc; khong cho dangling reference hoac request khong thanh cong.

### 10.3. Canonical hash

`inputHash` la lowercase hex SHA-256 cua UTF-8 RFC 8785 JSON Canonicalization Scheme object:

```json
{
  "algorithmVersion": "mce-binance-spot-v1.0.0",
  "parameterSetId": "mce-default-v1",
  "venue": "BINANCE",
  "productType": "SPOT",
  "symbol": "BTCUSDT",
  "timeframe": "1m",
  "phase": "ENTRY",
  "episodeProjectionVersion": 1,
  "eventFillId": "...",
  "eventSequence": 1,
  "eventAt": 0,
  "eventTimeEndExclusive": 1,
  "eventTimestampPrecision": "MILLISECOND",
  "referencePrice": "decimal-normalized",
  "bars": [
    {
      "revisionId": "...",
      "openAt": 0,
      "contentHash": "..."
    }
  ]
}
```

Decimal canonical form khong co dau `+`, khong co trailing zero, va `0` khong mang dau am. Hash MUST duoc verify lai trong repository test bang it nhat hai implementation doc lap.

`inputHash` chi hash calculation input va MUST NOT chua request/batch/observation ID. Fetch lai cung exact bar revisions qua request moi khong duoc tao logical calculation khac.

`provenanceHash` la lowercase SHA-256 cua RFC 8785 bytes of this exact closed object:

```json
{
  "ingestionBatches": [{
    "completedAt": 0,
    "fetcherVersion": "...",
    "ingestionBatchId": "...",
    "productType": "SPOT",
    "sourceVenue": "BINANCE",
    "startedAt": 0,
    "status": "COMPLETE"
  }],
  "inputBarSources": [{
    "ingestionBatchId": "...",
    "marketBarResolutionId": null,
    "marketBarRevisionId": "...",
    "sourceObservationId": "...",
    "sourceRequestId": "..."
  }],
  "sourceRequests": [{
    "fetchedAt": 0,
    "requestMetadataHash": "...",
    "responseSha256": "...",
    "sourceRequestId": "..."
  }]
}
```

All timestamps are integer epoch milliseconds. `completedAt` is non-null because only terminal COMPLETE/PARTIAL batches are eligible; `status` is exactly `COMPLETE | PARTIAL`. `marketBarResolutionId` is the only nullable member and follows `market_bar_as_of_v1`; every other member is non-null. `inputBarSources` has exactly one item per `inputBarRevisionIds` entry in the same declared `(timeframe,openAt,revisionId)` order. `sourceRequests` and `ingestionBatches` are deduplicated and sorted respectively by `sourceRequestId` and `ingestionBatchId` exact UTF-8 bytes. Unknown/missing member, duplicate, wrong null, wrong order or a reference not reachable from the corresponding selected observation rejects before hash/persist.

Hai lan fetch MAY cho cung `inputHash` nhung provenance khac. Neu idempotency key da ton tai, service tra logical snapshot da co va giu nguyen provenance cua lan tao snapshot dau tien; fetch/job attempt moi MAY duoc audit rieng nhung khong mutate calculation snapshot hoac doi output.

Canonical export cua snapshot MUST kem:

- exact `MarketBarRevision` content cho moi input;
- selected `MarketBarSourceObservation`;
- referenced `MarketDataSourceRequest`, gom `sourceBaseUrl`, `fetchedAt`, request/response hash;
- referenced `MarketDataIngestionBatch`;
- coverage values, `inputHash` va `provenanceHash`.

Offline replay tu export MUST tinh lai duoc metric, quality, coverage va `inputHash` ma khong goi network. `provenanceHash` MUST verify duoc tu referenced metadata; raw HTTP response body khong bat buoc nam trong export neu canonical bar content va `responseSha256` duoc giu.

### 10.4. Algorithm va parameter version

`mce-binance-spot-v1.0.0` dong bang:

- scope, as-of rule va baseline trong tai lieu nay;
- threshold `0.35`, `50`, `3.5`, `97.5`, `2.5`;
- precision/rounding;
- label code va quality thresholds.

Bat ky thay doi nao lam output, quality hoac eligibility thay doi MUST tang `algorithmVersion` hoac `parameterSetId`. Khong duoc thay doi hidden config duoi cung version.

Algorithm deployment is represented by immutable `ContextAlgorithmRelease`:

```text
contextAlgorithmReleaseId
algorithmVersion
parameterSetId
calculationContractVersion
calculationContractSha256
implementationArtifactSha256
parameterPayloadSha256
releasedAt
releasedBySystemPrincipalId
releaseSha256
```

`(algorithmVersion,parameterSetId)` and `releaseSha256` are unique; no field can be updated/reused. `releaseSha256` is lowercase SHA-256 of RFC 8785 exact object `{ "algorithmVersion":str, "calculationContractSha256":hash, "calculationContractVersion":str, "implementationArtifactSha256":hash, "parameterPayloadSha256":hash, "parameterSetId":str }`. All referenced bytes are immutable/readable by release verification. Registration is the v1 allowlist: existence of this complete immutable row means the tuple is approved, there is no hidden enabled flag/current pointer/retirement state, and removing or mutating a row is forbidden while any control job/snapshot references it. ALGORITHM_UPGRADE uses this exact digest; disabling a defective release is an operational enqueue kill switch, not a domain state change, and a corrected calculation requires a new tuple/release.

Initial and manual trigger identities are durable:

```text
ContextEpisodeTrigger
contextEpisodeTriggerId
workspaceId
tradeEpisodeId
episodeProjectionVersion
phase                           ENTRY | EXIT
sourceEventSequence
eventFillId
createdAt
contentSha256

ManualContextRecomputeRequest
manualContextRecomputeRequestId
workspaceId
tradeEpisodeId
episodeProjectionVersion
phase                           ENTRY | EXIT
timeframe                       1m | 5m
sourceEventSequence
algorithmVersion
parameterSetId
actorUserId
idempotencyKey
requestSha256
requestedAt
```

Projection publication atomically inserts/returns one `ContextEpisodeTrigger` per `(workspaceId,tradeEpisodeId,episodeProjectionVersion,phase)` before CONTEXT enqueue; ID is a server RFC 9562 UUID and uniqueness, not a reconstructed UUID namespace, makes retry stable. `contentSha256 = SHA-256(RFC8785({ "createdAt":epoch-ms, "episodeProjectionVersion":int, "eventFillId":id, "phase":str, "sourceEventSequence":int, "tradeEpisodeId":id, "workspaceId":id }))`; that is the exact member set and spelling. ENTRY requires `sourceEventSequence = 1` and the first allocation/fill; EXIT exists only for CLOSED projection and requires sequence `N = count(EpisodeFillAllocation)` plus the closing allocation/fill. The same mapping is authoritative for every CONTEXT reason; payload sequence never comes from the client or timestamp.

`ManualContextRecomputeRequest` has composite same-workspace projection ownership, unique `(workspaceId,idempotencyKey)`, and `requestSha256 = SHA-256(RFC8785({ "actorUserId":id, "algorithmVersion":str, "episodeProjectionVersion":int, "idempotencyKey":str, "parameterSetId":str, "phase":str, "sourceEventSequence":int, "timeframe":str, "tradeEpisodeId":id, "workspaceId":id }))`. The authenticated command locks Workspace/projection, revalidates active projection, event sequence and registered release, inserts this request plus the exact CONTEXT job/fence/ENQUEUE atomically, and uses its persisted ID as `triggerId`. Same key/hash returns the same request/job; changed bytes conflict. This receipt contains no market/user content.

## 11. Recompute policy

- `ContextSnapshot` immutable. Khong `UPDATE` metric, quality, input list, hash hay algorithm version.
- Unique idempotency key la `(workspaceId, tradeEpisodeId, episodeProjectionVersion, phase, timeframe, algorithmVersion, parameterSetId, inputHash)`. Job lap lai voi cung key tra snapshot da co va MUST NOT insert duplicate; composite FK/constraint phai ngan episode cua workspace khac.
- Revision-chain scope is exact `(workspaceId, tradeEpisodeId, episodeProjectionVersion, phase, timeframe, algorithmVersion, parameterSetId)`. Within it, `snapshotRevisionNo` starts at 1, is contiguous, and is unique with the scope. Revision 1 has `supersedesSnapshotId = null`; revision N>1 points exactly to revision N-1 in the same scope. Publisher locks the scope, so there is exactly one leaf and `computedAt` is nondecreasing by revision number.
- Recompute within the same scope creates the next revision and uses `supersedesSnapshotId`; `recomputeReason` is one of:
  - `SOURCE_GAP_FILLED`
  - `SOURCE_REVISION_RESOLVED`
  - `MANUAL_RETRY`
- Every initial/recompute enqueue uses the exact TP-SEC `TenantControlJob` CONTEXT tagged union. One job owns exactly one `(phase,timeframe)` slot and pins the authoritative `sourceEventSequence`, reason and typed trigger identity. INITIAL_EVENT, SOURCE_GAP_FILLED, SOURCE_REVISION_RESOLVED and MANUAL_RETRY carry only `triggerId`: respectively exact ContextEpisodeTrigger ID, MarketDataIngestionBatch ID, MarketBarResolution ID and ManualContextRecomputeRequest ID. EPISODE_PROJECTION_REPLAYED and ALGORITHM_UPGRADE carry only `triggerSha256`: respectively lowercase SHA-256 of RFC 8785 new projection record-key bytes and exact `ContextAlgorithmRelease.releaseSha256`; those two start a new scope. Validation follows record ownership, not a fictitious common tuple: ContextEpisodeTrigger must match the job's Workspace/projection/phase/authoritative sequence; ManualContextRecomputeRequest must match its complete Workspace/projection/phase/timeframe/sequence/algorithm/parameter slot; the global ingestion batch or resolution row must exist and affect a required source interval/revision selected for that slot; the projection digest must equal the exact subject projection record key; and the global release must match only the job's algorithm/parameter/digest. Timeframe/algorithm values absent from ContextEpisodeTrigger are validated against the subject and registered release during enqueue. `triggerId` and `triggerSha256` are mutually exclusive and reason/type/member mismatches reject before enqueue. A new trigger may enqueue a new computation in the same scope; byte-identical final `inputHash` still returns the existing snapshot instead of creating a duplicate revision.
- A changed episode projection or dependency tuple starts a new exact scope at revision 1 with null supersedes ID and reason `EPISODE_PROJECTION_REPLAYED` or `ALGORITHM_UPGRADE`; it never links two incompatible scopes.
- Active-as-of within an exact scope is the greatest `snapshotRevisionNo` whose `computedAt <= T`; sequence, never opaque ID, resolves same-millisecond revisions. Two leaves, a gap, wrong predecessor or cross-scope supersede is invalid. A current-context API first resolves the active TradeEpisodeProjection and requested algorithm/parameter tuple, then applies this exact scope resolver.
- Ingest them bar dong **sau** `asOfAt` khong duoc trigger recompute.
- Khi replay tao active projection moi, MCE chi recompute phase ma event-defining input thay doi (`eventFillId`, `eventSequence`, source time interval/precision, reference price hoac position transition). Snapshot moi dung `EPISODE_PROJECTION_REPLAYED`; snapshot cu va projection version cu van giu de audit.
- Algorithm deploy khong auto-thay the history. Batch migration phai duoc goi ro, luu version moi va giu snapshot cu de audit.
- PARTIAL/UNRELIABLE MAY retry khi required missing interval da duoc ingest hoac source conflict da resolve. Retry chi tao row moi.
- API phai cho phep lay active snapshot va toan bo revision chain.

## 12. Pipeline tham chieu

```text
TradeEpisode event
  -> load exactly one active TradeEpisodeProjection
  -> load same-version EpisodeFillAllocation by event_sequence
  -> join immutable admitted NormalizedFill va pass accounting gate
  -> validate SPOT long-only, contiguous sequence va event transition
  -> tao 2 jobs doc lap cho 1m/5m
  -> tinh cutoff va required time intervals
  -> derive tenant-free aligned public request key
  -> internal global market-data service fetch/read immutable MarketBarRevision
  -> validate point-in-time va data integrity
  -> tinh coverage/quality
  -> neu UNRELIABLE: persist provenance + diagnostics
  -> neu COMPLETE/PARTIAL: tinh metric bang deterministic functions
  -> canonicalize inputs, tinh SHA-256
  -> persist immutable ContextSnapshot
  -> publish ContextSnapshotCreated(snapshotId)
```

Metric functions MUST la pure functions cua `(event, selected bar revisions, algorithm parameters)`; khong doc clock, locale, user timezone hoac random state.

## 13. Ngon ngu va guardrail san pham

### 13.1. Duoc phep

- `cao/thap tuong doi so voi 12 tuan truoc trong cung gio UTC`;
- `historically unusual` / `khac thuong trong baseline lich su`;
- `nhom TREND_HIGH_VOL co median P&L ... trong N trade du dieu kien`;
- `co lien he`, `dong xuat hien`, `trong nhom`, kem sample size va version.

### 13.2. Cam trong API label, UI, notification va AI summary

- `bullish`, `bearish`, `buy`, `sell`, `long ngay`, `thoat ngay`;
- `xac suat tang/giam`, `entry opportunity`, `win probability`;
- `xac nhan xu huong`, `se tiep dien`, `sap dao chieu`;
- `smart money`, `ca map`, `tich luy`, `phan phoi`, `absorption`;
- `regime/anomaly nay gay lai/lo`.

MCE khong phat notification theo anomaly va khong duoc goi tren pre-trade screen trong MVP. Context Card chi duoc gan sau fill va trong review.

## 14. Acceptance criteria va golden tests

Tat ca test duoi day la bat buoc trong CI. Fixture timestamp la UTC va ket qua so ap dung rounding o muc 7.8.

### 14.1. Point-in-time

1. `eventAt=2026-08-24T10:02:30.000Z`, `1m`: target MUST la `10:01:00Z`; thay doi bar `10:02:00Z` va moi bar sau do khong doi output/input hash.
2. `eventAt=2026-08-24T10:05:00.000Z`, `5m`: target MUST la `10:00:00Z`.
3. `eventAt=2026-08-24T10:04:59.999Z`, `5m`: target MUST la `09:55:00Z`; bar `10:00:00Z` bi loai.
4. Chay cung event truoc va sau khi ingest 24 gio future bars MUST cho output va `inputHash` giong byte-for-byte.
5. Xoa dung target bar nhung giu bar truoc do MUST cho `UNRELIABLE/MISSING_TARGET_BAR`, khong fallback.

### 14.2. Entry/exit

6. CSV khong co `exchangeTradeId`, active projection co allocation `BUY 2`, `BUY 1`, `SELL 1`, `SELL 2` theo contiguous `event_sequence` MUST dung `projection.first_fill_id` lam entry va `projection.closed_fill_id` lam exit; hai fill o giua khong tao phase snapshot.
7. Duplicate/gap `event_sequence`, day contiguous nhung bat dau tu `0`, day khong ket thuc tai `count(allocations)`, allocation khac projection version, hoac quality `SEQUENCE_PENDING|REPLAY_PENDING|INVALID` MUST cho `ACCOUNTING_CONTEXT_NOT_READY` va khong tao snapshot. `FEE_CONVERSION_MISSING` voi day `1..N` va ledger hop le van duoc phep tao context.
8. Fill precision `SECOND` voi interval `[10:05:00.000,10:05:01.000)` MUST cho `eventAt/asOfAt=10:05:00.000`, giu `eventTimestampPrecision=SECOND` va dung `eventTimeEndExclusive`; projection timestamp khac lower bound MUST fail `ACCOUNTING_CONTEXT_NOT_READY`; SELL vuot remaining quantity bi reject, con re-entry sau quantity ve 0 tao episode/context chain moi.

### 14.3. Source va pagination

9. Range 1.001 bars MUST tao hai page 1.000 + 1, khong duplicate va khong bo slot.
10. Page ngan nhung chua cham `toExclusive` MUST tiep tuc request.
11. Page rong truoc `toExclusive` MUST ghi chinh xac missing interval.
12. Payload co `high < close`, close time sai, hoac taker volume lon hon total MUST khong duoc coi la valid bar.
13. Cung logical key/cung hash deduplicate; cung logical key/khac hash tao conflict. Khong resolution cho exact visible candidate set thi `UNRELIABLE/SOURCE_REVISION_CONFLICT`; valid resolution pins exact revision, nullable/non-null aligned resolution ID and lowest visible observation sequence.
14. Mock `429` MUST dung den `Retry-After`; mock `418` MUST mo circuit va khong co request som.

### 14.4. Baseline, RVOL va percentile

15. Moi target MUST co baseline range dung 12 ISO week truoc, loai toan bo target week. Full fixture co dung 720 candidate `1m` va 144 candidate `5m`.
16. Baseline quote volume `[90, 100, 100, 110]`, target `200` cho unit formula MUST co:

```text
medianQuoteVolume = 100
rvol = 2.000000
effortPercentile = 100.000000
volumeRobustZ = 9.833186
volumeAnomalyCode = UNUSUALLY_HIGH_VOLUME
```

17. Percentile baseline `[1, 2, 2, 4]`, target `2` MUST bang `50.000000` theo mid-rank.
18. MAD bang 0 va target bang median cho z `0`; target lon hon median cho z `99`; khong duoc sinh `NaN`/infinity.

### 14.5. Session VWAP

19. Hai closed bar tu 00:00 UTC co `(base, quote)=(2,200)` va `(1,110)` MUST cho:

```text
sessionVwap = 103.333333333333
referencePrice = 105
vwapDistanceBps = 161.290323
```

20. Thieu mot session bar MUST cho `sessionVwap=null` va snapshot khong duoc `COMPLETE`; neu coverage con `>=95%` thi `PARTIAL`, neu thap hon thi `UNRELIABLE`. Ca hai deu khong aggregation eligible.
21. Event truoc closed bar dau tien cua UTC date MUST co reason `SESSION_HAS_NO_CLOSED_BAR`, khong lay bar cua ngay truoc vao Session VWAP.
22. Doi UI timezone tu UTC sang `Asia/Ho_Chi_Minh` MUST khong doi session input, output hoac hash.

### 14.6. Range, Effort-Response va regime

23. `previousClose=100`, target `H=106`, `L=99` MUST co `trueRange=7`, `normalizedTrueRange=0.070000`.
24. Boundary Effort-Response: percentile bang chinh xac `50` la high; `49.999999` la low cho moi truc.
25. 21 close tang deu tu `100` den `120` MUST co `efficiencyRatio20=1.000000` va structure `TREND`.
26. 21 close xen ke `100,101,...,100` MUST co `efficiencyRatio20=0.000000` va structure `RANGE`.
27. `realizedVolPercentile=50` MUST la `LOW_VOL`; gia tri lon hon `50` MUST la `HIGH_VOL`.
28. Khong output nao cua regime chua huong `UP`, `DOWN`, `BULL` hoac `BEAR`; raw taker fields khong xuat hien trong ContextSnapshot/UI.

### 14.7. Quality va aggregate

29. Coverage day du cho status `COMPLETE` va `aggregationEligible=true`; snapshot persist `coreCoverage=1.000000`, `sessionCoverage=1.000000`, `baselineCoverage=1.000000`, `baselineDistinctWeeks=12`.
30. Thieu 1/720 baseline candidate `1m` cho status `PARTIAL`, khong phai `COMPLETE`, va `aggregationEligible=false`.
31. Chi co 10 tuan baseline day du (`600/720` cho `1m`, `120/144` cho `5m`) van du dieu kien `PARTIAL` neu core/session day du.
32. Chi co 9 tuan baseline cho `UNRELIABLE`.
33. Session coverage `95%` la boundary `PARTIAL`; nho hon `95%` cho `UNRELIABLE`.
34. Aggregate MUST exclude PARTIAL/UNRELIABLE va tra dung ba counter eligibility.
35. Aggregate tron hai algorithm version MUST fail validation, khong am tham merge.

### 14.8. Immutability va determinism

36. Cung event, parameter set va bar revision set chay 100 lan MUST cho output/hash giong nhau.
37. Recompute sau market-data gap fill trong same scope MUST insert next contiguous snapshot revision with exact predecessor; projection replay starts revision 1 in the new projection scope with null supersedes; neither changes old rows.
38. Algorithm upgrade MUST giu chain cu, tao scope/version revision 1 moi va khong thay active history neu migration chua duoc goi.
39. Hash/export fixture MUST trung nhau giua hai implementation doc lap cua RFC 8785 + SHA-256; round-trip export phai giu `workspaceId`, reject cross-workspace episode/fill refs, resolve moi bar -> observation -> request -> batch, giu exact `sourceBaseUrl`/`fetchedAt`, verify `inputHash`/`provenanceHash` va replay metric/quality/coverage khong goi network.
40. Static contract/UI tests MUST chan cac cum tu cam o muc 13 trong generated context label va insight template.
41. Quality golden covers COMPLETE/PARTIAL/UNRELIABLE, zero-slot session and every source/hash failure; exact sorted `qualityReasons`, coalesced `missingIntervals`, coverage and derived-field null coupling match across producers. Unknown/duplicate/reordered reason, overlapping/wrong-bound interval or quality mismatch rejects.
42. Conflict golden freezes one/multiple bar revisions, resolution candidate sets, same-millisecond resolution sequence, later new candidate and multiple observations. `market_bar_as_of_v1` yields identical missing/sole/unresolved/resolved revision-resolution-observation triples for MCE and TP-ACC fee conversion; stale candidate resolution and current/latest substitution reject.
43. Snapshot-chain golden runs concurrent retry/recompute, same-millisecond commits, projection replay and algorithm upgrade. Exact-scope revisions are contiguous with one leaf; retry reuses ID, within-scope supersede is N-1, cross-scope starts 1/null, and as-of selection never uses opaque ID.
44. CONTEXT work-control golden covers all six reason/type branches, immutable ContextEpisodeTrigger/ManualContextRecomputeRequest/ContextAlgorithmRelease rows and independently mutates each record field/hash, ENTRY sequence 1, EXIT sequence N, `triggerId`, `triggerSha256` and extra/missing member. The four ID branches persist only the exact authoritative record ID; projection replay and algorithm upgrade persist only the exact record-key/release digest; same-key manual retry returns one request/job, and every stale projection, wrong phase/timeframe/sequence/release, cross-branch substitution or wrong digest fails before enqueue and allocates no work sequence.

## 15. Definition of Done cho implementation

MCE MVP chi duoc xem la hoan thanh khi:

- schema va migration enforce logical key, immutable revision, snapshot chain, persisted coverage va khong co dangling bar-observation-request-batch reference;
- ContextSnapshot/job co immutable workspace scope va tenant-isolation tests; current selection uses only the exact scope/as-of resolver in section 11, while global public market entities khong chua user/episode/workspace ID;
- pure calculation library pass toan bo golden tests;
- Binance fetcher pass pagination, retry, rate-limit va malformed-payload integration tests;
- repository query enforce `barEndExclusive <= asOfAt` o tang database, khong chi filter trong memory;
- API tra provenance, quality, coverage, version va eligibility;
- canonical export/round-trip replay giu source base URL, fetched-at, request/response hash, selected bar content va verify duoc ca `inputHash`/`provenanceHash` ma khong goi network;
- Weekly Lab chi aggregate COMPLETE snapshot cung version;
- observability co counter cho missing target, source conflict, retry, quality status va excluded aggregate;
- khong co context realtime/pre-trade, anomaly notification hoac signal language trong MVP.
- all 44 golden assertions in section 14 and relevant TP-ACC/TP-LAB/TP-EXP acceptance gates pass from a clean validation store.
