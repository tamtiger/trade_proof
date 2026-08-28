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
    "src/TradeProof.Application/Foundation/MarketContextApp.cs",
    "src/TradeProof.Application/Foundation/TradeProofApp.cs",
    "src/TradeProof.Domain/Foundation/AccountingContracts.cs",
    "src/TradeProof.Domain/Foundation/IngestionContracts.cs",
    "src/TradeProof.Domain/Foundation/MarketContextContracts.cs",
    "src/TradeProof.Infrastructure/Migrations/004_phase4_fee_context_source.sql",
    "tests/TradeProof.App.Tests/Phase4Tests.cs",
    "tests/TradeProof.App.Tests/TestProgram.cs",
    "tools/test-phase4.ps1",
    "tools/verify-phase4.ps1"
)

foreach ($file in $requiredFiles) {
    Assert-File $file
}

$changelog = Get-Content -Raw -LiteralPath "CHANGELOG.md"
if (-not $changelog.StartsWith("# Changelog`n`n")) {
    throw "CHANGELOG.md must keep the changelog title at the top."
}
if ($changelog -notmatch "## 2026-08-28 - Phase 4: fee conversion and context source") {
    throw "CHANGELOG.md must keep the Phase 4 entry."
}

Assert-Contains "src/TradeProof.Domain/Foundation/MarketContextContracts.cs" 'mce-binance-spot-v1\.0\.0'
Assert-Contains "src/TradeProof.Domain/Foundation/MarketContextContracts.cs" 'mce-default-v1'
Assert-Contains "src/TradeProof.Domain/Foundation/MarketContextContracts.cs" 'market_bar_as_of_v1'
Assert-Contains "src/TradeProof.Domain/Foundation/MarketContextContracts.cs" 'MarketConversionCatalogVersionRecord'
Assert-Contains "src/TradeProof.Domain/Foundation/MarketContextContracts.cs" 'MarketDataIngestionBatchRecord'
Assert-Contains "src/TradeProof.Domain/Foundation/MarketContextContracts.cs" 'MarketDataSourceRequestRecord'
Assert-Contains "src/TradeProof.Domain/Foundation/MarketContextContracts.cs" 'MarketBarRevisionRecord'
Assert-Contains "src/TradeProof.Domain/Foundation/MarketContextContracts.cs" 'MarketBarSourceObservationRecord'
Assert-Contains "src/TradeProof.Domain/Foundation/MarketContextContracts.cs" 'ContextAlgorithmReleaseRecord'
Assert-Contains "src/TradeProof.Domain/Foundation/MarketContextContracts.cs" 'ContextEpisodeTriggerRecord'
Assert-Contains "src/TradeProof.Domain/Foundation/MarketContextContracts.cs" 'ManualContextRecomputeRequestRecord'
Assert-Contains "src/TradeProof.Domain/Foundation/MarketContextContracts.cs" 'ContextSnapshotRecord'
Assert-Contains "src/TradeProof.Domain/Foundation/IngestionContracts.cs" 'Context'

Assert-Contains "src/TradeProof.Domain/Foundation/AccountingContracts.cs" 'MarketBarIdsJson'
Assert-Contains "src/TradeProof.Domain/Foundation/AccountingContracts.cs" 'MarketBarSourceObservationIdsJson'
Assert-Contains "src/TradeProof.Domain/Foundation/AccountingContracts.cs" 'MarketConversionCatalogVersion'
Assert-Contains "src/TradeProof.Domain/Foundation/AccountingContracts.cs" 'ConversionPathJson'

Assert-Contains "src/TradeProof.Application/Foundation/MarketContextApp.cs" 'PublishMarketConversionCatalogAsync'
Assert-Contains "src/TradeProof.Application/Foundation/MarketContextApp.cs" 'RecordMarketBarsAsync'
Assert-Contains "src/TradeProof.Application/Foundation/MarketContextApp.cs" 'ResolveMarketFeeConversion'
Assert-Contains "src/TradeProof.Application/Foundation/MarketContextApp.cs" 'DIRECT_1M_CLOSE'
Assert-Contains "src/TradeProof.Application/Foundation/MarketContextApp.cs" 'INVERSE_1M_CLOSE'
Assert-Contains "src/TradeProof.Application/Foundation/MarketContextApp.cs" 'barEndExclusiveEpochMs'
Assert-Contains "src/TradeProof.Application/Foundation/MarketContextApp.cs" 'ComputeContextSnapshotsAsync'
Assert-Contains "src/TradeProof.Application/Foundation/MarketContextApp.cs" 'RequestManualContextRecomputeAsync'
Assert-Contains "src/TradeProof.Application/Foundation/MarketContextApp.cs" 'EnqueueContextForProjection'
Assert-Contains "src/TradeProof.Application/Foundation/TradeProofApp.cs" 'internal:context'

$sql = Get-Content -Raw -LiteralPath "src/TradeProof.Infrastructure/Migrations/004_phase4_fee_context_source.sql"
$tables = @(
    "market_conversion_catalog_version",
    "market_data_ingestion_batch",
    "market_data_source_request",
    "market_bar_revision",
    "market_bar_source_observation",
    "context_algorithm_release",
    "context_episode_trigger",
    "manual_context_recompute_request",
    "context_snapshot"
)
foreach ($table in $tables) {
    if ($sql -notmatch "CREATE TABLE $table") {
        throw "Migration missing table $table."
    }
}
Assert-Contains "src/TradeProof.Infrastructure/Migrations/004_phase4_fee_context_source.sql" "ALTER TABLE fee_conversion ADD COLUMN market_bar_ids_json"
Assert-Contains "src/TradeProof.Infrastructure/Migrations/004_phase4_fee_context_source.sql" "CHECK \(algorithm_version = 'mce-binance-spot-v1\.0\.0'\)"
Assert-Contains "src/TradeProof.Infrastructure/Migrations/004_phase4_fee_context_source.sql" "CHECK \(parameter_set_id = 'mce-default-v1'\)"
Assert-Contains "src/TradeProof.Infrastructure/Migrations/004_phase4_fee_context_source.sql" "CHECK \(selector_algorithm_version = 'market_bar_as_of_v1'\)"
Assert-Contains "src/TradeProof.Infrastructure/Migrations/004_phase4_fee_context_source.sql" "CHECK \(source_base_url = 'https://data-api\.binance\.vision'\)"

Assert-Contains "src/TradeProof.Api/Program.cs" 'phase-[45]'
Assert-Contains "src/TradeProof.Api/Program.cs" '/api/market/conversion-catalog'
Assert-Contains "src/TradeProof.Api/Program.cs" '/api/market/bars'
Assert-Contains "src/TradeProof.Api/Program.cs" '/api/context/compute'
Assert-Contains "src/TradeProof.Api/Program.cs" '/api/context/manual-recompute'
Assert-Contains "src/TradeProof.Api/wwwroot/index.html" 'Context'
Assert-Contains "src/TradeProof.Api/wwwroot/quick-plan.js" 'seedMarketData'
Assert-Contains "src/TradeProof.Api/wwwroot/quick-plan.js" 'computeContextButton'
Assert-Contains "tests/TradeProof.App.Tests/TestProgram.cs" 'phase4'

$ui = Get-Content -Raw -LiteralPath "src/TradeProof.Api/wwwroot/index.html"
$ui += Get-Content -Raw -LiteralPath "src/TradeProof.Api/wwwroot/quick-plan.js"
if ($ui -match "(?i)exchange api key|api key|private sync|live sync|generic market browser|generic csv mapper|buy signal|sell signal|should buy|should sell|nên mua|nên bán|bullish|bearish|win probability") {
    throw "Phase 4 UI must not include key/private-sync/generic-browser/trading-signal screen text."
}

$trackedBuildOutput = git ls-files -- ':(glob)**/bin/**' ':(glob)**/obj/**'
if ($trackedBuildOutput) {
    $trackedBuildOutput
    throw "bin/obj files must not be tracked."
}

Write-Host "Phase 4 artifact verification passed."
