# Research: Binance market-data terms boundary

- Task ID: `20260827-154053-phase-0-adr-fixture-freeze`
- Date: 2026-08-27
- Material unknown: TradeProof MVP có thể dùng Binance Spot public market-data source nào, cache/retention/redistribution ra sao, và điều gì phải review lại trước pilot?

## Repository evidence

- `Product_Brief.md` khóa MVP là Binance Spot, USDT quote, không exchange API key/live sync và context source là Binance Spot public candles 1m/5m.
- `docs/IMPLEMENTATION_PLAN.md` Week 0 yêu cầu review Binance Product Terms cho API usage, retention, caching và redistribution.
- `docs/SECURITY_PRIVACY_AI.md` cấm nhận exchange API key/secret trong MVP và cho phép global public MarketBar/cache/provenance không tenant-owned nếu key không chứa user/workspace/episode ID.

## Sources

1. Binance Spot API docs repository `binance/binance-spot-api-docs`, accessed 2026-08-27: official repository states documented streams/endpoints/parameters/payloads are official and unsupported interfaces are at own risk. URL: https://github.com/binance/binance-spot-api-docs
2. `PROD-TERMS-OF-USE.md`, accessed 2026-08-27: Spot Exchange Terms points to Binance Product Terms of Use. URL: https://raw.githubusercontent.com/binance/binance-spot-api-docs/master/PROD-TERMS-OF-USE.md
3. Binance Product Terms page, accessed 2026-08-27: regional terms page resolves from https://www.binance.com/en/terms and includes risk/legal framing; full page is dynamic/regional and must be re-reviewed before pilot. URL: https://www.binance.com/en/terms
4. Binance Spot REST API docs, accessed 2026-08-27: general API information identifies `https://data-api.binance.vision` as the base endpoint for APIs that only send public market data; signed/security types distinguish `NONE` public market data from `USER_DATA` and `TRADE`. URL: https://github.com/binance/binance-spot-api-docs/blob/master/rest-api.md
5. Binance Market Data Only FAQ, accessed 2026-08-27: public market-data-only REST/WebSocket URLs do not require authentication/API key, and User Data Streams cannot be accessed through the market-data-only URL. URL: https://github.com/binance/binance-spot-api-docs/blob/master/faqs/market_data_only.md

## Facts

- Binance official Spot docs expose a market-data-only base URL for unauthenticated public data.
- Market-data-only endpoints include `/api/v3/klines`, `/api/v3/exchangeInfo`, `/api/v3/time`, ticker and trade endpoints; user-data streams are explicitly not available on that URL.
- Binance Spot API security model distinguishes public `NONE` endpoints from signed `USER_DATA` and `TRADE` endpoints.
- The Spot docs route product terms to Binance Product Terms, which are regional/dynamic and may change.

## Inferences for TradeProof

- MVP should use only `data-api.binance.vision` REST market-data endpoints for context source in Phase 4, especially `/api/v3/klines` and `/api/v3/exchangeInfo`; no signed endpoint, user-data stream, trading endpoint, or exchange credential is needed.
- Raw Binance public bars may be cached internally only as implementation/provenance data for user-owned derived ContextSnapshots; TradeProof should not redistribute raw Binance cache as a public dataset or expose a raw market-data browsing API.
- Because Product Terms are regional/dynamic, Week 0 can record the technical boundary but pilot readiness must include a dated legal/terms review by the product owner/security owner before external launch.

## Conclusion

ADR 0010 should decide: use Binance official Spot market-data-only public REST endpoints, pin endpoint allowlist to `/api/v3/klines`, `/api/v3/exchangeInfo`, `/api/v3/time` initially, cache only internal public bar/provenance records needed by TradeProof artifacts, and block pilot release until the owner records a fresh Product Terms/cache/redistribution review.

## Remaining uncertainty

- No source inspected grants redistribution of raw Binance market data. Treat raw redistribution as disallowed until an explicit legal/terms review approves it.
- Regional Binance Product Terms can vary by user/location; review must be refreshed before pilot and whenever Binance Product Terms or Spot docs materially change.
