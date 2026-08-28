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
    "src/TradeProof.Application/Foundation/ReviewMetricsApp.cs",
    "src/TradeProof.Application/Foundation/TradeProofApp.cs",
    "src/TradeProof.Domain/Foundation/IngestionContracts.cs",
    "src/TradeProof.Domain/Foundation/ReviewMetricContracts.cs",
    "src/TradeProof.Infrastructure/Migrations/005_phase5_review_metrics_dashboard.sql",
    "tests/TradeProof.App.Tests/Phase5Tests.cs",
    "tests/TradeProof.App.Tests/TestProgram.cs",
    "tools/test-phase5.ps1",
    "tools/verify-phase5.ps1"
)

foreach ($file in $requiredFiles) {
    Assert-File $file
}

$changelog = Get-Content -Raw -LiteralPath "CHANGELOG.md"
if (-not $changelog.StartsWith("# Changelog`n`n")) {
    throw "CHANGELOG.md must keep the changelog title at the top."
}
if ($changelog -notmatch "## 2026-08-28 - Phase 5: review, metrics and dashboard") {
    throw "CHANGELOG.md must keep the Phase 5 entry."
}

Assert-Contains "src/TradeProof.Domain/Foundation/ReviewMetricContracts.cs" 'attachment_v1'
Assert-Contains "src/TradeProof.Domain/Foundation/ReviewMetricContracts.cs" 'attachment_content_version_v1'
Assert-Contains "src/TradeProof.Domain/Foundation/ReviewMetricContracts.cs" 'review_v1'
Assert-Contains "src/TradeProof.Domain/Foundation/ReviewMetricContracts.cs" 'review_revision_v1'
Assert-Contains "src/TradeProof.Domain/Foundation/ReviewMetricContracts.cs" 'review_revision_attachment_v1'
Assert-Contains "src/TradeProof.Domain/Foundation/ReviewMetricContracts.cs" 'review_taxonomy_v1'
Assert-Contains "src/TradeProof.Domain/Foundation/ReviewMetricContracts.cs" 'metric_snapshot_v1'
Assert-Contains "src/TradeProof.Domain/Foundation/ReviewMetricContracts.cs" 'metrics_v1'
Assert-Contains "src/TradeProof.Domain/Foundation/ReviewMetricContracts.cs" 'metrics_decimal_v1'
Assert-Contains "src/TradeProof.Domain/Foundation/ReviewMetricContracts.cs" 'ATTACHMENT_DELETE'
Assert-Contains "src/TradeProof.Domain/Foundation/ReviewMetricContracts.cs" 'METRICS'
Assert-Contains "src/TradeProof.Domain/Foundation/IngestionContracts.cs" 'AttachmentDelete'
Assert-Contains "src/TradeProof.Domain/Foundation/IngestionContracts.cs" 'Metrics'

Assert-Contains "src/TradeProof.Application/Foundation/ReviewMetricsApp.cs" 'ReserveReviewAttachmentAsync'
Assert-Contains "src/TradeProof.Application/Foundation/ReviewMetricsApp.cs" 'ValidateAttachmentUploadAsync'
Assert-Contains "src/TradeProof.Application/Foundation/ReviewMetricsApp.cs" 'DeleteAttachmentAsync'
Assert-Contains "src/TradeProof.Application/Foundation/ReviewMetricsApp.cs" 'CompleteEpisodeReviewAsync'
Assert-Contains "src/TradeProof.Application/Foundation/ReviewMetricsApp.cs" 'ReviseEpisodeReviewAsync'
Assert-Contains "src/TradeProof.Application/Foundation/ReviewMetricsApp.cs" 'PublishMetricSnapshotsAsync'
Assert-Contains "src/TradeProof.Application/Foundation/ReviewMetricsApp.cs" 'SANITIZED_ATTACHMENT'
Assert-Contains "src/TradeProof.Application/Foundation/ReviewMetricsApp.cs" 'SCREENSHOT'
Assert-Contains "src/TradeProof.Application/Foundation/ReviewMetricsApp.cs" 'exit_reason_v1'
Assert-Contains "src/TradeProof.Application/Foundation/ReviewMetricsApp.cs" 'breach_type_v1'
Assert-Contains "src/TradeProof.Application/Foundation/ReviewMetricsApp.cs" 'emotion_v1'
Assert-Contains "src/TradeProof.Application/Foundation/ReviewMetricsApp.cs" 'review_coverage_rate'
Assert-Contains "src/TradeProof.Application/Foundation/ReviewMetricsApp.cs" 'plan_adherence_rate'
Assert-Contains "src/TradeProof.Application/Foundation/ReviewMetricsApp.cs" 'context_coverage_counts'
Assert-Contains "src/TradeProof.Application/Foundation/ReviewMetricsApp.cs" 'EXPLORATORY'
Assert-Contains "src/TradeProof.Application/Foundation/ReviewMetricsApp.cs" 'ReviewRevisionAttachmentRecord'
Assert-Contains "src/TradeProof.Application/Foundation/TradeProofApp.cs" 'MetricSnapshots'
Assert-Contains "src/TradeProof.Application/Foundation/TradeProofApp.cs" 'DataQuality'
Assert-Contains "src/TradeProof.Application/Foundation/AccountingApp.cs" 'EnqueueMetricsForProjection'

$metricsApp = Get-Content -Raw -LiteralPath "src/TradeProof.Application/Foundation/ReviewMetricsApp.cs"
if ($metricsApp -match "\b(double|float)\b") {
    throw "Metrics implementation must not use binary floating point types."
}

$sql = Get-Content -Raw -LiteralPath "src/TradeProof.Infrastructure/Migrations/005_phase5_review_metrics_dashboard.sql"
$tables = @(
    "review_taxonomy_version",
    "review_taxonomy_item",
    "review_taxonomy_publish_event",
    "attachment",
    "attachment_state_event",
    "attachment_tombstone",
    "review",
    "review_revision",
    "review_revision_attachment",
    "metric_snapshot"
)
foreach ($table in $tables) {
    if ($sql -notmatch "CREATE TABLE $table") {
        throw "Migration missing table $table."
    }
}
Assert-Contains "src/TradeProof.Infrastructure/Migrations/005_phase5_review_metrics_dashboard.sql" "CHECK \(schema_version = 'review_taxonomy_v1'\)"
Assert-Contains "src/TradeProof.Infrastructure/Migrations/005_phase5_review_metrics_dashboard.sql" "CHECK \(schema_version = 'attachment_v1'\)"
Assert-Contains "src/TradeProof.Infrastructure/Migrations/005_phase5_review_metrics_dashboard.sql" "CHECK \(schema_version = 'review_v1'\)"
Assert-Contains "src/TradeProof.Infrastructure/Migrations/005_phase5_review_metrics_dashboard.sql" "CHECK \(schema_version = 'review_revision_v1'\)"
Assert-Contains "src/TradeProof.Infrastructure/Migrations/005_phase5_review_metrics_dashboard.sql" "CHECK \(schema_version = 'review_revision_attachment_v1'\)"
Assert-Contains "src/TradeProof.Infrastructure/Migrations/005_phase5_review_metrics_dashboard.sql" "CHECK \(metric_snapshot_schema_version = 'metric_snapshot_v1'\)"
Assert-Contains "src/TradeProof.Infrastructure/Migrations/005_phase5_review_metrics_dashboard.sql" "CHECK \(metric_algorithm_version = 'metrics_v1'\)"
Assert-Contains "src/TradeProof.Infrastructure/Migrations/005_phase5_review_metrics_dashboard.sql" "CHECK \(metric_decimal_version = 'metrics_decimal_v1'\)"
Assert-Contains "src/TradeProof.Infrastructure/Migrations/005_phase5_review_metrics_dashboard.sql" "INSUFFICIENT'.*'EXPLORATORY'.*'ESTIMATED"

Assert-Contains "src/TradeProof.Api/Program.cs" 'phase-[5678]'
Assert-Contains "src/TradeProof.Api/Program.cs" '/api/attachments/reserve'
Assert-Contains "src/TradeProof.Api/Program.cs" '/api/attachments/\{uploadId\}/validate'
Assert-Contains "src/TradeProof.Api/Program.cs" '/api/attachments/\{attachmentId\}/delete'
Assert-Contains "src/TradeProof.Api/Program.cs" '/api/reviews/complete'
Assert-Contains "src/TradeProof.Api/Program.cs" '/api/reviews/\{reviewId\}/revise'
Assert-Contains "src/TradeProof.Api/Program.cs" '/api/metrics/publish'
Assert-Contains "src/TradeProof.Api/wwwroot/index.html" 'Quick Review'
Assert-Contains "src/TradeProof.Api/wwwroot/index.html" 'Metrics'
Assert-Contains "src/TradeProof.Api/wwwroot/index.html" 'Attach screenshot'
Assert-Contains "src/TradeProof.Api/wwwroot/quick-plan.js" 'ReserveReviewAttachment|attachments/reserve'
Assert-Contains "src/TradeProof.Api/wwwroot/quick-plan.js" 'reviews/complete'
Assert-Contains "src/TradeProof.Api/wwwroot/quick-plan.js" 'metrics/publish'
Assert-Contains "src/TradeProof.Api/wwwroot/index.html" 'aria-label="Quick Review"'
Assert-Contains "src/TradeProof.Api/wwwroot/index.html" 'aria-label="Metrics dashboard"'
Assert-Contains "tests/TradeProof.App.Tests/TestProgram.cs" 'phase5'

$ui = Get-Content -Raw -LiteralPath "src/TradeProof.Api/wwwroot/index.html"
$ui += Get-Content -Raw -LiteralPath "src/TradeProof.Api/wwwroot/quick-plan.js"
if ($ui -match "(?i)exchange api key|api key|private sync|live sync|generic market browser|generic csv mapper|buy signal|sell signal|should buy|should sell|nên mua|nên bán|bullish|bearish|win probability") {
    throw "Phase 5 UI must not include key/private-sync/generic-browser/trading-signal screen text."
}

$trackedBuildOutput = git ls-files -- ':(glob)**/bin/**' ':(glob)**/obj/**'
if ($trackedBuildOutput) {
    $trackedBuildOutput
    throw "bin/obj files must not be tracked."
}

Write-Host "Phase 5 artifact verification passed."
