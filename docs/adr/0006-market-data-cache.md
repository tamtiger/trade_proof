# ADR 0006: Market-Data Cache

- Status: Accepted
- Date: 2026-08-27
- Owner: TamNT167

## Context

TradeProof ContextSnapshot uses Binance Spot public candles 1m/5m with no look-ahead. Public market data is global, but tenant ContextSnapshot commits are workspace-owned and deletion-aware.

## Decision

Use Binance Spot public market-data-only REST endpoints via `https://data-api.binance.vision`, initially `/api/v3/klines`, `/api/v3/exchangeInfo` and `/api/v3/time`. Cache immutable public bars/provenance internally in PostgreSQL using keys that contain no user, workspace, episode or event IDs. Tenant jobs only store references to selected public bars and recheck fences before committing snapshots.

## Alternatives

- Use signed Binance endpoints: rejected; MVP does not accept exchange API keys.
- Proxy another venue or aggregate volume: rejected by MVP venue decision.
- Public raw data export/browsing API: rejected until terms review explicitly allows redistribution.

## Security/privacy impact

Global cache contains public technical data only. Tenant mapping and snapshots remain workspace-owned and are deleted with workspace state. No user content is sent to Binance.

## Rollback

If terms or API reliability change, disable ContextSnapshot generation while preserving accounting/import flows and record context quality as unavailable.

