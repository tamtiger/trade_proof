# Pilot onboarding and support

Pilot onboarding is concierge-assisted, but support does not have product access to workspace content. The support flow uses user-provided screenshots or diagnostics text only when the user chooses to share it.

## Onboarding steps

1. Confirm the trader matches the Binance Spot, USDT quote, long-only MVP scope.
2. Confirm the product is not a trading signal, exchange sync, tax report or order execution tool.
3. Explain export and Delete TradeProof account before importing user data.
4. Run the local diagnostics script when a repository build or artifact question appears.
5. Ask the user to reproduce import issues with their own UI session; support does not impersonate the workspace.

## Support boundary

No WorkspaceId, token, secret, database credential, object-store credential or workspace export is required for pilot support diagnostics. Break-glass access is outside the normal support path and requires the strict incident process from `TP-SEC`.

## Known limitations

- Binance Spot Trade History CSV only; no exchange API key, read-only sync or generic mapper.
- One workspace and one Binance Spot account in the MVP.
- Long-only, USDT-quoted Spot episodes; no futures, margin, short, funding or borrow.
- AI extensions remain disabled for the core pilot-readiness package.
- Phase 8 is not a production deployment or paid pilot launch.

## Support script output

`tools/pilot-support-diagnostics.ps1` prints repo-local status, recent commits, Harnix public status and excerpts from the Phase 8 operations docs. It does not read database rows, object storage, workspace export archives or user-owned product data.
