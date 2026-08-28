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

$requiredFiles = @(
    ".github/workflows/ci.yml",
    "CHANGELOG.md",
    "src/TradeProof.Api/Program.cs",
    "src/TradeProof.Api/wwwroot/index.html",
    "src/TradeProof.Api/wwwroot/quick-plan.js",
    "src/TradeProof.Api/wwwroot/styles.css",
    "src/TradeProof.Application/Foundation/AccountingApp.cs",
    "src/TradeProof.Application/Foundation/IngestionApp.cs",
    "src/TradeProof.Domain/Foundation/AccountingContracts.cs",
    "src/TradeProof.Domain/Foundation/IngestionContracts.cs",
    "src/TradeProof.Infrastructure/Migrations/003_phase3_accounting_core.sql",
    "tests/TradeProof.App.Tests/Phase3Tests.cs",
    "tests/TradeProof.App.Tests/TestProgram.cs",
    "tools/test-phase3.ps1",
    "tools/verify-phase3.ps1"
)

foreach ($file in $requiredFiles) {
    Assert-File $file
}

$changelog = Get-Content -Raw -LiteralPath "CHANGELOG.md"
if (-not $changelog.StartsWith("# Changelog`n`n")) {
    throw "CHANGELOG.md must keep the changelog title at the top."
}
if ($changelog -notmatch "## 2026-08-28 - Phase 3: episode and accounting core") {
    throw "CHANGELOG.md must keep the Phase 3 entry."
}

Assert-Contains "src/TradeProof.Domain/Foundation/AccountingContracts.cs" 'normalized_fill_v1'
Assert-Contains "src/TradeProof.Domain/Foundation/AccountingContracts.cs" 'episode_projection_v1'
Assert-Contains "src/TradeProof.Domain/Foundation/AccountingContracts.cs" 'plan_proof_v1'
Assert-Contains "src/TradeProof.Domain/Foundation/AccountingContracts.cs" 'fee_conversion_v1'
Assert-Contains "src/TradeProof.Domain/Foundation/AccountingContracts.cs" 'wac_episode_v1'
Assert-Contains "src/TradeProof.Domain/Foundation/AccountingContracts.cs" 'ImportRowRecord'
Assert-Contains "src/TradeProof.Domain/Foundation/AccountingContracts.cs" 'NormalizedFillRecord'
Assert-Contains "src/TradeProof.Domain/Foundation/AccountingContracts.cs" 'TradeEpisodeProjectionRecord'
Assert-Contains "src/TradeProof.Domain/Foundation/AccountingContracts.cs" 'AccountingLedgerEntryRecord'

Assert-Contains "src/TradeProof.Application/Foundation/AccountingApp.cs" 'ProcessImportAsync'
Assert-Contains "src/TradeProof.Application/Foundation/AccountingApp.cs" 'SELL_WITHOUT_OPEN_POSITION'
Assert-Contains "src/TradeProof.Application/Foundation/AccountingApp.cs" 'SELL_EXCEEDS_POSITION'
Assert-Contains "src/TradeProof.Application/Foundation/AccountingApp.cs" 'FEE_CONVERSION_MISSING'
Assert-Contains "src/TradeProof.Application/Foundation/AccountingApp.cs" 'VERIFIED_BEFORE_INTERVAL'
Assert-Contains "src/TradeProof.Application/Foundation/AccountingApp.cs" 'ARM_INSIDE_INTERVAL'
Assert-Contains "src/TradeProof.Application/Foundation/AccountingApp.cs" 'RoundScale18'
Assert-Contains "src/TradeProof.Application/Foundation/TradeProofApp.cs" 'internal:import'

$sql = Get-Content -Raw -LiteralPath "src/TradeProof.Infrastructure/Migrations/003_phase3_accounting_core.sql"
$tables = @(
    "import_row",
    "normalized_fill",
    "fee_conversion",
    "trade_episode",
    "trade_episode_projection",
    "episode_fill_allocation",
    "accounting_ledger_entry"
)
foreach ($table in $tables) {
    if ($sql -notmatch "CREATE TABLE $table") {
        throw "Migration missing table $table."
    }
}
Assert-Contains "src/TradeProof.Infrastructure/Migrations/003_phase3_accounting_core.sql" "CHECK \(fill_schema_version = 'normalized_fill_v1'\)"
Assert-Contains "src/TradeProof.Infrastructure/Migrations/003_phase3_accounting_core.sql" "CHECK \(projection_algorithm_version = 'episode_projection_v1'\)"
Assert-Contains "src/TradeProof.Infrastructure/Migrations/003_phase3_accounting_core.sql" "CHECK \(plan_proof_rule_version = 'plan_proof_v1'\)"
Assert-Contains "src/TradeProof.Infrastructure/Migrations/003_phase3_accounting_core.sql" "CHECK \(algorithm_version = 'fee_conversion_v1'\)"
Assert-Contains "src/TradeProof.Infrastructure/Migrations/003_phase3_accounting_core.sql" "CHECK \(algorithm_version = 'wac_episode_v1'\)"
Assert-Contains "src/TradeProof.Infrastructure/Migrations/003_phase3_accounting_core.sql" "UNIQUE \(trading_account_id, dedup_key\)"

Assert-Contains "src/TradeProof.Api/Program.cs" 'phase-[34567]'
Assert-Contains "src/TradeProof.Api/Program.cs" '/api/imports/\{importBatchId\}/process'
Assert-Contains "src/TradeProof.Api/wwwroot/index.html" 'Process'
Assert-Contains "src/TradeProof.Api/wwwroot/index.html" 'Episode'
Assert-Contains "src/TradeProof.Api/wwwroot/quick-plan.js" 'processImportButton'
Assert-Contains "src/TradeProof.Api/wwwroot/quick-plan.js" 'Không process được import'
Assert-Contains "tests/TradeProof.App.Tests/TestProgram.cs" 'phase3'

$ui = Get-Content -Raw -LiteralPath "src/TradeProof.Api/wwwroot/index.html"
$ui += Get-Content -Raw -LiteralPath "src/TradeProof.Api/wwwroot/quick-plan.js"
if ($ui -match "(?i)exchange api key|api key|private sync|live sync|generic csv mapper|buy signal|sell signal|should buy|should sell|nên mua|nên bán") {
    throw "Phase 3 UI must not include key/private-sync/generic-mapper/trading-signal screen text."
}

$trackedBuildOutput = git ls-files -- ':(glob)**/bin/**' ':(glob)**/obj/**'
if ($trackedBuildOutput) {
    $trackedBuildOutput
    throw "bin/obj files must not be tracked."
}

Write-Host "Phase 3 artifact verification passed."
