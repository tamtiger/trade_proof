$ErrorActionPreference = "Stop"

dotnet restore "TradeProof.sln" --disable-build-servers --verbosity minimal -maxcpucount:1
dotnet test "TradeProof.sln" --no-restore --configuration Release --disable-build-servers --verbosity minimal -maxcpucount:1

pwsh -NoProfile -File "tools/verify-phase0.ps1"

$secretPattern = "(api[_-]?secret|secret[_-]?key|private[_-]?key|BEGIN (RSA|OPENSSH|EC) PRIVATE KEY|password=)"
$matches = git grep --untracked -n -E $secretPattern -- . ':!docs/**' ':!README.md' ':!tools/test-phase0.ps1' ':!**/bin/**' ':!**/obj/**'
if ($LASTEXITCODE -eq 0) {
    $matches
    throw "Potential secret-like content found."
}

if ($LASTEXITCODE -ne 1) {
    throw "git grep failed."
}

Write-Host "Phase 0 local CI verification passed."
