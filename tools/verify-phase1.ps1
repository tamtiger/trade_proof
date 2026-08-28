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
    "src/TradeProof.Domain/Foundation/TradeProofContracts.cs",
    "src/TradeProof.Infrastructure/Migrations/001_phase1_foundation.sql",
    "tests/TradeProof.App.Tests/Phase1Tests.cs",
    "tests/TradeProof.App.Tests/TestProgram.cs",
    "tools/test-phase1.ps1"
)

foreach ($file in $requiredFiles) {
    Assert-File $file
}

$changelog = Get-Content -Raw -LiteralPath "CHANGELOG.md"
if ($changelog -notmatch "## 2026-08-27 - Phase 1: tenant foundation and Quick Plan") {
    throw "CHANGELOG.md must keep the Phase 1 entry."
}

Assert-Contains "src/TradeProof.Domain/Foundation/TradeProofContracts.cs" 'tenant_control_job_payload_v1'
Assert-Contains "src/TradeProof.Domain/Foundation/TradeProofContracts.cs" 'tenant_work_item_terminal_marker_v1'
Assert-Contains "src/TradeProof.Domain/Foundation/TradeProofContracts.cs" 'product_measurement_run_v1'
Assert-Contains "src/TradeProof.Application/Foundation/TradeProofApp.cs" 'ManagedIdentity'
Assert-Contains "src/TradeProof.Application/Foundation/TradeProofApp.cs" 'PRE_AUTH'
Assert-Contains "src/TradeProof.Application/Foundation/TradeProofApp.cs" 'POST_AUTH'
Assert-Contains "src/TradeProof.Application/Foundation/TradeProofApp.cs" 'TENANT_CONTROL_JOB_IDEMPOTENCY_CONFLICT'
Assert-Contains "src/TradeProof.Application/Foundation/TradeProofApp.cs" 'MEASUREMENT_REQUIRES_THREE_PRACTICES'
Assert-Contains "src/TradeProof.Application/Foundation/TradeProofApp.cs" 'PLAN_ACTIVE_CONFLICT'

$sql = Get-Content -Raw -LiteralPath "src/TradeProof.Infrastructure/Migrations/001_phase1_foundation.sql"
$tables = @(
    "tp_user",
    "user_identity",
    "workspace",
    "trading_account",
    "audit_event",
    "idempotency_receipt",
    "setup_preset_revision",
    "trade_plan",
    "trade_plan_revision",
    "tenant_control_job",
    "tenant_work_item_fence",
    "tenant_work_item_fence_event",
    "tenant_external_operation_lease",
    "tenant_work_item_terminal_marker",
    "product_measurement_run"
)
foreach ($table in $tables) {
    if ($sql -notmatch "CREATE TABLE $table") {
        throw "Migration missing table $table."
    }
}
Assert-Contains "src/TradeProof.Infrastructure/Migrations/001_phase1_foundation.sql" 'workspace_id text NOT NULL'
Assert-Contains "src/TradeProof.Infrastructure/Migrations/001_phase1_foundation.sql" 'FOREIGN KEY \(workspace_id, trading_account_id\)'
Assert-Contains "src/TradeProof.Infrastructure/Migrations/001_phase1_foundation.sql" "CHECK \(schema_version = 'product_measurement_run_v1'\)"
Assert-Contains "src/TradeProof.Infrastructure/Migrations/001_phase1_foundation.sql" "CHECK \(operation_payload_schema_version = 'tenant_control_job_payload_v1'\)"
Assert-Contains "src/TradeProof.Infrastructure/Migrations/001_phase1_foundation.sql" "CHECK \(terminal_marker_digest_profile = 'tenant_work_item_terminal_marker_v1'\)"

Assert-Contains "src/TradeProof.Api/Program.cs" 'X-TradeProof-Issuer'
Assert-Contains "src/TradeProof.Api/Program.cs" '/api/plans/arm'
Assert-Contains "src/TradeProof.Api/Program.cs" '/api/product-measurements/start'
Assert-Contains "src/TradeProof.Api/wwwroot/index.html" 'Quick Plan'
Assert-Contains "src/TradeProof.Api/wwwroot/index.html" 'Trang thai|Trạng thái'
Assert-Contains "src/TradeProof.Api/wwwroot/quick-plan.js" 'Khong|Không'
Assert-Contains "src/TradeProof.Api/wwwroot/quick-plan.js" 'Da arm|Đa arm|Đã arm'

$ui = Get-Content -Raw -LiteralPath "src/TradeProof.Api/wwwroot/index.html"
if ($ui -match "(?i)exchange api key|api key|secret") {
    throw "Quick Plan UI must not include an exchange API key screen."
}

$trackedBuildOutput = git ls-files -- ':(glob)**/bin/**' ':(glob)**/obj/**'
if ($trackedBuildOutput) {
    $trackedBuildOutput
    throw "bin/obj files must not be tracked."
}

Write-Host "Phase 1 artifact verification passed."
