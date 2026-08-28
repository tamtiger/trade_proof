using TradeProof.Application.Foundation;
using TradeProof.Domain.Foundation;

namespace TradeProof.App.Tests;

public static class Phase7Tests
{
    private static readonly DateTimeOffset ClockStart = new(2026, 8, 28, 4, 0, 0, TimeSpan.Zero);

    public static async Task Run()
    {
        await CoreReadinessKeepsAiDisabledAndPublishesHardeningEvidence();
    }

    private static async Task CoreReadinessKeepsAiDisabledAndPublishesHardeningEvidence()
    {
        FixedTradeProofClock clock = new(ClockStart);
        TradeProofApp app = new(clock);
        ManagedIdentity identity = new("https://dev.identity.tradeproof.local/tenant", $"phase7-{Guid.NewGuid():N}", "Local Binance Spot");
        BootstrapResponse bootstrap = Ok(await app.BootstrapAsync(identity), "bootstrap");
        ActorContext actor = app.ActorFromBootstrap(bootstrap, identity);

        ReleaseReadinessPublicationResult publication = Ok(await app.PublishReleaseReadinessAsync(actor, new PublishReleaseReadinessRequest(
            "phase7-readiness")), "publish readiness");

        Equal("ai_disabled_profile_v1", publication.AiProfile.SchemaVersion, "ai profile schema");
        Require(publication.AiProfile.AllFeaturesDisabled, "all AI feature flags stay disabled");
        Require(!publication.AiProfile.AiProcessorConfigured, "no processor configured");
        Require(!publication.AiProfile.OutboundAiRouteConfigured, "no outbound AI route configured");
        Require(!publication.AiProfile.AiRunWorkRegistered, "AI_RUN work type not registered");
        Require(!publication.AiProfile.AiCancelWorkRegistered, "AI_CANCEL work type not registered");
        Require(!publication.AiProfile.AiOutputDeleteWorkRegistered, "AI_OUTPUT_DELETE work type not registered");
        Equal("TP-SEC:AI-00", publication.AiProfile.DisabledGateId, "disabled gate");

        Equal("release_hardening_evidence_v1", publication.HardeningEvidence.SchemaVersion, "hardening schema");
        Equal(0, publication.HardeningEvidence.P0DefectCount, "P0 defects");
        Equal(0, publication.HardeningEvidence.P1DefectCount, "P1 defects");
        Equal("PASS", publication.HardeningEvidence.SecuritySmokeState, "security smoke");
        Equal("PASS", publication.HardeningEvidence.AccessibilitySmokeState, "accessibility smoke");
        Equal("PASS", publication.HardeningEvidence.PerformanceSmokeState, "performance smoke");
        Equal("PASS", publication.HardeningEvidence.ReliabilitySmokeState, "reliability smoke");
        Equal("BLOCKED_AND_CORE_CONTINUES", publication.HardeningEvidence.AiDependencyState, "AI dependency state");
        Require(publication.HardeningEvidence.EvidenceSummaryJson.Contains("AI_DEPENDENCY_BLOCKED_CORE_CONTINUES", StringComparison.Ordinal), "AI outage evidence recorded");

        Equal("core_release_readiness_v1", publication.Report.SchemaVersion, "readiness schema");
        Equal("READY_WITH_AI_DISABLED", publication.Report.State, "readiness state");
        Equal(publication.AiProfile.AiDisabledFeatureProfileId, publication.Report.AiDisabledFeatureProfileId, "profile link");
        Equal(publication.HardeningEvidence.ReleaseHardeningEvidenceId, publication.Report.ReleaseHardeningEvidenceId, "hardening link");
        Require(publication.Report.FeatureFlagsJson.Contains("\"voiceTranscriptionEnabled\":false", StringComparison.Ordinal), "voice flag false");
        Require(publication.Report.FeatureFlagsJson.Contains("\"aiTaxonomyEnabled\":false", StringComparison.Ordinal), "taxonomy flag false");
        Require(publication.Report.FeatureFlagsJson.Contains("\"aiWeeklySummaryEnabled\":false", StringComparison.Ordinal), "summary flag false");

        ReleaseReadinessPublicationResult replay = Ok(await app.PublishReleaseReadinessAsync(actor, new PublishReleaseReadinessRequest(
            "phase7-readiness")), "publish readiness replay");
        Equal(publication.Report.CoreReleaseReadinessReportId, replay.Report.CoreReleaseReadinessReportId, "readiness publish is idempotent");

        DashboardResponse dashboard = await app.GetDashboardAsync(actor);
        Equal(publication.AiProfile.AiDisabledFeatureProfileId, dashboard.AiDisabledProfiles.Single().AiDisabledFeatureProfileId, "dashboard profile");
        Equal(publication.HardeningEvidence.ReleaseHardeningEvidenceId, dashboard.ReleaseHardeningEvidence.Single().ReleaseHardeningEvidenceId, "dashboard hardening");
        Equal(publication.Report.CoreReleaseReadinessReportId, dashboard.ReleaseReadinessReports.Single().CoreReleaseReadinessReportId, "dashboard readiness");

        Require(!ContractVersions.RegisteredWorkTypes.Contains("AI_RUN", StringComparer.Ordinal), "AI_RUN not registered");
        Require(!ContractVersions.RegisteredWorkTypes.Contains("AI_CANCEL", StringComparer.Ordinal), "AI_CANCEL not registered");
        Require(!ContractVersions.RegisteredWorkTypes.Contains("AI_OUTPUT_DELETE", StringComparer.Ordinal), "AI_OUTPUT_DELETE not registered");
    }

    private static T Ok<T>(CommandResult<T> result, string label)
    {
        if (!result.Succeeded || result.Value is null)
        {
            throw new InvalidOperationException($"{label} expected success but got {result.ErrorCode ?? "null value"}.");
        }

        return result.Value;
    }

    private static void Require(bool condition, string label)
    {
        if (!condition)
        {
            throw new InvalidOperationException(label);
        }
    }

    private static void Equal<T>(T expected, T actual, string label)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
        }
    }
}
