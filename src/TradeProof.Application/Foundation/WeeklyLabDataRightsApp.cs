using System.Globalization;
using System.Text.Json;
using TradeProof.Domain.Foundation;

namespace TradeProof.Application.Foundation;

public sealed partial class TradeProofApp
{
    private readonly Dictionary<string, WeeklyCohortRecord> _weeklyCohorts = [];
    private readonly Dictionary<string, WeeklyCohortInputRevisionRecord> _weeklyCohortInputRevisions = [];
    private readonly Dictionary<string, WeeklyReportRevisionRecord> _weeklyReportRevisions = [];
    private readonly Dictionary<string, List<BehavioralExperimentRevisionRecord>> _behavioralExperimentRevisions = [];
    private readonly List<WeeklyReviewCompletionRecord> _weeklyReviewCompletions = [];
    private readonly List<WorkspaceProductMetricSnapshotRecord> _workspaceProductMetricSnapshots = [];
    private readonly List<InternalAggregateProductMetricSnapshotRecord> _internalAggregateProductMetricSnapshots = [];
    private readonly Dictionary<string, ExternalAnalyticsProjectionRecord> _externalAnalyticsProjections = [];
    private readonly List<ExternalAnalyticsPurgeRecord> _externalAnalyticsPurges = [];
    private readonly Dictionary<string, TradeProofExportRecord> _tradeProofExports = [];
    private readonly List<ExportRoundTripValidationRecord> _exportRoundTripValidations = [];
    private readonly List<ExportExpiryRecord> _exportExpiries = [];
    private readonly Dictionary<string, WorkspaceDeletionRecord> _workspaceDeletions = [];
    private readonly Dictionary<string, List<WorkspaceDeletionTargetRecord>> _workspaceDeletionTargets = [];
    private readonly List<WorkspaceDeletionTombstoneRecord> _workspaceDeletionTombstones = [];

    public Task<CommandResult<WeeklyLabPublicationResult>> PublishWeeklyLabAsync(
        ActorContext actor,
        PublishWeeklyLabRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            try
            {
                EnsureActorWorkspace(actor);
                return Task.FromResult(RunIdempotent(actor.WorkspaceId, "PublishWeeklyLab", request.IdempotencyKey, request, () =>
                {
                    WeeklyInterval interval = ValidateWeeklyInterval(actor.WorkspaceId, request.ReportingStartAtUtc, request.ReportingEndAtUtc);
                    DateTimeOffset asOfAt = Now;
                    List<TradeEpisodeProjectionRecord> episodes = ActiveEpisodeProjections()
                        .Where(p => p.WorkspaceId == actor.WorkspaceId &&
                                    p.State == "CLOSED" &&
                                    p.ClosedAt is not null &&
                                    p.ClosedAt.Value >= interval.StartUtc &&
                                    p.ClosedAt.Value < interval.EndUtc)
                        .OrderBy(p => p.ClosedAt)
                        .ThenBy(p => p.EpisodeId, StringComparer.Ordinal)
                        .ThenBy(p => p.ProjectionVersion)
                        .ToList();
                    List<ReviewRevisionRecord> reviewRevisions = episodes
                        .Select(CurrentReviewRevision)
                        .OfType<ReviewRevisionRecord>()
                        .OrderBy(r => r.ReviewRevisionId, StringComparer.Ordinal)
                        .ToList();
                    List<ContextSnapshotRecord> contextSnapshots = ContextSnapshots
                        .Where(c => c.WorkspaceId == actor.WorkspaceId &&
                                    episodes.Any(p => p.EpisodeId == c.EpisodeId && p.ProjectionVersion == c.ProjectionVersion))
                        .OrderBy(c => c.EpisodeId, StringComparer.Ordinal)
                        .ThenBy(c => c.Phase, StringComparer.Ordinal)
                        .ThenBy(c => c.Timeframe, StringComparer.Ordinal)
                        .ThenBy(c => c.SnapshotRevisionNo)
                        .ToList();
                    List<MetricSnapshotRecord> metricSnapshots = MetricSnapshots
                        .Where(m => m.WorkspaceId == actor.WorkspaceId &&
                                    m.ReportingStartAtUtc == interval.StartUtc &&
                                    m.ReportingEndAtUtc == interval.EndUtc)
                        .OrderBy(m => m.MetricId, StringComparer.Ordinal)
                        .ThenBy(m => m.MetricSnapshotId, StringComparer.Ordinal)
                        .ToList();
                    if (metricSnapshots.Count == 0)
                    {
                        throw new TradeProofException("WEEKLY_LAB_METRICS_REQUIRED");
                    }

                    string cohortId = StableScopedId("weeklycohort", actor.WorkspaceId, interval.StartUtc, interval.EndUtc);
                    string nextCohortId = StableScopedId("weeklycohort", actor.WorkspaceId, interval.StartUtc.AddDays(7), interval.EndUtc.AddDays(7));
                    WeeklyCohortRecord cohort = new(
                        cohortId,
                        actor.WorkspaceId,
                        ContractVersions.WeeklyLab,
                        "REGULAR",
                        "LOCKED",
                        interval.Timezone,
                        interval.LocalStartText,
                        interval.LocalEndText,
                        interval.StartUtc,
                        interval.EndUtc,
                        asOfAt,
                        null);
                    WeeklyCohortRecord nextCohort = new(
                        nextCohortId,
                        actor.WorkspaceId,
                        ContractVersions.WeeklyLab,
                        "REGULAR",
                        "OPEN",
                        interval.Timezone,
                        FormatLocal(interval.LocalStart.AddDays(7)),
                        FormatLocal(interval.LocalEnd.AddDays(7)),
                        interval.StartUtc.AddDays(7),
                        interval.EndUtc.AddDays(7),
                        asOfAt,
                        cohortId);
                    _weeklyCohorts[cohortId] = cohort;
                    _weeklyCohorts[nextCohortId] = nextCohort;

                    string dependencyTupleJson = JsonSerializer.Serialize(new
                    {
                        accounting = ContractVersions.WeightedAverageEpisode,
                        behavioralExperiment = ContractVersions.BehavioralExperiment,
                        context = ContractVersions.ContextAlgorithm,
                        exportProjection = ContractVersions.WeeklyLabExportProjection,
                        metric = ContractVersions.MetricsAlgorithm,
                        renderer = ContractVersions.WeeklyLabRenderer,
                        review = ContractVersions.ReviewRevision,
                        weeklyLab = ContractVersions.WeeklyLab
                    }, ContractVersions.JsonOptions);
                    string dependencyTupleHash = ContractVersions.Sha256CanonicalJson(JsonSerializer.Deserialize<JsonElement>(dependencyTupleJson));
                    string episodeRefsJson = JsonSerializer.Serialize(episodes.Select(p => new
                    {
                        episode_id = p.EpisodeId,
                        projection_version = p.ProjectionVersion
                    }), ContractVersions.JsonOptions);
                    string reviewRefsJson = JsonSerializer.Serialize(reviewRevisions.Select(r => new
                    {
                        review_revision_id = r.ReviewRevisionId,
                        review_id = r.ReviewId,
                        revision_no = r.RevisionNo
                    }), ContractVersions.JsonOptions);
                    string contextRefsJson = JsonSerializer.Serialize(contextSnapshots.Select(c => new
                    {
                        context_snapshot_id = c.SnapshotId,
                        episode_id = c.EpisodeId,
                        phase = c.Phase,
                        projection_version = c.ProjectionVersion,
                        timeframe = c.Timeframe
                    }), ContractVersions.JsonOptions);
                    string metricRefsJson = JsonSerializer.Serialize(metricSnapshots.Select(m => new
                    {
                        metric_id = m.MetricId,
                        metric_snapshot_id = m.MetricSnapshotId
                    }), ContractVersions.JsonOptions);
                    string inputDigest = ContractVersions.Sha256CanonicalJson(new
                    {
                        dependencyTupleHash,
                        episodeRefsJson,
                        metricRefsJson,
                        reviewRefsJson,
                        contextRefsJson,
                        asOfAt,
                        cohortId
                    });
                    string inputRevisionId = StableScopedId("weeklyinput", actor.WorkspaceId, cohortId, inputDigest);
                    WeeklyCohortInputRevisionRecord inputRevision = new(
                        inputRevisionId,
                        cohortId,
                        actor.WorkspaceId,
                        1,
                        ContractVersions.WeeklyLab,
                        "INITIAL_LOCK",
                        request.IdempotencyKey,
                        asOfAt,
                        dependencyTupleJson,
                        dependencyTupleHash,
                        episodeRefsJson,
                        reviewRefsJson,
                        contextRefsJson,
                        metricRefsJson,
                        inputDigest);
                    _weeklyCohortInputRevisions[inputRevisionId] = inputRevision;

                    string weeklyReportId = StableScopedId("weeklyreport", actor.WorkspaceId, cohortId);
                    string metricSnapshotIdsJson = JsonSerializer.Serialize(metricSnapshots.Select(m => m.MetricSnapshotId), ContractVersions.JsonOptions);
                    string sectionPayloadJson = JsonSerializer.Serialize(new
                    {
                        cohortId,
                        cohortInputRevisionId = inputRevisionId,
                        metrics = metricSnapshots.Select(m => new
                        {
                            metric_id = m.MetricId,
                            metric_snapshot_id = m.MetricSnapshotId,
                            value_decimal = m.ValueDecimal,
                            value_integer = m.ValueInteger,
                            value_object_json = m.ValueObjectJson
                        }),
                        reportingAsOfAt = asOfAt,
                        schemaVersion = ContractVersions.WeeklyLab
                    }, ContractVersions.JsonOptions);
                    string renderedSectionsJson = JsonSerializer.Serialize(new
                    {
                        renderer = ContractVersions.WeeklyLabRenderer,
                        sections = metricSnapshots.Select(m => new
                        {
                            metric_id = m.MetricId,
                            metric_snapshot_id = m.MetricSnapshotId
                        })
                    }, ContractVersions.JsonOptions);
                    string reportHash = ContractVersions.Sha256CanonicalJson(new
                    {
                        cohortId,
                        dependencyTupleHash,
                        inputDigest,
                        metricSnapshotIdsJson,
                        sectionPayloadJson,
                        weeklyReportId
                    });
                    WeeklyReportRevisionRecord reportRevision = new(
                        StableScopedId("weeklyreportrev", actor.WorkspaceId, weeklyReportId, reportHash),
                        weeklyReportId,
                        actor.WorkspaceId,
                        cohortId,
                        inputRevisionId,
                        1,
                        "PUBLISHED",
                        ContractVersions.WeeklyLab,
                        ContractVersions.WeeklyLabRenderer,
                        "vi-VN",
                        metricSnapshotIdsJson,
                        sectionPayloadJson,
                        renderedSectionsJson,
                        reportHash,
                        asOfAt,
                        null,
                        nextCohortId);
                    _weeklyReportRevisions[reportRevision.WeeklyReportRevisionId] = reportRevision;

                    TenantControlJobRecord cohortJob = EnqueueTenantWorkCore(
                        actor.WorkspaceId,
                        ContractVersions.CohortLock,
                        "WeeklyCohort",
                        JsonSerializer.Serialize(new { weekly_cohort_id = cohortId }, ContractVersions.JsonOptions),
                        JsonSerializer.Serialize(new { weeklyLabSchemaVersion = ContractVersions.WeeklyLab }, ContractVersions.JsonOptions),
                        $"weekly-cohort-lock:{cohortId}");
                    TerminalizeTenantWorkCore(cohortJob.TenantControlJobId, "WEEKLY_COHORT_LOCKED");
                    TenantControlJobRecord reportJob = EnqueueTenantWorkCore(
                        actor.WorkspaceId,
                        ContractVersions.Report,
                        "WeeklyReportRevision",
                        JsonSerializer.Serialize(new { weekly_report_revision_id = reportRevision.WeeklyReportRevisionId }, ContractVersions.JsonOptions),
                        JsonSerializer.Serialize(new { renderer = ContractVersions.WeeklyLabRenderer }, ContractVersions.JsonOptions),
                        $"weekly-report:{reportRevision.WeeklyReportRevisionId}");
                    TerminalizeTenantWorkCore(reportJob.TenantControlJobId, "WEEKLY_REPORT_PUBLISHED");
                    return new WeeklyLabPublicationResult(cohort, inputRevision, reportRevision, nextCohort);
                }));
            }
            catch (TradeProofException ex)
            {
                return Task.FromResult(CommandResult<WeeklyLabPublicationResult>.Fail(ex.Code));
            }
        }
    }

    public Task<CommandResult<BehavioralExperimentRevisionRecord>> ProposeBehavioralExperimentAsync(
        ActorContext actor,
        ProposeBehavioralExperimentRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            try
            {
                EnsureActorWorkspace(actor);
                return Task.FromResult(RunIdempotent(actor.WorkspaceId, "ProposeBehavioralExperiment", request.IdempotencyKey, request, () =>
                {
                    WeeklyReportRevisionRecord report = GetOwnedWeeklyReport(actor, request.WeeklyReportRevisionId);
                    string experimentType = NormalizeExperimentType(request.ExperimentTypeId);
                    string proposalText = NormalizeShortText(request.ProposalText, "BEHAVIORAL_EXPERIMENT_TEXT_INVALID", 280);
                    if (CurrentBehavioralExperiments(actor.WorkspaceId).Any(e => e.TargetWeeklyCohortId == report.NextWeeklyCohortId && e.State == "CONFIRMED"))
                    {
                        throw new TradeProofException("BEHAVIORAL_EXPERIMENT_TARGET_CONFLICT");
                    }

                    string experimentId = NextId("bexp");
                    BehavioralExperimentRevisionRecord revision = BuildBehavioralExperimentRevision(
                        actor,
                        experimentId,
                        1,
                        experimentType,
                        "PROPOSED",
                        report.NextWeeklyCohortId,
                        report.WeeklyReportRevisionId,
                        proposalText,
                        request.IdempotencyKey);
                    _behavioralExperimentRevisions[experimentId] = [revision];
                    return revision;
                }));
            }
            catch (TradeProofException ex)
            {
                return Task.FromResult(CommandResult<BehavioralExperimentRevisionRecord>.Fail(ex.Code));
            }
        }
    }

    public Task<CommandResult<BehavioralExperimentRevisionRecord>> ConfirmBehavioralExperimentAsync(
        ActorContext actor,
        ConfirmBehavioralExperimentRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            try
            {
                EnsureActorWorkspace(actor);
                return Task.FromResult(RunIdempotent(actor.WorkspaceId, "ConfirmBehavioralExperiment", request.IdempotencyKey, request, () =>
                {
                    BehavioralExperimentRevisionRecord current = GetOwnedBehavioralExperiment(actor, request.BehavioralExperimentId);
                    if (current.RevisionNo != request.ExpectedRevisionNo)
                    {
                        throw new TradeProofException("STALE_BEHAVIORAL_EXPERIMENT_REVISION");
                    }

                    if (current.State != "PROPOSED")
                    {
                        throw new TradeProofException("BEHAVIORAL_EXPERIMENT_NOT_PROPOSED");
                    }

                    if (CurrentBehavioralExperiments(actor.WorkspaceId).Any(e =>
                            e.BehavioralExperimentId != current.BehavioralExperimentId &&
                            e.TargetWeeklyCohortId == current.TargetWeeklyCohortId &&
                            e.State == "CONFIRMED"))
                    {
                        throw new TradeProofException("BEHAVIORAL_EXPERIMENT_TARGET_CONFLICT");
                    }

                    BehavioralExperimentRevisionRecord revision = BuildBehavioralExperimentRevision(
                        actor,
                        current.BehavioralExperimentId,
                        current.RevisionNo + 1,
                        current.ExperimentTypeId,
                        "CONFIRMED",
                        current.TargetWeeklyCohortId,
                        current.SourceWeeklyReportRevisionId,
                        current.ProposalText,
                        request.IdempotencyKey);
                    _behavioralExperimentRevisions[current.BehavioralExperimentId].Add(revision);
                    return revision;
                }));
            }
            catch (TradeProofException ex)
            {
                return Task.FromResult(CommandResult<BehavioralExperimentRevisionRecord>.Fail(ex.Code));
            }
        }
    }

    public Task<CommandResult<BehavioralExperimentRevisionRecord>> CancelBehavioralExperimentAsync(
        ActorContext actor,
        CancelBehavioralExperimentRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            try
            {
                EnsureActorWorkspace(actor);
                return Task.FromResult(RunIdempotent(actor.WorkspaceId, "CancelBehavioralExperiment", request.IdempotencyKey, request, () =>
                {
                    BehavioralExperimentRevisionRecord current = GetOwnedBehavioralExperiment(actor, request.BehavioralExperimentId);
                    if (current.RevisionNo != request.ExpectedRevisionNo)
                    {
                        throw new TradeProofException("STALE_BEHAVIORAL_EXPERIMENT_REVISION");
                    }

                    if (current.State is not ("PROPOSED" or "CONFIRMED"))
                    {
                        throw new TradeProofException("BEHAVIORAL_EXPERIMENT_NOT_CANCELLABLE");
                    }

                    string reason = NormalizeShortText(request.Reason, "BEHAVIORAL_EXPERIMENT_CANCEL_REASON_INVALID", 120);
                    BehavioralExperimentRevisionRecord revision = BuildBehavioralExperimentRevision(
                        actor,
                        current.BehavioralExperimentId,
                        current.RevisionNo + 1,
                        current.ExperimentTypeId,
                        "CANCELLED",
                        current.TargetWeeklyCohortId,
                        current.SourceWeeklyReportRevisionId,
                        $"{current.ProposalText} Cancel reason: {reason}",
                        request.IdempotencyKey);
                    _behavioralExperimentRevisions[current.BehavioralExperimentId].Add(revision);
                    return revision;
                }));
            }
            catch (TradeProofException ex)
            {
                return Task.FromResult(CommandResult<BehavioralExperimentRevisionRecord>.Fail(ex.Code));
            }
        }
    }

    public Task<CommandResult<WeeklyReviewCompletionRecord>> CompleteWeeklyReviewAsync(
        ActorContext actor,
        CompleteWeeklyReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            try
            {
                EnsureActorWorkspace(actor);
                return Task.FromResult(RunIdempotent(actor.WorkspaceId, "CompleteWeeklyReview", request.IdempotencyKey, request, () =>
                {
                    if (!_weeklyCohorts.TryGetValue(request.WeeklyCohortId, out WeeklyCohortRecord? cohort) || cohort.WorkspaceId != actor.WorkspaceId)
                    {
                        throw new TradeProofException("WEEKLY_COHORT_NOT_FOUND");
                    }

                    WeeklyReportRevisionRecord report = GetOwnedWeeklyReport(actor, request.WeeklyReportRevisionId);
                    if (report.WeeklyCohortId != cohort.WeeklyCohortId)
                    {
                        throw new TradeProofException("WEEKLY_COMPLETION_REPORT_MISMATCH");
                    }

                    BehavioralExperimentRevisionRecord experiment = CurrentBehavioralExperiments(actor.WorkspaceId)
                        .SingleOrDefault(e => e.BehavioralExperimentRevisionId == request.BehavioralExperimentRevisionId)
                        ?? throw new TradeProofException("BEHAVIORAL_EXPERIMENT_REVISION_NOT_FOUND");
                    if (experiment.State != "CONFIRMED")
                    {
                        throw new TradeProofException("BEHAVIORAL_EXPERIMENT_NOT_CONFIRMED");
                    }

                    string contentHash = ContractVersions.Sha256CanonicalJson(new
                    {
                        experimentRevisionId = experiment.BehavioralExperimentRevisionId,
                        reportRevisionId = report.WeeklyReportRevisionId,
                        schemaVersion = ContractVersions.WeeklyReviewCompletion,
                        weeklyCohortId = cohort.WeeklyCohortId
                    });
                    WeeklyReviewCompletionRecord completion = new(
                        StableScopedId("weeklycomplete", actor.WorkspaceId, cohort.WeeklyCohortId, report.WeeklyReportRevisionId, experiment.BehavioralExperimentRevisionId),
                        actor.WorkspaceId,
                        ContractVersions.WeeklyReviewCompletion,
                        cohort.WeeklyCohortId,
                        report.WeeklyReportRevisionId,
                        experiment.BehavioralExperimentRevisionId,
                        Now,
                        request.IdempotencyKey,
                        contentHash);
                    _weeklyReviewCompletions.Add(completion);
                    return completion;
                }));
            }
            catch (TradeProofException ex)
            {
                return Task.FromResult(CommandResult<WeeklyReviewCompletionRecord>.Fail(ex.Code));
            }
        }
    }

    public Task<CommandResult<ProductAnalyticsEventRecord>> RecordProductAnalyticsEventAsync(
        ActorContext actor,
        RecordProductAnalyticsEventRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            try
            {
                EnsureActorWorkspace(actor);
                return Task.FromResult(RunIdempotent(actor.WorkspaceId, "RecordProductAnalyticsEvent", request.IdempotencyKey, request, () =>
                {
                    string eventType = NormalizeAnalyticsEvent(request.EventType);
                    string sourceType = NormalizeAnalyticsSourceType(request.SourceRecordType);
                    DateTimeOffset occurredAt = request.OccurredAt.ToUniversalTime();
                    string sourceHash = ContractVersions.Sha256CanonicalJson(new
                    {
                        sourceId = request.SourceRecordId,
                        sourceType,
                        workspaceId = actor.WorkspaceId
                    });
                    string sourceRecordKeyJson = JsonSerializer.Serialize(new
                    {
                        source_record_hash = sourceHash,
                        source_record_type = sourceType
                    }, ContractVersions.JsonOptions);
                    string payloadJson = JsonSerializer.Serialize(new
                    {
                        event_type = eventType,
                        occurred_day_utc = occurredAt.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        source_record_type = sourceType
                    }, ContractVersions.JsonOptions);
                    ProductAnalyticsEventRecord record = new(
                        NextId("ana"),
                        actor.WorkspaceId,
                        ContractVersions.ProductAnalyticsEvent,
                        eventType,
                        sourceRecordKeyJson,
                        payloadJson,
                        occurredAt);
                    _analyticsEvents.Add(record);
                    return record;
                }));
            }
            catch (TradeProofException ex)
            {
                return Task.FromResult(CommandResult<ProductAnalyticsEventRecord>.Fail(ex.Code));
            }
        }
    }

    public Task<CommandResult<IReadOnlyList<WorkspaceProductMetricSnapshotRecord>>> PublishWorkspaceProductMetricsAsync(
        ActorContext actor,
        PublishWorkspaceProductMetricsRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            try
            {
                EnsureActorWorkspace(actor);
                return Task.FromResult(RunIdempotent(actor.WorkspaceId, "PublishWorkspaceProductMetrics", request.IdempotencyKey, request, () =>
                {
                    DateTimeOffset start = request.ReportingStartAtUtc.ToUniversalTime();
                    DateTimeOffset end = request.ReportingEndAtUtc.ToUniversalTime();
                    if (start >= end)
                    {
                        throw new TradeProofException("PRODUCT_METRIC_INTERVAL_INVALID");
                    }

                    List<ProductAnalyticsEventRecord> openedEvents = _analyticsEvents
                        .Where(e => e.WorkspaceId == actor.WorkspaceId &&
                                    e.EventType == "weekly_lab_opened" &&
                                    e.RecordedAt >= start &&
                                    e.RecordedAt < end)
                        .OrderBy(e => e.RecordedAt)
                        .ThenBy(e => e.ProductAnalyticsEventId, StringComparer.Ordinal)
                        .ToList();
                    string sourceRefsJson = JsonSerializer.Serialize(openedEvents.Select(e => new
                    {
                        product_analytics_event_id = e.ProductAnalyticsEventId,
                        schema_version = e.SchemaVersion
                    }), ContractVersions.JsonOptions);
                    string digest = ContractVersions.Sha256CanonicalJson(new
                    {
                        metricId = "weekly_lab_opened_count",
                        sourceRefsJson,
                        start,
                        end
                    });
                    WorkspaceProductMetricSnapshotRecord snapshot = new(
                        StableScopedId("wspmetric", actor.WorkspaceId, "weekly_lab_opened_count", start, end, digest),
                        actor.WorkspaceId,
                        ContractVersions.WorkspaceProductMetricSnapshot,
                        ContractVersions.ProductMetrics,
                        "weekly_lab_opened_count",
                        start,
                        end,
                        Now,
                        "INTEGER",
                        openedEvents.Count,
                        null,
                        null,
                        sourceRefsJson,
                        digest);
                    _workspaceProductMetricSnapshots.Add(snapshot);
                    TenantControlJobRecord job = EnqueueTenantWorkCore(
                        actor.WorkspaceId,
                        ContractVersions.ProductMetric,
                        "WorkspaceProductMetricSnapshot",
                        JsonSerializer.Serialize(new { workspace_product_metric_snapshot_id = snapshot.WorkspaceProductMetricSnapshotId }, ContractVersions.JsonOptions),
                        JsonSerializer.Serialize(new { metricDictionaryVersion = ContractVersions.ProductMetrics }, ContractVersions.JsonOptions),
                        $"product-metric:{snapshot.WorkspaceProductMetricSnapshotId}");
                    TerminalizeTenantWorkCore(job.TenantControlJobId, "PRODUCT_METRIC_PUBLISHED");
                    return (IReadOnlyList<WorkspaceProductMetricSnapshotRecord>)new[] { snapshot };
                }));
            }
            catch (TradeProofException ex)
            {
                return Task.FromResult(CommandResult<IReadOnlyList<WorkspaceProductMetricSnapshotRecord>>.Fail(ex.Code));
            }
        }
    }

    public Task<CommandResult<InternalAggregateProductMetricSnapshotRecord>> PublishInternalAggregateProductMetricAsync(
        PublishInternalAggregateProductMetricRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            try
            {
                return Task.FromResult(RunIdempotent("service", "PublishInternalAggregateProductMetric", request.IdempotencyKey, request, () =>
                {
                    DateTimeOffset start = request.ReportingStartAtUtc.ToUniversalTime();
                    DateTimeOffset end = request.ReportingEndAtUtc.ToUniversalTime();
                    if (start >= end)
                    {
                        throw new TradeProofException("PRODUCT_METRIC_INTERVAL_INVALID");
                    }

                    string metricId = NormalizeProductMetricId(request.MetricId);
                    List<string> workspaceIds = request.WorkspaceIds
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(id => id, StringComparer.Ordinal)
                        .ToList();
                    foreach (string workspaceId in workspaceIds)
                    {
                        if (!_workspaces.ContainsKey(workspaceId))
                        {
                            throw new TradeProofException("WORKSPACE_NOT_FOUND");
                        }
                    }

                    int? value = null;
                    string? nullReason = "PRIVACY_THRESHOLD";
                    if (workspaceIds.Count >= 10)
                    {
                        value = _workspaceProductMetricSnapshots
                            .Where(m => workspaceIds.Contains(m.WorkspaceId, StringComparer.Ordinal) &&
                                        m.MetricId == metricId &&
                                        m.ReportingStartAtUtc == start &&
                                        m.ReportingEndAtUtc == end)
                            .Sum(m => m.ValueInteger ?? 0);
                        nullReason = null;
                    }

                    string sourceRefsJson = JsonSerializer.Serialize(workspaceIds.Select(id => new
                    {
                        workspace_hash = ContractVersions.Sha256CanonicalJson(new { workspaceId = id })
                    }), ContractVersions.JsonOptions);
                    string digest = ContractVersions.Sha256CanonicalJson(new
                    {
                        metricId,
                        nullReason,
                        sourceRefsJson,
                        start,
                        end,
                        value
                    });
                    InternalAggregateProductMetricSnapshotRecord record = new(
                        StableScopedId("intmetric", "service", metricId, start, end, digest),
                        ContractVersions.InternalAggregateProductMetricSnapshot,
                        ContractVersions.ProductMetrics,
                        metricId,
                        start,
                        end,
                        Now,
                        workspaceIds.Count,
                        "INTEGER",
                        value,
                        null,
                        nullReason,
                        sourceRefsJson,
                        digest);
                    _internalAggregateProductMetricSnapshots.Add(record);
                    return record;
                }));
            }
            catch (TradeProofException ex)
            {
                return Task.FromResult(CommandResult<InternalAggregateProductMetricSnapshotRecord>.Fail(ex.Code));
            }
        }
    }

    public Task<CommandResult<ExternalAnalyticsProjectionRecord>> ProjectExternalAnalyticsAsync(
        ActorContext actor,
        ProjectExternalAnalyticsRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            try
            {
                EnsureActorWorkspace(actor);
                return Task.FromResult(RunIdempotent(actor.WorkspaceId, "ProjectExternalAnalytics", request.IdempotencyKey, request, () =>
                {
                    ProductAnalyticsEventRecord analytics = _analyticsEvents.SingleOrDefault(e =>
                        e.WorkspaceId == actor.WorkspaceId &&
                        e.ProductAnalyticsEventId == request.ProductAnalyticsEventId)
                        ?? throw new TradeProofException("PRODUCT_ANALYTICS_EVENT_NOT_FOUND");
                    string payloadJson = JsonSerializer.Serialize(new
                    {
                        event_type = analytics.EventType,
                        source_record_class = ExternalSourceClass(analytics.SourceRecordKeyJson),
                        source_schema_version = analytics.SchemaVersion
                    }, ContractVersions.JsonOptions);
                    string payloadHash = ContractVersions.Sha256CanonicalJson(JsonSerializer.Deserialize<JsonElement>(payloadJson));
                    ExternalAnalyticsProjectionRecord projection = new(
                        StableScopedId("extana", actor.WorkspaceId, analytics.ProductAnalyticsEventId, payloadHash),
                        actor.WorkspaceId,
                        analytics.ProductAnalyticsEventId,
                        ContractVersions.ProductAnalyticsExternal,
                        "PROJECTED",
                        payloadJson,
                        payloadHash,
                        Now);
                    _externalAnalyticsProjections[projection.ExternalAnalyticsProjectionId] = projection;
                    EnqueueTenantWorkCore(
                        actor.WorkspaceId,
                        ContractVersions.AnalyticsDelivery,
                        "ProductAnalyticsExternalProjection",
                        JsonSerializer.Serialize(new { external_analytics_projection_id = projection.ExternalAnalyticsProjectionId }, ContractVersions.JsonOptions),
                        JsonSerializer.Serialize(new { externalSchemaVersion = ContractVersions.ProductAnalyticsExternal }, ContractVersions.JsonOptions),
                        $"analytics-delivery:{projection.ExternalAnalyticsProjectionId}");
                    return projection;
                }));
            }
            catch (TradeProofException ex)
            {
                return Task.FromResult(CommandResult<ExternalAnalyticsProjectionRecord>.Fail(ex.Code));
            }
        }
    }

    public Task<CommandResult<ExternalAnalyticsPurgeRecord>> PurgeExternalAnalyticsAsync(
        ActorContext actor,
        PurgeExternalAnalyticsRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            try
            {
                EnsureActorWorkspace(actor);
                return Task.FromResult(RunIdempotent(actor.WorkspaceId, "PurgeExternalAnalytics", request.IdempotencyKey, request, () =>
                {
                    ExternalAnalyticsProjectionRecord projection = GetOwnedExternalAnalyticsProjection(actor, request.ExternalAnalyticsProjectionId);
                    ExternalAnalyticsProjectionRecord purgedProjection = projection with { State = "PURGED" };
                    _externalAnalyticsProjections[projection.ExternalAnalyticsProjectionId] = purgedProjection;
                    string absenceHash = ContractVersions.Sha256CanonicalJson(new
                    {
                        projectionId = projection.ExternalAnalyticsProjectionId,
                        state = "ABSENCE_VERIFIED",
                        workType = ContractVersions.AnalyticsPurge
                    });
                    ExternalAnalyticsPurgeRecord purge = new(
                        StableScopedId("anapurge", actor.WorkspaceId, projection.ExternalAnalyticsProjectionId, absenceHash),
                        actor.WorkspaceId,
                        projection.ExternalAnalyticsProjectionId,
                        ContractVersions.AnalyticsPurge,
                        "ABSENCE_VERIFIED",
                        absenceHash,
                        Now);
                    _externalAnalyticsPurges.Add(purge);
                    TenantControlJobRecord job = EnqueueTenantWorkCore(
                        actor.WorkspaceId,
                        ContractVersions.AnalyticsPurge,
                        "ProductAnalyticsExternalProjection",
                        JsonSerializer.Serialize(new { external_analytics_projection_id = projection.ExternalAnalyticsProjectionId }, ContractVersions.JsonOptions),
                        JsonSerializer.Serialize(new { absenceDigestSha256 = absenceHash }, ContractVersions.JsonOptions),
                        $"analytics-purge:{projection.ExternalAnalyticsProjectionId}");
                    TerminalizeTenantWorkCore(job.TenantControlJobId, "ANALYTICS_ABSENCE_VERIFIED");
                    return purge;
                }));
            }
            catch (TradeProofException ex)
            {
                return Task.FromResult(CommandResult<ExternalAnalyticsPurgeRecord>.Fail(ex.Code));
            }
        }
    }

    public Task<CommandResult<TradeProofExportRecord>> RequestTradeProofExportAsync(
        ActorContext actor,
        RequestTradeProofExportRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            try
            {
                EnsureActorWorkspace(actor);
                return Task.FromResult(RunIdempotent(actor.WorkspaceId, "RequestTradeProofExport", request.IdempotencyKey, request, () =>
                {
                    WeeklyReportRevisionRecord report = GetOwnedWeeklyReport(actor, request.WeeklyReportRevisionId);
                    DateTimeOffset exportAsOfAt = request.ExportAsOfAt.ToUniversalTime();
                    List<object> entries =
                    [
                        new
                        {
                            byte_size = report.SectionPayloadJson.Length,
                            media_type = "application/json",
                            name = "weekly_lab/report.json",
                            schema_version = ContractVersions.WeeklyLabExportProjection,
                            sha256 = ContractVersions.Sha256Utf8(report.SectionPayloadJson)
                        },
                        new
                        {
                            byte_size = report.MetricSnapshotIdsJson.Length,
                            media_type = "application/json",
                            name = "weekly_lab/metric_refs.json",
                            schema_version = ContractVersions.MetricSnapshot,
                            sha256 = ContractVersions.Sha256Utf8(report.MetricSnapshotIdsJson)
                        },
                        new
                        {
                            byte_size = CsvConvenienceEntries(report).Length,
                            media_type = "text/csv",
                            name = "convenience/weekly_lab.csv",
                            schema_version = ContractVersions.SpreadsheetEscape,
                            sha256 = ContractVersions.Sha256Utf8(CsvConvenienceEntries(report))
                        }
                    ];
                    string serviceClass = entries.Count <= 1000 ? "STANDARD" : "OVERSIZE";
                    string manifestJson = JsonSerializer.Serialize(new
                    {
                        exportAsOfAt,
                        export_schema_version = ContractVersions.TradeProofExport,
                        entries,
                        generatedAt = Now,
                        manifest_schema_version = ContractVersions.TradeProofExportManifest,
                        service_class = serviceClass,
                        sla_envelope = ContractVersions.ExportSlaEnvelope,
                        weekly_lab_projection_schema = ContractVersions.WeeklyLabExportProjection
                    }, ContractVersions.JsonOptions);
                    string csvEntriesJson = CsvConvenienceEntries(report);
                    string contentHash = ContractVersions.Sha256CanonicalJson(new
                    {
                        csvEntriesJson,
                        manifestJson,
                        reportRevisionId = report.WeeklyReportRevisionId
                    });
                    string exportId = StableScopedId("tptexp", actor.WorkspaceId, report.WeeklyReportRevisionId, exportAsOfAt, contentHash);
                    TenantControlJobRecord exportJob = EnqueueTenantWorkCore(
                        actor.WorkspaceId,
                        ContractVersions.Export,
                        "TradeProofExport",
                        JsonSerializer.Serialize(new { tradeproof_export_id = exportId }, ContractVersions.JsonOptions),
                        JsonSerializer.Serialize(new { exportSchemaVersion = ContractVersions.TradeProofExport }, ContractVersions.JsonOptions),
                        $"export:{exportId}");
                    TerminalizeTenantWorkCore(exportJob.TenantControlJobId, "EXPORT_READY");
                    TenantControlJobRecord expiryJob = EnqueueTenantWorkCore(
                        actor.WorkspaceId,
                        ContractVersions.ExportExpiry,
                        "TradeProofExport",
                        JsonSerializer.Serialize(new { tradeproof_export_id = exportId }, ContractVersions.JsonOptions),
                        JsonSerializer.Serialize(new { retention = "24h", state = "READY" }, ContractVersions.JsonOptions),
                        $"export-expiry:{exportId}");
                    TradeProofExportRecord record = new(
                        exportId,
                        actor.WorkspaceId,
                        report.WeeklyReportRevisionId,
                        ContractVersions.TradeProofExport,
                        ContractVersions.TradeProofExportJob,
                        "READY",
                        serviceClass,
                        exportAsOfAt,
                        Now,
                        Now.AddHours(24),
                        manifestJson,
                        csvEntriesJson,
                        contentHash,
                        expiryJob.TenantControlJobId);
                    _tradeProofExports[record.TradeProofExportId] = record;
                    RecordAnalytics(actor.WorkspaceId, "export_completed", new { export_record = "owner_first_party" }, new { service_class = serviceClass }, Now);
                    return record;
                }));
            }
            catch (TradeProofException ex)
            {
                return Task.FromResult(CommandResult<TradeProofExportRecord>.Fail(ex.Code));
            }
        }
    }

    public Task<CommandResult<ExportRoundTripValidationRecord>> ValidateExportRoundTripAsync(
        ActorContext actor,
        ValidateExportRoundTripRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            try
            {
                EnsureActorWorkspace(actor);
                return Task.FromResult(RunIdempotent(actor.WorkspaceId, "ValidateExportRoundTrip", request.IdempotencyKey, request, () =>
                {
                    TradeProofExportRecord export = GetOwnedTradeProofExport(actor, request.TradeProofExportId);
                    bool passed = export.ManifestJson.Contains(ContractVersions.TradeProofExportManifest, StringComparison.Ordinal) &&
                                  export.ManifestJson.Contains(ContractVersions.WeeklyLabExportProjection, StringComparison.Ordinal) &&
                                  export.CsvEntriesJson.Contains(ContractVersions.SpreadsheetEscape, StringComparison.Ordinal) &&
                                  !export.CsvEntriesJson.Contains("=BTCUSDT", StringComparison.Ordinal);
                    ExportRoundTripValidationRecord record = new(
                        StableScopedId("exprt", actor.WorkspaceId, export.TradeProofExportId, export.ContentSha256),
                        actor.WorkspaceId,
                        export.TradeProofExportId,
                        ContractVersions.TradeProofExportRoundTrip,
                        passed,
                        export.ContentSha256,
                        Now);
                    _exportRoundTripValidations.Add(record);
                    return record;
                }));
            }
            catch (TradeProofException ex)
            {
                return Task.FromResult(CommandResult<ExportRoundTripValidationRecord>.Fail(ex.Code));
            }
        }
    }

    public Task<CommandResult<ExportExpiryRecord>> ExpireExportAsync(
        ActorContext actor,
        ExpireExportRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            try
            {
                EnsureActorWorkspace(actor);
                return Task.FromResult(RunIdempotent(actor.WorkspaceId, "ExpireExport", request.IdempotencyKey, request, () =>
                {
                    TradeProofExportRecord export = GetOwnedTradeProofExport(actor, request.TradeProofExportId);
                    TradeProofExportRecord expired = export with { State = "EXPIRED" };
                    _tradeProofExports[export.TradeProofExportId] = expired;
                    string absenceHash = ContractVersions.Sha256CanonicalJson(new
                    {
                        exportId = export.TradeProofExportId,
                        state = "ABSENCE_VERIFIED",
                        workType = ContractVersions.ExportExpiry
                    });
                    ExportExpiryRecord expiry = new(
                        StableScopedId("expexpiry", actor.WorkspaceId, export.TradeProofExportId, absenceHash),
                        actor.WorkspaceId,
                        export.TradeProofExportId,
                        ContractVersions.ExportExpiry,
                        "ABSENCE_VERIFIED",
                        absenceHash,
                        Now);
                    _exportExpiries.Add(expiry);
                    TerminalizeTenantWorkCore(export.ExportExpiryTenantControlJobId, "EXPORT_ARCHIVE_EXPIRED");
                    return expiry;
                }));
            }
            catch (TradeProofException ex)
            {
                return Task.FromResult(CommandResult<ExportExpiryRecord>.Fail(ex.Code));
            }
        }
    }

    public Task<CommandResult<WorkspaceDeletionResult>> RequestWorkspaceDeletionAsync(
        ActorContext actor,
        RequestWorkspaceDeletionRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            try
            {
                EnsureWorkspaceOwnerForDeletion(actor);
                return Task.FromResult(RunIdempotent(actor.WorkspaceId, "RequestWorkspaceDeletion", request.IdempotencyKey, request, () =>
                {
                    WorkspaceRecord workspace = _workspaces[actor.WorkspaceId];
                    if (workspace.LifecycleState == "DELETED")
                    {
                        throw new TradeProofException("WORKSPACE_ALREADY_DELETED");
                    }

                    int nextGeneration = workspace.DeletionGuardGeneration + 1;
                    _workspaces[actor.WorkspaceId] = workspace with
                    {
                        LifecycleState = "DELETING",
                        DeletionGuardGeneration = nextGeneration
                    };
                    int cancelled = CancelQueuedTenantWork(actor.WorkspaceId);
                    int revoked = RevokeExportsForDeletion(actor.WorkspaceId);
                    string deletionId = StableScopedId("wspdel", actor.WorkspaceId, nextGeneration, Now);
                    string contentHash = ContractVersions.Sha256CanonicalJson(new
                    {
                        deletionId,
                        guardGeneration = nextGeneration,
                        schemaVersion = ContractVersions.WorkspaceDeletion,
                        workspaceId = actor.WorkspaceId
                    });
                    WorkspaceDeletionRecord deletion = new(
                        deletionId,
                        actor.WorkspaceId,
                        ContractVersions.WorkspaceDeletion,
                        "FENCED",
                        nextGeneration,
                        Now,
                        null,
                        actor.ActorUserId,
                        contentHash);
                    _workspaceDeletions[deletionId] = deletion;
                    List<WorkspaceDeletionTargetRecord> targets = BuildDeletionTargets(deletion, "FENCED");
                    _workspaceDeletionTargets[deletionId] = targets;
                    EnqueueTenantWorkCore(
                        actor.WorkspaceId,
                        ContractVersions.WorkspaceDeletionWork,
                        "WorkspaceDeletion",
                        JsonSerializer.Serialize(new { workspace_deletion_id = deletionId }, ContractVersions.JsonOptions),
                        JsonSerializer.Serialize(new { guardGeneration = nextGeneration, schemaVersion = ContractVersions.WorkspaceDeletion }, ContractVersions.JsonOptions),
                        $"workspace-deletion:{deletionId}");
                    return new WorkspaceDeletionResult(deletion, targets, cancelled, revoked);
                }));
            }
            catch (TradeProofException ex)
            {
                return Task.FromResult(CommandResult<WorkspaceDeletionResult>.Fail(ex.Code));
            }
        }
    }

    public Task<CommandResult<WorkspaceDeletionResult>> CompleteWorkspaceDeletionAsync(
        ActorContext actor,
        CompleteWorkspaceDeletionRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            try
            {
                EnsureWorkspaceOwnerForDeletion(actor);
                return Task.FromResult(RunIdempotent(actor.WorkspaceId, "CompleteWorkspaceDeletion", request.IdempotencyKey, request, () =>
                {
                    if (!_workspaceDeletions.TryGetValue(request.WorkspaceDeletionId, out WorkspaceDeletionRecord? deletion) ||
                        deletion.WorkspaceId != actor.WorkspaceId)
                    {
                        throw new TradeProofException("WORKSPACE_DELETION_NOT_FOUND");
                    }

                    if (deletion.State == "DELETED")
                    {
                        return new WorkspaceDeletionResult(
                            deletion,
                            _workspaceDeletionTargets[deletion.WorkspaceDeletionId],
                            0,
                            0);
                    }

                    WorkspaceDeletionRecord completed = deletion with
                    {
                        State = "DELETED",
                        CompletedAt = Now
                    };
                    _workspaceDeletions[deletion.WorkspaceDeletionId] = completed;
                    List<WorkspaceDeletionTargetRecord> targets = BuildDeletionTargets(completed, "ABSENCE_VERIFIED");
                    _workspaceDeletionTargets[deletion.WorkspaceDeletionId] = targets;
                    _workspaces[actor.WorkspaceId] = _workspaces[actor.WorkspaceId] with { LifecycleState = "DELETED" };
                    WorkspaceDeletionTombstoneRecord tombstone = new(
                        StableScopedId("wsptomb", actor.WorkspaceId, deletion.WorkspaceDeletionId, completed.GuardGeneration),
                        deletion.WorkspaceDeletionId,
                        actor.WorkspaceId,
                        completed.GuardGeneration,
                        Now,
                        ContractVersions.Sha256CanonicalJson(new
                        {
                            deletionId = deletion.WorkspaceDeletionId,
                            state = completed.State,
                            targetCount = targets.Count
                        }));
                    _workspaceDeletionTombstones.Add(tombstone);
                    return new WorkspaceDeletionResult(completed, targets, 0, 0);
                }));
            }
            catch (TradeProofException ex)
            {
                return Task.FromResult(CommandResult<WorkspaceDeletionResult>.Fail(ex.Code));
            }
        }
    }

    public IReadOnlyList<WeeklyCohortRecord> WeeklyCohorts
    {
        get
        {
            lock (_gate)
            {
                return _weeklyCohorts.Values.OrderBy(c => c.ReportingStartAtUtc).ThenBy(c => c.WeeklyCohortId, StringComparer.Ordinal).ToList();
            }
        }
    }

    public IReadOnlyList<WeeklyReportRevisionRecord> WeeklyReports
    {
        get
        {
            lock (_gate)
            {
                return _weeklyReportRevisions.Values.OrderBy(r => r.PublishedAt).ThenBy(r => r.WeeklyReportRevisionId, StringComparer.Ordinal).ToList();
            }
        }
    }

    public IReadOnlyList<BehavioralExperimentRevisionRecord> BehavioralExperiments
    {
        get
        {
            lock (_gate)
            {
                return CurrentBehavioralExperiments(null).ToList();
            }
        }
    }

    public IReadOnlyList<WeeklyReviewCompletionRecord> WeeklyReviewCompletions
    {
        get
        {
            lock (_gate)
            {
                return _weeklyReviewCompletions.OrderBy(c => c.CompletedAt).ThenBy(c => c.WeeklyReviewCompletionId, StringComparer.Ordinal).ToList();
            }
        }
    }

    public IReadOnlyList<WorkspaceProductMetricSnapshotRecord> ProductMetrics
    {
        get
        {
            lock (_gate)
            {
                return _workspaceProductMetricSnapshots.OrderBy(m => m.ReportingAsOfAt).ThenBy(m => m.WorkspaceProductMetricSnapshotId, StringComparer.Ordinal).ToList();
            }
        }
    }

    public IReadOnlyList<InternalAggregateProductMetricSnapshotRecord> InternalProductMetrics
    {
        get
        {
            lock (_gate)
            {
                return _internalAggregateProductMetricSnapshots.OrderBy(m => m.ReportingAsOfAt).ThenBy(m => m.InternalAggregateProductMetricSnapshotId, StringComparer.Ordinal).ToList();
            }
        }
    }

    public IReadOnlyList<ExternalAnalyticsProjectionRecord> ExternalAnalyticsProjections
    {
        get
        {
            lock (_gate)
            {
                return _externalAnalyticsProjections.Values.OrderBy(p => p.ProjectedAt).ThenBy(p => p.ExternalAnalyticsProjectionId, StringComparer.Ordinal).ToList();
            }
        }
    }

    public IReadOnlyList<ExternalAnalyticsPurgeRecord> ExternalAnalyticsPurges
    {
        get
        {
            lock (_gate)
            {
                return _externalAnalyticsPurges.OrderBy(p => p.PurgedAt).ThenBy(p => p.ExternalAnalyticsPurgeId, StringComparer.Ordinal).ToList();
            }
        }
    }

    public IReadOnlyList<TradeProofExportRecord> TradeProofExports
    {
        get
        {
            lock (_gate)
            {
                return _tradeProofExports.Values.OrderBy(e => e.GeneratedAt).ThenBy(e => e.TradeProofExportId, StringComparer.Ordinal).ToList();
            }
        }
    }

    public IReadOnlyList<ExportRoundTripValidationRecord> ExportRoundTripValidations
    {
        get
        {
            lock (_gate)
            {
                return _exportRoundTripValidations.OrderBy(v => v.ValidatedAt).ThenBy(v => v.ExportRoundTripValidationId, StringComparer.Ordinal).ToList();
            }
        }
    }

    public IReadOnlyList<ExportExpiryRecord> ExportExpiries
    {
        get
        {
            lock (_gate)
            {
                return _exportExpiries.OrderBy(e => e.ExpiredAt).ThenBy(e => e.ExportExpiryRecordId, StringComparer.Ordinal).ToList();
            }
        }
    }

    public IReadOnlyList<WorkspaceDeletionRecord> WorkspaceDeletions
    {
        get
        {
            lock (_gate)
            {
                return _workspaceDeletions.Values.OrderBy(d => d.RequestedAt).ThenBy(d => d.WorkspaceDeletionId, StringComparer.Ordinal).ToList();
            }
        }
    }

    public IReadOnlyList<WorkspaceDeletionTombstoneRecord> WorkspaceDeletionTombstones
    {
        get
        {
            lock (_gate)
            {
                return _workspaceDeletionTombstones.OrderBy(t => t.TombstonedAt).ThenBy(t => t.WorkspaceDeletionTombstoneId, StringComparer.Ordinal).ToList();
            }
        }
    }

    private WeeklyReportRevisionRecord GetOwnedWeeklyReport(ActorContext actor, string weeklyReportRevisionId)
    {
        if (!_weeklyReportRevisions.TryGetValue(weeklyReportRevisionId, out WeeklyReportRevisionRecord? report) ||
            report.WorkspaceId != actor.WorkspaceId)
        {
            throw new TradeProofException("WEEKLY_REPORT_REVISION_NOT_FOUND");
        }

        return report;
    }

    private BehavioralExperimentRevisionRecord GetOwnedBehavioralExperiment(ActorContext actor, string behavioralExperimentId)
    {
        if (!_behavioralExperimentRevisions.TryGetValue(behavioralExperimentId, out List<BehavioralExperimentRevisionRecord>? revisions))
        {
            throw new TradeProofException("BEHAVIORAL_EXPERIMENT_NOT_FOUND");
        }

        BehavioralExperimentRevisionRecord current = revisions.MaxBy(r => r.RevisionNo) ?? throw new TradeProofException("BEHAVIORAL_EXPERIMENT_NOT_FOUND");
        if (current.WorkspaceId != actor.WorkspaceId)
        {
            throw new TradeProofException("BEHAVIORAL_EXPERIMENT_NOT_FOUND");
        }

        return current;
    }

    private BehavioralExperimentRevisionRecord BuildBehavioralExperimentRevision(
        ActorContext actor,
        string behavioralExperimentId,
        int revisionNo,
        string experimentType,
        string state,
        string targetWeeklyCohortId,
        string sourceWeeklyReportRevisionId,
        string proposalText,
        string idempotencyKey)
    {
        DateTimeOffset now = Now;
        string revisionId = NextId("bexprev");
        string contentHash = ContractVersions.Sha256CanonicalJson(new
        {
            behavioralExperimentId,
            experimentType,
            proposalText,
            revisionNo,
            sourceWeeklyReportRevisionId,
            state,
            targetWeeklyCohortId,
            taxonomyVersion = ContractVersions.BehavioralExperiment
        });
        return new BehavioralExperimentRevisionRecord(
            revisionId,
            behavioralExperimentId,
            actor.WorkspaceId,
            revisionNo,
            ContractVersions.BehavioralExperiment,
            experimentType,
            state,
            targetWeeklyCohortId,
            sourceWeeklyReportRevisionId,
            proposalText,
            now,
            actor.ActorUserId,
            idempotencyKey,
            contentHash);
    }

    private IEnumerable<BehavioralExperimentRevisionRecord> CurrentBehavioralExperiments(string? workspaceId)
    {
        IEnumerable<BehavioralExperimentRevisionRecord> current = _behavioralExperimentRevisions.Values
            .Select(revisions => revisions.MaxBy(r => r.RevisionNo))
            .OfType<BehavioralExperimentRevisionRecord>();
        return workspaceId is null ? current.OrderBy(r => r.RecordedAt).ThenBy(r => r.BehavioralExperimentRevisionId, StringComparer.Ordinal) : current.Where(r => r.WorkspaceId == workspaceId);
    }

    private TradeProofExportRecord GetOwnedTradeProofExport(ActorContext actor, string tradeProofExportId)
    {
        if (!_tradeProofExports.TryGetValue(tradeProofExportId, out TradeProofExportRecord? export) ||
            export.WorkspaceId != actor.WorkspaceId)
        {
            throw new TradeProofException("TRADEPROOF_EXPORT_NOT_FOUND");
        }

        return export;
    }

    private ExternalAnalyticsProjectionRecord GetOwnedExternalAnalyticsProjection(ActorContext actor, string externalAnalyticsProjectionId)
    {
        if (!_externalAnalyticsProjections.TryGetValue(externalAnalyticsProjectionId, out ExternalAnalyticsProjectionRecord? projection) ||
            projection.WorkspaceId != actor.WorkspaceId)
        {
            throw new TradeProofException("EXTERNAL_ANALYTICS_PROJECTION_NOT_FOUND");
        }

        return projection;
    }

    private int CancelQueuedTenantWork(string workspaceId)
    {
        int cancelled = 0;
        foreach (TenantControlJobRecord job in _jobs.Values.Where(j => j.WorkspaceId == workspaceId && j.State != "COMPLETED" && j.State != "CANCELLED_DELETION").ToList())
        {
            _jobs[job.TenantControlJobId] = job with
            {
                State = "CANCELLED_DELETION",
                Compacted = true,
                PayloadJson = null
            };
            cancelled++;
        }

        return cancelled;
    }

    private int RevokeExportsForDeletion(string workspaceId)
    {
        int revoked = 0;
        foreach (TradeProofExportRecord export in _tradeProofExports.Values.Where(e => e.WorkspaceId == workspaceId && e.State != "REVOKED_BY_DELETION").ToList())
        {
            _tradeProofExports[export.TradeProofExportId] = export with { State = "REVOKED_BY_DELETION" };
            revoked++;
        }

        return revoked;
    }

    private List<WorkspaceDeletionTargetRecord> BuildDeletionTargets(WorkspaceDeletionRecord deletion, string state)
    {
        string[] targetTypes =
        [
            "PRIMARY_TENANT_DATA",
            "EXPORT_ARCHIVES",
            "TEMPORARY_OBJECTS",
            "EXTERNAL_ANALYTICS"
        ];
        return targetTypes.Select((target, index) =>
        {
            string evidenceHash = ContractVersions.Sha256CanonicalJson(new
            {
                deletionId = deletion.WorkspaceDeletionId,
                state,
                target
            });
            return new WorkspaceDeletionTargetRecord(
                StableScopedId("wsptarget", deletion.WorkspaceId, deletion.WorkspaceDeletionId, target),
                deletion.WorkspaceDeletionId,
                deletion.WorkspaceId,
                index + 1,
                target,
                state,
                Now,
                evidenceHash);
        }).ToList();
    }

    private void EnsureWorkspaceOwnerForDeletion(ActorContext actor)
    {
        WorkspaceRecord workspace = _workspaces.GetValueOrDefault(actor.WorkspaceId) ?? throw new TradeProofException("WORKSPACE_NOT_FOUND");
        if (workspace.OwnerUserId != actor.ActorUserId)
        {
            throw new TradeProofException("WORKSPACE_ACCESS_DENIED");
        }
    }

    private static WeeklyInterval ValidateWeeklyInterval(string workspaceId, DateTimeOffset requestedStart, DateTimeOffset requestedEnd)
    {
        DateTimeOffset start = requestedStart.ToUniversalTime();
        DateTimeOffset end = requestedEnd.ToUniversalTime();
        if (start >= end || end - start != TimeSpan.FromDays(7))
        {
            throw new TradeProofException("WEEKLY_LAB_INTERVAL_INVALID");
        }

        DateTime localStart = start.UtcDateTime.AddHours(7);
        DateTime localEnd = end.UtcDateTime.AddHours(7);
        if (localStart.DayOfWeek != DayOfWeek.Monday ||
            localStart.TimeOfDay != TimeSpan.Zero ||
            localEnd.DayOfWeek != DayOfWeek.Monday ||
            localEnd.TimeOfDay != TimeSpan.Zero)
        {
            throw new TradeProofException("WEEKLY_LAB_INTERVAL_INVALID");
        }

        _ = workspaceId;
        return new WeeklyInterval(start, end, localStart, localEnd, "Asia/Ho_Chi_Minh");
    }

    private static string FormatLocal(DateTime value) =>
        value.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);

    private static string NormalizeExperimentType(string value)
    {
        string normalized = value.Trim().ToUpperInvariant();
        string[] allowed =
        [
            "WAIT_FOR_CLOSE",
            "REDUCE_SIZE_AFTER_LOSS",
            "JOURNAL_BEFORE_ENTRY",
            "SKIP_LOW_CONFIDENCE",
            "OTHER"
        ];
        if (!allowed.Contains(normalized, StringComparer.Ordinal))
        {
            throw new TradeProofException("BEHAVIORAL_EXPERIMENT_TYPE_INVALID");
        }

        return normalized;
    }

    private static string NormalizeAnalyticsEvent(string value)
    {
        string normalized = value.Trim().ToLowerInvariant();
        string[] allowed =
        [
            "weekly_lab_opened",
            "weekly_review_completed",
            "export_completed",
            "plan_armed",
            "import_previewed",
            "measurement_abandoned"
        ];
        if (!allowed.Contains(normalized, StringComparer.Ordinal))
        {
            throw new TradeProofException("PRODUCT_ANALYTICS_EVENT_INVALID");
        }

        return normalized;
    }

    private static string NormalizeAnalyticsSourceType(string value)
    {
        string normalized = value.Trim();
        string[] allowed =
        [
            "WeeklyReportRevision",
            "WeeklyReviewCompletion",
            "TradeProofExport",
            "TradePlanRevision",
            "ImportPreview",
            "ProductMeasurementRun"
        ];
        if (!allowed.Contains(normalized, StringComparer.Ordinal))
        {
            throw new TradeProofException("PRODUCT_ANALYTICS_SOURCE_INVALID");
        }

        return normalized;
    }

    private static string NormalizeProductMetricId(string value)
    {
        string normalized = value.Trim().ToLowerInvariant();
        if (normalized != "weekly_lab_opened_count")
        {
            throw new TradeProofException("PRODUCT_METRIC_UNSUPPORTED");
        }

        return normalized;
    }

    private static string NormalizeShortText(string value, string errorCode, int maxLength)
    {
        string trimmed = value.Trim();
        if (trimmed.Length == 0 || trimmed.Length > maxLength)
        {
            throw new TradeProofException(errorCode);
        }

        return trimmed;
    }

    private static string ExternalSourceClass(string sourceRecordKeyJson)
    {
        Dictionary<string, JsonElement>? key = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(sourceRecordKeyJson, ContractVersions.JsonOptions);
        return key is not null && key.TryGetValue("source_record_type", out JsonElement value)
            ? value.GetString() ?? "Unknown"
            : "Unknown";
    }

    private static string CsvConvenienceEntries(WeeklyReportRevisionRecord report)
    {
        object[] entries =
        [
            new
            {
                cell = "weekly_lab_schema",
                escape_profile = ContractVersions.SpreadsheetEscape,
                original_kind = "SCHEMA_VERSION",
                value = report.WeeklyLabSchemaVersion
            },
            new
            {
                cell = "weekly_report_revision",
                escape_profile = ContractVersions.SpreadsheetEscape,
                original_kind = "OPAQUE_ID",
                value = report.WeeklyReportRevisionId
            },
            new
            {
                cell = "symbol_fixture",
                escape_profile = ContractVersions.SpreadsheetEscape,
                original_kind = "TEXT",
                value = "'BTCUSDT"
            }
        ];
        return JsonSerializer.Serialize(new
        {
            entries,
            schema_version = ContractVersions.SpreadsheetEscape
        }, ContractVersions.JsonOptions);
    }

    private sealed record WeeklyInterval(
        DateTimeOffset StartUtc,
        DateTimeOffset EndUtc,
        DateTime LocalStart,
        DateTime LocalEnd,
        string Timezone)
    {
        public string LocalStartText => FormatLocal(LocalStart);
        public string LocalEndText => FormatLocal(LocalEnd);
    }
}
