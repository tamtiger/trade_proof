# ADR 0010: Binance Market-Data Terms

- Status: Accepted with pre-pilot review gate
- Date: 2026-08-27
- Owner: TamNT167

## Context

TradeProof needs Binance Spot public candles for point-in-time market context. Week 0 requires review of API usage, retention, caching and redistribution. The current task research is recorded in `.harnix/tasks/20260827-154053-phase-0-adr-fixture-freeze/research/binance-market-data-terms.md`.

## Decision

Use only official Binance Spot market-data-only public endpoints for market context:

- `https://data-api.binance.vision/api/v3/klines`
- `https://data-api.binance.vision/api/v3/exchangeInfo`
- `https://data-api.binance.vision/api/v3/time`

Cache raw public bars internally only to compute and prove user-owned ContextSnapshots. Do not redistribute raw Binance market-data cache, expose a public market-data browsing API or use signed/user-data/trading endpoints in MVP. Require a fresh Product Terms/cache/redistribution review before pilot onboarding.

## Alternatives

- Signed Spot API or User Data Stream: rejected because MVP imports CSV and must not receive exchange API credentials.
- Data from a non-Binance venue: rejected by MVP same-venue context rule.
- Raw market-data export to users: rejected until explicit terms/legal review approves it.

## Security/privacy impact

No user data or API key is sent to Binance. Public cache keys cannot include user, workspace, episode or event IDs. Tenant mappings are deletion-scoped.

## Rollback

If Binance terms or endpoint support changes, freeze ContextSnapshot generation, keep import/accounting available and mark context sections unavailable until a reviewed replacement source is approved.

