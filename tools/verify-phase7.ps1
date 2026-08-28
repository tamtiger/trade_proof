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
    "src/TradeProof.Application/Foundation/ReleaseReadinessApp.cs",
    "src/TradeProof.Application/Foundation/TradeProofApp.cs",
    "src/TradeProof.Domain/Foundation/IngestionContracts.cs",
    "src/TradeProof.Domain/Foundation/ReleaseReadinessContracts.cs",
    "src/TradeProof.Infrastructure/Migrations/007_phase7_core_hardening.sql",
    "tests/TradeProof.App.Tests/Phase7Tests.cs",
    "tests/TradeProof.App.Tests/TestProgram.cs",
    "tools/test-phase7.ps1",
    "tools/verify-phase7.ps1"
)

foreach ($file in $requiredFiles) {
    Assert-File $file
}

$changelog = Get-Content -Raw -LiteralPath "CHANGELOG.md"
if (-not $changelog.StartsWith("# Changelog`n`n## 2026-08-28 - Phase 7")) {
    throw "CHANGELOG.md must keep the newest Phase 7 entry at the top."
}

Assert-Contains "src/TradeProof.Domain/Foundation/ReleaseReadinessContracts.cs" 'ai_disabled_profile_v1'
Assert-Contains "src/TradeProof.Domain/Foundation/ReleaseReadinessContracts.cs" 'release_hardening_evidence_v1'
Assert-Contains "src/TradeProof.Domain/Foundation/ReleaseReadinessContracts.cs" 'core_release_readiness_v1'
Assert-Contains "src/TradeProof.Domain/Foundation/ReleaseReadinessContracts.cs" 'CORE_HARDENING'
Assert-Contains "src/TradeProof.Domain/Foundation/ReleaseReadinessContracts.cs" 'RELEASE_READINESS'
Assert-Contains "src/TradeProof.Domain/Foundation/IngestionContracts.cs" 'CoreHardening'
Assert-Contains "src/TradeProof.Domain/Foundation/IngestionContracts.cs" 'ReleaseReadinessWork'

Assert-Contains "src/TradeProof.Application/Foundation/ReleaseReadinessApp.cs" 'PublishReleaseReadinessAsync'
Assert-Contains "src/TradeProof.Application/Foundation/ReleaseReadinessApp.cs" 'TP-SEC:AI-00'
Assert-Contains "src/TradeProof.Application/Foundation/ReleaseReadinessApp.cs" 'AI_DEPENDENCY_BLOCKED_CORE_CONTINUES'
Assert-Contains "src/TradeProof.Application/Foundation/ReleaseReadinessApp.cs" 'READY_WITH_AI_DISABLED'
Assert-Contains "src/TradeProof.Application/Foundation/ReleaseReadinessApp.cs" 'CORE_HARDENING_PASSED'
Assert-Contains "src/TradeProof.Application/Foundation/ReleaseReadinessApp.cs" 'CORE_RELEASE_READY_WITH_AI_DISABLED'
Assert-Contains "src/TradeProof.Application/Foundation/TradeProofApp.cs" 'AiDisabledProfiles'
Assert-Contains "src/TradeProof.Application/Foundation/TradeProofApp.cs" 'ReleaseHardeningEvidence'
Assert-Contains "src/TradeProof.Application/Foundation/TradeProofApp.cs" 'ReleaseReadinessReports'

$sql = Get-Content -Raw -LiteralPath "src/TradeProof.Infrastructure/Migrations/007_phase7_core_hardening.sql"
$tables = @(
    "ai_disabled_feature_profile",
    "release_hardening_evidence",
    "core_release_readiness_report"
)
foreach ($table in $tables) {
    if ($sql -notmatch "CREATE TABLE $table") {
        throw "Migration missing table $table."
    }
}
Assert-Contains "src/TradeProof.Infrastructure/Migrations/007_phase7_core_hardening.sql" "CHECK \(schema_version = 'ai_disabled_profile_v1'\)"
Assert-Contains "src/TradeProof.Infrastructure/Migrations/007_phase7_core_hardening.sql" "CHECK \(voice_transcription_enabled = false\)"
Assert-Contains "src/TradeProof.Infrastructure/Migrations/007_phase7_core_hardening.sql" "CHECK \(ai_taxonomy_enabled = false\)"
Assert-Contains "src/TradeProof.Infrastructure/Migrations/007_phase7_core_hardening.sql" "CHECK \(ai_weekly_summary_enabled = false\)"
Assert-Contains "src/TradeProof.Infrastructure/Migrations/007_phase7_core_hardening.sql" "CHECK \(schema_version = 'release_hardening_evidence_v1'\)"
Assert-Contains "src/TradeProof.Infrastructure/Migrations/007_phase7_core_hardening.sql" "CHECK \(security_smoke_state = 'PASS'\)"
Assert-Contains "src/TradeProof.Infrastructure/Migrations/007_phase7_core_hardening.sql" "CHECK \(accessibility_smoke_state = 'PASS'\)"
Assert-Contains "src/TradeProof.Infrastructure/Migrations/007_phase7_core_hardening.sql" "CHECK \(performance_smoke_state = 'PASS'\)"
Assert-Contains "src/TradeProof.Infrastructure/Migrations/007_phase7_core_hardening.sql" "CHECK \(reliability_smoke_state = 'PASS'\)"
Assert-Contains "src/TradeProof.Infrastructure/Migrations/007_phase7_core_hardening.sql" "CHECK \(schema_version = 'core_release_readiness_v1'\)"
Assert-Contains "src/TradeProof.Infrastructure/Migrations/007_phase7_core_hardening.sql" "READY_WITH_AI_DISABLED"

Assert-Contains "src/TradeProof.Api/Program.cs" 'phase-7'
Assert-Contains "src/TradeProof.Api/Program.cs" '/api/release-readiness/publish'
Assert-Contains "src/TradeProof.Api/wwwroot/index.html" 'Release readiness'
Assert-Contains "src/TradeProof.Api/wwwroot/quick-plan.js" 'release-readiness/publish'
Assert-Contains "tests/TradeProof.App.Tests/TestProgram.cs" 'phase7'
Assert-Contains ".github/workflows/ci.yml" 'test-phase7.ps1'

$apiAndUi = Get-Content -Raw -LiteralPath "src/TradeProof.Api/Program.cs"
$apiAndUi += Get-Content -Raw -LiteralPath "src/TradeProof.Api/wwwroot/index.html"
$apiAndUi += Get-Content -Raw -LiteralPath "src/TradeProof.Api/wwwroot/quick-plan.js"
if ($apiAndUi -match "/api/ai|AI_RUN|AI_CANCEL|AI_OUTPUT_DELETE|voice transcription|taxonomy suggestion|weekly summary enabled") {
    throw "Phase 7 API/UI must not expose enabled AI extension routes or work controls."
}

$ui = Get-Content -Raw -LiteralPath "src/TradeProof.Api/wwwroot/index.html"
$ui += Get-Content -Raw -LiteralPath "src/TradeProof.Api/wwwroot/quick-plan.js"
if ($ui -match "(?i)exchange api key|api key|private sync|live sync|generic market browser|generic csv mapper|buy signal|sell signal|should buy|should sell|nên mua|nên bán|bullish|bearish|win probability") {
    throw "Phase 7 UI must not include key/private-sync/generic-browser/trading-signal screen text."
}

$registered = Get-Content -Raw -LiteralPath "src/TradeProof.Domain/Foundation/IngestionContracts.cs"
if ($registered -match "AI_RUN|AI_CANCEL|AI_OUTPUT_DELETE") {
    throw "AI work types must remain unregistered for core hardening."
}

$trackedBuildOutput = git ls-files -- ':(glob)**/bin/**' ':(glob)**/obj/**'
if ($trackedBuildOutput) {
    $trackedBuildOutput
    throw "bin/obj files must not be tracked."
}

Write-Host "Phase 7 artifact verification passed."
