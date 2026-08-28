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

function Assert-NotContains {
    param(
        [string] $Path,
        [string] $Pattern
    )
    $content = Get-Content -Raw -LiteralPath $Path
    if ($content -match $Pattern) {
        throw "File '$Path' contains prohibited pattern: $Pattern"
    }
}

$requiredFiles = @(
    ".github/workflows/ci.yml",
    "CHANGELOG.md",
    "docs/operations/pilot-readiness-review.md",
    "docs/operations/alert-dashboard.md",
    "docs/operations/runbook-exercise.md",
    "docs/operations/pilot-onboarding-support.md",
    "docs/operations/data-processor-disclosure.md",
    "docs/operations/release-evidence-bundle.md",
    "src/TradeProof.Api/Program.cs",
    "tests/TradeProof.App.Tests/Phase8Tests.cs",
    "tests/TradeProof.App.Tests/TestProgram.cs",
    "tools/pilot-support-diagnostics.ps1",
    "tools/test-phase8.ps1",
    "tools/verify-phase8.ps1"
)

foreach ($file in $requiredFiles) {
    Assert-File $file
}

$changelog = Get-Content -Raw -LiteralPath "CHANGELOG.md"
if (-not $changelog.StartsWith("# Changelog`n`n## 2026-08-28 - Phase 8")) {
    throw "CHANGELOG.md must keep the newest Phase 8 entry at the top."
}

Assert-Contains "docs/operations/pilot-readiness-review.md" "Production readiness review"
Assert-Contains "docs/operations/pilot-readiness-review.md" "Local release candidate does not self-deploy"
Assert-Contains "docs/operations/pilot-readiness-review.md" "P0/P1 defects: 0"
Assert-Contains "docs/operations/pilot-readiness-review.md" "Non-waivable gates: pass"
Assert-Contains "docs/operations/pilot-readiness-review.md" "AI extensions: disabled"

Assert-Contains "docs/operations/alert-dashboard.md" "Alert dashboard and on-call ownership"
Assert-Contains "docs/operations/alert-dashboard.md" "cross-tenant denial"
Assert-Contains "docs/operations/alert-dashboard.md" "export/deletion age"
Assert-Contains "docs/operations/alert-dashboard.md" "queue health"
Assert-Contains "docs/operations/alert-dashboard.md" "On-call ownership table"

Assert-Contains "docs/operations/runbook-exercise.md" "Incident exercise"
Assert-Contains "docs/operations/runbook-exercise.md" "Backup/restore exercise"
Assert-Contains "docs/operations/runbook-exercise.md" "Deletion exercise"
Assert-Contains "docs/operations/runbook-exercise.md" "Processor dependency exercise"
Assert-Contains "docs/operations/runbook-exercise.md" "RPO <=24 hours"
Assert-Contains "docs/operations/runbook-exercise.md" "RTO <=8 hours"

Assert-Contains "docs/operations/pilot-onboarding-support.md" "Pilot onboarding and support"
Assert-Contains "docs/operations/pilot-onboarding-support.md" "support does not have product access to workspace content"
Assert-Contains "docs/operations/pilot-onboarding-support.md" "No WorkspaceId, token, secret, database credential, object-store credential or workspace export"
Assert-Contains "docs/operations/pilot-onboarding-support.md" "Known limitations"

Assert-Contains "docs/operations/data-processor-disclosure.md" "Data processor disclosure"
Assert-Contains "docs/operations/data-processor-disclosure.md" "Azure Southeast Asia"
Assert-Contains "docs/operations/data-processor-disclosure.md" "Azure Monitor/Application Insights"
Assert-Contains "docs/operations/data-processor-disclosure.md" "daily encrypted backups"
Assert-Contains "docs/operations/data-processor-disclosure.md" "processor contracts/disclosures ready"

Assert-Contains "docs/operations/release-evidence-bundle.md" "Release evidence bundle"
Assert-Contains "docs/operations/release-evidence-bundle.md" "Build/commit capture policy"
Assert-Contains "docs/operations/release-evidence-bundle.md" "Migration version: 007_phase7_core_hardening.sql"
Assert-Contains "docs/operations/release-evidence-bundle.md" "Requirements-to-tests matrix"
Assert-Contains "docs/operations/release-evidence-bundle.md" "Security/secret scan"
Assert-Contains "docs/operations/release-evidence-bundle.md" "Performance/usability/accessibility evidence"
Assert-Contains "docs/operations/release-evidence-bundle.md" "Known limitations, disabled flags and risk exceptions"
Assert-Contains "docs/operations/release-evidence-bundle.md" "Version list"
Assert-Contains "docs/operations/release-evidence-bundle.md" "No P0/P1 defect"
Assert-Contains "docs/operations/release-evidence-bundle.md" "AI eval: not applicable for core-disabled release"

Assert-Contains "tools/pilot-support-diagnostics.ps1" "git status --short"
Assert-Contains "tools/pilot-support-diagnostics.ps1" "git log --oneline -8"
Assert-Contains "tools/pilot-support-diagnostics.ps1" "harnix status"
Assert-Contains "tools/pilot-support-diagnostics.ps1" "docs/operations/release-evidence-bundle.md"
Assert-Contains "tools/pilot-support-diagnostics.ps1" "docs/operations/pilot-onboarding-support.md"
Assert-NotContains "tools/pilot-support-diagnostics.ps1" "param\s*\([^)]*(WorkspaceId|Token|Secret|DbCredential|ObjectStoreCredential|ExportPath)"
Assert-NotContains "tools/pilot-support-diagnostics.ps1" "Invoke-WebRequest|Invoke-RestMethod|curl|wget"
Assert-NotContains "tools/pilot-support-diagnostics.ps1" "SqlConnection|Npgsql|AzureStorage|BlobClient"

Assert-Contains "src/TradeProof.Api/Program.cs" "phase-8"
Assert-Contains "tests/TradeProof.App.Tests/TestProgram.cs" "Phase8Tests.Run"
Assert-Contains "tests/TradeProof.App.Tests/TestProgram.cs" "phase8"
Assert-Contains ".github/workflows/ci.yml" "Test and verify Phase 8"
Assert-Contains ".github/workflows/ci.yml" "tools/test-phase8.ps1"
Assert-Contains "tools/test-phase8.ps1" "phase8"
Assert-Contains "tools/test-phase8.ps1" "verify-phase8.ps1"

Assert-Contains "tools/verify-phase2.ps1" "phase-\[2345678\]"
Assert-Contains "tools/verify-phase3.ps1" "phase-\[345678\]"
Assert-Contains "tools/verify-phase4.ps1" "phase-\[45678\]"
Assert-Contains "tools/verify-phase5.ps1" "phase-\[5678\]"
Assert-Contains "tools/verify-phase6.ps1" "phase-\[678\]"
Assert-Contains "tools/verify-phase6.ps1" "test-phase\[678\]"
Assert-Contains "tools/verify-phase7.ps1" "phase-\[78\]"
Assert-Contains "tools/verify-phase7.ps1" "test-phase\[78\]"

$trackedBuildOutput = git ls-files -- ':(glob)**/bin/**' ':(glob)**/obj/**'
if ($trackedBuildOutput) {
    $trackedBuildOutput
    throw "bin/obj files must not be tracked."
}

Write-Host "Phase 8 artifact verification passed."
