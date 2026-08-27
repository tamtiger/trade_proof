# Phase 0 Plan: technical ADR and fixture freeze

## Implementation Checklist

- [ ] `S1-ADR-BASELINE` — tạo đủ ADR Week 0 và decision index.
- [ ] `S2-BINANCE-RESEARCH` — ghi Binance Terms/market-data research vào ADR và task research.
- [ ] `S3-FIXTURE-SCANNER` — tạo fixture intake/manifest và scanner release evidence baseline, không giả mạo sample thật.
- [ ] `S4-TOOLING-CI` — dựng .NET 10 solution skeleton, local CI script, verifier và CI/lint/secret-scan baseline.
- [ ] `S5-CHANGELOG-VERIFY` — cập nhật CHANGELOG ở đầu file và chạy required checks.

### Slice `S1-ADR-BASELINE`

Criteria: `AC-001`
Checks: `phase0-artifact-review`
Paths: `docs/adr/0001-runtime-and-frontend.md`, `docs/adr/0002-managed-identity.md`, `docs/adr/0003-relational-database-and-tenant-enforcement.md`, `docs/adr/0004-queue-worker-and-idempotency.md`, `docs/adr/0005-object-storage-and-malware-scanner.md`, `docs/adr/0006-market-data-cache.md`, `docs/adr/0007-ai-processor.md`, `docs/adr/0008-deployment-region-backup-and-disclosure.md`, `docs/adr/0009-observability-and-redaction.md`, `docs/adr/0010-binance-market-data-terms.md`

Tạo mỗi ADR theo cùng template: Context, Decision, Alternatives, Security/privacy impact, Rollback, Owner. Quyết định không được làm yếu `TP-PLAN`, `TP-SEC` hoặc scope MVP.

### Slice `S2-BINANCE-RESEARCH`

Criteria: `AC-002`
Checks: `phase0-artifact-review`
Paths: `docs/adr/0010-binance-market-data-terms.md`

Dùng nguồn chính thức hiện hành của Binance Spot docs và Product Terms pointer. Ghi facts/inferences riêng: MVP chỉ dùng public market-data endpoint allowlist, không exchange API key, không user data stream, cache raw public bars chỉ nội bộ và review terms lại trước pilot.

### Slice `S3-FIXTURE-SCANNER`

Criteria: `AC-003`, `AC-004`
Checks: `phase0-artifact-review`
Paths: `docs/operations/fixture-intake.md`, `fixtures/README.md`, `docs/adr/0005-object-storage-and-malware-scanner.md`

Tạo fixture intake procedure với 5 sample slots và trạng thái evidence. Nếu chưa có real samples, ghi blocker rõ trong artifact thay vì tạo sample giả. Tạo scanner policy/pin evidence file và conformance expectations.

### Slice `S4-TOOLING-CI`

Criteria: `AC-005`
Checks: `phase0-tooling-check`, `phase0-local-ci-check`
Paths: `TradeProof.sln`, `Directory.Build.props`, `src/TradeProof.Api/Program.cs`, `src/TradeProof.Api/TradeProof.Api.csproj`, `tests/TradeProof.App.Tests/Phase0Tests.cs`, `tests/TradeProof.App.Tests/TradeProof.App.Tests.csproj`, `.github/workflows/ci.yml`, `tools/verify-phase0.ps1`, `tools/test-phase0.ps1`

Dựng skeleton tối thiểu compile được bằng .NET 10, không dùng production secret, và có test/verification đọc artifacts Phase 0. Local CI chạy restore/test tuần tự qua `tools/test-phase0.ps1` với `--disable-build-servers` và `-maxcpucount:1` để tránh runner sinh process treo trong môi trường hiện tại.

### Slice `S5-CHANGELOG-VERIFY`

Criteria: `AC-006`, `AC-001`, `AC-002`, `AC-003`, `AC-004`, `AC-005`
Checks: `phase0-artifact-review`, `phase0-tooling-check`, `phase0-local-ci-check`
Paths: `CHANGELOG.md`, `tools/verify-phase0.ps1`, `tools/test-phase0.ps1`

Thêm mục mới nhất ở đầu `CHANGELOG.md`, chạy required checks với Harnix snapshot trước/sau. Chỉ finish và commit khi Phase 0 gate thật sự pass; nếu thiếu real sample, giữ trạng thái thiếu sample thật trong fixture inventory thay vì tự tạo dữ liệu.