using System.Globalization;
using System.Text.Json;
using TradeProof.Domain.Foundation;

namespace TradeProof.Application.Foundation;

public sealed partial class TradeProofApp
{
    private readonly Dictionary<string, AttachmentRecord> _attachments = [];
    private readonly Dictionary<string, List<AttachmentStateEventRecord>> _attachmentEvents = [];
    private readonly List<AttachmentTombstoneRecord> _attachmentTombstones = [];
    private readonly Dictionary<string, ReviewRecord> _reviews = [];
    private readonly Dictionary<string, string> _reviewIdsByEpisode = [];
    private readonly Dictionary<string, List<ReviewRevisionRecord>> _reviewRevisions = [];
    private readonly List<ReviewRevisionAttachmentRecord> _reviewRevisionAttachments = [];
    private readonly Dictionary<string, ReviewTaxonomyVersionRecord> _reviewTaxonomyVersions = [];
    private readonly Dictionary<string, List<ReviewTaxonomyItemRecord>> _reviewTaxonomyItems = [];
    private readonly List<ReviewTaxonomyPublishEventRecord> _reviewTaxonomyPublishEvents = [];
    private readonly List<MetricSnapshotRecord> _metricSnapshots = [];

    public Task<CommandResult<ObjectIngestReservationRecord>> ReserveReviewAttachmentAsync(
        ActorContext actor,
        ReserveReviewAttachmentRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            try
            {
                EnsureActorWorkspace(actor);
                return Task.FromResult(RunIdempotent(actor.WorkspaceId, "ReserveReviewAttachment", request.IdempotencyKey, request, () =>
                {
                    ValidateTradingAccount(actor, request.TradingAccountId);
                    if (request.UploadKind != "SCREENSHOT")
                    {
                        throw new TradeProofException("ATTACHMENT_KIND_UNSUPPORTED");
                    }

                    DateTimeOffset now = Now;
                    string reservationId = NextId("oir");
                    string uploadId = NextId("upl");
                    string attachmentId = NextId("att");
                    int leaseGeneration = 1;
                    string providerKeySha256 = ContractVersions.Sha256Utf8($"{actor.WorkspaceId}\u001F{reservationId}\u001F{uploadId}\u001F{attachmentId}\u001F{leaseGeneration}");
                    string writeCapability = BuildWriteCapability(actor.WorkspaceId, reservationId, providerKeySha256, leaseGeneration, now.AddMinutes(15));
                    TenantControlJobRecord finalizer = EnqueueTenantWorkCore(
                        actor.WorkspaceId,
                        ContractVersions.ObjectIngestFinalize,
                        "ObjectIngestReservation",
                        ObjectIngestSubject(reservationId),
                        JsonSerializer.Serialize(new
                        {
                            leaseGeneration,
                            purpose = "SANITIZED_ATTACHMENT",
                            reservedAttachmentId = attachmentId
                        }, ContractVersions.JsonOptions),
                        $"object-ingest-reservation:{reservationId}:finalize");

                    ObjectIngestReservationRecord reservation = new(
                        reservationId,
                        actor.WorkspaceId,
                        request.TradingAccountId,
                        "SANITIZED_ATTACHMENT",
                        uploadId,
                        attachmentId,
                        "SCREENSHOT",
                        ContractVersions.UploadAttachment,
                        leaseGeneration,
                        providerKeySha256,
                        writeCapability,
                        "RESERVED",
                        now,
                        now.AddMinutes(15),
                        now.AddHours(1),
                        null,
                        null,
                        finalizer.TenantControlJobId);
                    _objectIngestReservations[reservationId] = reservation;
                    _objectIngestReservationEvents[reservationId] =
                    [
                        new ObjectIngestReservationEventRecord(NextId("oirevt"), reservationId, actor.WorkspaceId, 1, "RESERVE", now, null)
                    ];
                    return reservation;
                }));
            }
            catch (TradeProofException ex)
            {
                return Task.FromResult(CommandResult<ObjectIngestReservationRecord>.Fail(ex.Code));
            }
        }
    }

    public Task<CommandResult<AttachmentValidationResponse>> ValidateAttachmentUploadAsync(
        ActorContext actor,
        ValidateAttachmentUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            try
            {
                EnsureActorWorkspace(actor);
                EnsureIdempotencyKey(request.IdempotencyKey);
                UploadRecord upload = GetOwnedUpload(actor, request.UploadId);
                if (upload.Kind != "SCREENSHOT")
                {
                    throw new TradeProofException("ATTACHMENT_KIND_UNSUPPORTED");
                }

                AttachmentRecord? existing = _attachments.Values.SingleOrDefault(a => a.SourceUploadId == upload.UploadId);
                if (upload.State == "ACCEPTED")
                {
                    return Task.FromResult(CommandResult<AttachmentValidationResponse>.Ok(new AttachmentValidationResponse(upload, existing, null)));
                }

                if (upload.State == "REJECTED")
                {
                    return Task.FromResult(CommandResult<AttachmentValidationResponse>.Ok(new AttachmentValidationResponse(upload, null, upload.SafeErrorCode)));
                }

                if (upload.State != "QUARANTINED" && upload.State != "VALIDATING")
                {
                    throw new TradeProofException("UPLOAD_NOT_VALIDATABLE");
                }

                UploadRecord validating = upload.State == "VALIDATING" ? upload : upload with { State = "VALIDATING" };
                _uploads[upload.UploadId] = validating;
                if (upload.State != "VALIDATING")
                {
                    AddUploadEvent(validating, "START_VALIDATION", "SYSTEM", null, null, Now);
                }

                if (!_providerObjectsByUploadId.TryGetValue(upload.UploadId, out ProviderObjectVersion? providerObject))
                {
                    return Task.FromResult(CommandResult<AttachmentValidationResponse>.Ok(RejectAttachmentUpload(validating, "PROVIDER_OBJECT_NOT_FOUND")));
                }

                if (!IsSupportedScreenshot(providerObject.Bytes))
                {
                    return Task.FromResult(CommandResult<AttachmentValidationResponse>.Ok(RejectAttachmentUpload(validating, "SCREENSHOT_UNSUPPORTED")));
                }

                ObjectIngestReservationRecord reservation = GetOwnedReservation(actor, upload.SourceObjectIngestReservationId);
                string attachmentId = reservation.ReservedAttachmentId ?? throw new TradeProofException("ATTACHMENT_RESERVATION_NOT_FOUND");
                DateTimeOffset now = Now;
                AttachmentRecord attachment = new(
                    attachmentId,
                    actor.WorkspaceId,
                    upload.UploadId,
                    "SCREENSHOT",
                    "ACTIVE",
                    "PASSED",
                    providerObject.ContentSha256,
                    providerObject.SizeBytes,
                    $"attv_{providerObject.ContentSha256[..24]}",
                    now,
                    now,
                    null,
                    null);
                _attachments[attachment.AttachmentId] = attachment;
                _attachmentEvents[attachment.AttachmentId] =
                [
                    new AttachmentStateEventRecord(NextId("attevt"), actor.WorkspaceId, attachment.AttachmentId, 1, "ACTIVATE", now, null)
                ];

                UploadRecord accepted = validating with
                {
                    State = "ACCEPTED",
                    AcceptedAt = now,
                    SafeErrorCode = null
                };
                _uploads[upload.UploadId] = accepted;
                AddUploadEvent(accepted, "ACCEPT", "SYSTEM", null, null, now);
                EnsureAttachmentDeleteJob(attachment);
                TerminalizeTenantWorkCore(upload.ValidateTenantControlJobId, "ATTACHMENT_ACCEPTED");
                return Task.FromResult(CommandResult<AttachmentValidationResponse>.Ok(new AttachmentValidationResponse(accepted, attachment, null)));
            }
            catch (TradeProofException ex)
            {
                return Task.FromResult(CommandResult<AttachmentValidationResponse>.Fail(ex.Code));
            }
        }
    }

    public Task<CommandResult<AttachmentDeleteResponse>> DeleteAttachmentAsync(
        ActorContext actor,
        DeleteAttachmentRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            try
            {
                EnsureActorWorkspace(actor);
                return Task.FromResult(RunIdempotent(actor.WorkspaceId, "DeleteAttachment", request.IdempotencyKey, request, () =>
                {
                    AttachmentRecord attachment = GetOwnedAttachment(actor, request.AttachmentId);
                    if (attachment.State == "DELETED")
                    {
                        AttachmentTombstoneRecord existingTombstone = _attachmentTombstones.Single(t => t.AttachmentId == attachment.AttachmentId);
                        return new AttachmentDeleteResponse(attachment, existingTombstone, _uploadAbsenceVerifications[attachment.SourceUploadId]);
                    }

                    TenantControlJobRecord deleteJob = EnsureAttachmentDeleteJob(attachment);
                    DateTimeOffset now = Now;
                    _providerObjectsByUploadId.Remove(attachment.SourceUploadId);
                    UploadObjectAbsenceVerificationRecord absence = new(
                        NextId("uplabs"),
                        actor.WorkspaceId,
                        attachment.SourceUploadId,
                        1,
                        attachment.ContentSha256,
                        now);
                    _uploadAbsenceVerifications[attachment.SourceUploadId] = absence;
                    AttachmentRecord deleted = attachment with
                    {
                        State = "DELETED",
                        DeletedAt = now
                    };
                    _attachments[attachment.AttachmentId] = deleted;
                    AddAttachmentEvent(deleted, "DELETE", null, now);
                    AttachmentTombstoneRecord tombstone = new(
                        NextId("atttomb"),
                        actor.WorkspaceId,
                        attachment.AttachmentId,
                        attachment.SourceUploadId,
                        attachment.ContentSha256,
                        now,
                        absence.UploadObjectAbsenceVerificationId);
                    _attachmentTombstones.Add(tombstone);
                    TerminalizeTenantWorkCore(deleteJob.TenantControlJobId, "ATTACHMENT_DELETED");
                    return new AttachmentDeleteResponse(deleted, tombstone, absence);
                }));
            }
            catch (TradeProofException ex)
            {
                return Task.FromResult(CommandResult<AttachmentDeleteResponse>.Fail(ex.Code));
            }
        }
    }

    public Task<CommandResult<EpisodeReviewResult>> CompleteEpisodeReviewAsync(
        ActorContext actor,
        CompleteEpisodeReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            try
            {
                EnsureActorWorkspace(actor);
                return Task.FromResult(RunIdempotent(actor.WorkspaceId, "CompleteEpisodeReview", request.IdempotencyKey, request, () =>
                {
                    EnsureReviewTaxonomies();
                    TradeEpisodeProjectionRecord projection = GetActiveClosedProjection(actor.WorkspaceId, request.EpisodeId, request.ExpectedEpisodeProjectionVersion);
                    if (_reviewIdsByEpisode.ContainsKey(projection.EpisodeId))
                    {
                        throw new TradeProofException("REVIEW_ALREADY_COMPLETED");
                    }

                    DateTimeOffset now = Now;
                    string reviewId = NextId("revw");
                    string reviewRevisionId = NextId("revrev");
                    AttachmentRecord? attachment = ResolveOptionalReviewAttachment(actor, request.AttachmentId);
                    ReviewPayload payload = ValidateReviewPayload(
                        projection,
                        reviewId,
                        reviewRevisionId,
                        1,
                        now,
                        actor.ActorUserId,
                        request.ExitReason,
                        request.ExitReasonOtherText,
                        request.RuleBreach,
                        request.BreachTypeIds,
                        request.BreachOtherText,
                        request.StopMovedAway,
                        request.RiskExceeded,
                        request.RequiredChecklistResults,
                        request.Emotion,
                        request.Lesson,
                        request.IdempotencyKey,
                        attachment);
                    ReviewRecord review = new(reviewId, projection.EpisodeId, actor.WorkspaceId, "COMPLETED", now, now);
                    ReviewRevisionRecord revision = BuildReviewRevision(reviewRevisionId, actor.WorkspaceId, reviewId, 1, projection.ProjectionVersion, now, actor.ActorUserId, request.IdempotencyKey, payload);
                    _reviews[reviewId] = review;
                    _reviewIdsByEpisode[projection.EpisodeId] = reviewId;
                    _reviewRevisions[reviewId] = [revision];
                    AddReviewAttachmentJoin(revision, attachment, now);
                    return new EpisodeReviewResult(review, revision);
                }));
            }
            catch (TradeProofException ex)
            {
                return Task.FromResult(CommandResult<EpisodeReviewResult>.Fail(ex.Code));
            }
        }
    }

    public Task<CommandResult<EpisodeReviewResult>> ReviseEpisodeReviewAsync(
        ActorContext actor,
        ReviseEpisodeReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            try
            {
                EnsureActorWorkspace(actor);
                return Task.FromResult(RunIdempotent(actor.WorkspaceId, "ReviseEpisodeReview", request.IdempotencyKey, request, () =>
                {
                    EnsureReviewTaxonomies();
                    ReviewRecord review = GetOwnedReview(actor, request.ReviewId);
                    TradeEpisodeProjectionRecord projection = GetActiveClosedProjection(actor.WorkspaceId, review.EpisodeId, request.ExpectedEpisodeProjectionVersion);
                    ReviewRevisionRecord current = _reviewRevisions[review.ReviewId].MaxBy(r => r.RevisionNo) ?? throw new TradeProofException("REVIEW_REVISION_NOT_FOUND");
                    if (current.RevisionNo != request.ExpectedRevisionNo)
                    {
                        throw new TradeProofException("STALE_REVIEW_REVISION");
                    }

                    DateTimeOffset now = Now;
                    int revisionNo = current.RevisionNo + 1;
                    string reviewRevisionId = NextId("revrev");
                    AttachmentRecord? attachment = ResolveOptionalReviewAttachment(actor, request.AttachmentId);
                    ReviewPayload payload = ValidateReviewPayload(
                        projection,
                        review.ReviewId,
                        reviewRevisionId,
                        revisionNo,
                        now,
                        actor.ActorUserId,
                        request.ExitReason,
                        request.ExitReasonOtherText,
                        request.RuleBreach,
                        request.BreachTypeIds,
                        request.BreachOtherText,
                        request.StopMovedAway,
                        request.RiskExceeded,
                        request.RequiredChecklistResults,
                        request.Emotion,
                        request.Lesson,
                        request.IdempotencyKey,
                        attachment);
                    ReviewRevisionRecord revision = BuildReviewRevision(reviewRevisionId, actor.WorkspaceId, review.ReviewId, revisionNo, projection.ProjectionVersion, now, actor.ActorUserId, request.IdempotencyKey, payload);
                    ReviewRecord updated = review with { State = "COMPLETED" };
                    _reviews[review.ReviewId] = updated;
                    _reviewRevisions[review.ReviewId].Add(revision);
                    AddReviewAttachmentJoin(revision, attachment, now);
                    return new EpisodeReviewResult(updated, revision);
                }));
            }
            catch (TradeProofException ex)
            {
                return Task.FromResult(CommandResult<EpisodeReviewResult>.Fail(ex.Code));
            }
        }
    }

    public Task<CommandResult<IReadOnlyList<MetricSnapshotRecord>>> PublishMetricSnapshotsAsync(
        ActorContext actor,
        PublishMetricSnapshotsRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            try
            {
                EnsureActorWorkspace(actor);
                return Task.FromResult(RunIdempotent(actor.WorkspaceId, "PublishMetricSnapshots", request.IdempotencyKey, request, () =>
                {
                    DateTimeOffset start = request.ReportingStartAtUtc.ToUniversalTime();
                    DateTimeOffset end = request.ReportingEndAtUtc.ToUniversalTime();
                    if (start >= end)
                    {
                        throw new TradeProofException("METRIC_INTERVAL_INVALID");
                    }

                    DateTimeOffset asOfAt = Now;
                    List<EpisodeMetricSource> closed = ActiveEpisodeProjections()
                        .Where(p => p.WorkspaceId == actor.WorkspaceId &&
                                    p.State == "CLOSED" &&
                                    p.ClosedAt is not null &&
                                    p.ClosedAt.Value >= start &&
                                    p.ClosedAt.Value < end)
                        .OrderBy(p => p.ClosedAt)
                        .ThenBy(p => p.EpisodeId, StringComparer.Ordinal)
                        .ThenBy(p => p.ProjectionVersion)
                        .Select(BuildMetricSource)
                        .ToList();
                    string cohortId = StableScopedId("cohort", actor.WorkspaceId, start, end);
                    string inputId = StableScopedId("cohortinput", actor.WorkspaceId, start, end, asOfAt);
                    string tupleHash = ContractVersions.Sha256CanonicalJson(new
                    {
                        accounting = ContractVersions.WeightedAverageEpisode,
                        context = ContractVersions.ContextAlgorithm,
                        metric = ContractVersions.MetricsAlgorithm,
                        metricDecimal = ContractVersions.MetricsDecimal,
                        review = ContractVersions.ReviewRevision
                    });
                    List<MetricSnapshotRecord> snapshots =
                    [
                        BuildRateMetric(actor.WorkspaceId, cohortId, inputId, tupleHash, start, end, asOfAt, "accounting_completeness_rate", "accounting_completeness_rate_v1", "closed_base_eligible_v1", "RATIO", closed, closed.Where(s => s.Projection.AccountingQuality == "COMPLETE").ToList(), closed.Count(s => s.Projection.AccountingQuality == "COMPLETE"), s => s.Projection.AccountingQuality == "COMPLETE" ? null : "ACCOUNTING_INCOMPLETE"),
                        BuildRateMetric(actor.WorkspaceId, cohortId, inputId, tupleHash, start, end, asOfAt, "planned_trade_rate", "planned_trade_rate_v1", "net_eligible_v1", "RATIO", closed, closed.Where(IsNetEligible).ToList(), closed.Count(s => IsNetEligible(s) && IsPlanned(s)), s => IsNetEligible(s) ? null : AccountingExclusionReason(s)),
                        BuildRateMetric(actor.WorkspaceId, cohortId, inputId, tupleHash, start, end, asOfAt, "review_coverage_rate", "review_coverage_rate_v1", "net_eligible_v1", "RATIO", closed.Where(IsNetEligible).ToList(), closed.Where(s => IsNetEligible(s) && s.Revision is not null).ToList(), closed.Count(s => IsNetEligible(s) && s.Revision is not null), s => s.Revision is null ? "REVIEW_MISSING" : null, closed.Count(IsNetEligible)),
                        BuildRateMetric(actor.WorkspaceId, cohortId, inputId, tupleHash, start, end, asOfAt, "plan_adherence_rate", "plan_adherence_rate_v1", "planned_reviewed_net_eligible_v1", "RATIO", closed.Where(s => IsNetEligible(s) && IsPlanned(s)).ToList(), closed.Where(s => IsNetEligible(s) && IsPlanned(s) && s.Revision is not null).ToList(), closed.Count(s => IsNetEligible(s) && IsPlanned(s) && s.Revision is not null && IsAdherent(s.Revision)), s => s.Revision is null ? "REVIEW_MISSING" : null),
                        BuildRateMetric(actor.WorkspaceId, cohortId, inputId, tupleHash, start, end, asOfAt, "rule_breach_rate", "rule_breach_rate_v1", "planned_reviewed_net_eligible_v1", "RATIO", closed.Where(s => IsNetEligible(s) && IsPlanned(s)).ToList(), closed.Where(s => IsNetEligible(s) && IsPlanned(s) && s.Revision is not null).ToList(), closed.Count(s => IsNetEligible(s) && IsPlanned(s) && s.Revision?.RuleBreach == true), s => s.Revision is null ? "REVIEW_MISSING" : null),
                        BuildRateMetric(actor.WorkspaceId, cohortId, inputId, tupleHash, start, end, asOfAt, "stop_moved_away_rate", "stop_moved_away_rate_v1", "planned_reviewed_net_eligible_v1", "RATIO", closed.Where(s => IsNetEligible(s) && IsPlanned(s)).ToList(), closed.Where(s => IsNetEligible(s) && IsPlanned(s) && s.Revision is not null).ToList(), closed.Count(s => IsNetEligible(s) && IsPlanned(s) && s.Revision?.StopMovedAway == true), s => s.Revision is null ? "REVIEW_MISSING" : null),
                        BuildRateMetric(actor.WorkspaceId, cohortId, inputId, tupleHash, start, end, asOfAt, "risk_exceeded_rate", "risk_exceeded_rate_v1", "planned_reviewed_net_eligible_v1", "RATIO", closed.Where(s => IsNetEligible(s) && IsPlanned(s)).ToList(), closed.Where(s => IsNetEligible(s) && IsPlanned(s) && s.Revision is not null).ToList(), closed.Count(s => IsNetEligible(s) && IsPlanned(s) && s.Revision?.RiskExceeded == true), s => s.Revision is null ? "REVIEW_MISSING" : null),
                        BuildRateMetric(actor.WorkspaceId, cohortId, inputId, tupleHash, start, end, asOfAt, "required_checklist_completion_rate", "required_checklist_completion_rate_v1", "planned_reviewed_net_eligible_v1", "RATIO", closed.Where(s => IsNetEligible(s) && IsPlanned(s)).ToList(), closed.Where(s => IsNetEligible(s) && IsPlanned(s) && s.Revision is not null).ToList(), closed.Count(s => IsNetEligible(s) && IsPlanned(s) && s.Revision is not null && RequiredChecklistComplete(s.Revision)), s => s.Revision is null ? "REVIEW_MISSING" : null)
                    ];
                    foreach ((string phase, string timeframe) in new[] { ("ENTRY", "5m"), ("ENTRY", "1m"), ("EXIT", "5m"), ("EXIT", "1m") })
                    {
                        snapshots.Add(BuildContextCoverageMetric(actor.WorkspaceId, cohortId, inputId, tupleHash, start, end, asOfAt, closed.Where(IsNetEligible).ToList(), phase, timeframe));
                    }

                    _metricSnapshots.AddRange(snapshots);
                    return (IReadOnlyList<MetricSnapshotRecord>)snapshots;
                }));
            }
            catch (TradeProofException ex)
            {
                return Task.FromResult(CommandResult<IReadOnlyList<MetricSnapshotRecord>>.Fail(ex.Code));
            }
        }
    }

    public IReadOnlyList<AttachmentRecord> Attachments
    {
        get
        {
            lock (_gate)
            {
                return _attachments.Values.OrderBy(a => a.AttachmentId, StringComparer.Ordinal).ToList();
            }
        }
    }

    public IReadOnlyList<AttachmentTombstoneRecord> AttachmentTombstones
    {
        get
        {
            lock (_gate)
            {
                return _attachmentTombstones.OrderBy(t => t.AttachmentTombstoneId, StringComparer.Ordinal).ToList();
            }
        }
    }

    public IReadOnlyList<ReviewRecord> Reviews
    {
        get
        {
            lock (_gate)
            {
                return _reviews.Values.OrderBy(r => r.ReviewId, StringComparer.Ordinal).ToList();
            }
        }
    }

    public IReadOnlyList<ReviewRevisionRecord> ReviewRevisions
    {
        get
        {
            lock (_gate)
            {
                return _reviewRevisions.Values.SelectMany(r => r).OrderBy(r => r.ReviewRevisionId, StringComparer.Ordinal).ToList();
            }
        }
    }

    public IReadOnlyList<ReviewRevisionAttachmentRecord> ReviewRevisionAttachments
    {
        get
        {
            lock (_gate)
            {
                return _reviewRevisionAttachments.OrderBy(a => a.ReviewRevisionId, StringComparer.Ordinal).ToList();
            }
        }
    }

    public IReadOnlyList<ReviewTaxonomyVersionRecord> ReviewTaxonomyVersions
    {
        get
        {
            lock (_gate)
            {
                EnsureReviewTaxonomies();
                return _reviewTaxonomyVersions.Values.OrderBy(v => v.TaxonomyVersion, StringComparer.Ordinal).ToList();
            }
        }
    }

    public IReadOnlyList<ReviewTaxonomyItemRecord> ReviewTaxonomyItems
    {
        get
        {
            lock (_gate)
            {
                EnsureReviewTaxonomies();
                return _reviewTaxonomyItems.Values.SelectMany(i => i).OrderBy(i => i.TaxonomyVersion, StringComparer.Ordinal).ThenBy(i => i.ItemOrder).ToList();
            }
        }
    }

    public IReadOnlyList<ReviewTaxonomyPublishEventRecord> ReviewTaxonomyPublishEvents
    {
        get
        {
            lock (_gate)
            {
                EnsureReviewTaxonomies();
                return _reviewTaxonomyPublishEvents.OrderBy(e => e.TaxonomyType, StringComparer.Ordinal).ThenBy(e => e.EventSequence).ToList();
            }
        }
    }

    public IReadOnlyList<MetricSnapshotRecord> MetricSnapshots
    {
        get
        {
            lock (_gate)
            {
                return _metricSnapshots.OrderBy(m => m.MetricSnapshotId, StringComparer.Ordinal).ToList();
            }
        }
    }

    private void EnqueueMetricsForProjection(ActorContext actor, TradeEpisodeProjectionRecord projection)
    {
        if (projection.State != "CLOSED")
        {
            return;
        }

        EnqueueTenantWorkCore(
            actor.WorkspaceId,
            ContractVersions.Metrics,
            "MetricSnapshot",
            JsonSerializer.Serialize(new
            {
                episode_id = projection.EpisodeId,
                projection_version = projection.ProjectionVersion
            }, ContractVersions.JsonOptions),
            JsonSerializer.Serialize(new
            {
                metricAlgorithmVersion = ContractVersions.MetricsAlgorithm,
                reason = "EPISODE_CLOSED"
            }, ContractVersions.JsonOptions),
            $"metrics-initial:{projection.EpisodeId}:{projection.ProjectionVersion.ToString(CultureInfo.InvariantCulture)}");
    }

    private IReadOnlyList<EpisodeDashboardRecord> BuildDashboardEpisodes(string workspaceId)
    {
        return ActiveEpisodeProjections()
            .Where(p => p.WorkspaceId == workspaceId)
            .OrderByDescending(p => p.FirstFillAt)
            .Select(p =>
            {
                IReadOnlyList<string> fillIds = ActiveAllocationFillIds(p);
                ReviewRevisionRecord? revision = CurrentReviewRevision(p);
                string? reviewState = CurrentReviewState(p);
                return new EpisodeDashboardRecord(
                    p.EpisodeId,
                    p.ProjectionVersion,
                    p.State,
                    _normalizedFills[p.FirstFillId].VenueSymbol,
                    p.PlanProofStatus,
                    p.AccountingQuality,
                    _feeConversions.Values.Where(c => fillIds.Contains(c.FillId) && c.SupersededAt is null).OrderBy(c => c.FillId, StringComparer.Ordinal).ToList(),
                    _accountingLedgerEntries.Where(l => l.EpisodeId == p.EpisodeId && l.ProjectionVersion == p.ProjectionVersion).OrderBy(l => l.EntrySequence).ToList(),
                    reviewState,
                    revision?.ReviewRevisionId);
            })
            .ToList();
    }

    private DashboardDataQualityRecord BuildDashboardDataQuality(string workspaceId)
    {
        List<string> banners = _metricSnapshots
            .Where(m => m.WorkspaceId == workspaceId && m.ExcludedEpisodeCount > 0)
            .OrderBy(m => m.MetricId, StringComparer.Ordinal)
            .Select(m => $"{m.MetricId}:{m.ExclusionReasonCountsJson}")
            .ToList();
        return new DashboardDataQualityRecord(banners);
    }

    private AttachmentValidationResponse RejectAttachmentUpload(UploadRecord upload, string safeErrorCode)
    {
        DateTimeOffset now = Now;
        UploadRecord rejected = upload with
        {
            State = "REJECTED",
            SafeErrorCode = safeErrorCode
        };
        _uploads[upload.UploadId] = rejected;
        AddUploadEvent(rejected, "REJECT", "SYSTEM", safeErrorCode, null, now);
        TerminalizeTenantWorkCore(upload.ValidateTenantControlJobId, "ATTACHMENT_REJECTED");
        return new AttachmentValidationResponse(rejected, null, safeErrorCode);
    }

    private TenantControlJobRecord EnsureAttachmentDeleteJob(AttachmentRecord attachment)
    {
        return EnqueueTenantWorkCore(
            attachment.WorkspaceId,
            ContractVersions.AttachmentDelete,
            "Attachment",
            JsonSerializer.Serialize(new { attachment_id = attachment.AttachmentId }, ContractVersions.JsonOptions),
            JsonSerializer.Serialize(new
            {
                attachmentContentVersionId = attachment.AttachmentContentVersionId,
                operation = "DELETE_ATTACHMENT_BYTES",
                sourceUploadId = attachment.SourceUploadId
            }, ContractVersions.JsonOptions),
            $"attachment:{attachment.AttachmentId}:delete");
    }

    private void AddAttachmentEvent(AttachmentRecord attachment, string eventType, string? safeReasonCode, DateTimeOffset recordedAt)
    {
        List<AttachmentStateEventRecord> events = _attachmentEvents[attachment.AttachmentId];
        events.Add(new AttachmentStateEventRecord(
            NextId("attevt"),
            attachment.WorkspaceId,
            attachment.AttachmentId,
            events.Count + 1,
            eventType,
            recordedAt,
            safeReasonCode));
    }

    private AttachmentRecord GetOwnedAttachment(ActorContext actor, string attachmentId)
    {
        if (!_attachments.TryGetValue(attachmentId, out AttachmentRecord? attachment) || attachment.WorkspaceId != actor.WorkspaceId)
        {
            throw new TradeProofException("ATTACHMENT_NOT_FOUND");
        }

        return attachment;
    }

    private AttachmentRecord? ResolveOptionalReviewAttachment(ActorContext actor, string? attachmentId)
    {
        if (attachmentId is null)
        {
            return null;
        }

        AttachmentRecord attachment = GetOwnedAttachment(actor, attachmentId);
        if (attachment.State != "ACTIVE" || attachment.ScanStatus != "PASSED")
        {
            throw new TradeProofException("REVIEW_ATTACHMENT_NOT_READY");
        }

        return attachment;
    }

    private TradeEpisodeProjectionRecord GetActiveClosedProjection(string workspaceId, string episodeId, int expectedProjectionVersion)
    {
        TradeEpisodeProjectionRecord projection = ActiveEpisodeProjections().SingleOrDefault(p => p.WorkspaceId == workspaceId && p.EpisodeId == episodeId)
            ?? throw new TradeProofException("EPISODE_PROJECTION_NOT_FOUND");
        if (projection.ProjectionVersion != expectedProjectionVersion)
        {
            throw new TradeProofException("STALE_EPISODE_PROJECTION");
        }

        if (projection.State != "CLOSED")
        {
            throw new TradeProofException("REVIEW_EPISODE_NOT_CLOSED");
        }

        return projection;
    }

    private ReviewRecord GetOwnedReview(ActorContext actor, string reviewId)
    {
        if (!_reviews.TryGetValue(reviewId, out ReviewRecord? review) || review.WorkspaceId != actor.WorkspaceId)
        {
            throw new TradeProofException("REVIEW_NOT_FOUND");
        }

        return review;
    }

    private ReviewRevisionRecord BuildReviewRevision(
        string reviewRevisionId,
        string workspaceId,
        string reviewId,
        int revisionNo,
        int projectionVersion,
        DateTimeOffset recordedAt,
        string actorUserId,
        string idempotencyKey,
        ReviewPayload payload)
    {
        return new ReviewRevisionRecord(
            reviewRevisionId,
            workspaceId,
            reviewId,
            revisionNo,
            projectionVersion,
            recordedAt,
            actorUserId,
            idempotencyKey,
            payload.ExitReason,
            "exit_reason_v1",
            payload.ExitReasonOtherText,
            payload.RuleBreach,
            "breach_type_v1",
            payload.BreachTypeIds,
            payload.BreachOtherText,
            payload.StopMovedAway,
            payload.RiskExceeded,
            payload.RequiredChecklistResultsJson,
            payload.Emotion,
            payload.Emotion is null ? null : "emotion_v1",
            payload.Lesson,
            payload.ContentSha256);
    }

    private void AddReviewAttachmentJoin(ReviewRevisionRecord revision, AttachmentRecord? attachment, DateTimeOffset createdAt)
    {
        if (attachment is null)
        {
            return;
        }

        _reviewRevisionAttachments.Add(new ReviewRevisionAttachmentRecord(
            revision.ReviewRevisionId,
            revision.WorkspaceId,
            attachment.AttachmentId,
            "SCREENSHOT",
            1,
            attachment.ContentSha256,
            attachment.AttachmentContentVersionId,
            createdAt));
    }

    private ReviewPayload ValidateReviewPayload(
        TradeEpisodeProjectionRecord projection,
        string reviewId,
        string reviewRevisionId,
        int revisionNo,
        DateTimeOffset recordedAt,
        string actorUserId,
        string exitReasonInput,
        string? exitReasonOtherTextInput,
        bool ruleBreach,
        IReadOnlyList<string> breachTypeIdsInput,
        string? breachOtherTextInput,
        bool stopMovedAway,
        bool riskExceeded,
        IReadOnlyDictionary<string, bool> requiredChecklistResultsInput,
        string? emotionInput,
        string? lessonInput,
        string idempotencyKey,
        AttachmentRecord? attachment)
    {
        string exitReason = NormalizeReviewId(exitReasonInput);
        EnsureTaxonomyItem("exit_reason_v1", "EXIT_REASON", exitReason);
        string? exitReasonOtherText = NormalizeOptionalText(exitReasonOtherTextInput, 500);
        if ((exitReason == "OTHER") != (exitReasonOtherText is not null))
        {
            throw new TradeProofException("REVIEW_VALIDATION_FAILED");
        }

        List<string> breachTypeIds = breachTypeIdsInput.Select(NormalizeReviewId).ToList();
        if (breachTypeIds.Count != breachTypeIds.Distinct(StringComparer.Ordinal).Count())
        {
            throw new TradeProofException("REVIEW_VALIDATION_FAILED");
        }

        foreach (string id in breachTypeIds)
        {
            EnsureTaxonomyItem("breach_type_v1", "BREACH_TYPE", id);
        }

        breachTypeIds = SortTaxonomyIds("breach_type_v1", breachTypeIds);
        string? breachOtherText = NormalizeOptionalText(breachOtherTextInput, 500);
        if (breachTypeIds.Contains("OTHER", StringComparer.Ordinal) != (breachOtherText is not null))
        {
            throw new TradeProofException("REVIEW_VALIDATION_FAILED");
        }

        if (stopMovedAway != breachTypeIds.Contains("STOP_MOVED_AWAY", StringComparer.Ordinal) ||
            riskExceeded != breachTypeIds.Contains("RISK_EXCEEDED", StringComparer.Ordinal))
        {
            throw new TradeProofException("REVIEW_VALIDATION_FAILED");
        }

        SortedDictionary<string, bool> requiredChecklistResults = NormalizeChecklistResults(projection, requiredChecklistResultsInput);
        bool hasMissedChecklist = requiredChecklistResults.Values.Any(v => !v);
        if (hasMissedChecklist != breachTypeIds.Contains("CHECKLIST_MISSED", StringComparer.Ordinal))
        {
            throw new TradeProofException("REVIEW_VALIDATION_FAILED");
        }

        if (breachTypeIds.Contains("UNPLANNED_ENTRY", StringComparer.Ordinal) && projection.PlanProofStatus == "VERIFIED")
        {
            throw new TradeProofException("REVIEW_VALIDATION_FAILED");
        }

        bool derivedBreach = stopMovedAway || riskExceeded || hasMissedChecklist || breachTypeIds.Contains("UNPLANNED_ENTRY", StringComparer.Ordinal);
        if (derivedBreach && !ruleBreach)
        {
            throw new TradeProofException("REVIEW_VALIDATION_FAILED");
        }

        if (!ruleBreach)
        {
            if (breachTypeIds.Count != 0 || breachOtherText is not null || stopMovedAway || riskExceeded || hasMissedChecklist)
            {
                throw new TradeProofException("REVIEW_VALIDATION_FAILED");
            }
        }
        else if (breachTypeIds.Count == 0)
        {
            throw new TradeProofException("REVIEW_VALIDATION_FAILED");
        }

        string? emotion = NormalizeOptionalId(emotionInput);
        if (emotion is not null)
        {
            EnsureTaxonomyItem("emotion_v1", "EMOTION", emotion);
        }

        string? lesson = NormalizeOptionalText(lessonInput, 2000);
        string checklistJson = JsonSerializer.Serialize(requiredChecklistResults, ContractVersions.JsonOptions);
        object[] attachments = attachment is null
            ? []
            :
            [
                new
                {
                    attachment_content_sha256 = attachment.ContentSha256,
                    attachment_id = attachment.AttachmentId,
                    ordinal = 1,
                    role = "SCREENSHOT"
                }
            ];
        string contentSha256 = ContractVersions.Sha256CanonicalJson(new
        {
            attachments,
            breach_other_text = breachOtherText,
            breach_taxonomy_version = "breach_type_v1",
            breach_type_ids = breachTypeIds,
            emotion,
            emotion_taxonomy_version = emotion is null ? null : "emotion_v1",
            episode_projection_version = projection.ProjectionVersion,
            exit_reason = exitReason,
            exit_reason_other_text = exitReasonOtherText,
            exit_reason_taxonomy_version = "exit_reason_v1",
            idempotency_key = idempotencyKey,
            lesson,
            recorded_at = recordedAt,
            recorded_by_user_id = actorUserId,
            required_checklist_results_json = requiredChecklistResults,
            review_id = reviewId,
            review_revision_id = reviewRevisionId,
            revision_no = revisionNo,
            risk_exceeded = riskExceeded,
            rule_breach = ruleBreach,
            stop_moved_away = stopMovedAway,
            workspace_id = projection.WorkspaceId
        });
        return new ReviewPayload(exitReason, exitReasonOtherText, ruleBreach, breachTypeIds, breachOtherText, stopMovedAway, riskExceeded, checklistJson, emotion, lesson, contentSha256);
    }

    private SortedDictionary<string, bool> NormalizeChecklistResults(TradeEpisodeProjectionRecord projection, IReadOnlyDictionary<string, bool> requested)
    {
        List<string> requiredIds = RequiredChecklistIds(projection);
        if (requested.Count != requiredIds.Count)
        {
            throw new TradeProofException("REVIEW_VALIDATION_FAILED");
        }

        SortedDictionary<string, bool> results = new(StringComparer.Ordinal);
        foreach (string id in requiredIds)
        {
            if (!requested.TryGetValue(id, out bool value))
            {
                throw new TradeProofException("REVIEW_VALIDATION_FAILED");
            }

            results[id] = value;
        }

        foreach (string key in requested.Keys)
        {
            if (!requiredIds.Contains(key, StringComparer.Ordinal))
            {
                throw new TradeProofException("REVIEW_VALIDATION_FAILED");
            }
        }

        return results;
    }

    private List<string> RequiredChecklistIds(TradeEpisodeProjectionRecord projection)
    {
        if (projection.PlanProofStatus != "VERIFIED" || projection.FrozenPlanRevisionId is null)
        {
            return [];
        }

        TradePlanRevisionRecord planRevision = _planRevisions.Values
            .SelectMany(r => r)
            .SingleOrDefault(r => r.WorkspaceId == projection.WorkspaceId && r.TradePlanRevisionId == projection.FrozenPlanRevisionId)
            ?? throw new TradeProofException("FROZEN_PLAN_REVISION_NOT_FOUND");
        return planRevision.Checklist.Where(i => i.Required).Select(i => i.ChecklistItemId).OrderBy(id => id, StringComparer.Ordinal).ToList();
    }

    private ReviewRevisionRecord? CurrentReviewRevision(TradeEpisodeProjectionRecord projection)
    {
        if (!_reviewIdsByEpisode.TryGetValue(projection.EpisodeId, out string? reviewId) ||
            !_reviewRevisions.TryGetValue(reviewId, out List<ReviewRevisionRecord>? revisions))
        {
            return null;
        }

        ReviewRevisionRecord latest = revisions.MaxBy(r => r.RevisionNo)!;
        return latest.EpisodeProjectionVersion == projection.ProjectionVersion ? latest : null;
    }

    private string? CurrentReviewState(TradeEpisodeProjectionRecord projection)
    {
        if (!_reviewIdsByEpisode.TryGetValue(projection.EpisodeId, out string? reviewId))
        {
            return null;
        }

        return CurrentReviewRevision(projection) is null ? "RECONFIRM_REQUIRED" : _reviews[reviewId].State;
    }

    private EpisodeMetricSource BuildMetricSource(TradeEpisodeProjectionRecord projection) =>
        new(projection, CurrentReviewRevision(projection));

    private static bool IsNetEligible(EpisodeMetricSource source) =>
        source.Projection.State == "CLOSED" &&
        source.Projection.AccountingQuality == "COMPLETE" &&
        source.Projection.NetRealizedPnlQuote is not null;

    private static bool IsPlanned(EpisodeMetricSource source) =>
        source.Projection.PlanProofStatus == "VERIFIED" && source.Projection.FrozenPlanRevisionId is not null;

    private static string? AccountingExclusionReason(EpisodeMetricSource source) =>
        source.Projection.AccountingQuality == "FEE_CONVERSION_MISSING" ? "FEE_CONVERSION_MISSING" : "ACCOUNTING_INCOMPLETE";

    private static bool IsAdherent(ReviewRevisionRecord? revision) =>
        revision is not null &&
        !revision.RuleBreach &&
        !revision.StopMovedAway &&
        !revision.RiskExceeded &&
        RequiredChecklistComplete(revision);

    private static bool RequiredChecklistComplete(ReviewRevisionRecord revision)
    {
        Dictionary<string, bool>? values = JsonSerializer.Deserialize<Dictionary<string, bool>>(revision.RequiredChecklistResultsJson, ContractVersions.JsonOptions);
        return values is not null && values.Values.All(v => v);
    }

    private MetricSnapshotRecord BuildRateMetric(
        string workspaceId,
        string cohortId,
        string inputId,
        string tupleHash,
        DateTimeOffset start,
        DateTimeOffset end,
        DateTimeOffset asOfAt,
        string metricId,
        string formulaVersion,
        string eligibilityPolicyId,
        string unit,
        IReadOnlyList<EpisodeMetricSource> candidates,
        IReadOnlyList<EpisodeMetricSource> denominatorSources,
        int numerator,
        Func<EpisodeMetricSource, string?> exclusionReason,
        int? valueDenominatorOverride = null)
    {
        List<EpisodeMetricSource> excluded = candidates.Where(s => !denominatorSources.Any(d => SameProjection(d, s))).ToList();
        int valueDenominator = valueDenominatorOverride ?? denominatorSources.Count;
        string? value = valueDenominator == 0 ? null : Ratio(numerator, valueDenominator);
        string displayState = value is null ? "UNDEFINED" : "NORMAL";
        string? nullReason = value is null ? "NO_ELIGIBLE_EPISODE" : null;
        string candidateRefs = EpisodeRefsJson(candidates);
        string includedRefs = EpisodeRefsJson(denominatorSources);
        string excludedRefs = ExcludedEpisodeRefsJson(excluded, exclusionReason);
        string exclusionCounts = ExclusionReasonCountsJson(excluded, exclusionReason);
        string sourceReviews = ReviewSourceRefsJson(denominatorSources.Select(s => s.Revision).OfType<ReviewRevisionRecord>());
        string inputDigest = ContractVersions.Sha256CanonicalJson(new
        {
            candidateRefs,
            denominator = valueDenominator,
            excludedRefs,
            formulaVersion,
            includedRefs,
            metricId,
            numerator,
            sourceReviews,
            tupleHash,
            value
        });
        return new MetricSnapshotRecord(
            NextId("metric"),
            workspaceId,
            cohortId,
            inputId,
            ContractVersions.MetricSnapshot,
            ContractVersions.WeeklyLab,
            metricId,
            formulaVersion,
            ContractVersions.MetricsAlgorithm,
            eligibilityPolicyId,
            tupleHash,
            start,
            end,
            asOfAt,
            JsonSerializer.Serialize(new { dimensionType = "OVERALL" }, ContractVersions.JsonOptions),
            null,
            null,
            "DECIMAL",
            value,
            null,
            null,
            null,
            null,
            unit,
            valueDenominator == 0 ? null : numerator.ToString(CultureInfo.InvariantCulture),
            valueDenominator == 0 ? null : valueDenominator.ToString(CultureInfo.InvariantCulture),
            nullReason,
            displayState,
            "COMPLETE",
            candidates.Count,
            denominatorSources.Count,
            excluded.Count,
            candidateRefs,
            includedRefs,
            excludedRefs,
            exclusionCounts,
            sourceReviews,
            "[]",
            EvidenceLabel(denominatorSources.Count),
            inputDigest,
            Now,
            null);
    }

    private MetricSnapshotRecord BuildContextCoverageMetric(
        string workspaceId,
        string cohortId,
        string inputId,
        string tupleHash,
        DateTimeOffset start,
        DateTimeOffset end,
        DateTimeOffset asOfAt,
        IReadOnlyList<EpisodeMetricSource> candidates,
        string phase,
        string timeframe)
    {
        List<ContextSnapshotRecord> available = candidates
            .Select(s => ContextSnapshots.SingleOrDefault(c =>
                c.WorkspaceId == workspaceId &&
                c.EpisodeId == s.Projection.EpisodeId &&
                c.ProjectionVersion == s.Projection.ProjectionVersion &&
                c.Phase == phase &&
                c.Timeframe == timeframe &&
                c.AlgorithmVersion == ContractVersions.ContextAlgorithm &&
                c.ParameterSetId == ContractVersions.ContextParameterSet &&
                c.Quality == "COMPLETE" &&
                c.AggregationEligible))
            .OfType<ContextSnapshotRecord>()
            .ToList();
        int missing = candidates.Count - available.Count;
        string valueObject = JsonSerializer.Serialize(new
        {
            availableCount = available.Count,
            missingCount = missing,
            reasonCounts = missing == 0 ? [] : new[] { new { count = missing, reasonCode = "CONTEXT_MISSING" } }
        }, ContractVersions.JsonOptions);
        string candidateRefs = EpisodeRefsJson(candidates);
        string contextRefs = ContextSourceRefsJson(available);
        string inputDigest = ContractVersions.Sha256CanonicalJson(new
        {
            candidateRefs,
            contextRefs,
            metricId = "context_coverage_counts",
            phase,
            timeframe,
            tupleHash,
            valueObject
        });
        return new MetricSnapshotRecord(
            NextId("metric"),
            workspaceId,
            cohortId,
            inputId,
            ContractVersions.MetricSnapshot,
            ContractVersions.WeeklyLab,
            "context_coverage_counts",
            "context_coverage_counts_v1",
            ContractVersions.MetricsAlgorithm,
            "context_coverage_all_candidates_v1",
            tupleHash,
            start,
            end,
            asOfAt,
            JsonSerializer.Serialize(new { dimensionType = "CONTEXT_COVERAGE" }, ContractVersions.JsonOptions),
            phase,
            timeframe,
            "OBJECT",
            null,
            null,
            null,
            null,
            valueObject,
            "EPISODE_COUNT",
            null,
            null,
            null,
            "NORMAL",
            "COMPLETE",
            candidates.Count,
            candidates.Count,
            0,
            candidateRefs,
            candidateRefs,
            "[]",
            "[]",
            "[]",
            contextRefs,
            EvidenceLabel(candidates.Count),
            inputDigest,
            Now,
            null);
    }

    private static bool SameProjection(EpisodeMetricSource left, EpisodeMetricSource right) =>
        left.Projection.EpisodeId == right.Projection.EpisodeId && left.Projection.ProjectionVersion == right.Projection.ProjectionVersion;

    private static string Ratio(int numerator, int denominator) =>
        CanonicalDecimal(RoundScale18((decimal)numerator / denominator));

    private static string EvidenceLabel(int n) =>
        n < 2 ? "INSUFFICIENT" : n < 30 ? "EXPLORATORY" : "ESTIMATED";

    private static string EpisodeRefsJson(IEnumerable<EpisodeMetricSource> sources) =>
        JsonSerializer.Serialize(sources.Select(s => new
        {
            episode_id = s.Projection.EpisodeId,
            projection_version = s.Projection.ProjectionVersion
        }), ContractVersions.JsonOptions);

    private static string ExcludedEpisodeRefsJson(IEnumerable<EpisodeMetricSource> sources, Func<EpisodeMetricSource, string?> reason)
    {
        return JsonSerializer.Serialize(sources.Select(s =>
        {
            string safeReason = reason(s) ?? "USER_EXCLUDED";
            return new
            {
                episodeRecordKey = new
                {
                    episode_id = s.Projection.EpisodeId,
                    projection_version = s.Projection.ProjectionVersion
                },
                primaryReason = safeReason,
                reasonCodes = new[] { safeReason }
            };
        }), ContractVersions.JsonOptions);
    }

    private static string ExclusionReasonCountsJson(IEnumerable<EpisodeMetricSource> sources, Func<EpisodeMetricSource, string?> reason)
    {
        return JsonSerializer.Serialize(sources
            .Select(s => reason(s) ?? "USER_EXCLUDED")
            .GroupBy(r => r, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new { count = g.Count(), reasonCode = g.Key }), ContractVersions.JsonOptions);
    }

    private static string ReviewSourceRefsJson(IEnumerable<ReviewRevisionRecord> revisions) =>
        JsonSerializer.Serialize(revisions
            .Select(r => r.ReviewRevisionId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .Select(id => new { review_revision_id = id }), ContractVersions.JsonOptions);

    private static string ContextSourceRefsJson(IEnumerable<ContextSnapshotRecord> snapshots) =>
        JsonSerializer.Serialize(snapshots
            .Select(s => s.SnapshotId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .Select(id => new { id }), ContractVersions.JsonOptions);

    private static string StableScopedId(string prefix, string workspaceId, params object[] values)
    {
        string digest = ContractVersions.Sha256CanonicalJson(new { prefix, values, workspaceId });
        return $"{prefix}_{digest[..16]}";
    }

    private void EnsureReviewTaxonomies()
    {
        if (_reviewTaxonomyVersions.Count != 0)
        {
            return;
        }

        AddReviewTaxonomy("exit_reason_v1", "EXIT_REASON",
        [
            ("TARGET_REACHED", "Dat muc tieu"),
            ("STOP_HIT", "Cham stop"),
            ("THESIS_INVALIDATED", "Luan diem khong con dung"),
            ("RISK_REDUCTION", "Giam rui ro"),
            ("TIME_EXIT", "Thoat theo thoi gian"),
            ("OTHER", "Khac")
        ]);
        AddReviewTaxonomy("breach_type_v1", "BREACH_TYPE",
        [
            ("ENTRY_OUTSIDE_ZONE", "Vao ngoai entry zone"),
            ("STOP_MOVED_AWAY", "Doi stop xa invalidation"),
            ("RISK_EXCEEDED", "Vuot planned risk"),
            ("CHECKLIST_MISSED", "Khong tuan thu checklist"),
            ("UNPLANNED_ENTRY", "Vao lenh khong co verified plan"),
            ("OTHER", "Khac")
        ]);
        AddReviewTaxonomy("emotion_v1", "EMOTION",
        [
            ("CALM", "Binh tinh"),
            ("FOCUSED", "Tap trung"),
            ("ANXIOUS", "Lo lang"),
            ("IMPULSIVE", "Boc dong"),
            ("FRUSTRATED", "That vong")
        ]);
    }

    private void AddReviewTaxonomy(string version, string type, IReadOnlyList<(string Id, string Label)> items)
    {
        DateTimeOffset now = Now;
        List<ReviewTaxonomyItemRecord> rows = items.Select((item, index) => new ReviewTaxonomyItemRecord(
            version,
            type,
            item.Id,
            item.Label,
            index + 1)).ToList();
        string contentSha256 = ContractVersions.Sha256CanonicalJson(new
        {
            items = rows.Select(i => new { itemId = i.ItemId, itemOrder = i.ItemOrder, labelVi = i.LabelVi }),
            taxonomyType = type,
            taxonomyVersion = version
        });
        _reviewTaxonomyVersions[version] = new ReviewTaxonomyVersionRecord(version, type, contentSha256, now);
        _reviewTaxonomyItems[version] = rows;
        int sequence = _reviewTaxonomyPublishEvents.Count(e => e.TaxonomyType == type) + 1;
        _reviewTaxonomyPublishEvents.Add(new ReviewTaxonomyPublishEventRecord(NextId("taxevt"), type, sequence, version, now, contentSha256));
    }

    private void EnsureTaxonomyItem(string version, string type, string itemId)
    {
        if (!_reviewTaxonomyItems.TryGetValue(version, out List<ReviewTaxonomyItemRecord>? items) ||
            items.All(i => i.TaxonomyType != type || i.ItemId != itemId))
        {
            throw new TradeProofException("REVIEW_VALIDATION_FAILED");
        }
    }

    private List<string> SortTaxonomyIds(string version, IEnumerable<string> ids)
    {
        Dictionary<string, int> order = _reviewTaxonomyItems[version].ToDictionary(i => i.ItemId, i => i.ItemOrder, StringComparer.Ordinal);
        return ids.OrderBy(id => order[id]).ThenBy(id => id, StringComparer.Ordinal).ToList();
    }

    private static string NormalizeReviewId(string value)
    {
        string normalized = value.Trim().ToUpperInvariant();
        if (normalized.Length == 0 || !normalized.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
        {
            throw new TradeProofException("REVIEW_VALIDATION_FAILED");
        }

        return normalized;
    }

    private static string? NormalizeOptionalId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : NormalizeReviewId(value);

    private static string? NormalizeOptionalText(string? value, int maxLength)
    {
        if (value is null)
        {
            return null;
        }

        string trimmed = value.Trim();
        if (trimmed.Length == 0 || trimmed.Length > maxLength)
        {
            throw new TradeProofException("REVIEW_VALIDATION_FAILED");
        }

        return trimmed;
    }

    private static bool IsSupportedScreenshot(byte[] bytes)
    {
        bool png = bytes.Length >= 8 &&
                   bytes[0] == 0x89 &&
                   bytes[1] == 0x50 &&
                   bytes[2] == 0x4E &&
                   bytes[3] == 0x47 &&
                   bytes[4] == 0x0D &&
                   bytes[5] == 0x0A &&
                   bytes[6] == 0x1A &&
                   bytes[7] == 0x0A;
        bool jpeg = bytes.Length >= 3 &&
                    bytes[0] == 0xFF &&
                    bytes[1] == 0xD8 &&
                    bytes[2] == 0xFF;
        return png || jpeg;
    }

    private sealed record ReviewPayload(
        string ExitReason,
        string? ExitReasonOtherText,
        bool RuleBreach,
        IReadOnlyList<string> BreachTypeIds,
        string? BreachOtherText,
        bool StopMovedAway,
        bool RiskExceeded,
        string RequiredChecklistResultsJson,
        string? Emotion,
        string? Lesson,
        string ContentSha256);

    private sealed record EpisodeMetricSource(TradeEpisodeProjectionRecord Projection, ReviewRevisionRecord? Revision);
}
