$ErrorActionPreference = "Stop"

function Invoke-Checked {
    param([scriptblock] $Command)
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE."
    }
}

Invoke-Checked { dotnet restore "TradeProof.sln" --disable-build-servers --verbosity minimal -maxcpucount:1 }
Invoke-Checked { dotnet build "tests/TradeProof.App.Tests/TradeProof.App.Tests.csproj" --configuration Release --no-restore --disable-build-servers -maxcpucount:1 }
Invoke-Checked { dotnet "tests/TradeProof.App.Tests/bin/Release/net10.0/TradeProof.App.Tests.dll" phase0 }
Invoke-Checked { dotnet "tests/TradeProof.App.Tests/bin/Release/net10.0/TradeProof.App.Tests.dll" phase1 }
Invoke-Checked { pwsh -NoProfile -File "tools/verify-phase0.ps1" }
Invoke-Checked { pwsh -NoProfile -File "tools/verify-phase1.ps1" }

$secretPattern = "(api[_-]?secret|secret[_-]?key|private[_-]?key|BEGIN (RSA|OPENSSH|EC) PRIVATE KEY|password=)"
$matches = git grep --untracked -n -E $secretPattern -- . ':!docs/**' ':!README.md' ':!tools/test-phase0.ps1' ':!tools/test-phase1.ps1' ':!**/bin/**' ':!**/obj/**'
if ($LASTEXITCODE -eq 0) {
    $matches
    throw "Potential secret-like content found."
}

if ($LASTEXITCODE -ne 1) {
    throw "git grep failed."
}

Write-Host "Phase 1 local CI verification passed."
