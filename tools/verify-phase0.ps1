$ErrorActionPreference = "Stop"

function Assert-File {
    param([string] $Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Missing required file: $Path"
    }
}

function Assert-Contains {
    param(
        [string] $Path,
        [string] $Pattern
    )
    $content = Get-Content -Raw -LiteralPath $Path
    if ($content -notmatch $Pattern) {
        throw "File '$Path' does not contain required pattern: $Pattern"
    }
}

$adrFiles = @(
    "docs/adr/0001-runtime-and-frontend.md",
    "docs/adr/0002-managed-identity.md",
    "docs/adr/0003-relational-database-and-tenant-enforcement.md",
    "docs/adr/0004-queue-worker-and-idempotency.md",
    "docs/adr/0005-object-storage-and-malware-scanner.md",
    "docs/adr/0006-market-data-cache.md",
    "docs/adr/0007-ai-processor.md",
    "docs/adr/0008-deployment-region-backup-and-disclosure.md",
    "docs/adr/0009-observability-and-redaction.md",
    "docs/adr/0010-binance-market-data-terms.md"
)

$requiredFiles = @(
    ".github/workflows/ci.yml",
    "CHANGELOG.md",
    "Directory.Build.props",
    "TradeProof.sln",
    "docs/operations/fixture-intake.md",
    "fixtures/README.md",
    "src/TradeProof.Api/Program.cs",
    "src/TradeProof.Api/TradeProof.Api.csproj",
    "tests/TradeProof.App.Tests/Phase0Tests.cs",
    "tests/TradeProof.App.Tests/TradeProof.App.Tests.csproj"
) + $adrFiles

foreach ($file in $requiredFiles) {
    Assert-File $file
}

foreach ($file in $adrFiles) {
    Assert-Contains $file "## Context"
    Assert-Contains $file "## Decision"
    Assert-Contains $file "## Alternatives"
    Assert-Contains $file "## Security/privacy impact"
    Assert-Contains $file "## Rollback"
    Assert-Contains $file "Owner: TamNT167"
}

Assert-Contains "docs/adr/0010-binance-market-data-terms.md" "data-api\.binance\.vision"
Assert-Contains "docs/adr/0010-binance-market-data-terms.md" "Do not redistribute raw Binance market-data cache"
Assert-Contains "docs/adr/0010-binance-market-data-terms.md" "fresh Product Terms/cache/redistribution review before pilot"

Assert-Contains "docs/adr/0005-object-storage-and-malware-scanner.md" "self-hosted"
Assert-Contains "docs/adr/0005-object-storage-and-malware-scanner.md" "stateless"
Assert-Contains "docs/adr/0005-object-storage-and-malware-scanner.md" "no network egress"
Assert-Contains "docs/adr/0005-object-storage-and-malware-scanner.md" "no external scanning API"
Assert-Contains "docs/adr/0005-object-storage-and-malware-scanner.md" "no retained external copy"
Assert-Contains "docs/adr/0005-object-storage-and-malware-scanner.md" "fails closed|fail closed"

Assert-Contains "docs/operations/fixture-intake.md" 'Current count: `0/5`'
Assert-Contains "fixtures/README.md" '0/5'
Assert-Contains "fixtures/README.md" "Synthetic fixtures"

$changelog = Get-Content -Raw -LiteralPath "CHANGELOG.md"
if (-not $changelog.StartsWith("# Changelog`n`n## 2026-08-27 - Phase 0")) {
    throw "CHANGELOG.md must keep the newest Phase 0 entry at the top."
}

Write-Host "Phase 0 artifact verification passed."
