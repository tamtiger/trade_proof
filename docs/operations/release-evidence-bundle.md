# Release evidence bundle

- Candidate: TradeProof Phase 8 local release candidate.
- Build/commit capture policy: record the final Git commit after the Phase 8 commit is created; until then use the checked-out candidate commit from `git rev-parse HEAD` during local verification.
- Migration version: 007_phase7_core_hardening.sql.
- No P0/P1 defect.
- AI eval: not applicable for core-disabled release.

## Requirements-to-tests matrix

| Requirement | Evidence |
|---|---|
| Core release profile with AI disabled | `tests/TradeProof.App.Tests/Phase7Tests.cs`, `tools/verify-phase7.ps1` |
| Phase 8 operations package | `tests/TradeProof.App.Tests/Phase8Tests.cs`, `tools/verify-phase8.ps1` |
| Release evidence bundle | `docs/operations/release-evidence-bundle.md`, `tools/verify-phase8.ps1` |
| Support without workspace access | `tools/pilot-support-diagnostics.ps1`, `tests/TradeProof.App.Tests/Phase8Tests.cs` |
| Phase 0-8 regression gates | `tools/test-phase8.ps1` |
| Export/deletion/data rights | `tests/TradeProof.App.Tests/Phase6Tests.cs`, `tools/verify-phase6.ps1` |
| Security, privacy and AI-disabled profile | `docs/SECURITY_PRIVACY_AI.md`, `tools/test-phase8.ps1` |

## Test reports

Required local commands:

- `dotnet build tests/TradeProof.App.Tests/TradeProof.App.Tests.csproj --configuration Release --no-restore --disable-build-servers -maxcpucount:1 -p:UseSharedCompilation=false`
- `dotnet tests/TradeProof.App.Tests/bin/Release/net10.0/TradeProof.App.Tests.dll phase8`
- `pwsh -NoProfile -File tools/verify-phase8.ps1`
- `pwsh -NoProfile -File tools/test-phase8.ps1`

## Security/secret scan

`tools/test-phase8.ps1` runs the existing repository secret-like content scan with generated docs and local verification tools excluded from false-positive matching. Production keys, tokens, private keys and password assignments must not be committed.

## Performance/usability/accessibility evidence

- Performance: Phase 7 hardening evidence records local smoke state as `PASS`; pilot-equivalent benchmark evidence remains required before production onboarding.
- Usability: Product Brief targets Quick Plan median under 15 seconds and Quick Review median under 30 seconds; Phase 8 does not claim measured pilot results.
- Accessibility: Phase 7 hardening evidence records accessibility smoke as `PASS`; browser/screen-reader pilot evidence remains a pre-onboarding requirement.

## Backup/restore, deletion and incident exercise evidence

Current tabletop records live in `docs/operations/runbook-exercise.md`. A pilot-equivalent restore log with RPO/RTO, deletion tombstone application and traffic smoke result remains required before real pilot traffic.

## Market data terms review

Binance public market-data cache boundaries remain governed by `docs/adr/0010-binance-market-data-terms.md`; raw market-data redistribution is not approved by Phase 8.

## Known limitations, disabled flags and risk exceptions

- `voice_transcription_enabled=false`.
- `ai_taxonomy_enabled=false`.
- `ai_weekly_summary_enabled=false`.
- No paid pilot, production deployment, exchange key sync or support workspace access is enabled by this package.
- No waived non-waivable gate is recorded.

## Version list

| Contract | Version |
|---|---|
| Binance CSV adapter | `binance_spot_trade_history_csv_v1` |
| Accounting normalized fill | `normalized_fill_v1` |
| Episode projection | `episode_projection_v1` |
| Plan proof | `plan_proof_v1` |
| Fee conversion | `fee_conversion_v1` |
| Metrics | `metrics_v1`, `metrics_decimal_v1` |
| Context | `mce-binance-spot-v1.0.0`, `mce-default-v1`, `market_bar_as_of_v1` |
| Weekly Lab renderer | `weekly_lab_renderer_v1` |
| Product analytics | `product_analytics_external_v1`, `product_metrics_v1` |
| Tenant work-control marker | `tenant_work_control_marker_v1` |
| Export | `tradeproof_export_v1`, `tradeproof_export_round_trip_v1`, `export_sla_envelope_v1` |
| AI disabled profile | `ai_disabled_profile_v1` |
| Release readiness | `release_hardening_evidence_v1`, `core_release_readiness_v1` |
