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
    "src/TradeProof.Application/Foundation/IngestionApp.cs",
    "src/TradeProof.Domain/Foundation/TradeProofContracts.cs",
    "src/TradeProof.Domain/Foundation/IngestionContracts.cs",
    "src/TradeProof.Infrastructure/Migrations/001_phase1_foundation.sql",
    "src/TradeProof.Infrastructure/Migrations/002_phase2_secure_ingestion.sql",
    "tests/TradeProof.App.Tests/Phase1Tests.cs",
    "tests/TradeProof.App.Tests/Phase2Tests.cs",
    "tests/TradeProof.App.Tests/TestProgram.cs",
    "tools/test-phase2.ps1",
    "tools/verify-phase2.ps1"
)

foreach ($file in $requiredFiles) {
    Assert-File $file
}

$changelog = Get-Content -Raw -LiteralPath "CHANGELOG.md"
if (-not $changelog.StartsWith("# Changelog`n`n")) {
    throw "CHANGELOG.md must keep the changelog title at the top."
}
if ($changelog -notmatch "## 2026-08-27 - Phase 2: secure ingestion") {
    throw "CHANGELOG.md must keep the Phase 2 entry."
}

Assert-Contains "src/TradeProof.Domain/Foundation/IngestionContracts.cs" 'object_ingest_reservation_v1'
Assert-Contains "src/TradeProof.Domain/Foundation/IngestionContracts.cs" 'upload_attachment_v1'
Assert-Contains "src/TradeProof.Domain/Foundation/IngestionContracts.cs" 'import_preview_v1'
Assert-Contains "src/TradeProof.Domain/Foundation/IngestionContracts.cs" 'staged_fill_v1'
Assert-Contains "src/TradeProof.Domain/Foundation/IngestionContracts.cs" 'binance_spot_trade_history_csv_v1'
Assert-Contains "src/TradeProof.Domain/Foundation/IngestionContracts.cs" 'OBJECT_INGEST_FINALIZE'
Assert-Contains "src/TradeProof.Domain/Foundation/IngestionContracts.cs" 'UPLOAD_VALIDATE'
Assert-Contains "src/TradeProof.Domain/Foundation/IngestionContracts.cs" 'UPLOAD_PURGE'
Assert-Contains "src/TradeProof.Domain/Foundation/IngestionContracts.cs" 'IMPORT'
Assert-Contains "src/TradeProof.Domain/Foundation/IngestionContracts.cs" 'RegisteredWorkTypes'

Assert-Contains "src/TradeProof.Application/Foundation/IngestionApp.cs" 'ReserveRawUploadAsync'
Assert-Contains "src/TradeProof.Application/Foundation/IngestionApp.cs" 'RecordReservedBytesAsync'
Assert-Contains "src/TradeProof.Application/Foundation/IngestionApp.cs" 'TransferRawUploadAsync'
Assert-Contains "src/TradeProof.Application/Foundation/IngestionApp.cs" 'ValidateUploadAsync'
Assert-Contains "src/TradeProof.Application/Foundation/IngestionApp.cs" 'ConfirmImportAsync'
Assert-Contains "src/TradeProof.Application/Foundation/IngestionApp.cs" 'PurgeUploadAsync'
Assert-Contains "src/TradeProof.Application/Foundation/IngestionApp.cs" 'TextFieldParser'
Assert-Contains "src/TradeProof.Application/Foundation/IngestionApp.cs" 'MaxRawUploadBytes = 20 \* 1024 \* 1024'
Assert-Contains "src/TradeProof.Application/Foundation/IngestionApp.cs" 'MaxCsvDataRows = 100_000'
Assert-Contains "src/TradeProof.Application/Foundation/IngestionApp.cs" 'WRITE_CAPABILITY_ALREADY_CONSUMED'
Assert-Contains "src/TradeProof.Application/Foundation/IngestionApp.cs" 'IMPORT_PREVIEW_EXPIRED'
Assert-Contains "src/TradeProof.Application/Foundation/IngestionApp.cs" 'RAW_UPLOAD_RETENTION_DEADLINE'

$sql = Get-Content -Raw -LiteralPath "src/TradeProof.Infrastructure/Migrations/002_phase2_secure_ingestion.sql"
$tables = @(
    "object_ingest_reservation",
    "object_ingest_reservation_event",
    "upload",
    "upload_state_event",
    "upload_object_lease",
    "upload_object_absence_verification",
    "import_preview",
    "import_batch",
    "staged_fill",
    "staged_fill_disposition"
)
foreach ($table in $tables) {
    if ($sql -notmatch "CREATE TABLE $table") {
        throw "Migration missing table $table."
    }
}
Assert-Contains "src/TradeProof.Infrastructure/Migrations/002_phase2_secure_ingestion.sql" "CHECK \(schema_version = 'import_preview_v1'\)"
Assert-Contains "src/TradeProof.Infrastructure/Migrations/002_phase2_secure_ingestion.sql" "CHECK \(staged_fill_schema_version = 'staged_fill_v1'\)"
Assert-Contains "src/TradeProof.Infrastructure/Migrations/002_phase2_secure_ingestion.sql" "CHECK \(contract_version = 'upload_attachment_v1'\)"
Assert-Contains "src/TradeProof.Infrastructure/Migrations/002_phase2_secure_ingestion.sql" "CHECK \(adapter_contract_version = 'binance_spot_trade_history_csv_v1'\)"
Assert-Contains "src/TradeProof.Infrastructure/Migrations/002_phase2_secure_ingestion.sql" "FOREIGN KEY \(workspace_id, trading_account_id\)"

Assert-Contains "src/TradeProof.Api/Program.cs" 'phase-[23]'
Assert-Contains "src/TradeProof.Api/Program.cs" '/api/imports/reserve'
Assert-Contains "src/TradeProof.Api/Program.cs" '/api/imports/\{objectIngestReservationId\}/record-bytes'
Assert-Contains "src/TradeProof.Api/Program.cs" '/api/uploads/\{uploadId\}/validate'
Assert-Contains "src/TradeProof.Api/Program.cs" '/api/imports/confirm'
Assert-Contains "src/TradeProof.Api/Program.cs" '/api/imports/\{importBatchId\}/progress'

Assert-Contains "src/TradeProof.Api/wwwroot/index.html" 'Import CSV'
Assert-Contains "src/TradeProof.Api/wwwroot/index.html" 'Reserve'
Assert-Contains "src/TradeProof.Api/wwwroot/index.html" 'Validate'
Assert-Contains "src/TradeProof.Api/wwwroot/index.html" 'Confirm'
Assert-Contains "src/TradeProof.Api/wwwroot/index.html" 'Purge'
Assert-Contains "src/TradeProof.Api/wwwroot/quick-plan.js" 'binance_spot_trade_history_csv_v1'
Assert-Contains "src/TradeProof.Api/wwwroot/quick-plan.js" 'Không validate được upload'

$ui = Get-Content -Raw -LiteralPath "src/TradeProof.Api/wwwroot/index.html"
$ui += Get-Content -Raw -LiteralPath "src/TradeProof.Api/wwwroot/quick-plan.js"
if ($ui -match "(?i)exchange api key|api key|private sync|live sync|generic csv mapper|secret") {
    throw "Import UI must not include key, private sync, live sync, secret or generic mapper screen text."
}

$safeErrorProperties = Select-String -Path "src/TradeProof.Domain/Foundation/IngestionContracts.cs" -Pattern 'public sealed record SafeRowErrorRecord\(([^\)]*)\)' -AllMatches
if (-not $safeErrorProperties) {
    throw "SafeRowErrorRecord must be declared."
}
$safeErrorLine = $safeErrorProperties.Matches[0].Groups[1].Value
if ($safeErrorLine -match "(?i)raw|filename|path|cell|value") {
    throw "SafeRowErrorRecord must not expose raw cell, filename, path or value fields."
}

$trackedBuildOutput = git ls-files -- ':(glob)**/bin/**' ':(glob)**/obj/**'
if ($trackedBuildOutput) {
    $trackedBuildOutput
    throw "bin/obj files must not be tracked."
}

Write-Host "Phase 2 artifact verification passed."
