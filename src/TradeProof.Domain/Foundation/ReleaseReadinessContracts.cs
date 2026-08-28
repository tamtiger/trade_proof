namespace TradeProof.Domain.Foundation;

public static partial class ContractVersions
{
    public const string AiDisabledProfile = "ai_disabled_profile_v1";
    public const string ReleaseHardeningEvidence = "release_hardening_evidence_v1";
    public const string CoreReleaseReadiness = "core_release_readiness_v1";

    public const string CoreHardening = "CORE_HARDENING";
    public const string ReleaseReadinessWork = "RELEASE_READINESS";
}

public sealed record AiDisabledFeatureProfileRecord(
    string AiDisabledFeatureProfileId,
    string WorkspaceId,
    string SchemaVersion,
    bool VoiceTranscriptionEnabled,
    bool AiTaxonomyEnabled,
    bool AiWeeklySummaryEnabled,
    bool AiProcessorConfigured,
    bool OutboundAiRouteConfigured,
    bool AiRunWorkRegistered,
    bool AiCancelWorkRegistered,
    bool AiOutputDeleteWorkRegistered,
    string DisabledGateId,
    string ProcessorState,
    DateTimeOffset RecordedAt)
{
    public bool AllFeaturesDisabled =>
        !VoiceTranscriptionEnabled &&
        !AiTaxonomyEnabled &&
        !AiWeeklySummaryEnabled;
}

public sealed record ReleaseHardeningEvidenceRecord(
    string ReleaseHardeningEvidenceId,
    string WorkspaceId,
    string SchemaVersion,
    string GateProfile,
    int P0DefectCount,
    int P1DefectCount,
    string SecuritySmokeState,
    string AccessibilitySmokeState,
    string PerformanceSmokeState,
    string ReliabilitySmokeState,
    string AiDependencyState,
    string CoreFlowState,
    string EvidenceSummaryJson,
    string CoreHardeningTenantControlJobId,
    DateTimeOffset RecordedAt);

public sealed record CoreReleaseReadinessReportRecord(
    string CoreReleaseReadinessReportId,
    string WorkspaceId,
    string SchemaVersion,
    string State,
    string FeatureFlagsJson,
    string BlockedDependencyResultsJson,
    int P0DefectCount,
    int P1DefectCount,
    string AiDisabledFeatureProfileId,
    string ReleaseHardeningEvidenceId,
    string ReleaseReadinessTenantControlJobId,
    DateTimeOffset PublishedAt);

public sealed record ReleaseReadinessPublicationResult(
    AiDisabledFeatureProfileRecord AiProfile,
    ReleaseHardeningEvidenceRecord HardeningEvidence,
    CoreReleaseReadinessReportRecord Report,
    TenantControlJobRecord CoreHardeningJob,
    TenantControlJobRecord ReleaseReadinessJob);
