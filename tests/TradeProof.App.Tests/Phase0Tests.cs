namespace TradeProof.App.Tests;

public static class Phase0Tests
{
    public static void Run()
    {
        Phase0ArtifactsCoverRequiredAdrSet();
        FixtureInventoryDoesNotPretendRealSamplesExist();
    }

    private static void Phase0ArtifactsCoverRequiredAdrSet()
    {
        string root = FindRepositoryRoot();
        string adrRoot = Path.Combine(root, "docs", "adr");
        string[] required =
        [
            "0001-runtime-and-frontend.md",
            "0002-managed-identity.md",
            "0003-relational-database-and-tenant-enforcement.md",
            "0004-queue-worker-and-idempotency.md",
            "0005-object-storage-and-malware-scanner.md",
            "0006-market-data-cache.md",
            "0007-ai-processor.md",
            "0008-deployment-region-backup-and-disclosure.md",
            "0009-observability-and-redaction.md",
            "0010-binance-market-data-terms.md"
        ];

        foreach (string file in required)
        {
            string path = Path.Combine(adrRoot, file);
            if (!File.Exists(path))
            {
                throw new InvalidOperationException($"Missing ADR file {file}.");
            }

            string text = File.ReadAllText(path);
            RequireContains(text, "## Context", file);
            RequireContains(text, "## Decision", file);
            RequireContains(text, "## Alternatives", file);
            RequireContains(text, "## Security/privacy impact", file);
            RequireContains(text, "## Rollback", file);
        }
    }

    private static void FixtureInventoryDoesNotPretendRealSamplesExist()
    {
        string root = FindRepositoryRoot();
        string fixtureReadme = File.ReadAllText(Path.Combine(root, "fixtures", "README.md"));
        string intake = File.ReadAllText(Path.Combine(root, "docs", "operations", "fixture-intake.md"));

        RequireContains(fixtureReadme, "0/5", "fixtures/README.md");
        RequireContains(intake, "0/5", "docs/operations/fixture-intake.md");
        RequireContains(fixtureReadme, "Synthetic fixtures", "fixtures/README.md");
    }

    private static void RequireContains(string text, string expected, string label)
    {
        if (!text.Contains(expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{label} missing expected text: {expected}");
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? cursor = new(AppContext.BaseDirectory);
        while (cursor is not null)
        {
            if (File.Exists(Path.Combine(cursor.FullName, "README.md")) &&
                Directory.Exists(Path.Combine(cursor.FullName, "docs")))
            {
                return cursor.FullName;
            }

            cursor = cursor.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
