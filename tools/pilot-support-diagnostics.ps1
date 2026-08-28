$ErrorActionPreference = "Stop"

function Write-Section {
    param([string] $Title)
    Write-Host ""
    Write-Host "== $Title =="
}

function Write-DocExcerpt {
    param([string] $Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        Write-Host "Missing: $Path"
        return
    }

    Write-Host "-- $Path"
    Get-Content -LiteralPath $Path -TotalCount 24
}

Write-Section "Repository status"
git status --short

Write-Section "Recent commits"
git log --oneline -8

Write-Section "Harnix public status"
harnix status

Write-Section "Phase 8 operations excerpts"
Write-DocExcerpt "docs/operations/release-evidence-bundle.md"
Write-DocExcerpt "docs/operations/pilot-onboarding-support.md"
Write-DocExcerpt "docs/operations/pilot-readiness-review.md"

Write-Section "Boundary"
Write-Host "Diagnostics are repo-local and do not require product workspace content or private credentials."
