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
    "src/TradeProof.Application/Foundation/TradeProofApp.cs",
    "src/TradeProof.Application/Foundation/WeeklyLabDataRightsApp.cs",
    "src/TradeProof.Domain/Foundation/IngestionContracts.cs",
    "src/TradeProof.Domain/Foundation/ReviewMetricContracts.cs",
    "src/TradeProof.Domain/Foundation/TradeProofContracts.cs",
    "src/TradeProof.Domain/Foundation/WeeklyLabDataRightsContracts.cs",
    "src/TradeProof.Infrastructure/Migrations/006_phase6_weekly_lab_data_rights.sql",
    "tests/TradeProof.App.Tests/Phase6Tests.cs",
    "tests/TradeProof.App.Tests/TestProgram.cs",
    "tools/test-phase6.ps1",
    "tools/verify-phase6.ps1"
)

foreach ($file in $requiredFiles) {
    Assert-File $file
}

$changelog = Get-Content -Raw -LiteralPath "CHANGELOG.md"
if (-not $changelog.StartsWith("# Changelog`n`n")) {
    throw "CHANGELOG.md must keep the changelog title at the top."
}
if ($changelog -notmatch "## 2026-08-28 - Phase 6: Weekly Lab and data rights") {
    throw "CHANGELOG.md must keep the Phase 6 entry."
}

Assert-Contains "src/TradeProof.Domain/Foundation/ReviewMetricContracts.cs" 'weekly_lab_v1'
Assert-Contains "src/TradeProof.Domain/Foundation/WeeklyLabDataRightsContracts.cs" 'weekly_lab_renderer_v1'
Assert-Contains "src/TradeProof.Domain/Foundation/WeeklyLabDataRightsContracts.cs" 'behavioral_experiment_v1'
Assert-Contains "src/TradeProof.Domain/Foundation/WeeklyLabDataRightsContracts.cs" 'weekly_review_completion_v1'
Assert-Contains "src/TradeProof.Domain/Foundation/WeeklyLabDataRightsContracts.cs" 'weekly_lab_export_projection_v1'
Assert-Contains "src/TradeProof.Domain/Foundation/WeeklyLabDataRightsContracts.cs" 'product_metrics_v1'
Assert-Contains "src/TradeProof.Domain/Foundation/WeeklyLabDataRightsContracts.cs" 'workspace_product_metric_snapshot_v1'
Assert-Contains "src/TradeProof.Domain/Foundation/WeeklyLabDataRightsContracts.cs" 'internal_aggregate_product_metric_snapshot_v1'
Assert-Contains "src/TradeProof.Domain/Foundation/WeeklyLabDataRightsContracts.cs" 'product_analytics_external_v1'
Assert-Contains "src/TradeProof.Domain/Foundation/WeeklyLabDataRightsContracts.cs" 'tradeproof_export_v1'
Assert-Contains "src/TradeProof.Domain/Foundation/WeeklyLabDataRightsContracts.cs" 'tradeproof_export_round_trip_v1'
Assert-Contains "src/TradeProof.Domain/Foundation/WeeklyLabDataRightsContracts.cs" 'export_sla_envelope_v1'
Assert-Contains "src/TradeProof.Domain/Foundation/WeeklyLabDataRightsContracts.cs" 'spreadsheet_escape_v1'
Assert-Contains "src/TradeProof.Domain/Foundation/WeeklyLabDataRightsContracts.cs" 'workspace_deletion_v1'
Assert-Contains "src/TradeProof.Domain/Foundation/WeeklyLabDataRightsContracts.cs" 'COHORT_LOCK'
Assert-Contains "src/TradeProof.Domain/Foundation/WeeklyLabDataRightsContracts.cs" 'REPORT'
Assert-Contains "src/TradeProof.Domain/Foundation/WeeklyLabDataRightsContracts.cs" 'PRODUCT_METRIC'
Assert-Contains "src/TradeProof.Domain/Foundation/WeeklyLabDataRightsContracts.cs" 'ANALYTICS_DELIVERY'
Assert-Contains "src/TradeProof.Domain/Foundation/WeeklyLabDataRightsContracts.cs" 'ANALYTICS_PURGE'
Assert-Contains "src/TradeProof.Domain/Foundation/WeeklyLabDataRightsContracts.cs" 'EXPORT_EXPIRY'
Assert-Contains "src/TradeProof.Domain/Foundation/TradeProofContracts.cs" 'ProductAnalyticsEventRecord\([^)]*SchemaVersion'
Assert-Contains "src/TradeProof.Domain/Foundation/IngestionContracts.cs" 'CohortLock'
Assert-Contains "src/TradeProof.Domain/Foundation/IngestionContracts.cs" 'WorkspaceDeletionWork'

Assert-Contains "src/TradeProof.Application/Foundation/WeeklyLabDataRightsApp.cs" 'PublishWeeklyLabAsync'
Assert-Contains "src/TradeProof.Application/Foundation/WeeklyLabDataRightsApp.cs" 'ProposeBehavioralExperimentAsync'
Assert-Contains "src/TradeProof.Application/Foundation/WeeklyLabDataRightsApp.cs" 'ConfirmBehavioralExperimentAsync'
Assert-Contains "src/TradeProof.Application/Foundation/WeeklyLabDataRightsApp.cs" 'CompleteWeeklyReviewAsync'
Assert-Contains "src/TradeProof.Application/Foundation/WeeklyLabDataRightsApp.cs" 'RecordProductAnalyticsEventAsync'
Assert-Contains "src/TradeProof.Application/Foundation/WeeklyLabDataRightsApp.cs" 'PublishWorkspaceProductMetricsAsync'
Assert-Contains "src/TradeProof.Application/Foundation/WeeklyLabDataRightsApp.cs" 'PublishInternalAggregateProductMetricAsync'
Assert-Contains "src/TradeProof.Application/Foundation/WeeklyLabDataRightsApp.cs" 'ProjectExternalAnalyticsAsync'
Assert-Contains "src/TradeProof.Application/Foundation/WeeklyLabDataRightsApp.cs" 'PurgeExternalAnalyticsAsync'
Assert-Contains "src/TradeProof.Application/Foundation/WeeklyLabDataRightsApp.cs" 'RequestTradeProofExportAsync'
Assert-Contains "src/TradeProof.Application/Foundation/WeeklyLabDataRightsApp.cs" 'ValidateExportRoundTripAsync'
Assert-Contains "src/TradeProof.Application/Foundation/WeeklyLabDataRightsApp.cs" 'ExpireExportAsync'
Assert-Contains "src/TradeProof.Application/Foundation/WeeklyLabDataRightsApp.cs" 'RequestWorkspaceDeletionAsync'
Assert-Contains "src/TradeProof.Application/Foundation/WeeklyLabDataRightsApp.cs" 'CompleteWorkspaceDeletionAsync'
Assert-Contains "src/TradeProof.Application/Foundation/WeeklyLabDataRightsApp.cs" 'PRIVACY_THRESHOLD'
Assert-Contains "src/TradeProof.Application/Foundation/WeeklyLabDataRightsApp.cs" 'source_record_hash'
Assert-Contains "src/TradeProof.Application/Foundation/WeeklyLabDataRightsApp.cs" 'REVOKED_BY_DELETION'
Assert-Contains "src/TradeProof.Application/Foundation/TradeProofApp.cs" 'internal:weekly-cohort-lock'
Assert-Contains "src/TradeProof.Application/Foundation/TradeProofApp.cs" 'local:export-expiry'
Assert-Contains "src/TradeProof.Application/Foundation/TradeProofApp.cs" 'WorkspaceDeletionTombstones'

$weeklyApp = Get-Content -Raw -LiteralPath "src/TradeProof.Application/Foundation/WeeklyLabDataRightsApp.cs"
if ($weeklyApp -match "\b(double|float)\b") {
    throw "Phase 6 implementation must not use binary floating point types."
}

$sql = Get-Content -Raw -LiteralPath "src/TradeProof.Infrastructure/Migrations/006_phase6_weekly_lab_data_rights.sql"
$tables = @(
    "weekly_cohort",
    "weekly_cohort_input_revision",
    "weekly_report_revision",
    "behavioral_experiment_revision",
    "weekly_review_completion",
    "workspace_product_metric_snapshot",
    "internal_aggregate_product_metric_snapshot",
    "product_analytics_external_projection",
    "external_analytics_purge",
    "tradeproof_export",
    "export_round_trip_validation",
    "export_expiry",
    "workspace_deletion",
    "workspace_deletion_target",
    "workspace_deletion_tombstone"
)
foreach ($table in $tables) {
    if ($sql -notmatch "CREATE TABLE $table") {
        throw "Migration missing table $table."
    }
}
Assert-Contains "src/TradeProof.Infrastructure/Migrations/006_phase6_weekly_lab_data_rights.sql" "CHECK \(weekly_lab_schema_version = 'weekly_lab_v1'\)"
Assert-Contains "src/TradeProof.Infrastructure/Migrations/006_phase6_weekly_lab_data_rights.sql" "CHECK \(renderer_id = 'weekly_lab_renderer_v1'\)"
Assert-Contains "src/TradeProof.Infrastructure/Migrations/006_phase6_weekly_lab_data_rights.sql" "CHECK \(taxonomy_version = 'behavioral_experiment_v1'\)"
Assert-Contains "src/TradeProof.Infrastructure/Migrations/006_phase6_weekly_lab_data_rights.sql" "CHECK \(schema_version = 'workspace_product_metric_snapshot_v1'\)"
Assert-Contains "src/TradeProof.Infrastructure/Migrations/006_phase6_weekly_lab_data_rights.sql" "CHECK \(schema_version = 'internal_aggregate_product_metric_snapshot_v1'\)"
Assert-Contains "src/TradeProof.Infrastructure/Migrations/006_phase6_weekly_lab_data_rights.sql" "CHECK \(schema_version = 'product_analytics_external_v1'\)"
Assert-Contains "src/TradeProof.Infrastructure/Migrations/006_phase6_weekly_lab_data_rights.sql" "CHECK \(export_schema_version = 'tradeproof_export_v1'\)"
Assert-Contains "src/TradeProof.Infrastructure/Migrations/006_phase6_weekly_lab_data_rights.sql" "CHECK \(reader_profile_version = 'tradeproof_export_round_trip_v1'\)"
Assert-Contains "src/TradeProof.Infrastructure/Migrations/006_phase6_weekly_lab_data_rights.sql" "CHECK \(work_type = 'EXPORT_EXPIRY'\)"
Assert-Contains "src/TradeProof.Infrastructure/Migrations/006_phase6_weekly_lab_data_rights.sql" "CHECK \(schema_version = 'workspace_deletion_v1'\)"
Assert-Contains "src/TradeProof.Infrastructure/Migrations/006_phase6_weekly_lab_data_rights.sql" "PRIMARY_TENANT_DATA"
Assert-Contains "src/TradeProof.Infrastructure/Migrations/006_phase6_weekly_lab_data_rights.sql" "EXPORT_ARCHIVES"

Assert-Contains "src/TradeProof.Api/Program.cs" 'phase-[678]'
Assert-Contains "src/TradeProof.Api/Program.cs" '/api/weekly-lab/publish'
Assert-Contains "src/TradeProof.Api/Program.cs" '/api/weekly-lab/experiments/propose'
Assert-Contains "src/TradeProof.Api/Program.cs" '/api/product-analytics/events'
Assert-Contains "src/TradeProof.Api/Program.cs" '/api/product-metrics/workspace/publish'
Assert-Contains "src/TradeProof.Api/Program.cs" '/api/exports/request'
Assert-Contains "src/TradeProof.Api/Program.cs" '/api/workspace/delete-request'
Assert-Contains "src/TradeProof.Api/wwwroot/index.html" 'Weekly Lab'
Assert-Contains "src/TradeProof.Api/wwwroot/index.html" 'Data rights'
Assert-Contains "src/TradeProof.Api/wwwroot/quick-plan.js" 'weekly-lab/publish'
Assert-Contains "src/TradeProof.Api/wwwroot/quick-plan.js" 'product-analytics/events'
Assert-Contains "src/TradeProof.Api/wwwroot/quick-plan.js" 'exports/request'
Assert-Contains "src/TradeProof.Api/wwwroot/quick-plan.js" 'workspace/delete-request'
Assert-Contains "tests/TradeProof.App.Tests/TestProgram.cs" 'phase6'
Assert-Contains ".github/workflows/ci.yml" 'test-phase[678]\.ps1'

$ui = Get-Content -Raw -LiteralPath "src/TradeProof.Api/wwwroot/index.html"
$ui += Get-Content -Raw -LiteralPath "src/TradeProof.Api/wwwroot/quick-plan.js"
if ($ui -match "(?i)exchange api key|api key|private sync|live sync|generic market browser|generic csv mapper|buy signal|sell signal|should buy|should sell|nên mua|nên bán|bullish|bearish|win probability") {
    throw "Phase 6 UI must not include key/private-sync/generic-browser/trading-signal screen text."
}

$trackedBuildOutput = git ls-files -- ':(glob)**/bin/**' ':(glob)**/obj/**'
if ($trackedBuildOutput) {
    $trackedBuildOutput
    throw "bin/obj files must not be tracked."
}

Write-Host "Phase 6 artifact verification passed."
