# PRD - Phase 4: fee conversion and context source

## Outcome

Phase 4 biến dữ liệu market public thành nguồn deterministic trong local harness: fee third-asset có thể được quy đổi bằng bar 1m point-in-time, episode có ContextSnapshot ENTRY/EXIT 1m/5m, và mọi output bị khóa bởi provenance/hash/no-lookahead thay vì current price hay timestamp client.

## Scope

In scope là contract/domain/app/API/UI/migration/test/tooling cần cho Week 4: market conversion catalog, public market-bar provenance, fee conversion direct/inverse, context algorithm release, context trigger/manual retry, context snapshot publication và no-lookahead checks. Harness không gọi network thật; deterministic ingest đại diện cho kết quả public Binance adapter và giữ source/request/observation provenance.

Out of scope là signed exchange sync, raw market-data redistribution, full metric/Weekly Lab, Episode Review, export/deletion và AI.

### AC `AC-001`
Domain/application contracts có exact literals `mce-binance-spot-v1.0.0`, `mce-default-v1`, `market_bar_as_of_v1` và records cho MarketConversionCatalogVersion, MarketDataIngestionBatch, MarketDataSourceRequest, MarketBarRevision, MarketBarSourceObservation, ContextAlgorithmRelease, ContextEpisodeTrigger, ManualContextRecomputeRequest, ContextSnapshot; `FeeConversionRecord` mở rộng market-bar provenance mà vẫn giữ Phase 3 exact/null cases.

### AC `AC-002`
Third-asset fee conversion dùng public 1m bar point-in-time: direct `fee_asset+USDT` ưu tiên inverse `USDT+fee_asset`, chỉ dùng bar có `barEndExclusive <= fill.source_time_start` và cách fill không quá 5 phút, persist DERIVED method/rate/value/asOf/bar+observation arrays/path; missing/stale/unresolved giữ `UNAVAILABLE` và không dùng future/current substitution.

### AC `AC-003`
Local market-data source lưu public tenant-free provenance immutable cho batch/request/observation/bar revision, canonical source base URL/hash, pagination/cache dedup và revision conflict surface tối thiểu; global public rows không chứa workspace/user/episode ID và không tạo TenantExternalOperationLease.

### AC `AC-004`
Context control có registered `CONTEXT` work type, immutable ContextAlgorithmRelease allowlist, ContextEpisodeTrigger cho ENTRY sequence 1 và EXIT sequence N, ManualContextRecomputeRequest idempotent/conflict-safe, và validation reject wrong release/trigger/request hash, wrong phase/timeframe/sequence hoặc cross-workspace projection trước enqueue.

### AC `AC-005`
ContextSnapshot publisher tạo immutable ENTRY/EXIT snapshots cho 1m/5m từ active episode projection, dùng only selected bars có `barEndExclusive <= asOfAt`, input/provenance hash ổn định, quality/aggregation eligibility rõ ràng, và published snapshot không đổi khi thêm future bars sau `asOfAt`.

### AC `AC-006`
Phase 4 API/UI/migration/tests/verifier/local CI/CHANGELOG được cập nhật; Phase 0/1/2/3 checks vẫn xanh, secret/bin-obj guard không báo lỗi, và UI/API không thêm exchange API key/private sync/live sync/generic browser hoặc ngôn ngữ signal bị cấm.