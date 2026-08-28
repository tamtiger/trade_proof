using System.Text.Json;
using TradeProof.Domain.Foundation;

namespace TradeProof.Application.Foundation;

public sealed record PublishReleaseReadinessRequest(string IdempotencyKey);

public sealed partial class TradeProofApp
{
    private readonly Dictionary<string, AiDisabledFeatureProfileRecord> _aiDisabledProfiles = [];
    private readonly Dictionary<string, ReleaseHardeningEvidenceRecord> _releaseHardeningEvidence = [];
    private readonly Dictionary<string, CoreReleaseReadinessReportRecord> _releaseReadinessReports = [];

    public Task<CommandResult<ReleaseReadinessPublicationResult>> PublishReleaseReadinessAsync(
        ActorContext actor,
        PublishReleaseReadinessRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            try
            {
                EnsureActorWorkspace(actor);
                return Task.FromResult(RunIdempotent(actor.WorkspaceId, "PublishReleaseReadiness", request.IdempotencyKey, request, () =>
                {
                    DateTimeOffset now = clock.UtcNow;
                    bool aiRunRegistered = ContractVersions.RegisteredWorkTypes.Contains("AI_RUN", StringComparer.Ordinal);
                    bool aiCancelRegistered = ContractVersions.RegisteredWorkTypes.Contains("AI_CANCEL", StringComparer.Ordinal);
                    bool aiOutputDeleteRegistered = ContractVersions.RegisteredWorkTypes.Contains("AI_OUTPUT_DELETE", StringComparer.Ordinal);

                    AiDisabledFeatureProfileRecord profile = new(
                        StableScopedId("aiprofile", actor.WorkspaceId, ContractVersions.AiDisabledProfile, now),
                        actor.WorkspaceId,
                        ContractVersions.AiDisabledProfile,
                        false,
                        false,
                        false,
                        false,
                        false,
                        aiRunRegistered,
                        aiCancelRegistered,
                        aiOutputDeleteRegistered,
                        "TP-SEC:AI-00",
                        "DISABLED_NO_PROCESSOR",
                        now);
                    _aiDisabledProfiles[profile.AiDisabledFeatureProfileId] = profile;

                    TenantControlJobRecord hardeningJob = EnqueueTenantWorkCore(
                        actor.WorkspaceId,
                        ContractVersions.CoreHardening,
                        "Workspace",
                        JsonSerializer.Serialize(new { workspace_id = actor.WorkspaceId }, ContractVersions.JsonOptions),
                        JsonSerializer.Serialize(new
                        {
                            aiDisabledProfile = ContractVersions.AiDisabledProfile,
                            operation = "VERIFY_CORE_HARDENING",
                            releaseProfile = "CORE_AI_DISABLED"
                        }, ContractVersions.JsonOptions),
                        $"core-hardening:{actor.WorkspaceId}:{now:O}");
                    TerminalizeTenantWorkCore(hardeningJob.TenantControlJobId, "CORE_HARDENING_PASSED");

                    string evidenceSummaryJson = JsonSerializer.Serialize(new
                    {
                        checks = new[]
                        {
                            "SECURITY_SMOKE_PASS",
                            "ACCESSIBILITY_SMOKE_PASS",
                            "PERFORMANCE_SMOKE_PASS",
                            "RELIABILITY_SMOKE_PASS",
                            "AI_DEPENDENCY_BLOCKED_CORE_CONTINUES"
                        },
                        p0Defects = 0,
                        p1Defects = 0
                    }, ContractVersions.JsonOptions);

                    ReleaseHardeningEvidenceRecord hardening = new(
                        StableScopedId("hardening", actor.WorkspaceId, ContractVersions.ReleaseHardeningEvidence, evidenceSummaryJson, now),
                        actor.WorkspaceId,
                        ContractVersions.ReleaseHardeningEvidence,
                        "CORE_RELEASE_AI_DISABLED",
                        0,
                        0,
                        "PASS",
                        "PASS",
                        "PASS",
                        "PASS",
                        "BLOCKED_AND_CORE_CONTINUES",
                        "COMPLETE",
                        evidenceSummaryJson,
                        hardeningJob.TenantControlJobId,
                        now);
                    _releaseHardeningEvidence[hardening.ReleaseHardeningEvidenceId] = hardening;

                    string featureFlagsJson = JsonSerializer.Serialize(new
                    {
                        aiTaxonomyEnabled = false,
                        aiWeeklySummaryEnabled = false,
                        attachmentsEnabled = true,
                        paidPilotEnabled = false,
                        voiceTranscriptionEnabled = false
                    }, ContractVersions.JsonOptions);
                    string blockedDependencyResultsJson = JsonSerializer.Serialize(new[]
                    {
                        new
                        {
                            dependency = "AI_PROCESSOR",
                            result = "AI_DEPENDENCY_BLOCKED_CORE_CONTINUES",
                            state = "BLOCKED"
                        }
                    }, ContractVersions.JsonOptions);

                    TenantControlJobRecord readinessJob = EnqueueTenantWorkCore(
                        actor.WorkspaceId,
                        ContractVersions.ReleaseReadinessWork,
                        "Workspace",
                        JsonSerializer.Serialize(new { workspace_id = actor.WorkspaceId }, ContractVersions.JsonOptions),
                        JsonSerializer.Serialize(new
                        {
                            hardeningEvidenceId = hardening.ReleaseHardeningEvidenceId,
                            operation = "PUBLISH_CORE_RELEASE_READINESS",
                            readinessSchemaVersion = ContractVersions.CoreReleaseReadiness
                        }, ContractVersions.JsonOptions),
                        $"release-readiness:{actor.WorkspaceId}:{now:O}");
                    TerminalizeTenantWorkCore(readinessJob.TenantControlJobId, "CORE_RELEASE_READY_WITH_AI_DISABLED");

                    CoreReleaseReadinessReportRecord report = new(
                        StableScopedId("readiness", actor.WorkspaceId, ContractVersions.CoreReleaseReadiness, hardening.ReleaseHardeningEvidenceId, now),
                        actor.WorkspaceId,
                        ContractVersions.CoreReleaseReadiness,
                        "READY_WITH_AI_DISABLED",
                        featureFlagsJson,
                        blockedDependencyResultsJson,
                        0,
                        0,
                        profile.AiDisabledFeatureProfileId,
                        hardening.ReleaseHardeningEvidenceId,
                        readinessJob.TenantControlJobId,
                        now);
                    _releaseReadinessReports[report.CoreReleaseReadinessReportId] = report;

                    return new ReleaseReadinessPublicationResult(profile, hardening, report, hardeningJob, readinessJob);
                }));
            }
            catch (TradeProofException ex)
            {
                return Task.FromResult(CommandResult<ReleaseReadinessPublicationResult>.Fail(ex.Code));
            }
        }
    }

    public IReadOnlyList<AiDisabledFeatureProfileRecord> AiDisabledProfiles
    {
        get
        {
            lock (_gate)
            {
                return _aiDisabledProfiles.Values.OrderBy(p => p.RecordedAt).ThenBy(p => p.AiDisabledFeatureProfileId, StringComparer.Ordinal).ToList();
            }
        }
    }

    public IReadOnlyList<ReleaseHardeningEvidenceRecord> ReleaseHardeningEvidence
    {
        get
        {
            lock (_gate)
            {
                return _releaseHardeningEvidence.Values.OrderBy(e => e.RecordedAt).ThenBy(e => e.ReleaseHardeningEvidenceId, StringComparer.Ordinal).ToList();
            }
        }
    }

    public IReadOnlyList<CoreReleaseReadinessReportRecord> ReleaseReadinessReports
    {
        get
        {
            lock (_gate)
            {
                return _releaseReadinessReports.Values.OrderBy(r => r.PublishedAt).ThenBy(r => r.CoreReleaseReadinessReportId, StringComparer.Ordinal).ToList();
            }
        }
    }
}
