using System.Text.RegularExpressions;

namespace TradeProof.App.Tests;

public static class Phase8Tests
{
    public static Task Run()
    {
        string root = FindRepositoryRoot();

        PilotReadinessDocsAreComplete(root);
        SupportDiagnosticsStayRepoLocal(root);
        Phase8WiringIsCurrent(root);

        return Task.CompletedTask;
    }

    private static void PilotReadinessDocsAreComplete(string root)
    {
        string[] docs =
        [
            "docs/operations/pilot-readiness-review.md",
            "docs/operations/alert-dashboard.md",
            "docs/operations/runbook-exercise.md",
            "docs/operations/pilot-onboarding-support.md",
            "docs/operations/data-processor-disclosure.md",
            "docs/operations/release-evidence-bundle.md"
        ];

        foreach (string doc in docs)
        {
            RequireFile(root, doc);
        }

        string readiness = Read(root, "docs/operations/pilot-readiness-review.md");
        Contains(readiness, "Production readiness review");
        Contains(readiness, "Local release candidate does not self-deploy");
        Contains(readiness, "P0/P1 defects: 0");
        Contains(readiness, "Non-waivable gates: pass");
        Contains(readiness, "AI extensions: disabled");

        string alertDashboard = Read(root, "docs/operations/alert-dashboard.md");
        Contains(alertDashboard, "Alert dashboard and on-call ownership");
        Contains(alertDashboard, "cross-tenant denial");
        Contains(alertDashboard, "export/deletion age");
        Contains(alertDashboard, "queue health");
        Contains(alertDashboard, "On-call ownership table");

        string runbook = Read(root, "docs/operations/runbook-exercise.md");
        Contains(runbook, "Incident exercise");
        Contains(runbook, "Backup/restore exercise");
        Contains(runbook, "Deletion exercise");
        Contains(runbook, "Processor dependency exercise");
        Contains(runbook, "RPO <=24 hours");
        Contains(runbook, "RTO <=8 hours");

        string onboarding = Read(root, "docs/operations/pilot-onboarding-support.md");
        Contains(onboarding, "Pilot onboarding and support");
        Contains(onboarding, "support does not have product access to workspace content");
        Contains(onboarding, "No WorkspaceId, token, secret, database credential, object-store credential or workspace export");
        Contains(onboarding, "Known limitations");

        string disclosure = Read(root, "docs/operations/data-processor-disclosure.md");
        Contains(disclosure, "Data processor disclosure");
        Contains(disclosure, "Azure Southeast Asia");
        Contains(disclosure, "Azure Monitor/Application Insights");
        Contains(disclosure, "daily encrypted backups");
        Contains(disclosure, "processor contracts/disclosures ready");

        string evidence = Read(root, "docs/operations/release-evidence-bundle.md");
        Contains(evidence, "Release evidence bundle");
        Contains(evidence, "Build/commit capture policy");
        Contains(evidence, "Migration version: 007_phase7_core_hardening.sql");
        Contains(evidence, "Requirements-to-tests matrix");
        Contains(evidence, "Security/secret scan");
        Contains(evidence, "Performance/usability/accessibility evidence");
        Contains(evidence, "Known limitations, disabled flags and risk exceptions");
        Contains(evidence, "Version list");
        Contains(evidence, "No P0/P1 defect");
        Contains(evidence, "AI eval: not applicable for core-disabled release");
    }

    private static void SupportDiagnosticsStayRepoLocal(string root)
    {
        string script = Read(root, "tools/pilot-support-diagnostics.ps1");

        Contains(script, "git status --short");
        Contains(script, "git log --oneline -8");
        Contains(script, "harnix status");
        Contains(script, "docs/operations/release-evidence-bundle.md");
        Contains(script, "docs/operations/pilot-onboarding-support.md");

        DoesNotMatch(script, @"param\s*\([^)]*(WorkspaceId|Token|Secret|DbCredential|ObjectStoreCredential|ExportPath)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        DoesNotMatch(script, @"Get-Content\s+.*workspace", RegexOptions.IgnoreCase);
        DoesNotMatch(script, @"Invoke-WebRequest|Invoke-RestMethod|curl|wget", RegexOptions.IgnoreCase);
        DoesNotMatch(script, @"SqlConnection|Npgsql|AzureStorage|BlobClient", RegexOptions.IgnoreCase);
    }

    private static void Phase8WiringIsCurrent(string root)
    {
        string api = Read(root, "src/TradeProof.Api/Program.cs");
        Contains(api, "phase = \"phase-8\"");
        Contains(api, "version = \"phase-8\"");

        string runner = Read(root, "tests/TradeProof.App.Tests/TestProgram.cs");
        Contains(runner, "Phase8Tests.Run");
        Contains(runner, "phase8");

        string ci = Read(root, ".github/workflows/ci.yml");
        Contains(ci, "Test and verify Phase 8");
        Contains(ci, "tools/test-phase8.ps1");

        string changelog = Read(root, "CHANGELOG.md");
        if (!changelog.StartsWith("# Changelog\n\n## 2026-08-28 - Phase 8", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("CHANGELOG.md must keep the newest Phase 8 entry at the top.");
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "TradeProof.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate TradeProof.sln from test output directory.");
    }

    private static void RequireFile(string root, string relativePath)
    {
        if (!File.Exists(Path.Combine(root, relativePath)))
        {
            throw new InvalidOperationException($"Missing required file: {relativePath}");
        }
    }

    private static string Read(string root, string relativePath)
    {
        RequireFile(root, relativePath);
        return File.ReadAllText(Path.Combine(root, relativePath)).Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static void Contains(string content, string expected)
    {
        if (!content.Contains(expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Expected artifact content: {expected}");
        }
    }

    private static void DoesNotMatch(string content, string pattern, RegexOptions options)
    {
        if (Regex.IsMatch(content, pattern, options))
        {
            throw new InvalidOperationException($"Unexpected artifact pattern: {pattern}");
        }
    }
}
